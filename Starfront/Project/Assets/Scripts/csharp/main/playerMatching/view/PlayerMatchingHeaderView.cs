using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingHeaderView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingHeaderView : BaseView {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtTitle;
    /******************* UIComponent Define finish ************************/

    public void Render(PlayerMatchingUiState state) {
        switch (state) {
            case PlayerMatchingUiState.Searching: txtTitle.text="搜索对手"; break;
            case PlayerMatchingUiState.Countdown: txtTitle.text="准备就绪"; break;
            case PlayerMatchingUiState.Failed: txtTitle.text="准备失败"; break;
            case PlayerMatchingUiState.Entering: txtTitle.text="进入战斗"; break;
            default: txtTitle.text="匹配成功"; break;
        }
    }

}
