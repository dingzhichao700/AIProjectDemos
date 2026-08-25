using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 开场界面。播完 Logo 后预加载 Title 资源并进入标题界面。
/// </summary>
public class OpeningPanel : BasePanel
{
    public RectTransform boxEff;
    public CanvasGroup boxContent;
    public CanvasGroup canvasGroupStudioMark;

    FrameAnimationView effect;
    bool preloadComplete;
    bool effectPlayComplete;

    public OpeningPanel()
    {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen()
    {
        canvasGroupStudioMark.alpha = 0;
#if UNITY_EDITOR
        CfgManager.Clear();
#endif
        LoadSelfResource();
        LoadTitleResource();
    }

    async void LoadSelfResource()
    {
        var preload = new List<ResLoadInfo>
        {
            new ResLoadInfo(ResourceConst.GetAudio(AudioConst.EFFECT_OPENING), ResType.Audio),
            new ResLoadInfo(ResourceConst.GetFrameAnimationPath("opening/box_open"), ResType.FrameAnim)
        };
        await ResourceLoader.LoadListAsync(preload, LoadSelfResourceComplete);
    }

    void LoadSelfResourceComplete()
    {
        AudioManager.ins.PlaySound(AudioBusType.HINT, ResourceConst.GetAudio(AudioConst.EFFECT_OPENING));

        effect = FrameAnimationView.GetInstance();
        effect.trans.SetParent(boxEff);
        effect.trans.localPosition = Vector3.zero;
        effect.Play(ResourceConst.GetFrameAnimationPath("opening/box_open"), false, null, false, 2f);
        canvasGroupStudioMark.DOFade(1, 1.5f).OnComplete(OnMarkTweenComplete).SetDelay(0.5f);
    }

    void OnMarkTweenComplete()
    {
        effectPlayComplete = true;
        TryPlayClose();
    }

    async void LoadTitleResource()
    {
        var preload = new List<ResLoadInfo>
        {
            new ResLoadInfo(ResourceConst.PATH_CONFIG + "cfgobj_settingoptionobj", ResType.Json),
            new ResLoadInfo(ResourceConst.GetFrameAnimationPath("title/title_open"), ResType.FrameAnim),
            new ResLoadInfo(ResourceConst.GetFrameAnimationPath("title/title_loop"), ResType.FrameAnim),
            new ResLoadInfo(ResourceConst.GetAudio(AudioConst.BGM_TITLE), ResType.Audio)
        };
        await ResourceLoader.LoadListAsync(preload, LoadTitleComplete);
    }

    void LoadTitleComplete()
    {
        preloadComplete = true;
        TryPlayClose();
    }

    void TryPlayClose()
    {
        if (preloadComplete && effectPlayComplete)
        {
            Close();
        }
    }

    protected override void PlayClose()
    {
        boxContent.DOFade(0, 1.5f).OnComplete(() =>
        {
            effect?.Destroy();
            effect = null;
            PlayCloseComplete();
        }).SetDelay(0.5f);
    }

    public override void OnClose()
    {
        PanelMgr.ins.OpenPanel(UIEnum.TITLE_PANEL);
    }
}
