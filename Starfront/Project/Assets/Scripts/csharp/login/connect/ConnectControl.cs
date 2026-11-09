using System;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// 连接与登录编排。会话保存在这里，连接面板只负责输入和展示结果。
/// </summary>
public class ConnectControl {

    public const int LoginTimeoutMs = 15000;

    private static ConnectControl _ins;

    public static ConnectControl ins {
        get {
            if (_ins == null) {
                _ins = new ConnectControl();
            }
            return _ins;
        }
    }

    NetSession session;
    Action timeoutAction;

    public NetSession Session => session;

    public bool IsConnected => session != null && session.IsConnected;

    /**上次连接参数，方便重试。*/
    public string LastHost = "10.9.124.46";
    public int LastPort = 10087;
    public string LastAccount = "ll0";
    public int LastServerId = 1;

    /// <summary>
    /// 建立 TCP 并发送 CM_Login。回调一定在主线程。
    /// </summary>
    public void ConnectAndLogin(string host, int port, string account, int serverId, Action<LoginResult> onComplete) {
        LastHost = host;
        LastPort = port;
        LastAccount = account;
        LastServerId = serverId;
        EnsureDispatcher();
        if (NetSession.Log == null) {
            NetSession.Log = msg => Debug.Log("[Net] " + msg);
        }
        ClearTimeout();
        try {
            CloseSession();
            session = new NetSession();
            LoginResult result = new LoginResult();
            ManualResetBridge bridge = new ManualResetBridge();
            Action finish = () => {
                ClearTimeout();
                bridge.Complete(result, onComplete);
            };

            // 先建连、发 CM_Login，再开读线程，避免对端瞬断冲掉登录包
            Debug.Log(string.Format("[Connect] connecting {0}:{1} account={2} server={3}", host, port, account, serverId));
            session.Connect(host, port, false);

            session.Register(LoginClient.SmLoginSuccess, delegate (int opIndex, byte[] body) {
                result.Success = true;
                result.MapId = LoginClient.DecodeSuccess(body);
                session.Disconnected = null;
                Debug.Log("[Connect] SM_LoginSuccess mapId=" + result.MapId);
                finish();
            });
            session.Register(LoginClient.SmLoginFail, delegate (int opIndex, byte[] body) {
                result.Success = false;
                result.FailCode = LoginClient.DecodeFail(body);
                session.Disconnected = null;
                Debug.LogWarning("[Connect] SM_LoginFail code=" + result.FailCode);
                finish();
            });
            session.Disconnected = () => {
                if (bridge.Finished) {
                    return;
                }
                result.Success = false;
                result.FailCode = -3;
                result.Disconnected = true;
                Debug.LogWarning("[Connect] disconnected while waiting login");
                finish();
            };

            byte[] loginBody = LoginClient.EncodeLogin(account, serverId);
            Debug.Log(string.Format("[Connect] sending CM_Login id={0} bodyBytes={1}", LoginClient.CmLogin, loginBody.Length));
            session.Send(LoginClient.CmLogin, loginBody);
            session.StartReading();

            timeoutAction = () => {
                if (bridge.Finished) {
                    return;
                }
                result.TimedOut = true;
                result.Success = false;
                Debug.LogWarning("[Connect] login timeout");
                CloseSession();
                bridge.Complete(result, onComplete);
            };
            RookieEngine.timer.Once(this, LoginTimeoutMs, timeoutAction);
        } catch (SocketException exception) {
            CloseSession();
            LoginResult fail = new LoginResult();
            fail.Success = false;
            fail.FailCode = -1;
            Debug.LogWarning("连接失败: " + exception.Message);
            UnityMainThreadDispatcher.Instance.Enqueue(() => onComplete(fail));
        } catch (Exception exception) {
            CloseSession();
            LoginResult fail = new LoginResult();
            fail.Success = false;
            fail.FailCode = -2;
            Debug.LogException(exception);
            UnityMainThreadDispatcher.Instance.Enqueue(() => onComplete(fail));
        }
    }

    public void CloseSession() {
        if (session != null) {
            session.Disconnected = null;
            session.Close();
            session = null;
        }
    }

    void ClearTimeout() {
        if (timeoutAction != null) {
            RookieEngine.timer.Clear(this, timeoutAction);
            timeoutAction = null;
        }
    }

    static void EnsureDispatcher() {
        if (UnityMainThreadDispatcher.Instance != null) {
            return;
        }
        GameObject go = new GameObject("UnityMainThreadDispatcher");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<UnityMainThreadDispatcher>();
    }

    sealed class ManualResetBridge {
        public bool Finished;
        readonly object gate = new object();

        public void Complete(LoginResult result, Action<LoginResult> onComplete) {
            lock (gate) {
                if (Finished) {
                    return;
                }
                Finished = true;
            }
            EnsureDispatcher();
            UnityMainThreadDispatcher.Instance.Enqueue(() => onComplete(result));
        }
    }

}
