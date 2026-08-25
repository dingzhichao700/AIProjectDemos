using DG.Tweening;
using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 公共窗口视图
/// </summary>
public class CommonPanelView : BaseView {

    /******************* UIComponent Define begin ************************/
    public RectTransform boxTitle;
    public RectTransform boxShoulder;
    public TextMeshProUGUI txtTitle;
    public GameButton btnClose;
    public RectTransform boxContent;
    public RectTransform imgTitleDecoLeft;
    public RectTransform imgTitleDecoRight;
    public RectTransform imgShoulderDecoLeft;
    public RectTransform imgShoulderDecoRight;
    public RectTransform imgBottomDecoLeft;
    public RectTransform imgBottomDecoRight;
    public RectTransform boxTitleContent;
    /******************* UIComponent Define finish ************************/

    /**设计宽度*/
    private float designWidth;
    /**设计高度*/
    private float designHeight;

    private CanvasGroup contentAlpha;
    private CanvasGroup titleAlpha;
    private CanvasGroup btnCloseAlpha;
    private CanvasGroup imgTitleDecoLeftAlpha;
    private CanvasGroup imgTitleDecoRightAlpha;
    private CanvasGroup imgShoulderDecoLeftAlpha;
    private CanvasGroup imgShoulderDecoRightAlpha;
    private CanvasGroup imgBottomDecoLeftAlpha;
    private CanvasGroup imgBottomDecoRightAlpha;

    private Action openHandler;
    private Action closeHandler;

    private const float TWEEN_DURATION = 0.35f;

    /**第1段延迟*/
    private const float OPEN_DELAY_1 = 0.3f;
    /**第2段延迟*/
    private const float OPEN_DELAY_2 = 0.2f;

    /**初始化完成接口*/
    override public void OnInit() {
        designWidth = (transform as RectTransform).rect.width;
        designHeight = (transform as RectTransform).rect.height;
        contentAlpha = GetComponent<CanvasGroup>();
        titleAlpha = boxTitleContent.GetComponent<CanvasGroup>();
        btnCloseAlpha = btnClose.GetComponent<CanvasGroup>();
        imgTitleDecoLeftAlpha = imgTitleDecoLeft.GetComponent<CanvasGroup>();
        imgTitleDecoRightAlpha = imgTitleDecoRight.GetComponent<CanvasGroup>();
        imgShoulderDecoLeftAlpha = imgShoulderDecoLeft.GetComponent<CanvasGroup>();
        imgShoulderDecoRightAlpha = imgShoulderDecoRight.GetComponent<CanvasGroup>();
        imgBottomDecoLeftAlpha = imgBottomDecoLeft.GetComponent<CanvasGroup>();
        imgBottomDecoRightAlpha = imgBottomDecoRight.GetComponent<CanvasGroup>();
    }

    /**设置标题*/
    public void SetTitle(string value) {
        txtTitle.text = value;
    }

