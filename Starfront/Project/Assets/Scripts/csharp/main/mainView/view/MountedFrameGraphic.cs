using UnityEngine;
using UnityEngine.UI;

/// <summary>外部动作时钟驱动的挂点帧图形；还原裁剪前画布，不逐帧重新居中。</summary>
public sealed class MountedFrameGraphic : MaskableGraphic {
    public FrameAnimationRes Clip { get; private set; }
    public int FrameIndex { get; private set; } = -1;
    public Vector2 Attachment { get; private set; }
    public Vector2 Forward { get; private set; } = Vector2.right;
    public float PixelScale { get; private set; }
    public override Texture mainTexture => Clip != null && Clip.atlas != null ? Clip.atlas.texture : Texture2D.whiteTexture;

    public void Bind(FrameAnimationRes clip) { Clip = clip; raycastTarget = false; SetAllDirty(); }
    public void SetPose(int frame, Vector2 attachment, Vector2 forward, float pixelScale, Color tint) {
        FrameIndex = frame; Attachment = attachment; Forward = forward.normalized; PixelScale = pixelScale; color = tint;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper helper) {
        helper.Clear();
        if (Clip == null || FrameIndex < 0 || FrameIndex >= Clip.sprites.Length) { return; }
        Sprite sprite = Clip.sprites[FrameIndex];
        Vector2 offset = Clip.frameOffsets[FrameIndex] - Vector2.Scale(Clip.sourceSizes[FrameIndex], Clip.pivot);
        Vector2 side = new Vector2(-Forward.y, Forward.x);
        Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        Vector2[] points = { offset, offset + new Vector2(0, sprite.rect.height), offset + sprite.rect.size, offset + new Vector2(sprite.rect.width, 0) };
        Vector2[] uvs = { new Vector2(uv.x, uv.y), new Vector2(uv.x, uv.w), new Vector2(uv.z, uv.w), new Vector2(uv.z, uv.y) };
        for (int i = 0; i < 4; i++) { helper.AddVert(Attachment + (Forward * points[i].x + side * points[i].y) * PixelScale, color, uvs[i]); }
        helper.AddTriangle(0, 1, 2); helper.AddTriangle(2, 3, 0);
    }
}
