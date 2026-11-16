using System;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>固定原色整图；覆盖独立位于格线之后、阴影与实体之前。</summary>
public sealed class BattleGroundView : IDisposable {
    private readonly BattleSceneModel model;
    private RawImage ground;
    private Image grid;
    private RawImage coverage;
    private Material coverageMaterial;
    private Texture2D states;
    private RenderTexture result;
    private bool dirty;
    public RenderTexture texture => result;

    public BattleGroundView(BattleSceneModel scene, RectTransform[] layers) {
        model = scene;
        try {
            var source = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(scene.config.GroundResource));
            var lines = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(scene.config.GroundGridResource));
            var blue = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(BattleConst.FriendlyCoverageResource));
            var orange = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(BattleConst.EnemyCoverageResource));
            var template = ResourceManager.GetMaterial("Assets/Art/material/" + BattleConst.CoverageMaterialResource);
            if (source == null || lines == null || blue == null || orange == null || template == null) {
                throw new InvalidOperationException("Ground or coverage dependency not preloaded.");
            }
            result = new RenderTexture(source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            result.name = "BattleGroundOriginalColor";
            result.filterMode = FilterMode.Point;
            result.Create();
            var previous = RenderTexture.active;
            bool srgb = GL.sRGBWrite;
            try {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source.texture, result);
            } finally {
                GL.sRGBWrite = srgb;
                RenderTexture.active = previous;
            }
            ground = new GameObject("imgGround", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            Configure(ground.rectTransform, layers[(int)BattleSceneVisualLayer.GROUND - 1], source.rect.size);
            ground.texture = result;
            ground.raycastTarget = false;
            grid = new GameObject("imgGroundGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            Configure(grid.rectTransform, layers[(int)BattleSceneVisualLayer.GROUND_DECAL - 1], lines.rect.size);
            grid.sprite = lines;
            grid.raycastTarget = false;
            states = new Texture2D(128, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BattleCoverageStates" };
            coverageMaterial = new Material(template) { name = "BattleCoverageInstance" };
            coverageMaterial.SetTexture("_CellStates", states);
            coverageMaterial.SetTexture("_Blue", blue.texture);
            coverageMaterial.SetTexture("_Orange", orange.texture);
            coverageMaterial.SetVector("_SceneSize", new Vector4(source.rect.width, source.rect.height, 0, 0));
            coverageMaterial.SetVector("_Origin", new Vector4(scene.config.OriginX - scene.config.GroundOffset.X, scene.config.OriginY - scene.config.GroundOffset.Y, 0, 0));
            coverageMaterial.SetFloat("_Thickness", scene.config.GroundThickness);
            coverage = new GameObject("imgTerritoryCoverage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            Configure(coverage.rectTransform, layers[(int)BattleSceneVisualLayer.GROUND_DECAL - 1], source.rect.size);
            coverage.texture = blue.texture;
            coverage.material = coverageMaterial;
            coverage.raycastTarget = false;
            RefreshOwnership();
            scene.territory.Changed += OnChanged;
            scene.StructureChanged += OnChanged;
        } catch {
            Dispose();
            throw;
        }
    }

    private void Configure(RectTransform rect, RectTransform parent, Vector2 size) {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(model.config.GroundOffset.X, -model.config.GroundOffset.Y);
    }

    private void OnChanged() {
        dirty = true;
    }

    public void RefreshIfDirty() {
        if (dirty) {
            RefreshOwnership();
        }
    }

    public void RefreshOwnership() {
        dirty = false;
        var pixels = new Color32[128 * 128];
        foreach (var cell in model.cells.Values) {
            int x = cell.q + 64, y = cell.k + 64;
            if (x < 0 || x >= 128 || y < 0 || y >= 128) {
                throw new InvalidOperationException("Coverage coordinate outside state map.");
            }
            if (cell.capturable && !cell.unlocked && (cell.camp == BattleSceneCamp.FRIENDLY || cell.camp == BattleSceneCamp.ENEMY)) {
                pixels[y * 128 + x] = new Color32((byte)cell.camp, 0, 0, 255);
            }
        }
        states.SetPixels32(pixels);
        states.Apply(false, false);
    }

    public void Dispose() {
        if (model.territory != null) {
            model.territory.Changed -= OnChanged;
        }
        model.StructureChanged -= OnChanged;
        if (coverage != null) {
            coverage.texture = null;
            coverage.material = null;
            UnityEngine.Object.Destroy(coverage.gameObject);
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
        if (states != null) {
            UnityEngine.Object.Destroy(states);
        }
        if (coverageMaterial != null) {
            UnityEngine.Object.Destroy(coverageMaterial);
        }
        ground = null;
        grid = null;
        coverage = null;
        states = null;
        result = null;
        coverageMaterial = null;
    }
}
