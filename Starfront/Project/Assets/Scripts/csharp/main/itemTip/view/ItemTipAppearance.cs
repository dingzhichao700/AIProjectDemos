using UnityEngine;

/// <summary>R4平面细框与来源小尖角；默认UI材质，无金属或发光。</summary>
public static class ItemTipAppearance {
    private static Sprite body;
    private static Sprite pointer;
    public static Sprite Body => body != null ? body : (body = Create(false));
    public static Sprite Pointer => pointer != null ? pointer : (pointer = Create(true));

    private static Sprite Create(bool triangle) {
        int w = triangle ? 20 : 16;
        int h = triangle ? 15 : 16;
        var pixels = new Color32[w * h];
        Color fill = new Color32(4, 27, 45, 255);
        Color border = new Color32(71, 127, 146, 255);
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                if (triangle) {
                    float edge = (y + .5f) * 10f / 15;
                    float d = Mathf.Abs(x + .5f - 10);
                    pixels[y * w + x] = d <= edge ? (Color32)(d > edge - 1 ? border : fill) : (Color32)Color.clear;
                } else {
                    Vector2 q = new Vector2(Mathf.Abs(x + .5f - 8), Mathf.Abs(y + .5f - 8)) - new Vector2(3, 3);
                    float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 5;
                    pixels[y * w + x] = d <= 0 ? (Color32)(d >= -1 ? border : fill) : (Color32)Color.clear;
                }
            }
        }
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = triangle ? "ItemTipPointer" : "ItemTipBody", wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, triangle ? Vector4.zero : new Vector4(6, 6, 6, 6));
    }
}
