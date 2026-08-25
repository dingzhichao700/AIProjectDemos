using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用资源加载界面，展示真实加载进度并在结束后回调业务调用方。
/// </summary>
public class LoadingPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public Image imgBg;

    public TextMeshProUGUI txtTip;

    public RectTransform barLoadProgress;

    public Image imgLoadProgressFill;

    public TextMeshProUGUI txtLoadProgress;

    public TextMeshProUGUI txtStatus;
    /******************* UIComponent Define finish ************************/


    /**进度条满状态宽度*/
    private const float FullProgressWidth = 546f;

    /**三宫格填充保持端帽所需的最小宽度*/
    private const float MinimumProgressWidth = 24f;

    /**默认提示文本*/
    private const string DefaultTip = "\u6b63\u5728\u8fdb\u884c\u51fa\u51fb\u51c6\u5907\uff0c\u8bf7\u7a0d\u5019\u3002";

    /**加载中状态文本*/
    private const string LoadingText = "\u6b63\u5728\u52a0\u8f7d\u2026";

    /**加载完成状态文本*/
    private const string CompletedText = "\u52a0\u8f7d\u5b8c\u6210";

    /**加载失败状态文本*/
    private const string FailedText = "\u52a0\u8f7d\u5931\u8d25";

    /**本次加载请求*/
    private LoadingRequest request;

    /**背景是否已经应用到界面*/
    private bool backgroundApplied;

    public LoadingPanel() {
        layer = PanelLayer.SCALE_LOADING;
    }

    public override void OnOpen() {
        request = openParams != null && openParams.Length > 0
            ? openParams[0] as LoadingRequest
            : null;
        if (request == null) {
            Debug.LogError("LoadingPanel requires LoadingRequest");
            Close();
            return;
        }
        backgroundApplied = string.IsNullOrEmpty(request.backgroundPath);
        if (!backgroundApplied) {
            imgBg.sprite = null;
        }
        txtTip.text = SelectTip(request.tips);
        SetProgress(0f);
        LoadResources();
    }

    public override void OnClose() {
        request = null;
        backgroundApplied = false;
    }

    private async void LoadResources() {
        try {
            // 在异步开始前生成稳定快照，保证背景和业务资源共用同一条进度链。
            List<ResLoadInfo> resources = BuildLoadList(request);
            await ResourceLoader.LoadListAsync(resources, null, SetProgress);
            ApplyBackgroundIfReady();
            txtStatus.text = CompletedText;
            LoadingRequest completedRequest = request;
            completedRequest?.onComplete?.Invoke();
            Close();
        } catch (Exception exception) {
            Debug.LogError($"Resource loading failed: {exception}");
            txtStatus.text = FailedText;
            LoadingRequest failedRequest = request;
            Close();
            failedRequest?.onFailed?.Invoke(exception);
        }
    }

    private static List<ResLoadInfo> BuildLoadList(LoadingRequest loadingRequest) {
        List<ResLoadInfo> result = new List<ResLoadInfo>();
        if (!string.IsNullOrEmpty(loadingRequest.backgroundPath)) {
            result.Add(new ResLoadInfo(loadingRequest.backgroundPath, ResType.UnpackImage));
        }
        // 背景可能同时出现在调用方清单中，合并时按路径和资源类型去重。
        foreach (ResLoadInfo resource in loadingRequest.resources) {
            if (!Contains(result, resource)) {
                result.Add(resource);
            }
        }
        return result;
    }

    private static bool Contains(List<ResLoadInfo> list, ResLoadInfo target) {
        foreach (ResLoadInfo item in list) {
            if (item.path == target.path && item.resType == target.resType) {
                return true;
            }
        }
        return false;
    }

    private string SelectTip(IReadOnlyList<string> tips) {
        if (tips == null || tips.Count == 0) {
            return DefaultTip;
        }
        return tips[UnityEngine.Random.Range(0, tips.Count)];
    }

    private void SetProgress(float value) {
        float progress = Mathf.Clamp01(value);
        ApplyBackgroundIfReady();
        bool visible = progress > 0f;
        imgLoadProgressFill.gameObject.SetActive(visible);
        Vector2 size = imgLoadProgressFill.rectTransform.sizeDelta;
        // 三宫格填充在非零状态下保留最小端帽宽度；零值通过隐藏图片表达真正的空状态。
        size.x = visible
            ? Mathf.Max(MinimumProgressWidth, Mathf.Round(FullProgressWidth * progress))
            : MinimumProgressWidth;
        imgLoadProgressFill.rectTransform.sizeDelta = size;
        txtLoadProgress.text = $"{Mathf.FloorToInt(progress * 100f)}%";
        txtStatus.text = progress >= 1f ? CompletedText : LoadingText;
    }

    private void ApplyBackgroundIfReady() {
        if (backgroundApplied || request == null || string.IsNullOrEmpty(request.backgroundPath)) {
            return;
        }
        if (!ResourceManager.HasLoadedUnpackImage(request.backgroundPath)) {
            return;
        }
        imgBg.sprite = ResourceManager.GetUnpackImage(request.backgroundPath);
        backgroundApplied = true;
    }

}
