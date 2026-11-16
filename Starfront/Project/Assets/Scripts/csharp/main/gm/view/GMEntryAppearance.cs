using UnityEngine;

/// <summary>
/// 已批准GM入口的扁平圆角底板，使用默认UI材质。
/// </summary>
/// <remarks>由64×48矢量几何确定性栅格化并缓存一份Sprite，不生成图集、不依赖业务资源异步加载。</remarks>
public static class GMEntryAppearance {

    private static Sprite backplate;

    public static readonly Color Accent = new Color32(0, 221, 245, 255);

    /**小型常驻底板仅创建一次，源纹理随Sprite保留到运行结束。*/
    public static Sprite Backplate {
        get {
            if (backplate == null) {
                backplate = CreateBackplate();
            }
            return backplate;
        }
    }

    private static Sprite CreateBackplate() {
        const int width = 64;
        const int height = 48;
        const int samples = 4;
        const float radius = 6f;
        const float border = 1.25f;
        Color fill = new Color32(5, 24, 44, 247);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                Color sum = Color.clear;
                for (int sy = 0; sy < samples; sy++) {
                    for (int sx = 0; sx < samples; sx++) {
                        Vector2 p = new Vector2(x + (sx + .5f) / samples - width * .5f, y + (sy + .5f) / samples - height * .5f);
                        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(width * .5f - radius, height * .5f - radius);
                        float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                        if (distance <= 0) {
                            Color color = distance >= -border ? Accent : fill;
                            sum.r += color.r * color.a;
                            sum.g += color.g * color.a;
                            sum.b += color.b * color.a;
                            sum.a += color.a;
                        }
                    }
                }
                if (sum.a > 0) {
                    pixels[y * width + x] = new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a / (samples * samples));
                }
            }
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "GMEntryFlat", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100);
    }

}
