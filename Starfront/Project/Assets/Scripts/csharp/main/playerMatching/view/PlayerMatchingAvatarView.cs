using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingAvatarView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingAvatarView : BaseView {

    /******************* UIComponent Define begin ************************/
    public Image imgPortrait;
    /******************* UIComponent Define finish ************************/

    public void Render(Sprite portrait) { imgPortrait.sprite=portrait; imgPortrait.enabled=portrait!=null; }
    public override void Clear() { imgPortrait.sprite=null; imgPortrait.enabled=false; }

}
