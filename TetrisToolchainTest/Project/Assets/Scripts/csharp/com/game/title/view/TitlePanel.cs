using TMPro;
using UnityEngine;

/// <summary>
/// 标题界面
/// </summary>
public class TitlePanel : BasePanel
{
    public RectTransform boxEff;
    public TextMeshProUGUI txt;

    FrameAnimationView effect;

    public TitlePanel()
    {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen()
    {
        txt.gameObject.SetActive(false);

        effect = FrameAnimationView.GetInstance();
        effect.trans.SetParent(boxEff);
        effect.trans.localPosition = new Vector2(0, -500);
        effect.Play(ResourceConst.GetFrameAnimationPath("title/title_open"), false, Handler.Create(this, OnBornEffectComplete), false, 4, 1, 1f);

        RookieEngine.timer.Once(this, 300, OnShowTitle);
    }

    void AddLis()
    {
        KeyBoardControl.ins.OnAnyKeyDown(OnKeyDown);
    }

    void RemoveLis()
    {
        KeyBoardControl.ins.OffAnyKeyDown(OnKeyDown);
    }

    void OnShowTitle()
    {
        txt.gameObject.SetActive(true);
        AddLis();
    }

    void OnBornEffectComplete()
    {
        effect.Play(ResourceConst.GetFrameAnimationPath("title/title_loop"), true, null, false, 4, 1, 1f);
        AudioManager.ins.PlayBgm(ResourceConst.GetAudio(AudioConst.BGM_TITLE));
    }

    void OnKeyDown(KeyCode code)
    {
        if (code == KeyCode.Mouse0 || code == KeyCode.Mouse1 || code == KeyCode.Mouse2)
        {
            return;
        }

        Close();
        PanelMgr.ins.OpenPanel(UIEnum.OPTION_PANEL);
    }

    public override void OnClose()
    {
        RemoveLis();
        if (effect != null)
        {
            effect.Destroy();
            effect = null;
        }
    }
}
