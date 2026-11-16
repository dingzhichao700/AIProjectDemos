using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.UI;

/// <summary>一张蒙皮纹理对应一个uGUI Graphic；与原Image共用显示矩形、颜色和Mask。</summary>
[DefaultExecutionOrder(10001)]
public sealed class MedivacSkinGraphic : MaskableGraphic {
    public CardArtIdleSkin owner;
    public SpriteSkin skin;
    public Image source;
    public override Texture mainTexture => skin != null ? skin.GetComponent<SpriteRenderer>().sprite.texture : Texture2D.whiteTexture;
    private void LateUpdate() {
        if (source == null || owner == null) return;
        color = source.color;
        if (material != source.material) material = source.material;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper helper) {
        helper.Clear();
        if (owner == null || !owner.MedivacReady || source == null || !source.enabled || source.sprite == null || skin == null || !skin.HasCurrentDeformedVertices()) return;
        var sprite = skin.GetComponent<SpriteRenderer>().sprite;
        Rect drawn = source.GetPixelAdjustedRect();
        float aspect = source.sprite.rect.width / source.sprite.rect.height;
        if (source.preserveAspect && drawn.width > 0 && drawn.height > 0) {
            if (aspect > drawn.width / drawn.height) { float old = drawn.height; drawn.height = drawn.width / aspect; drawn.y += (old - drawn.height) * source.rectTransform.pivot.y; }
            else { float old = drawn.width; drawn.width = drawn.height * aspect; drawn.x += (old - drawn.width) * source.rectTransform.pivot.x; }
        }
        var uv = sprite.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0);
        var indices = sprite.GetIndices();
        int index = 0;
        foreach (var point in skin.GetDeformedVertexPositionData()) {
            Vector2 pixel = (Vector2)point * sprite.pixelsPerUnit + sprite.pivot;
            // Source Image and child Graphic can have different layout-time pivots.
            Vector3 sourceLocal = new Vector3(drawn.x + pixel.x / sprite.rect.width * drawn.width, drawn.y + pixel.y / sprite.rect.height * drawn.height, 0);
            Vector3 layerLocal = rectTransform.InverseTransformPoint(source.rectTransform.TransformPoint(sourceLocal));
            helper.AddVert(layerLocal, source.color, uv[index++]);
        }
        for (int i = 0; i < indices.Length; i += 3) helper.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
    }
}