    /**播打开*/
    public void PlayOpen(Action action) {
        KillAllTween();

        openHandler = action;
        contentAlpha.alpha = 0;
        contentAlpha.DOFade(1, TWEEN_DURATION).OnComplete(() => {
            openHandler.Invoke();
        });
        boxContent.sizeDelta = new Vector3(designWidth, designHeight * 0.6f, 0);
        boxContent.DOSizeDelta(new Vector3(designWidth, designHeight, 0), TWEEN_DURATION);

        //标题
        boxTitleContent.anchoredPosition = new Vector2(0, -30);
        boxTitleContent.DOAnchorPosY(0, TWEEN_DURATION).SetDelay(OPEN_DELAY_1);
        titleAlpha.alpha = 0;
        titleAlpha.DOFade(1, TWEEN_DURATION).SetDelay(OPEN_DELAY_1);

        //标题左右侧装饰
        if (imgTitleDecoLeft.gameObject.activeSelf) {
            float titleDelay = OPEN_DELAY_1 + 0.2f;
            //左侧
            imgTitleDecoLeft.anchoredPosition = new Vector2(-190, imgTitleDecoLeft.anchoredPosition.y);
            imgTitleDecoLeft.DOAnchorPosX(-210, TWEEN_DURATION).SetDelay(titleDelay);
            imgTitleDecoLeftAlpha.alpha = 0;
            imgTitleDecoLeftAlpha.DOFade(1, TWEEN_DURATION).SetDelay(titleDelay);

            //右侧
            imgTitleDecoRight.anchoredPosition = new Vector2(190, imgTitleDecoLeft.anchoredPosition.y);
            imgTitleDecoRight.DOAnchorPosX(210, TWEEN_DURATION).SetDelay(OPEN_DELAY_1 + OPEN_DELAY_2);
            imgTitleDecoRightAlpha.alpha = 0;
            imgTitleDecoRightAlpha.DOFade(1, TWEEN_DURATION).SetDelay(OPEN_DELAY_1 + OPEN_DELAY_2);
        }

        //窗口左右侧装饰
        if (imgShoulderDecoLeft.gameObject.activeSelf) {
            float shoulderDelay = OPEN_DELAY_1 + 0.4f;
            //左侧
            imgShoulderDecoLeft.anchoredPosition = new Vector2(-80, imgShoulderDecoLeft.anchoredPosition.y);
            imgShoulderDecoLeft.DOAnchorPosX(-100, TWEEN_DURATION).SetDelay(shoulderDelay);
            imgShoulderDecoLeftAlpha.alpha = 0;
            imgShoulderDecoLeftAlpha.DOFade(1, TWEEN_DURATION).SetDelay(shoulderDelay);
            //右侧
            imgShoulderDecoRight.anchoredPosition = new Vector2(80, imgShoulderDecoRight.anchoredPosition.y);
            imgShoulderDecoRight.DOAnchorPosX(100, TWEEN_DURATION).SetDelay(shoulderDelay);
            imgShoulderDecoRightAlpha.alpha = 0;
            imgShoulderDecoRightAlpha.DOFade(1, TWEEN_DURATION).SetDelay(shoulderDelay);
        }

        //窗口左右下角装饰
        if (imgBottomDecoLeft.gameObject.activeSelf) {
            imgBottomDecoLeft.eulerAngles = new Vector3(0, 0, 90);
            imgBottomDecoRight.eulerAngles = new Vector3(0, 0, -90);
            imgBottomDecoLeft.DORotate(Vector3.zero, TWEEN_DURATION).SetDelay(OPEN_DELAY_1);
            imgBottomDecoRight.DORotate(Vector3.zero, TWEEN_DURATION).SetDelay(OPEN_DELAY_1);
        }

        //关闭按钮
        float durationForBtnClose = TWEEN_DURATION * 1.3f;
        btnCloseAlpha.alpha = 0;
        btnCloseAlpha.DOFade(1, durationForBtnClose).SetDelay(OPEN_DELAY_1);
        btnClose.Trans.anchoredPosition = new Vector2(50, 90);
        btnClose.Trans.DOAnchorPosX(0, durationForBtnClose).SetDelay(OPEN_DELAY_1).SetEase(Ease.OutBounce);
    }

    /**播关闭*/
    public void PlayClose(Action action) {
        KillAllTween();

        closeHandler = action;

        contentAlpha.DOFade(0, TWEEN_DURATION).OnComplete(() => {
            closeHandler.Invoke();
        }).SetDelay(OPEN_DELAY_1); ;
        boxContent.DOSizeDelta(new Vector3(designWidth, designHeight * 0.6f, 0), TWEEN_DURATION).SetDelay(OPEN_DELAY_1); ;

        boxTitleContent.DOAnchorPosY(-30, TWEEN_DURATION);
        titleAlpha.DOFade(0, TWEEN_DURATION);

        if (imgTitleDecoLeft.gameObject.activeSelf) {
            imgTitleDecoLeft.DOAnchorPosX(-190, TWEEN_DURATION);
            imgTitleDecoRight.DOAnchorPosX(190, TWEEN_DURATION);
        }
        if (imgShoulderDecoLeft.gameObject.activeSelf) {
            imgShoulderDecoLeft.DOAnchorPosX(-80, TWEEN_DURATION);
            imgShoulderDecoRight.DOAnchorPosX(80, TWEEN_DURATION);
            imgShoulderDecoLeftAlpha.DOFade(0, TWEEN_DURATION);
            imgShoulderDecoRightAlpha.DOFade(0, TWEEN_DURATION);
        }
        if (imgBottomDecoLeft.gameObject.activeSelf) {
            imgBottomDecoLeft.DORotate(new Vector3(0, 0, 90), TWEEN_DURATION);
            imgBottomDecoRight.DORotate(new Vector3(0, 0, -90), TWEEN_DURATION);
        }

        btnCloseAlpha.DOFade(0, TWEEN_DURATION * 1.3f).SetDelay(OPEN_DELAY_1);
        btnClose.Trans.DOAnchorPosX(50, TWEEN_DURATION);
    }

    private void KillAllTween() {
        DOTween.Kill(contentAlpha);
        DOTween.Kill(boxContent);
        DOTween.Kill(titleAlpha);
        DOTween.Kill(btnCloseAlpha);
        DOTween.Kill(btnClose);
        DOTween.Kill(imgTitleDecoLeftAlpha);
        DOTween.Kill(imgTitleDecoRightAlpha);
        DOTween.Kill(imgShoulderDecoLeftAlpha);
        DOTween.Kill(imgShoulderDecoRightAlpha);
        DOTween.Kill(imgBottomDecoLeftAlpha);
        DOTween.Kill(imgBottomDecoRightAlpha);
        DOTween.Kill(imgBottomDecoLeft);
        DOTween.Kill(imgBottomDecoRight);
    }

}
