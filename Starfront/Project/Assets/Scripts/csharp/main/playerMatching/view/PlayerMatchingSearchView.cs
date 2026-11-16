using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingSearchView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingSearchView : BaseView {

    /******************* UIComponent Define begin ************************/
    public RectTransform arc42;
    public RectTransform arc30;
    public RectTransform arc18;
    /******************* UIComponent Define finish ************************/

    public void TickVisual(float deltaTime) {
        arc42.Rotate(0,0,-70*deltaTime); arc30.Rotate(0,0,100*deltaTime); arc18.Rotate(0,0,-130*deltaTime);
    }

    public void ResetVisual() { arc42.localRotation=arc30.localRotation=arc18.localRotation=Quaternion.identity; }

}
