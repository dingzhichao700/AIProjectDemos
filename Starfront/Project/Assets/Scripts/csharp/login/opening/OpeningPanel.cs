using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 播放开场表现并预加载登录表现资源。
/// </summary>
public class OpeningPanel : BasePanel
{
    public RectTransform boxEff;
    public CanvasGroup boxContent;
    public CanvasGroup canvasGroupStudioMark;

    FrameAnimationView effect;
    bool preloadComplete;
    bool effectPlayComplete;

    /**从界面打开起记录里程碑，区分资源等待与演出时间*/
    readonly System.Diagnostics.Stopwatch openingWatch = new System.Diagnostics.Stopwatch();

    public OpeningPanel()
    {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen()
    {
        openingWatch.Restart();
        canvasGroupStudioMark.alpha = 0;
        LoadSelfResource();
        LoadLoginResource();
    }

    //加载本界面资源
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
        LoadTiming.Report("Opening.SelfReady", openingWatch);
        AudioManager.ins.PlaySound(AudioBusType.HINT, ResourceConst.GetAudio(AudioConst.EFFECT_OPENING));

        effect = FrameAnimationView.GetInstance();
        effect.trans.SetParent(boxEff);
        effect.trans.localPosition = Vector3.zero;
        effect.Play(ResourceConst.GetFrameAnimationPath("opening/box_open"), false, null, false, 2f);
        canvasGroupStudioMark.DOFade(1, 1.5f).OnComplete(OnMarkTweenComplete).SetDelay(0.5f);
    }

    void OnMarkTweenComplete()
    {
        LoadTiming.Report("Opening.FadeInComplete", openingWatch);
        effectPlayComplete = true;
        TryPlayClose();
    }

    //预加载Login界面资源
    async void LoadLoginResource()
    {
        var preload = new List<ResLoadInfo>
        {
            new ResLoadInfo(ResourceConst.GetFrameAnimationPath("title/title_open"), ResType.FrameAnim),
            new ResLoadInfo(ResourceConst.GetFrameAnimationPath("title/title_loop"), ResType.FrameAnim),
            new ResLoadInfo(ResourceConst.GetAudio(AudioConst.BGM_TITLE), ResType.Audio)
        };
        await ResourceLoader.LoadListAsync(preload, LoadTitleComplete);
    }

    void LoadTitleComplete()
    {
        LoadTiming.Report("Opening.TitleResourcesReady", openingWatch);
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
        LoadTiming.Report("Opening.FadeOutBegin", openingWatch);
        boxContent.DOFade(0, 1.5f).OnComplete(() =>
        {
            effect?.Recover();
            effect = null;
            PlayCloseComplete();
        }).SetDelay(0.5f);
    }

    public override void OnClose()
    {
        LoadTiming.Report("Opening.RequestLoginPanel", openingWatch);
        PanelMgr.ins.OpenPanel(UIEnum.LOGIN_PANEL);
    }
}
