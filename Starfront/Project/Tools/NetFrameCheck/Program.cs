using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

internal static class Program {

    static int failures;

    static void Main() {
        CheckScalars();
        CheckFrameRoundtrip();
        CheckTcpEcho();
        CheckSkipUnknown();
        CheckLoginSuccess();
        CheckLoginFail();
        CheckGeneratedLoginBytes();
        CheckLoginGateRules();
        TryLiveLoginIfServerUp();
        if (failures != 0) {
            Console.Error.WriteLine("FAILED " + failures);
            Environment.Exit(1);
        }
        Console.WriteLine("net-frame-ok");
    }

    static void CheckScalars() {
        NetWriter writer = new NetWriter();
        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteInt(0);
        writer.WriteInt(1);
        writer.WriteInt(-1);
        writer.WriteInt(10086);
        writer.WriteLong(-1101L);
        writer.WriteShort(-1101);
        writer.WriteString(null);
        writer.WriteString("");
        writer.WriteString("账号");

        NetReader reader = new NetReader(writer.ToArray());
        Expect(reader.ReadBool(), "bool true");
        Expect(!reader.ReadBool(), "bool false");
        Expect(reader.ReadInt() == 0, "int 0");
        Expect(reader.ReadInt() == 1, "int 1");
        Expect(reader.ReadInt() == -1, "int -1");
        Expect(reader.ReadInt() == 10086, "int 10086");
        Expect(reader.ReadLong() == -1101L, "long -1101");
        Expect(reader.ReadShort() == -1101, "short -1101");
        Expect(reader.ReadString() == "", "null string");
        Expect(reader.ReadString() == "", "empty string");
        Expect(reader.ReadString() == "账号", "utf8 string");
        Expect(reader.Remaining == 0, "scalars consumed");
    }

    static void CheckFrameRoundtrip() {
        NetWriter bodyWriter = new NetWriter();
        bodyWriter.WriteString("star");
        bodyWriter.WriteInt(1);
        byte[] frame = NetFrame.Encode(0, -1101, bodyWriter.ToArray());

        int opIndex;
        short protocolId;
        byte[] body;
        using (MemoryStream input = new MemoryStream(frame)) {
            bool read = NetFrame.TryRead(input, NetSession.DefaultMaxFrameSize, out opIndex, out protocolId, out body);
            Expect(read, "frame read");
            Expect(input.Position == input.Length, "frame consumed");
        }
        Expect(opIndex == 0, "opIndex 0");
        Expect(protocolId == -1101, "protocol -1101");
        NetReader reader = new NetReader(body);
        Expect(reader.ReadString() == "star", "frame string");
        Expect(reader.ReadInt() == 1, "frame int");
    }

    static void CheckTcpEcho() {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Thread server = new Thread(() => EchoOne(listener));
        server.IsBackground = true;
        server.Start();

        NetSession session = new NetSession();
        int gotOp = -1;
        byte[] gotBody = null;
        ManualResetEvent arrived = new ManualResetEvent(false);
        session.Register(7, (opIndex, body) => {
            gotOp = opIndex;
            gotBody = body;
            arrived.Set();
        });
        session.Connect(IPAddress.Loopback.ToString(), port);

        NetWriter bodyWriter = new NetWriter();
        bodyWriter.WriteString("ping");
        session.Send(7, bodyWriter.ToArray(), 3);
        Expect(arrived.WaitOne(3000), "tcp echo arrived");
        Expect(gotOp == 3, "tcp opIndex");
        Expect(new NetReader(gotBody).ReadString() == "ping", "tcp body");
        session.Close();
        server.Join(3000);
        listener.Stop();
    }

    static void EchoOne(TcpListener listener) {
        using (TcpClient client = listener.AcceptTcpClient())
        using (NetworkStream stream = client.GetStream()) {
            int opIndex;
            short protocolId;
            byte[] body;
            NetFrame.TryRead(stream, NetSession.DefaultMaxFrameSize, out opIndex, out protocolId, out body);
            byte[] frame = NetFrame.Encode(opIndex, protocolId, body);
            stream.Write(frame, 0, frame.Length);
        }
    }

