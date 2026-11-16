using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingCardView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingCardView : BaseView {

    /******************* UIComponent Define begin ************************/
    public Image imgArtwork;
    public Image imgQuality;
    public Image imgProfession;
    public TextMeshProUGUI txtCost;
    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtLevel;
    /******************* UIComponent Define finish ************************/

    public void Render(PlayerMatchingCardDisplay card) {
        Assign(imgArtwork,card.Artwork); Assign(imgQuality,card.Quality); Assign(imgProfession,card.Profession);
        PlayerMatchingTextBounds.Fit(txtName); PlayerMatchingTextBounds.Fit(txtLevel); PlayerMatchingTextBounds.Fit(txtCost);
        txtCost.text=card.Cost.ToString(); txtName.text=card.Name; txtLevel.text="Lv."+card.Level;
    }

    public override void Clear() {
        Assign(imgArtwork,null); Assign(imgQuality,null); Assign(imgProfession,null);
        txtCost.text=txtName.text=txtLevel.text=string.Empty;
    }

    private static void Assign(Image image, Sprite sprite) { image.sprite=sprite; image.enabled=sprite!=null; }

}
