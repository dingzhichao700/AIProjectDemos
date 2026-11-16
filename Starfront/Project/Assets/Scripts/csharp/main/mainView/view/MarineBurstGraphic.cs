using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>同一图集的四层原生蒙皮合为一个UI网格；射击结束恢复原有待机Image。</summary>
[DefaultExecutionOrder(10001)]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MarineBurstGraphic : MaskableGraphic {
    public const string PrefabAddress = "Assets/Art/unpack/default/card/burst/2101/MarineBurstRig.prefab";
    public const string FlashAddress = "Assets/Art/frameAnimation/default/cardDisplayAnimation/2101/burst/muzzle-flash";
    // Approved compact flash: one source pixel = .001 rig units. Body motion is unchanged.
    public const float FlashPixelToRigUnits = .001f;
    private sealed class Lease { public int users; public bool owned; public bool flashOwned; public bool jsonOwned; public Task loading; public FrameAnimationRes clip; }
    public MountedFrameGraphic MuzzleFlash { get; private set; }
    public int FlashShot { get; private set; } = -1;
    public static int ActiveFlashLeases => ActiveLeases;

    private static async Task LoadLease(Lease lease) {
        try {
            await Task.WhenAll(ResourceManager.LoadAsync(new ResLoadInfo(PrefabAddress, ResType.Prefab)), ResourceManager.LoadAsync(new ResLoadInfo(FlashAddress + ".png", ResType.UnpackImage)), ResourceManager.LoadAsync(new ResLoadInfo(FlashAddress, ResType.Json)));
            lease.clip = await FrameAnimationManager.GenerateAsync(ResourceManager.GetJsonNode(FlashAddress), ResourceManager.GetUnpackImage(FlashAddress + ".png"), FlashAddress);
        } finally { if (lease.jsonOwned) { ResourceManager.Release(FlashAddress); } }
    }
    private static Lease shared;
    private Lease acquired;
    private CardArtIdleSkin owner;
    private Image source;
    private bool cancelled;
    private bool held;
    private bool ready;
    public MarineBurstRig Rig { get; private set; }
    public bool IsReady => ready && !cancelled;
    public static int ActiveLeases => shared == null ? 0 : shared.users;
    public override Texture mainTexture => Rig != null && Rig.layers.Length > 0 ? Rig.layers[0].GetComponent<SpriteRenderer>().sprite.texture : Texture2D.whiteTexture;

    public static async Task<bool> PlayAsync(CardArtIdleSkin owner) {
        if (owner == null || !owner.CanPlaySpecial || owner.GetComponent<CardArtImage>().artId != 2101 || !owner.GetComponent<CardArtImage>().AnimationActivated || owner.Rig == null || !owner.isActiveAndEnabled || owner.Burst != null) { return false; }
        var idle = owner.Rig;
        var go = new GameObject("MarineShortBurst", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(owner.transform, false);
        var view = go.AddComponent<MarineBurstGraphic>();
        view.owner = owner;
        view.source = owner.GetComponent<Image>();
        owner.Burst = view;
        view.raycastTarget = false;
        var rect = view.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = view.source.rectTransform.pivot;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        view.material = view.source.material;
        try {
            if (shared == null) {
                shared = new Lease { owned = ResourceManager.GetLoadedPrefab(PrefabAddress) == null, flashOwned = !ResourceManager.HasLoadedUnpackImage(FlashAddress + ".png"), jsonOwned = ResourceManager.GetJsonNode(FlashAddress) == null };
                shared.loading = LoadLease(shared);
            }
            view.acquired = shared;
            view.acquired.users++;
            view.held = true;
            await view.acquired.loading;
            if (view == null || view.cancelled || owner == null || owner.Rig != idle || !owner.CanPlaySpecial || !owner.isActiveAndEnabled) { if (view != null) { view.Cancel(); } return false; }
            var prefab = ResourceManager.GetLoadedPrefab(PrefabAddress);
            if (prefab == null) { throw new InvalidOperationException("短点射Prefab未加载"); }
            var instance = Instantiate(prefab, view.transform, false);
            view.Rig = instance.GetComponent<MarineBurstRig>();
            if (view.Rig == null || view.Rig.layers.Length != 4) { Destroy(instance); throw new InvalidOperationException("短点射四层骨架不完整"); }
            view.Rig.idleSource = idle;
            var flashObject = new GameObject("MuzzleFlashFrames", typeof(RectTransform), typeof(CanvasRenderer));
            flashObject.transform.SetParent(view.transform, false);
            view.MuzzleFlash = flashObject.AddComponent<MountedFrameGraphic>();
            view.MuzzleFlash.rectTransform.anchorMin = Vector2.zero; view.MuzzleFlash.rectTransform.anchorMax = Vector2.one;
            view.MuzzleFlash.rectTransform.pivot = view.rectTransform.pivot;
            view.MuzzleFlash.rectTransform.offsetMin = view.MuzzleFlash.rectTransform.offsetMax = Vector2.zero;
            view.MuzzleFlash.material = view.source.material;
            view.MuzzleFlash.Bind(view.acquired.clip);
            view.Rig.ApplyPose();
            // Native SpriteSkin completes LateUpdate before replacing the fallback Image.
            view.SetAllDirty();
            return true;
        } catch (Exception exception) {
            if (view != null) { view.Cancel(); }
            Debug.LogWarning("陆战队员短点射未就绪，保留待机：" + exception.GetType().Name);
            return false;
        } finally {
            if (view.cancelled) { view.ReleaseAcquired(); }
        }
    }

    private void LateUpdate() {
        if (Rig == null || cancelled) { return; }
        if (Rig.Finished || owner == null || owner.Rig == null || source == null || !source.enabled) { Cancel(); return; }
        bool complete = true;
        foreach (var skin in Rig.layers) {
            complete &= skin != null && skin.GetComponent<SpriteRenderer>().sprite != null && skin.HasCurrentDeformedVertices();
        }
        if (complete) { ready = true; }
        color = source.color;
        UpdateMuzzleFlash();
        SetVerticesDirty();
        source.SetVerticesDirty();
    }

    public void Cancel() {
        if (cancelled) { return; }
        cancelled = true;
        ready = false;
        if (owner != null && owner.Burst == this) { owner.Burst = null; }
        if (source != null) { source.SetVerticesDirty(); }
        if (MuzzleFlash != null) { MuzzleFlash.gameObject.SetActive(false); Destroy(MuzzleFlash.gameObject); MuzzleFlash = null; }
        if (Rig != null) { Rig.gameObject.SetActive(false); Destroy(Rig.gameObject); Rig = null; }
        if (acquired != null && acquired.loading.IsCompleted) { ReleaseAcquired(); }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void ReleaseAcquired() {
        if (!held) { return; }
        held = false;
        acquired.users--;
        if (acquired.users == 0) {
            if (acquired.owned) { ResourceManager.Release(PrefabAddress); }
            if (acquired.clip != null) { foreach (var sprite in acquired.clip.sprites) { if (sprite != null) { Destroy(sprite); } } acquired.clip = null; }
            if (acquired.flashOwned) { ResourceManager.Release(FlashAddress + ".png"); }
            if (ReferenceEquals(shared, acquired)) { shared = null; }
        }
    }

    protected override void OnDisable() { Cancel(); base.OnDisable(); }

    protected override void OnPopulateMesh(VertexHelper helper) {
        helper.Clear();
        if (!IsReady || Rig == null || source == null || source.sprite == null) { return; }
        Rect drawn = source.GetPixelAdjustedRect();
        float aspect = source.sprite.rect.width / source.sprite.rect.height;
        if (source.preserveAspect && drawn.width > 0 && drawn.height > 0) {
            if (aspect > drawn.width / drawn.height) { float old = drawn.height; drawn.height = drawn.width / aspect; drawn.y += (old - drawn.height) * source.rectTransform.pivot.y; }
            else { float old = drawn.width; drawn.width = drawn.height * aspect; drawn.x += (old - drawn.width) * source.rectTransform.pivot.x; }
        }
        foreach (var skin in Rig.layers) {
            var sprite = skin.GetComponent<SpriteRenderer>().sprite;
            if (sprite == null || !skin.HasCurrentDeformedVertices()) { continue; }
            var uv = sprite.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0);
            var indices = sprite.GetIndices();
            int start = helper.currentVertCount;
            int index = 0;
            foreach (var point in skin.GetDeformedVertexPositionData()) {
                helper.AddVert(Map(point, drawn), source.color, uv[index++]);
            }
            for (int i = 0; i < indices.Length; i += 3) { helper.AddTriangle(start + indices[i], start + indices[i + 1], start + indices[i + 2]); }
        }
    }

    private Rect DrawingRect() {
        Rect drawn = source.GetPixelAdjustedRect();
        float aspect = source.sprite.rect.width / source.sprite.rect.height;
        if (source.preserveAspect && drawn.width > 0 && drawn.height > 0) {
            if (aspect > drawn.width / drawn.height) { float old = drawn.height; drawn.height = drawn.width / aspect; drawn.y += (old - drawn.height) * source.rectTransform.pivot.y; }
            else { float old = drawn.width; drawn.width = drawn.height * aspect; drawn.x += (old - drawn.width) * source.rectTransform.pivot.x; }
        }
        return drawn;
    }

    private void UpdateMuzzleFlash() {
        if (MuzzleFlash == null || acquired.clip == null) { return; }
        Rect drawn = DrawingRect();
        Vector3 center = Rig.transform.InverseTransformPoint(Rig.muzzle.position);
        Vector3 forward = Rig.transform.InverseTransformDirection(Rig.muzzle.right).normalized;
        Vector2 attachment = Map(center, drawn);
        Vector2 direction = (Vector2)Map(center + forward, drawn) - attachment;
        float age = Rig.Elapsed - Rig.shotStartSeconds;
        FlashShot = age < 0 ? -1 : Mathf.Clamp(Mathf.FloorToInt((age + .000001f) / Rig.shotInterval), 0, 2);
        float ms = FlashShot < 0 ? -1 : (age - FlashShot * Rig.shotInterval) * 1000;
        int frame = ready && ms >= 0 && ms < acquired.clip.totalDuration ? acquired.clip.GetIndexByTime(ms) : -1;
        MuzzleFlash.SetPose(frame, attachment, direction, direction.magnitude * FlashPixelToRigUnits, source.color);
    }

    private Vector3 Map(Vector3 point, Rect drawn) {
        // Burst layers and muzzle share the idle rig coordinate system. Map that
        // full canvas onto the current static Image rect, retaining its UI pivot.
        var sprite = owner.Rig.skin.GetComponent<SpriteRenderer>().sprite;
        Vector2 pixel = (Vector2)point * sprite.pixelsPerUnit + sprite.pivot;
        return NativePortraitGeometry.MapSkinPixel(owner.GetComponent<CardArtImage>().artId, source.sprite, sprite, pixel, drawn);
    }

}