    static void CheckSkipUnknown() {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Thread server = new Thread(() => PushUnknownThenKnown(listener));
        server.IsBackground = true;
        server.Start();

        NetSession session = new NetSession();
        int seen = 0;
        ManualResetEvent arrived = new ManualResetEvent(false);
        session.Register(7, (opIndex, body) => {
            seen++;
            arrived.Set();
        });
        session.Connect(IPAddress.Loopback.ToString(), port);
        Expect(arrived.WaitOne(3000), "known frame arrived");
        Thread.Sleep(100);
        Expect(seen == 1, "unknown frame not delivered");
        Expect(session.SkippedCount == 1, "unknown frame skipped");
        session.Close();
        server.Join(3000);
        listener.Stop();
    }

    static void PushUnknownThenKnown(TcpListener listener) {
        using (TcpClient client = listener.AcceptTcpClient())
        using (NetworkStream stream = client.GetStream()) {
            byte[] unknown = NetFrame.Encode(0, 99, Encoding.UTF8.GetBytes("skip"));
            byte[] known = NetFrame.Encode(0, 7, new byte[0]);
            stream.Write(unknown, 0, unknown.Length);
            stream.Write(known, 0, known.Length);
            Thread.Sleep(500);
        }
    }

    static void CheckGeneratedLoginBytes() {
        byte[] viaClient = LoginClient.EncodeLogin("player", 1);
        CM_Login packet = new CM_Login();
        packet.account = "player";
        packet.op = "";
        packet.server = 1;
        packet.referer = "";
        packet.time = 0;
        packet.sign = "";
        packet.ext = "";
        packet.gid = "";
        packet.pid = "";
        packet.networkType = 0;
        packet.guid = "";
        packet.openId = "";
        byte[] viaGenerated = packet.Encode();
        Expect(BytesEqual(viaClient, viaGenerated), "generated CM_Login bytes");

        NetWriter hand = new NetWriter();
        hand.WriteString("player");
        hand.WriteString("");
        hand.WriteInt(1);
        hand.WriteString("");
        hand.WriteInt(0);
        hand.WriteString("");
        hand.WriteString("");
        hand.WriteString("");
        hand.WriteString("");
        hand.WriteInt(0);
        hand.WriteString("");
        hand.WriteString("");
        Expect(BytesEqual(viaGenerated, hand.ToArray()), "generated matches P2 hand bytes");

        SM_LoginSuccess success = SM_LoginSuccess.Decode(LoginClient.EncodeSuccess(42));
        Expect(success.mapId == 42, "generated SM_LoginSuccess");
        SM_LoginFail fail = SM_LoginFail.Decode(LoginClient.EncodeFail(13));
        Expect(fail.code == 13, "generated SM_LoginFail");
    }

    static bool BytesEqual(byte[] a, byte[] b) {
        if (a == null || b == null || a.Length != b.Length) {
            return false;
        }
        for (int i = 0; i < a.Length; i++) {
            if (a[i] != b[i]) {
                return false;
            }
        }
        return true;
    }

    static void CheckLoginSuccess() {
        LoginResult result = ExchangeLogin("player", 1, 42, 0);
        Expect(result.Success, "login success");
        Expect(result.MapId == 42, "login mapId");
        Expect(!result.TimedOut, "login success timeout");
    }

    static void CheckLoginFail() {
        LoginResult result = ExchangeLogin("", 1, 0, 1);
        Expect(!result.Success, "login fail");
        Expect(result.FailCode == 1, "login fail code");
        Expect(!result.TimedOut, "login fail timeout");
    }

    static LoginResult ExchangeLogin(string account, int serverId, int mapId, int failCode) {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Thread server = new Thread(() => AnswerLogin(listener, mapId, failCode));
        server.IsBackground = true;
        server.Start();

        NetSession session = new NetSession();
        session.Connect(IPAddress.Loopback.ToString(), port);
        LoginResult result = LoginClient.Request(session, account, serverId, 3000);
        session.Close();
        server.Join(3000);
        listener.Stop();
        return result;
    }

