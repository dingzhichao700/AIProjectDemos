using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 加载界面
/// </summary>
public class LoadingPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtProcess;
    public Image imgProcess;
    public CanvasGroup alphaContent;
    public RectTransform boxProcess;
    /******************* UIComponent Define finish ************************/

    /**加载完成回调*/
    private Action loadCompleteHandler;

    public LoadingPanel() {
        layer = PanelLayer.SCALE_LOADING;
    }

    override protected void PlayOpen() {
        imgProcess.fillAmount = 0;
        txtProcess.text = "加载中 0%";

        //不透明度提高，淡入
        alphaContent.alpha = 0;
        alphaContent.DOFade(1, 1.5f).OnComplete(() => {
            PlayOpenComplete();
        });
    }

    override public void OnOpen() {
        loadCompleteHandler = openParams[1] as Action;
        LoadResource();
    }

    /**加载资源*/
    private async void LoadResource() {
        List<ResLoadInfo> loadList = openParams[0] as List<ResLoadInfo>;
        await ResourceLoader.LoadListAsync(loadList, LoadResComplete, LoadResProcess);
    }

    /**加载完成*/
    private void LoadResComplete() {
        loadCompleteHandler?.Invoke();
        RookieEngine.timer.Once(this, 500, Close);
    }

    override protected void PlayClose() {
        //不透明度降低，淡出
        alphaContent.DOFade(0, 0.75f).OnComplete(() => {
            PlayCloseComplete();
        });
    }

    /**加载进度变化*/
    private void LoadResProcess(float process) {
        imgProcess.fillAmount = process;
        txtProcess.text = $"加载中 " + Mathf.FloorToInt(process * 100) + "%";
    }

    override public void OnClose() {
    }

}
