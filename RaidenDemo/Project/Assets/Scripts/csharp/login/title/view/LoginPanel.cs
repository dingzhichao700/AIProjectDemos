using TMPro;
using System;
using UnityEngine;

/// <summary>
/// 登录界面
/// </summary>
public class LoginPanel : BasePanel {

    public RectTransform boxEff;
    public TextMeshProUGUI txt;

    FrameAnimationView effect;

    /**提示出现后才接受输入，转场时立即关闭以避免重复触发*/
    private bool acceptingInput;

    /**配置加载状态与当前界面生命周期标识*/
    private bool configsReady;
    private bool loadingConfigs;
    private bool promptReady;
    private int openVersion;

    /**设置登录界面的显示层级*/
    public LoginPanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        acceptingInput = false;
        configsReady = false;
        promptReady = false;
        openVersion++;
        LoadConfigs(openVersion);
        txt.gameObject.SetActive(false);

        effect = FrameAnimationView.GetInstance();
        effect.trans.SetParent(boxEff);
        effect.trans.localPosition = new Vector2(0, -500);
        effect.Play(ResourceConst.GetFrameAnimationPath("title/title_open"), false, Handler.Create(this, OnBornEffectComplete), false, 4, 1, 1f);

        RookieEngine.timer.Once(this, 300, OnShowLogin);
    }

    void AddLis() {
        KeyBoardControl.ins.OnAnyKeyDown(OnKeyDown);
    }

    void RemoveLis() {
        KeyBoardControl.ins.OffAnyKeyDown(OnKeyDown);
    }

    void OnShowLogin() {
        promptReady = true;
        txt.gameObject.SetActive(true);
        acceptingInput = true;
        txt.text = configsReady ? "点击屏幕或按任意键开始" : loadingConfigs ? "正在加载配置…" : "配置加载失败，点击或按任意键重试";
        AddLis();
    }

    /**加载配置，失败后允许通过原有开始输入重试*/
    private async void LoadConfigs(int version) {
        loadingConfigs = true;
        try {
            await CfgManager.EnsureLoadedAsync();
            if (version != openVersion) {
                return;
            }
            configsReady = true;
            if (promptReady) {
                txt.text = "点击屏幕或按任意键开始";
            }
        } catch (Exception exception) {
            Debug.LogException(exception);
            if (version == openVersion) {
                txt.text = "配置加载失败，点击或按任意键重试";
            }
        } finally {
            if (version == openVersion) {
                loadingConfigs = false;
            }
        }
    }

    /**接收鼠标点击和触摸开始，兼容无键盘设备*/
    private void Update() {
        if (!acceptingInput) {
            return;
        }
        if (Input.GetMouseButtonDown(0)) {
            EnterOptions();
            return;
        }
        for (int i = 0; i < Input.touchCount; i++) {
            if (Input.GetTouch(i).phase == TouchPhase.Began) {
                EnterOptions();
                return;
            }
        }
    }

    void OnBornEffectComplete() {
        effect.Play(ResourceConst.GetFrameAnimationPath("title/title_loop"), true, null, false, 4, 1, 1f);
        AudioManager.ins.PlayBgm(ResourceConst.GetAudio(AudioConst.BGM_TITLE));
    }

    void OnKeyDown(KeyCode code) {
        if (code == KeyCode.Mouse0 || code == KeyCode.Mouse1 || code == KeyCode.Mouse2) {
            return;
        }

        EnterOptions();
    }

    /// <summary>
    /// 接受一次开始输入并进入选项界面。
    /// </summary>
    private void EnterOptions() {
        if (!acceptingInput) {
            return;
        }
        if (!configsReady) {
            if (!loadingConfigs) {
                txt.text = "正在加载配置…";
                LoadConfigs(openVersion);
            }
            return;
        }
        acceptingInput = false;
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.OPTION_PANEL);
    }

    public override void OnClose() {
        openVersion++;
        acceptingInput = false;
        RookieEngine.timer.Clear(this, OnShowLogin);
        RemoveLis();
        if (effect != null) {
            effect.Recover();
            effect = null;
        }
    }

}
