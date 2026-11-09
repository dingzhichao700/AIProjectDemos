using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// 编辑器或 PC 上的 TCP 会话。未登记的协议号按帧长度丢掉。
/// </summary>
public sealed class NetSession : IDisposable {

    public const int DefaultMaxFrameSize = 262144;

    readonly Dictionary<short, Action<int, byte[]>> handlers = new Dictionary<short, Action<int, byte[]>>();
    readonly int maxFrameSize;
    TcpClient client;
    NetworkStream stream;
    Thread readThread;
    int skipped;
    volatile bool closedByUser;
    volatile bool reading;

    /// <summary>读循环结束或连接断开时触发（可能在后台线程）。</summary>
    public Action Disconnected;

    /// <summary>可选日志（Unity 下接到 Debug.Log）。</summary>
    public static Action<string> Log;

    public NetSession() : this(DefaultMaxFrameSize) {
    }

    public NetSession(int maxFrameSize) {
        this.maxFrameSize = maxFrameSize;
    }

    public bool IsConnected {
        get { return client != null && client.Connected; }
    }

    public int SkippedCount {
        get { return skipped; }
    }

    public void Connect(string host, int port) {
        Connect(host, port, true);
    }

    /// <param name="startReading">false 时只建连，需先 Send 再调用 <see cref="StartReading"/>。</param>
    public void Connect(string host, int port, bool startReading) {
        Close();
        closedByUser = false;
        reading = false;
        skipped = 0;
        client = new TcpClient();
        client.NoDelay = true;
        client.Connect(host, port);
        stream = client.GetStream();
        WriteLog("connected " + host + ":" + port);
        if (startReading) {
            StartReading();
        }
    }

    public void StartReading() {
        if (reading || stream == null) {
            return;
        }
        reading = true;
        readThread = new Thread(ReadLoop);
        readThread.IsBackground = true;
        readThread.Name = "net-session";
        readThread.Start();
    }

    public void Register(short protocolId, Action<int, byte[]> handler) {
        lock (handlers) {
            handlers[protocolId] = handler;
        }
    }

    public void Send(short protocolId, byte[] body) {
        Send(protocolId, body, 0);
    }

    public void Send(short protocolId, byte[] body, int opIndex) {
        if (stream == null) {
            throw new InvalidOperationException("尚未连接");
        }
        byte[] frame = NetFrame.Encode(opIndex, protocolId, body ?? new byte[0]);
        lock (stream) {
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }
        WriteLog("send protocol=" + protocolId + " body=" + (body == null ? 0 : body.Length)
            + " frame=" + frame.Length + " hex=" + ToHex(frame, 24));
    }

    static void WriteLog(string message) {
        Action<string> log = Log;
        if (log != null) {
            log(message);
        }
    }

    static string ToHex(byte[] data, int max) {
        int n = data.Length < max ? data.Length : max;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(n * 3);
        for (int i = 0; i < n; i++) {
            if (i > 0) {
                sb.Append('-');
            }
            sb.Append(data[i].ToString("X2"));
        }
        if (data.Length > max) {
            sb.Append("...");
        }
        return sb.ToString();
    }

    public void Close() {
        closedByUser = true;
        NetworkStream current = stream;
        stream = null;
        reading = false;
        if (current != null) {
            try {
                current.Close();
            } catch (Exception) {
            }
        }
        if (client != null) {
            try {
                client.Close();
            } catch (Exception) {
            }
            client = null;
        }
    }

    public void Dispose() {
        Close();
    }

    void ReadLoop() {
        NetworkStream current = stream;
        try {
            while (current != null && current == stream) {
                int opIndex;
                short protocolId;
                byte[] body;
                if (!NetFrame.TryRead(current, maxFrameSize, out opIndex, out protocolId, out body)) {
                    break;
                }
                Action<int, byte[]> handler;
                lock (handlers) {
                    handlers.TryGetValue(protocolId, out handler);
                }
                if (handler == null) {
                    Interlocked.Increment(ref skipped);
                    WriteLog("skip protocol=" + protocolId + " body=" + (body == null ? 0 : body.Length));
                    continue;
                }
                WriteLog("recv protocol=" + protocolId + " body=" + (body == null ? 0 : body.Length));
                handler(opIndex, body);
            }
        } catch (Exception ex) {
            WriteLog("read end: " + ex.Message);
        } finally {
            bool userClosed = closedByUser;
            if (stream != null) {
                Close();
            }
            Action disconnected = Disconnected;
            if (!userClosed && disconnected != null) {
                try {
                    disconnected();
                } catch (Exception) {
                }
            }
        }
    }

}