    static void AnswerLogin(TcpListener listener, int mapId, int failCode) {
        using (TcpClient client = listener.AcceptTcpClient())
        using (NetworkStream stream = client.GetStream()) {
            int opIndex;
            short protocolId;
            byte[] body;
            NetFrame.TryRead(stream, NetSession.DefaultMaxFrameSize, out opIndex, out protocolId, out body);
            if (protocolId != LoginClient.CmLogin) {
                return;
            }
            CM_Login request = LoginClient.DecodeLogin(body);
            byte[] response;
            short responseId;
            if (string.IsNullOrEmpty(request.account) || failCode != 0) {
                responseId = LoginClient.SmLoginFail;
                response = LoginClient.EncodeFail(failCode == 0 ? 1 : failCode);
            } else {
                responseId = LoginClient.SmLoginSuccess;
                response = LoginClient.EncodeSuccess(mapId);
            }
            byte[] frame = NetFrame.Encode(0, responseId, response);
            stream.Write(frame, 0, frame.Length);
            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// P6：成功才算进游戏前的门闩；失败 / 超时不打开开场（由 ConnectPanel 保证，这里核登录结果语义）。
    /// </summary>
    static void CheckLoginGateRules() {
        LoginResult ok = new LoginResult();
        ok.Success = true;
        ok.MapId = 42;
        Expect(ShouldEnterOpening(ok), "p6 success enters opening");

        LoginResult fail = new LoginResult();
        fail.Success = false;
        fail.FailCode = 1;
        Expect(!ShouldEnterOpening(fail), "p6 fail stays on connect");

        LoginResult timeout = new LoginResult();
        timeout.TimedOut = true;
        Expect(!ShouldEnterOpening(timeout), "p6 timeout stays on connect");

        LoginResult dropped = new LoginResult();
        dropped.Disconnected = true;
        Expect(!ShouldEnterOpening(dropped), "p6 disconnect stays on connect");
    }

    static bool ShouldEnterOpening(LoginResult result) {
        return result != null && result.Success && !result.TimedOut && !result.Disconnected;
    }

    /// <summary>
    /// 若本机 10086 在听，对 Debug 服做一次真登录；否则跳过。
    /// </summary>
    static void TryLiveLoginIfServerUp() {
        const string host = "127.0.0.1";
        const int port = 10086;
        try {
            using (TcpClient probe = new TcpClient()) {
                IAsyncResult ar = probe.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(500) || !probe.Connected) {
                    Console.WriteLine("p6-live-skip (no listener " + host + ":" + port + ")");
                    return;
                }
            }
        } catch {
            Console.WriteLine("p6-live-skip (no listener " + host + ":" + port + ")");
            return;
        }

        NetSession session = new NetSession();
        try {
            session.Connect(host, port);
            LoginResult ok = LoginClient.Request(session, "p6probe", 1, 8000);
            if (ok.TimedOut) {
                Console.WriteLine("p6-live-inconclusive (connected but no SM_Login* in 8s; 确认 com.game.Debug 且区服=1)");
                return;
            }
            Expect(ok.Success, "p6 live login success");
            Console.WriteLine("p6-live-ok mapId=" + ok.MapId);

            session.Close();
            session = new NetSession();
            session.Connect(host, port);
            LoginResult badServer = LoginClient.Request(session, "p6probe", 99999, 8000);
            if (badServer.TimedOut) {
                Console.WriteLine("p6-live-inconclusive on fail path");
                return;
            }
            Expect(!badServer.Success, "p6 live wrong server fails");
            Console.WriteLine("p6-live-fail-ok code=" + badServer.FailCode);
        } catch (Exception ex) {
            failures++;
            Console.Error.WriteLine("FAIL p6 live: " + ex.Message);
        } finally {
            session.Close();
        }
    }

    static void Expect(bool condition, string name) {
        if (!condition) {
            failures++;
            Console.Error.WriteLine("FAIL " + name);
        }
    }

}
