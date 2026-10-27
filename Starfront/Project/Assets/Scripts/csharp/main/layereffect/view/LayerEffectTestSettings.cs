using System;
using UnityEngine;

/// <summary>
/// 图层效果测试界面的本地预览参数。
/// </summary>
/// <remarks>只用于预览，不写入玩法配置或正式材质。</remarks>
[Serializable]
public sealed class LayerEffectTestSettings {

    public int mode = 3;

    public int layout;

    public Color red = new Color32(184, 74, 51, 255);

    public Color blue = new Color32(74, 132, 184, 255);

    public float strength = 1f;

    public float brightness = 1.12f;

    public float contrast = .99f;

    public float boundary = .5f;

    public Vector2 origin = new Vector2(360, 634);

    public Vector2 hexSize = new Vector2(107, 93);

    // The approved source already contains its original grid lines.
    public bool showGrid = false;

    public float gridLineWidth = .8f;

    public float gridLineOpacity = .28f;

    public float cutBrushRadius = 22f;

    public const int PreviewWidth = 720;

    public const int PreviewHeight = 1280;

}
