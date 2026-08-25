using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 雷电战机关卡选择项
/// </summary>
public class StageSelectItem : BaseView {

    /******************* UIComponent Define begin ************************/
    public GameButton btnStage;

    public Image imgSelectionGlow;

    public TextMeshProUGUI txtStageNumber;

    public Image imgStar1;

    public Image imgStar2;

    public Image imgStar3;
    /******************* UIComponent Define finish ************************/


    /**选中状态图片*/
    [SerializeField] private Sprite selectedSprite;

    /**已解锁状态图片*/
    [SerializeField] private Sprite unlockedSprite;

    /**未解锁状态图片*/
    [SerializeField] private Sprite lockedSprite;

    /**关卡按钮背景*/
    private Image background;

    /**当前点击回调*/
    private UnityAction clickAction;

    /**关卡编号*/
    public int StageId { get; private set; }

    /**是否已经解锁*/
    public bool IsUnlocked { get; private set; }

    /// <summary>
    /// 使用运行时关卡数据初始化模板实例，并替换旧的点击回调
    /// </summary>
    /// <param name="stageId">关卡编号</param>
    /// <param name="unlocked">是否已经解锁</param>
    /// <param name="starCount">历史最高星级</param>
    /// <param name="onClick">选中该关卡时执行的回调</param>
    public void SetData(int stageId, bool unlocked, int starCount, UnityAction onClick) {
        StageId = stageId;
        IsUnlocked = unlocked;
        clickAction = onClick;
        background = btnStage.GetComponent<Image>();

        btnStage.text = stageId.ToString();
        txtStageNumber.text = stageId.ToString();
        txtStageNumber.fontSize = stageId >= 10 ? 39 : 47;
        txtStageNumber.rectTransform.anchoredPosition = new Vector2(unlocked ? 45 : 37, unlocked ? -21 : -34);
        txtStageNumber.rectTransform.sizeDelta = new Vector2(unlocked ? 50 : 66, unlocked ? 54 : 56);

        SetStars(Mathf.Clamp(starCount, 0, 3));
        SetSelected(false);
        OnClick(btnStage.gameObject, HandleClick);
    }

    /**切换选中态，锁定关卡始终保持锁定外观且不显示选中光效*/
    public void SetSelected(bool selected) {
        if (background == null) background = btnStage.GetComponent<Image>();
        background.sprite = selected && IsUnlocked ? selectedSprite : IsUnlocked ? unlockedSprite : lockedSprite;
        imgSelectionGlow.gameObject.SetActive(selected && IsUnlocked);
    }

    public override void Clear() {
        if (btnStage != null) OffClick(btnStage.gameObject, HandleClick);
        clickAction = null;
    }

    private void SetStars(int count) {
        Image[] stars = { imgStar1, imgStar2, imgStar3 };
        for (int i = 0; i < stars.Length; i++) stars[i].gameObject.SetActive(IsUnlocked && i < count);
    }

    private void HandleClick() {
        clickAction?.Invoke();
    }

}
