using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 雷电战机关卡路线项
/// </summary>
public class StagePathItem : BaseView {

    /******************* UIComponent Define begin ************************/
    public Image imgPath;
    /******************* UIComponent Define finish ************************/


    /**已解锁路线图片*/
    [SerializeField] private Sprite brightSprite;

    /**未解锁路线图片*/
    [SerializeField] private Sprite darkSprite;

    /**根据后继关卡的解锁状态切换路线段亮暗，并保持三宫格拉伸方式*/
    public void SetState(bool bright) {
        RectTransform rect = imgPath.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        imgPath.sprite = bright ? brightSprite : darkSprite;
        imgPath.type = Image.Type.Sliced;
    }

}
