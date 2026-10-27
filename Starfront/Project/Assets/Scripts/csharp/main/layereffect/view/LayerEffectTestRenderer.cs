using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时灰度与阵营染色预览的GPU资源。
/// </summary>
/// <remarks>仅持有界面本地材质和绘制覆盖；不依赖UnityEditor或正式地图。</remarks>
public sealed class LayerEffectTestRenderer : MonoBehaviour {

    [SerializeField] private Texture2D source;

    [SerializeField] private Shader shader;

    // Calibration belongs to terrainSource.png; offsets are measured in encoded grayscale.
    [SerializeField] private Vector2 sourceGrayOffsets = new Vector2(0.035687451f, -0.035687451f);

    [SerializeField] public Sprite neutralPlate;

    [SerializeField] public Sprite selectedPlate;

    private Material material;

    private Texture2D cellMap;

    private Texture2D holeMask;

    private Color32[] holePixels;

    private readonly HashSet<Vector2Int> holeCells = new HashSet<Vector2Int>();

    private RenderTexture gray;

    private RenderTexture result;

    private readonly Dictionary<Vector2Int, bool> painted = new Dictionary<Vector2Int, bool>();

    public RenderTexture Gray => gray;

    public RenderTexture Result => result;

    public int PaintCount => painted.Count;

