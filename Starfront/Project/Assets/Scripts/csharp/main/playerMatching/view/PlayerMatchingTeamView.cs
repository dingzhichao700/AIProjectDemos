using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingTeamView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingTeamView : BaseView {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtIdentity;
    public PlayerMatchingAvatarView avatarView;
    public PlayerMatchingCardView card1;
    public PlayerMatchingCardView card2;
    public PlayerMatchingCardView card3;
    public PlayerMatchingCardView card4;
    /******************* UIComponent Define finish ************************/

    public void Render(PlayerMatchingTeamDisplay team) {
        if (team == null) { Clear(); return; }
        PlayerMatchingTextBounds.Fit(txtName); PlayerMatchingTextBounds.Fit(txtIdentity);
        txtName.text=team.Name; txtIdentity.text=team.Identity; avatarView.Render(team.Portrait);
        var slots=new[] { card1,card2,card3,card4 };
        for (int i=0;i<slots.Length;i++) {
            if (slots[i] == null) continue;
            bool populated=i<team.Cards.Count; slots[i].gameObject.SetActive(populated);
            if (populated) slots[i].Render(team.Cards[i]); else slots[i].Clear();
        }
    }

    public override void Clear() {
        txtName.text=txtIdentity.text=string.Empty; avatarView.Clear();
        foreach (var card in new[] {card1,card2,card3,card4}) if (card != null) { card.Clear(); card.gameObject.SetActive(false); }
    }

}
