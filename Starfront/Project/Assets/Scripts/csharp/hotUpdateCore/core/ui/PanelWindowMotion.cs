using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>弹窗内容上移10设计像素淡入/淡出；遮罩只改变透明度。</summary>
public sealed class PanelWindowMotion : MonoBehaviour {
    private RectTransform content;
    private CanvasGroup alpha;
    private Image mask;
    private Image blocker;
    private Sequence tween;
    private Vector2 position;
    private float maskOpacity;
    private bool configured;

    public void Configure(BasePanel panel, RectTransform explicitContent, Image backdrop) {
        if (configured) {
            return;
        }
        content = explicitContent;
        if (content == null) {
            var parent = panel.SafeContentRoot;
            var children = new Transform[parent.childCount];
            for (int i = 0; i < children.Length; i++) {
                children[i] = parent.GetChild(i);
            }
            content = new GameObject("windowMotionContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(parent, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            foreach (var child in children) {
                child.SetParent(content, false);
            }
        }
        alpha = content.GetComponent<CanvasGroup>();
        if (alpha == null) {
            alpha = content.gameObject.AddComponent<CanvasGroup>();
        }
        mask = backdrop;
        maskOpacity = mask != null ? mask.color.a : 0f;
        var go = new GameObject("windowMotionInputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(panel.transform, false);
        blocker = go.GetComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;
        var rect = blocker.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        blocker.enabled = false;
        configured = true;
    }

    public void Open(Action complete) {
        tween?.Kill();
        position = content.anchoredPosition;
        content.anchoredPosition = position + Vector2.up * 10f;
        alpha.alpha = 0f;
        if (mask != null) {
            var color = mask.color;
            color.a = 0f;
            mask.color = color;
        }
        Play(true, complete);
    }

    public void Close(Action complete) {
        tween?.Kill();
        Play(false, complete);
    }

    private void Play(bool opening, Action complete) {
        blocker.transform.SetAsLastSibling();
        blocker.enabled = true;
        alpha.interactable = false;
        float duration = opening ? .48f : .18f;
        Ease ease = opening ? Ease.OutQuad : Ease.InQuad;
        tween = DOTween.Sequence().SetUpdate(true);
        tween.Join(content.DOAnchorPos(opening ? position : position + Vector2.up * 10f, duration).SetEase(ease));
        tween.Join(alpha.DOFade(opening ? 1f : 0f, duration).SetEase(ease));
        if (mask != null) {
            tween.Join(mask.DOFade(opening ? maskOpacity : 0f, duration).SetEase(ease));
        }
        tween.OnComplete(() => {
            tween = null;
            if (!opening) {
                content.anchoredPosition = position;
            }
            blocker.enabled = false;
            alpha.interactable = opening;
            complete();
        });
    }

    private void OnDisable() {
        tween?.Kill();
        tween = null;
        if (configured) {
            content.anchoredPosition = position;
            blocker.enabled = false;
        }
    }
}
