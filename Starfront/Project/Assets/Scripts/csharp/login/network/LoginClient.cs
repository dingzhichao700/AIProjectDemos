using System.Threading;

/// <summary>
/// 登录收发编排。协议体由 ProtocolGen 生成的 CM_/SM_ 类编码。
/// </summary>
public static class LoginClient {

    public const short CmLogin = CM_Login.ProtocolId;
    public const short SmLoginSuccess = SM_LoginSuccess.ProtocolId;
    public const short SmLoginFail = SM_LoginFail.ProtocolId;

    public static byte[] EncodeLogin(string account, int serverId) {
        CM_Login packet = new CM_Login();
        packet.account = account;
        packet.op = "";
        packet.server = serverId;
        packet.referer = "";
        packet.time = 0;
        packet.sign = "";
        packet.ext = "";
        packet.gid = "";
        packet.pid = "";
        packet.networkType = 0;
        packet.guid = "";
        packet.openId = "";
        return packet.Encode();
    }

    public static CM_Login DecodeLogin(byte[] body) {
        return CM_Login.Decode(body);
    }

    public static byte[] EncodeSuccess(int mapId) {
        SM_LoginSuccess packet = new SM_LoginSuccess();
        packet.mapId = mapId;
        return packet.Encode();
    }

    public static int DecodeSuccess(byte[] body) {
        return SM_LoginSuccess.Decode(body).mapId;
    }

    public static byte[] EncodeFail(int code) {
        SM_LoginFail packet = new SM_LoginFail();
        packet.code = code;
        return packet.Encode();
    }

    public static int DecodeFail(byte[] body) {
        return SM_LoginFail.Decode(body).code;
    }

    public static LoginResult Request(NetSession session, string account, int serverId, int timeoutMilliseconds) {
        LoginResult result = new LoginResult();
        ManualResetEvent done = new ManualResetEvent(false);
        session.Register(SmLoginSuccess, delegate (int opIndex, byte[] body) {
            result.Success = true;
            result.MapId = DecodeSuccess(body);
            done.Set();
        });
        session.Register(SmLoginFail, delegate (int opIndex, byte[] body) {
            result.Success = false;
            result.FailCode = DecodeFail(body);
            done.Set();
        });
        session.Send(CmLogin, EncodeLogin(account, serverId));
        if (!done.WaitOne(timeoutMilliseconds)) {
            result.TimedOut = true;
        }
        return result;
    }

}

public sealed class LoginResult {
    public bool Success;
    public int MapId;
    public int FailCode;
    public bool TimedOut;
    /**登录等待期间连接被对端或网络断开。*/
    public bool Disconnected;
}
