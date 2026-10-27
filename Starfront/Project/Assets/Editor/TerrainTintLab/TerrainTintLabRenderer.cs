using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 使用同一GPU混合Shader输出灰度与染色预览。
/// </summary>
/// <remarks>保留源Alpha；预览材质、绘制贴图和RenderTexture均由调用方生命周期持有。</remarks>
public sealed class TerrainTintLabRenderer : IDisposable {

    public const string SourcePath = "Assets/Editor/TerrainTintLab/TerrainReference.png";

    public const string ShaderPath = "Assets/Editor/TerrainTintLab/TerrainTintLab.shader";

    private readonly Material material;

    private readonly Texture2D cellMap;

    public TerrainTintLabRenderer() {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) {
            string messages = shader == null ? "找不到Shader资源。" : string.Join("; ", System.Linq.Enumerable.Select(ShaderUtil.GetShaderMessages(shader), item => item.message + " line=" + item.line));
            throw new InvalidOperationException("地表染色Shader缺失或编译失败：" + messages);
        }
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        cellMap = new Texture2D(128, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        SetPaint(new List<Vector3Int>());
    }

    public void SetPaint(IReadOnlyList<Vector3Int> painted) {
        var colors = new Color32[128 * 128];
        foreach (var cell in painted) {
            int x = cell.x + 64;
            int y = cell.y + 64;
            if (x >= 0 && x < 128 && y >= 0 && y < 128) {
                colors[y * 128 + x] = cell.z == 1 ? new Color32(255, 0, 0, 255) : new Color32(0, 255, 0, 255);
            }
        }
        cellMap.SetPixels32(colors);
        cellMap.Apply(false, false);
    }

    /**混合在sRGB编码域计算，按项目色彩空间转换输入/输出，Alpha保持独立。*/
    public RenderTexture Render(Texture2D source, TerrainTintLabSettings settings, int mode) {
        if (source == null) {
            throw new InvalidOperationException("请选择地表图片。");
        }
        var target = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave };
        target.Create();
        material.SetTexture("_CellMap", cellMap);
        material.SetVector("_CanvasSize", new Vector4(source.width, source.height, 0, 0));
        material.SetVector("_Hex", new Vector4(settings.origin.x, settings.origin.y, settings.hexSize.x, settings.hexSize.y));
        material.SetVector("_RedTint", new Vector4(settings.red.r, settings.red.g, settings.red.b, 1));
        material.SetVector("_BlueTint", new Vector4(settings.blue.r, settings.blue.g, settings.blue.b, 1));
        material.SetFloat("_DisplayMode", mode);
        material.SetFloat("_Layout", settings.layout);
        material.SetFloat("_Boundary", settings.boundary);
        material.SetFloat("_Strength", settings.strength);
        material.SetFloat("_Brightness", settings.brightness);
        material.SetFloat("_Contrast", settings.contrast);
        material.SetFloat("_ShowGrid", settings.showGrid ? 1 : 0);
        var previous = RenderTexture.active;
        bool previousWrite = GL.sRGBWrite;
        try {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, target, material);
        } catch {
            Release(target);
            throw;
        } finally {
            GL.sRGBWrite = previousWrite;
            RenderTexture.active = previous;
        }
        return target;
    }

    /**与Shader共用同一六边形格心规则，预览点击不会修改场景地图。*/
    public static Vector2Int CellAt(Vector2 point, TerrainTintLabSettings settings) {
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
                float distance = (point - center).sqrMagnitude;
                if (distance < nearest) {
                    nearest = distance;
                    best = new Vector2Int(x, y);
                }
            }
        }
        return best;
    }

    public static Texture2D Readback(RenderTexture target) {
        var previous = RenderTexture.active;
        try {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            return image;
        } finally {
            RenderTexture.active = previous;
        }
    }

    public static void Release(RenderTexture target) {
        if (target != null) {
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    public void Dispose() {
        UnityEngine.Object.DestroyImmediate(material);
        UnityEngine.Object.DestroyImmediate(cellMap);
    }

}
