using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>当前动画帧的贴地投影。共用图集，不复制纹理、不另开动画时钟。</summary>
public sealed class BattleUnitShadowGraphic : MaskableGraphic {
    private Sprite sprite;
    private readonly Vector2[] corners = new Vector2[4];
    public override Texture mainTexture => sprite != null ? sprite.texture : Texture2D.whiteTexture;

    /**UI坐标Y向上；角度从水平向左朝下旋转，30度指向左下。*/
    public static Vector2 Direction(float angle, float length) {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Cos(radians), -Mathf.Sin(radians)) * length;
    }

    public static Vector2 Project(Vector2 local, bool mirror, float angle, float length) {
        return new Vector2(mirror ? -local.x : local.x, 0) + Direction(angle, length) * local.y;
    }

    /**返回场景X右Y下坐标的包围框，用于与本体联合剔除。*/
    public static Rect Bounds(Vector2 size, Vector2 pivot, bool mirror, Vector2 ground, float altitude, float angle, float length) {
        Vector2 a = Project(-pivot, mirror, angle, length);
        Vector2 b = Project(new Vector2(0, size.y) - pivot, mirror, angle, length);
        Vector2 c = Project(size - pivot, mirror, angle, length);
        Vector2 d = Project(new Vector2(size.x, 0) - pivot, mirror, angle, length);
        Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        Vector2 offset = Direction(angle, length) * altitude;
        return new Rect(ground.x + offset.x + min.x, ground.y - offset.y - max.y, max.x - min.x, max.y - min.y);
    }

    public void SetFrame(Sprite frame, Vector2 pivot, bool mirror, Vector2 ground, float altitude, float angle, float length, float alpha) {
        bool textureChanged = sprite != frame;
        sprite = frame;
        Rect bounds = Bounds(frame.rect.size, pivot, mirror, ground, altitude, angle, length);
        Vector2 offset = Direction(angle, length) * altitude;
        Vector2 min = new Vector2(bounds.xMin - ground.x - offset.x, ground.y - offset.y - bounds.yMax);
        Vector2 size = frame.rect.size;
        corners[0] = Project(-pivot, mirror, angle, length) - min;
        corners[1] = Project(new Vector2(0, size.y) - pivot, mirror, angle, length) - min;
        corners[2] = Project(size - pivot, mirror, angle, length) - min;
        corners[3] = Project(new Vector2(size.x, 0) - pivot, mirror, angle, length) - min;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = new Vector2(bounds.xMin, -bounds.yMax);
        rectTransform.sizeDelta = bounds.size;
        color = new Color(0, 0, 0, alpha);
        SetVerticesDirty();
        if (textureChanged) {
            SetMaterialDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if (sprite == null) {
            return;
        }
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        vh.AddVert(corners[0], color, new Vector2(uv.x, uv.y));
        vh.AddVert(corners[1], color, new Vector2(uv.x, uv.w));
        vh.AddVert(corners[2], color, new Vector2(uv.z, uv.w));
        vh.AddVert(corners[3], color, new Vector2(uv.z, uv.y));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    public void ResetFrame() {
        sprite = null;
        color = Color.clear;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        SetAllDirty();
        gameObject.SetActive(false);
    }
}
