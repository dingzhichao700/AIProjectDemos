using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开场前的连接面板：账号、IP、端口、服务器标识。
/// </summary>
/// <remarks>
/// Prefab 只提供根节点。输入区在运行时搭，避免依赖尚未进 Addressables 的复杂预制。
/// 登录成功后才进入现有开场；失败留在本页。
/// </remarks>
public class ConnectPanel : BasePanel {

    InputField inputAccount;
    InputField inputHost;
    InputField inputPort;
    InputField inputServer;
    Text statusText;
    Button btnConnect;
    bool connecting;

    public ConnectPanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        EnsureUi();
        inputAccount.text = ConnectControl.ins.LastAccount;
        inputHost.text = ConnectControl.ins.LastHost;
        inputPort.text = ConnectControl.ins.LastPort.ToString();
        inputServer.text = ConnectControl.ins.LastServerId.ToString();
        statusText.text = "填写后点击连接";
        connecting = false;
        btnConnect.interactable = true;
        AddLis();
    }

    public override void OnClose() {
        RemoveLis();
    }

    void AddLis() {
        // 运行时搭的 Button 用 onClick；不要叠 EventTrigger，否则会连发两次
        btnConnect.onClick.RemoveListener(OnClickConnect);
        btnConnect.onClick.AddListener(OnClickConnect);
    }

    void RemoveLis() {
        if (btnConnect != null) {
            btnConnect.onClick.RemoveListener(OnClickConnect);
        }
    }

    void OnClickConnect() {
        if (connecting) {
            return;
        }
        Debug.Log("[ConnectPanel] click connect");
        string account = inputAccount.text == null ? "" : inputAccount.text.Trim();
        string host = inputHost.text == null ? "" : inputHost.text.Trim();
        if (string.IsNullOrEmpty(account)) {
            statusText.text = "请填写账号";
            return;
        }
        if (string.IsNullOrEmpty(host)) {
            statusText.text = "请填写 IP";
            return;
        }
        if (!int.TryParse(inputPort.text, out int port) || port <= 0) {
            statusText.text = "端口无效";
            return;
        }
        if (!int.TryParse(inputServer.text, out int serverId)) {
            statusText.text = "服务器标识无效";
            return;
        }
        connecting = true;
        btnConnect.interactable = false;
        statusText.text = "连接中…";
        ConnectControl.ins.ConnectAndLogin(host, port, account, serverId, OnLoginResult);
    }

    void OnLoginResult(LoginResult result) {
        if (this == null || !gameObject) {
            return;
        }
        connecting = false;
        btnConnect.interactable = true;
        // 失败 / 超时 / 断线：留在本页，不进开场
        if (result.TimedOut) {
            statusText.text = "登录超时";
            ConnectControl.ins.CloseSession();
            return;
        }
        if (result.Disconnected) {
            statusText.text = "连接已断开";
            ConnectControl.ins.CloseSession();
            return;
        }
        if (!result.Success) {
            if (result.FailCode == -1) {
                statusText.text = "连接失败（检查 IP/端口，服务端需 Debug 启动）";
            } else if (result.FailCode < 0) {
                statusText.text = "连接异常 code=" + result.FailCode;
            } else {
                statusText.text = "登录失败 code=" + result.FailCode;
            }
            ConnectControl.ins.CloseSession();
            return;
        }
        statusText.text = "登录成功 mapId=" + result.MapId;
        Debug.Log("[Connect] 登录成功 mapId=" + result.MapId + "，进入开场");
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.OPENING_PANEL);
    }

    void EnsureUi() {
        if (btnConnect != null) {
            return;
        }
        RectTransform root = trans;
        root.sizeDelta = new Vector2(720, 1280);

        GameObject bg = CreateChild(root, "bg");
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);
        Stretch(bg.GetComponent<RectTransform>());

        float y = 220f;
        CreateLabel(root, "账号", y);
        inputAccount = CreateInput(root, "inputAccount", y - 50f, "test");
        y -= 140f;
        CreateLabel(root, "IP", y);
        inputHost = CreateInput(root, "inputHost", y - 50f, "127.0.0.1");
        y -= 140f;
        CreateLabel(root, "端口", y);
        inputPort = CreateInput(root, "inputPort", y - 50f, "10086");
        y -= 140f;
        CreateLabel(root, "服务器标识", y);
        inputServer = CreateInput(root, "inputServer", y - 50f, "1");
        y -= 180f;

        GameObject buttonGo = DefaultControls.CreateButton(new DefaultControls.Resources());
        buttonGo.name = "btnConnect";
        buttonGo.transform.SetParent(root, false);
        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(360, 72);
        buttonRect.anchoredPosition = new Vector2(0, y);
        Text buttonText = buttonGo.GetComponentInChildren<Text>();
        if (buttonText != null) {
            buttonText.text = "连接并登录";
            buttonText.fontSize = 28;
        }
        btnConnect = buttonGo.GetComponent<Button>();

        GameObject statusGo = DefaultControls.CreateText(new DefaultControls.Resources());
        statusGo.name = "txtStatus";
        statusGo.transform.SetParent(root, false);
        RectTransform statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(560, 80);
        statusRect.anchoredPosition = new Vector2(0, y - 100f);
        statusText = statusGo.GetComponent<Text>();
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.fontSize = 24;
        statusText.color = Color.white;
    }

    static void CreateLabel(RectTransform parent, string text, float y) {
        GameObject labelGo = DefaultControls.CreateText(new DefaultControls.Resources());
        labelGo.name = "label_" + text;
        labelGo.transform.SetParent(parent, false);
        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(560, 36);
        rect.anchoredPosition = new Vector2(0, y);
        Text label = labelGo.GetComponent<Text>();
        label.text = text;
        label.fontSize = 26;
        label.color = new Color(0.85f, 0.88f, 0.92f, 1f);
        label.alignment = TextAnchor.MiddleLeft;
    }

    static InputField CreateInput(RectTransform parent, string name, float y, string value) {
        GameObject inputGo = DefaultControls.CreateInputField(new DefaultControls.Resources());
        inputGo.name = name;
        inputGo.transform.SetParent(parent, false);
        RectTransform rect = inputGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(560, 64);
        rect.anchoredPosition = new Vector2(0, y);
        InputField input = inputGo.GetComponent<InputField>();
        input.text = value;
        return input;
    }

    static GameObject CreateChild(RectTransform parent, string name) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

}
