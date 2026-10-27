using System;
using System.Linq;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>检查真实场景的地表加载、阵营映射、透明度及静态锚点契约。</summary>
public static class BattleGroundValidation {
    public static void Validate(BattleSceneView view, BattleSceneModel model) {
        Require(view.groundTexture != null && view.groundTexture.width == 2000 && view.groundTexture.height == 2000, "full-size ground target");
        var layer = view.layers[(int)BattleSceneVisualLayer.GROUND - 1];
        Require(layer.GetComponentsInChildren<RawImage>().Length == 1 && layer.GetComponentsInChildren<Image>().Length == 0, "single ground, no old per-cell sprites");
        var image = layer.GetComponentInChildren<RawImage>();
        Require(image.rectTransform.pivot == new Vector2(0, 1) && image.rectTransform.anchoredPosition == new Vector2(model.config.GroundOffset.X, -model.config.GroundOffset.Y), "ground origin");
        Require(!image.raycastTarget && view.layers[(int)BattleSceneVisualLayer.GROUND_DECAL - 1].GetComponentsInChildren<Image>().Length == 1, "independent grid and unblocked input");
        Require(model.config.GroundThickness == 10 && model.config.WaterDepth == 18, "static art dimensions");
        Require(model.config.Appearances.Count(a => a.Layer == BattleSceneVisualLayer.BELOW_GROUND) == 5 && model.config.Appearances.Where(a => a.Layer == BattleSceneVisualLayer.BELOW_GROUND).Sum(a => a.Cells.Count) == 17, "five complete water shapes cover seventeen cells");
        Require(model.config.Appearances.All(a => a.Offset.X == 0 && a.Offset.Y == 0), "new assets have no old 8px compensation");
        foreach (var a in model.config.Appearances) {
            Sprite sprite = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(a.Resource));
            if (a.Resource.EndsWith("/mountain")) { Require(Vector2.Distance(sprite.pivot, new Vector2(56, 49)) < .001f, "mountain elevation-zero pivot"); }
        }
        var pixels = Read(view.groundTexture);
        var original = Read(ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(model.config.GroundResource)).texture);
        int changed = 0, transparent = 0, neutral = 0;
        foreach (var cell in model.cells.Values) {
            if (cell.type == BattleSceneCellType.WATER) { continue; }
            // Near a cell's top edge avoids mountain silhouettes and samples only the ground target.
            int x = Mathf.FloorToInt(cell.position.x - model.config.GroundOffset.X);
            int y = Mathf.FloorToInt(cell.position.y - model.config.GroundThickness - model.config.GroundOffset.Y);
            int index = (view.groundTexture.height - 1 - y) * view.groundTexture.width + x;
            var value = pixels[index];
            if (cell.camp == BattleSceneCamp.ABSOLUTE_NEUTRAL) {
                Require(Equal(value, original[index]), "neutral artwork unchanged"); neutral++;
            } else {
                Require(cell.camp == BattleSceneCamp.FRIENDLY ? value.b > value.r : value.r > value.b, "color follows logical camp"); changed++;
            }
        }
        for (int i = 0; i < pixels.Length; i += 97) {
            Require(Math.Abs(pixels[i].a - original[i].a) <= 1, "source alpha preserved");
            if (pixels[i].a == 0) { transparent++; }
        }
        Require(changed == 146 && neutral == 8 && transparent > 1000, "all land cells verified and outside/holes transparent");
    }

    private static bool Equal(Color32 a, Color32 b) {
        return Math.Abs(a.r - b.r) <= 1 && Math.Abs(a.g - b.g) <= 1 && Math.Abs(a.b - b.b) <= 1 && Math.Abs(a.a - b.a) <= 1;
    }
    private static Color32[] Read(Texture source) {
        var previous = RenderTexture.active;
        bool srgb = GL.sRGBWrite;
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            return readable.GetPixels32();
        } finally {
            RenderTexture.active = previous;
            GL.sRGBWrite = srgb;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(readable);
        }
    }
    private static void Require(bool condition, string message) {
        if (!condition) { throw new InvalidOperationException("Battle ground: " + message); }
    }
}
