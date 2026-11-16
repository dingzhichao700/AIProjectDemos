using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingProgressView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingProgressView : BaseView {

    /******************* UIComponent Define begin ************************/
    public RectTransform fillMask;
    /******************* UIComponent Define finish ************************/

    private float fullWidth;
    public override void OnInit() { fullWidth=((RectTransform)transform).rect.width; }

    /**基准来自不会随进度缩短的根容器，换项和重开均重新按此长度求值。*/
    public void Render(float progress) {
        if (fullWidth <= 0) fullWidth=((RectTransform)transform).rect.width;
        if (fullWidth <= 0) throw new InvalidOperationException("缺少进度条满长度基准");
        float value=Mathf.Clamp01(progress); fillMask.gameObject.SetActive(value>0);
        fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Round(fullWidth*value));
    }

    public void ResetVisual() { Render(0); }

}
