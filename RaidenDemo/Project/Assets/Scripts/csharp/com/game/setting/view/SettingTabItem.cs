using cfg;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 设置界面-tab选项item
/// </summary>
public class SettingTabItem : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public RectTransform boxContent;
    public Image imgBg;
    public RectTransform imgHoverLight;
    public TextMeshProUGUI txtLabel;
    /******************* UIComponent Define finish ************************/

    private CanvasGroup contentAlpha;
    private CanvasGroup hoverLightAlpha;

    /**当前是否被选中*/
    private bool isSelected => SettingControl.ins.selectTabIndex == listIndex;

    private const float TWEEN_DURATION = 0.1f;

    /**初始化完成接口*/
    override public void OnInit() {
        SettingControl.ins.On(SettingEvent.FOCUS_TAB_UPDATE, OnTabUpdate);
        SettingControl.ins.On(SettingEvent.FOCUS_STATE_CHANGED, OnFocusUpdate);
    }

    override protected void OnSetData(int index, object value) {
        txtLabel.text = SettingConst.GetTabName((SettingType)value);

        hoverLightAlpha = imgHoverLight.GetComponent<CanvasGroup>();
        hoverLightAlpha.alpha = 0;
        contentAlpha = boxContent.GetComponent<CanvasGroup>();
        contentAlpha.alpha = 0;
        boxContent.anchoredPosition = new Vector2(0, -10);

        OnTabUpdate();

        KillTween();
        PlayBorn();
    }

    private void PlayBorn() {
        contentAlpha.DOFade(1, 0.5f).SetDelay(0.1f * (listIndex + 1));
        boxContent.DOAnchorPosY(isSelected ? 0 : -5, 0.5f).SetDelay(0.1f * (listIndex + 1)).OnComplete(() => {
            PlayBornComplete();
        });
    }

    private void PlayBornComplete() {
        //OnClick(boxContent.gameObject, OnMouseClick);
        //OnEnter(boxContent.gameObject, OnMouseEnter);
        //OnExit(boxContent.gameObject, OnMouseExit);

        OnTabUpdate();
    }

    public void PlayExit(int delayIndex) {
        KillTween();
        contentAlpha.DOFade(0, 0.5f).SetDelay(0.1f * delayIndex);
        boxContent.DOAnchorPosY(-10, 0.5f).SetDelay(0.1f * delayIndex).OnComplete(() => {
            PlayExitComplete();
        });
    }

    private void PlayExitComplete() {
        OnEnter(boxContent.gameObject, OnMouseEnter);
        OnExit(boxContent.gameObject, OnMouseExit);
    }

    private void KillTween() {
        DOTween.Kill(contentAlpha);
        DOTween.Kill(boxContent);
    }

    /**鼠标划入*/
    private void OnMouseEnter(PointerEventData data) {
        hoverLightAlpha.DOFade(1, TWEEN_DURATION);

        if (!isSelected) {
            AudioManager.ins.PlaySoundById(AudioBusType.HINT, AudioConst.SOUND_HOVER_3, 0.7f);
        }
    }

    /**鼠标划出*/
    private void OnMouseExit(PointerEventData data) {
        hoverLightAlpha.DOFade(0, TWEEN_DURATION);
    }

    /**鼠标点击*/
    private void OnMouseClick(PointerEventData data) {
        SettingControl.ins.SetTabIndex(listIndex);
        AudioManager.ins.PlaySoundById(AudioBusType.HINT, AudioConst.SOUND_CLICK_ENABLE_3);
    }

    private void OnTabUpdate() {
        //UITools.SetImage(imgBg, ResourceConst.PATH_ATLAS_COMMON + (isSelected ? "tabButton2_selected" : "tabButton2_unselected"));
        boxContent.DOAnchorPosY(isSelected ? 0 : -5, 0.5f);
        if (!isSelected) {
            hoverLightAlpha.DOFade(0, TWEEN_DURATION);
        }
        UpdateLabelColor();
    }

    private void OnFocusUpdate() {
        UpdateLabelColor();
    }

    private void UpdateLabelColor() {
        txtLabel.color = isSelected ? (SettingControl.ins.isFocusTab ? ColorConst.RED : ColorConst.RED_DARK) : ColorConst.BLACK;
    }

}