    /**每次打开重新创建，关闭完整释放，避免窗口池持有旧RenderTexture。*/
    public void Initialize() {
        Release();
        if (source == null || shader == null || !shader.isSupported) {
            throw new InvalidOperationException("图层测试源图或Shader不可用。");
        }
        material = new Material(shader);
        cellMap = new Texture2D(128, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        holeMask = new Texture2D(LayerEffectTestSettings.PreviewWidth, LayerEffectTestSettings.PreviewHeight, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        holePixels = new Color32[LayerEffectTestSettings.PreviewWidth * LayerEffectTestSettings.PreviewHeight];
        gray = NewTarget();
        result = NewTarget();
        ClearPaint();
    }

    private RenderTexture NewTarget() {
        var target = new RenderTexture(LayerEffectTestSettings.PreviewWidth, LayerEffectTestSettings.PreviewHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.Create();
        return target;
    }

    public void ClearPaint() {
        painted.Clear();
        holeCells.Clear();
        if (holePixels != null) {
            Array.Clear(holePixels, 0, holePixels.Length);
            UploadHoles();
        }
        UploadPaint();
    }

    public void Paint(Vector2Int cell, bool blue) {
        if (Mathf.Abs(cell.x) >= 64 || Mathf.Abs(cell.y) >= 64) {
            return;
        }
        painted[cell] = blue;
        UploadPaint();
    }

    public void SetCellHole(Vector2Int cell, bool value) {
        if (Mathf.Abs(cell.x) >= 64 || Mathf.Abs(cell.y) >= 64) {
            return;
        }
        if (value) {
            holeCells.Add(cell);
        } else {
            holeCells.Remove(cell);
        }
        UploadPaint();
    }

    /**局部笔划使用线段胶囊遮罩，快速拖动也不会留下离散圆点。*/
    public void CutStroke(Vector2 from, Vector2 to, float radius) {
        int width = LayerEffectTestSettings.PreviewWidth;
        int height = LayerEffectTestSettings.PreviewHeight;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius - 1));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + radius + 1));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - radius - 1));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + radius + 1));
        Vector2 delta = to - from;
        for (int y = minY; y <= maxY; y++) {
            for (int x = minX; x <= maxX; x++) {
                Vector2 point = new Vector2(x + .5f, y + .5f);
                float along = delta.sqrMagnitude < .001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude);
                float coverage = Mathf.Clamp01(radius + .5f - Vector2.Distance(point, from + delta * along));
                int index = (height - 1 - y) * width + x;
                holePixels[index].r = (byte)Mathf.Max(holePixels[index].r, Mathf.RoundToInt(coverage * 255));
            }
        }
        UploadHoles();
    }

    /**还原整格裁切和该格内部的局部笔划，不改变当前阵营颜色。*/
    public void RestoreCell(Vector2Int cell, LayerEffectTestSettings settings) {
        holeCells.Remove(cell);
        Vector2 center = settings.origin + new Vector2(cell.x * settings.hexSize.x * .75f, cell.y * settings.hexSize.y * .5f);
        int width = LayerEffectTestSettings.PreviewWidth;
        int height = LayerEffectTestSettings.PreviewHeight;
        for (int y = Mathf.Max(0, Mathf.FloorToInt(center.y - settings.hexSize.y * .5f)); y <= Mathf.Min(height - 1, Mathf.CeilToInt(center.y + settings.hexSize.y * .5f)); y++) {
            for (int x = Mathf.Max(0, Mathf.FloorToInt(center.x - settings.hexSize.x * .5f)); x <= Mathf.Min(width - 1, Mathf.CeilToInt(center.x + settings.hexSize.x * .5f)); x++) {
                if (CellAt(new Vector2(x + .5f, y + .5f), settings) == cell) {
                    holePixels[(height - 1 - y) * width + x] = default;
                }
            }
        }
        UploadPaint();
        UploadHoles();
    }

    private void UploadHoles() {
        if (holeMask != null) {
            holeMask.SetPixels32(holePixels);
            holeMask.Apply(false, false);
        }
    }

    private void UploadPaint() {
        if (cellMap == null) {
            return;
        }
        var pixels = new Color32[128 * 128];
        foreach (var cell in painted) {
            pixels[(cell.Key.y + 64) * 128 + cell.Key.x + 64] = cell.Value ? new Color32(0, 255, 0, 255) : new Color32(255, 0, 0, 255);
        }
        foreach (var cell in holeCells) {
            int index = (cell.y + 64) * 128 + cell.x + 64;
            pixels[index].b = 255;
        }
        cellMap.SetPixels32(pixels);
        cellMap.Apply(false, false);
    }

    /**灰度/染色共用同一源图；混合计算独立于Alpha，目标纹理重复使用。*/
    public void Render(LayerEffectTestSettings settings, bool original) {
        material.SetVector("_SourceGrayOffsets", new Vector4(sourceGrayOffsets.x, sourceGrayOffsets.y, 0, 0));
        material.SetTexture("_CellMap", cellMap);
        material.SetTexture("_HoleMask", holeMask);
        material.SetVector("_CanvasSize", new Vector4(LayerEffectTestSettings.PreviewWidth, LayerEffectTestSettings.PreviewHeight, 0, 0));
        float aspect = source.width / (float)source.height;
        float designAspect = LayerEffectTestSettings.PreviewWidth / (float)LayerEffectTestSettings.PreviewHeight;
        float sx = Mathf.Min(1, designAspect / aspect);
        float sy = Mathf.Min(1, aspect / designAspect);
        material.SetVector("_SourceUV", new Vector4(sx, sy, (1 - sx) * .5f, (1 - sy) * .5f));
        material.SetVector("_Hex", new Vector4(settings.origin.x, settings.origin.y, settings.hexSize.x, settings.hexSize.y));
        material.SetVector("_RedTint", new Vector4(settings.red.r, settings.red.g, settings.red.b, 1));
        material.SetVector("_BlueTint", new Vector4(settings.blue.r, settings.blue.g, settings.blue.b, 1));
        material.SetFloat("_Layout", settings.layout);
        material.SetFloat("_Boundary", settings.boundary);
        material.SetFloat("_Strength", settings.strength);
        material.SetFloat("_Brightness", settings.brightness);
        material.SetFloat("_Contrast", settings.contrast);
        material.SetFloat("_ShowGrid", settings.showGrid ? 1 : 0);
        material.SetFloat("_GridLineWidth", settings.gridLineWidth);
        material.SetFloat("_GridLineOpacity", settings.gridLineOpacity);
        var previous = RenderTexture.active;
        bool previousWrite = GL.sRGBWrite;
        try {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            material.SetFloat("_DisplayMode", original ? 0 : 1);
            Graphics.Blit(source, gray, material);
            material.SetFloat("_DisplayMode", settings.mode);
            Graphics.Blit(source, result, material);
        } finally {
            GL.sRGBWrite = previousWrite;
            RenderTexture.active = previous;
        }
    }

    /**点击坐标使用原图左上角原点，与Shader六边形格心规则一致。*/
    public static Vector2Int CellAt(Vector2 point, LayerEffectTestSettings settings) {
        int q = Mathf.RoundToInt((point.x - settings.origin.x) / (settings.hexSize.x * .75f));
        int k = Mathf.RoundToInt((point.y - settings.origin.y) / (settings.hexSize.y * .5f));
        var best = new Vector2Int(q, k);
        float nearest = float.MaxValue;
        for (int x = q - 1; x <= q + 1; x++) {
            for (int y = k - 2; y <= k + 2; y++) {
                if ((y - x) % 2 != 0) {
                    continue;
                }
                var center = settings.origin + new Vector2(x * settings.hexSize.x * .75f, y * settings.hexSize.y * .5f);
                var delta = point - center;
                float distance = new Vector2(delta.x / (settings.hexSize.x * .5f), delta.y / (settings.hexSize.y / Mathf.Sqrt(3))).sqrMagnitude;
                if (distance < nearest) {
                    nearest = distance;
                    best = new Vector2Int(x, y);
                }
            }
        }
        return best;
    }

    public void Release() {
        ReleaseTarget(gray);
        ReleaseTarget(result);
        gray = null;
        result = null;
        if (material != null) {
            Destroy(material);
        }
        if (cellMap != null) {
            Destroy(cellMap);
        }
        if (holeMask != null) {
            Destroy(holeMask);
        }
        holeMask = null;
        holePixels = null;
        holeCells.Clear();
        material = null;
        cellMap = null;
        painted.Clear();
    }

    private static void ReleaseTarget(RenderTexture target) {
        if (target != null) {
            target.Release();
            Destroy(target);
        }
    }

    private void OnDestroy() {
        Release();
    }

}
