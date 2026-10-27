using System;
using UnityEngine;

/// <summary>
/// 地表染色实验的编辑器参数。
/// </summary>
/// <remarks>只用于预览，不写入玩法配置或正式材质。</remarks>
[Serializable]
public sealed class TerrainTintLabSettings {

    public int mode = 3;

    public int layout;

    public Color red = new Color32(184, 74, 51, 255);

    public Color blue = new Color32(74, 132, 184, 255);

    public float strength = .85f;

    public float brightness = 1.25f;

    public float contrast = 1.15f;

    public float boundary = .5f;

    public Vector2 origin = new Vector2(360, 634);

    public Vector2 hexSize = new Vector2(BattleConst.CellWidth, BattleConst.CellHeight);

    public bool showGrid;

}
