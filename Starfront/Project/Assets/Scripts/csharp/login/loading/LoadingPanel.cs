using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示队列加载进度，并保持模板预制体的成员绑定。
/// </summary>
public class LoadingPanel : BasePanel {

    public TextMeshProUGUI txtProcess;
    public Image imgProcess;
    public CanvasGroup alphaContent;
    public RectTransform boxProcess;

    private int requestVersion;

    /**设置加载界面层级*/
    public LoadingPanel() {
        layer = PanelLayer.SCALE_LOADING;
    }

    public override void OnOpen() {
        alphaContent.alpha = 1f;
        var request = openParams != null && openParams.Length > 0 ? openParams[0] as LoadingRequest : null;
        if (request == null) {
            throw new InvalidOperationException("LoadingPanel requires LoadingRequest.");
        }
        RunQueue(request, ++requestVersion);
    }

    public override void OnClose() {
        requestVersion++;
    }

    /**执行稳定请求快照，关闭后的结果不再更新界面*/
    private async void RunQueue(LoadingRequest request, int version) {
        try {
            SetProgress(0f);
            if (request.steps != null && request.steps.Count > 0) {
                for (int i = 0; i < request.steps.Count; i++) {
                    int index = i;
                    await request.steps[i](value => {
                        if (version == requestVersion) {
                            SetProgress((index + Mathf.Clamp01(value)) / request.steps.Count);
                        }
                    });
                    if (version != requestVersion) {
                        request.onFailed?.Invoke(new OperationCanceledException("Loading panel closed."));
                        return;
                    }
                }
            } else {
                await ResourceLoader.LoadListAsync(request.resources, onProgress: value => {
                    if (version == requestVersion) {
                        SetProgress(value);
                    }
                });
            }
            if (version != requestVersion) {
                request.onFailed?.Invoke(new OperationCanceledException("Loading panel closed."));
                return;
            }
            SetProgress(1f);
            Close();
            request.onComplete?.Invoke();
        } catch (Exception exception) {
            if (version == requestVersion) {
                Close();
            }
            request.onFailed?.Invoke(exception);
        }
    }

    /**按已完成任务比例更新原有进度控件*/
    private void SetProgress(float value) {
        imgProcess.fillAmount = Mathf.Clamp01(value);
        txtProcess.text = "Loading " + Mathf.FloorToInt(value * 100f) + "%";
    }

}
