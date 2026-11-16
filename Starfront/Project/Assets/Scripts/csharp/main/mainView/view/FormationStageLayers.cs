using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>阵容底座独立图片层；逻辑Image继续供立绘定标和承托点计算。</summary>
public sealed class FormationStageLayers : MonoBehaviour {
    public Image metal;
    public Image shadow;
    public Image glow;
    public Sprite[] qualityGlows;
    private Image logicalStage;

    private void OnEnable() {
        logicalStage = GetComponent<Image>();
        logicalStage.enabled = false;
        Layout();
    }

    public void Refresh(bool occupied) {
        if (logicalStage == null) logicalStage = GetComponent<Image>();
        logicalStage.enabled = false;
        metal.enabled = true;
        if (shadow != null) shadow.enabled = true;
        glow.enabled = occupied;
        if (occupied) {
            int index;
            switch (logicalStage.sprite.name) {
                case "podium-green": index = 0; break;
                case "podium-blue": index = 1; break;
                case "podium-purple": index = 2; break;
                case "podium-gold": index = 3; break;
                default: throw new InvalidOperationException("阵容底座未映射品质：" + logicalStage.sprite.name);
            }
            glow.sprite = qualityGlows[index];
        }
        Layout();
    }

    private void LateUpdate() {
        Layout();
    }

    public void Layout() {
        if (logicalStage == null) logicalStage = GetComponent<Image>();
        if (metal == null || glow == null || !FormationDisplayLayout.TryGetStageTopWorld(logicalStage, out Vector3 world)) return;
        float factor = logicalStage.rectTransform.rect.width / 156f;
        if (shadow != null) Place(shadow.rectTransform, factor, world);
        Place(metal.rectTransform, factor, world);
        Place(glow.rectTransform, factor, world);
    }

    private static void Place(RectTransform rect, float factor, Vector3 world) {
        rect.pivot = new Vector2(.5f, 1f - (116f / 262f));
        rect.sizeDelta = new Vector2(183.4308725f, 93.914094f) * factor;
        rect.position = world;
    }
}
