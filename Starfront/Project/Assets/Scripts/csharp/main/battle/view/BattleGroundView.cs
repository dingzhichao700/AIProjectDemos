using System;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>完整底图只染色；静态厚度、岸壁及地形轮廓由素材提供。</summary>
public sealed class BattleGroundView : IDisposable {
    // Offline ownership encoding: R=q+64, G=k+64, B=initial camp, A=valid.
    private const int MapSize = 128;
    private const int CoordinateBias = 64;
    private readonly BattleSceneModel model;
    private Material material;
    private Texture2D camps;
    private RenderTexture result;
    private RawImage ground;
    private Image grid;
    private bool ownershipDirty;
    public RenderTexture texture => result;

    public BattleGroundView(BattleSceneModel scene, RectTransform[] layers) {
        model = scene;
        try {
            var config = scene.config;
            Sprite source = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(config.GroundResource));
            Sprite lines = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(config.GroundGridResource));
            Material template = ResourceManager.GetMaterial(BattleSceneResources.GroundMaterialPath(scene));
            if (source == null || lines == null || template == null || template.GetTexture("_SceneOwners") == null) {
                throw new InvalidOperationException("完整地表资源未预加载。");
            }
            if (source.rect.size != lines.rect.size || source.texture.width != source.rect.width || source.texture.height != source.rect.height) {
                throw new InvalidOperationException("底图须为完整独立纹理，格线须同尺寸。");
            }
            if (!ColorUtility.TryParseHtmlString(config.GroundRed, out Color red) || !ColorUtility.TryParseHtmlString(config.GroundBlue, out Color blue) || config.GroundThickness < 0 || config.WaterDepth < 0 || config.GroundTintStrength < 0 || config.GroundTintStrength > 1 || config.GroundBrightness < 0 || config.GroundContrast < 0) {
                throw new InvalidOperationException("地表视觉配置无效。");
            }
            material = new Material(template) { name = "BattleGroundInstance" };
            material.SetVector("_RedTint", red);
            material.SetVector("_BlueTint", blue);
            material.SetFloat("_Strength", config.GroundTintStrength);
            material.SetFloat("_Brightness", config.GroundBrightness);
            material.SetFloat("_Contrast", config.GroundContrast);
            camps = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BattleGroundCamps" };
            material.SetTexture("_CellMap", camps);
            result = new RenderTexture(source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "BattleGroundTint", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            result.Create();
            ground = new GameObject("imgGround", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            ConfigureRect(ground.rectTransform, layers[(int)BattleSceneVisualLayer.GROUND - 1], source.rect.size);
            ground.texture = result;
            ground.raycastTarget = false;
            grid = new GameObject("imgGroundGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            ConfigureRect(grid.rectTransform, layers[(int)BattleSceneVisualLayer.GROUND_DECAL - 1], lines.rect.size);
            grid.sprite = lines;
            grid.raycastTarget = false;
            RefreshOwnership();
            model.territory.Changed += OnOwnershipChanged;
        } catch {
            Dispose();
            throw;
        }
    }

    private void ConfigureRect(RectTransform rect, RectTransform parent, Vector2 size) {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(model.config.GroundOffset.X, -model.config.GroundOffset.Y);
    }

    private void OnOwnershipChanged() {
        ownershipDirty = true;
    }

    public void RefreshIfDirty() {
        if (ownershipDirty) {
            RefreshOwnership();
        }
    }

    /**消费运行时归属，同帧多个变化合并为一次染色；不重建地形或材质。*/
    public void RefreshOwnership() {
        ownershipDirty = false;
        var pixels = new Color32[MapSize * MapSize];
        foreach (var cell in model.cells.Values) {
            int x = cell.q + CoordinateBias, y = cell.k + CoordinateBias;
            if (x < 0 || x >= MapSize || y < 0 || y >= MapSize) {
                throw new InvalidOperationException("格子超出地表像素归属编码范围。");
            }
            pixels[y * MapSize + x] = new Color32((byte)cell.camp, 0, 0, 255);
        }
        camps.SetPixels32(pixels);
        camps.Apply(false, false);
        Texture source = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(model.config.GroundResource)).texture;
        var previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        try {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, result, material);
        } finally {
            GL.sRGBWrite = previousSrgb;
            RenderTexture.active = previous;
        }
    }

    public void Dispose() {
        if (model.territory != null) {
            model.territory.Changed -= OnOwnershipChanged;
        }
        if (ground != null) {
            ground.texture = null;
            UnityEngine.Object.Destroy(ground.gameObject);
        }
        if (grid != null) {
            grid.sprite = null;
            UnityEngine.Object.Destroy(grid.gameObject);
        }
        if (result != null) {
            result.Release();
            UnityEngine.Object.Destroy(result);
        }
        if (material != null) { UnityEngine.Object.Destroy(material); }
        if (camps != null) { UnityEngine.Object.Destroy(camps); }
        ground = null;
        grid = null;
        result = null;
        material = null;
        camps = null;
    }
}
