using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>将原生SpriteSkin变形网格送入原Image，保留其材质、颜色、遮罩和点击契约。</summary>
/// <remarks>当前仅2101/2202激活后阵容/培养启用；骨架Prefab共享租用，实例独立，失败保留静态原图。</remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[DefaultExecutionOrder(10000)]
public sealed class CardArtIdleSkin : BaseMeshEffect {

    public const string MarinePrefabAddress = "Assets/Art/unpack/default/card/idle/2101/MarineIdleRig.prefab";

    private sealed class Lease {

        public string address;

        public int users;

        public bool owned;

        public Task loading;

    }

    private static readonly Dictionary<string, Lease> leases = new Dictionary<string, Lease>();
    private Lease heldLease;
    public const string HydraliskPrefabAddress = "Assets/Art/unpack/default/card/idle/2202/HydraliskIdleRig.prefab";
    public static string AddressFor(int artId) => artId == 2101 ? MarinePrefabAddress : artId == 2202 ? HydraliskPrefabAddress : artId == 2102 ? MedivacPrefabAddress : null;

    public const string MedivacPrefabAddress = "Assets/Prefab/default/cardDisplayAnimation/2102/display/MedivacDisplayRig.prefab";
    private MedivacDisplayRig medivac;
    private readonly List<MedivacSkinGraphic> medivacGraphics = new List<MedivacSkinGraphic>();
    public MedivacDisplayRig Medivac => medivac;
    public bool MedivacReady {
        get {
            if (medivac == null || !isActiveAndEnabled || !(graphic is Image image) || !image.enabled || image.sprite != original) return false;
            foreach (var skin in medivac.skins) if (skin == null || !skin.HasCurrentDeformedVertices()) return false;
            return true;
        }
    }
    private int version;

    private bool holding;

    private Sprite original;

    private MarineIdleRig rig;

    /**实际启用的独立骨架；仅供表现层与开发验收读取。*/
    public MarineIdleRig Rig => rig;

    public MarineBurstGraphic Burst { get; internal set; }

    public bool AutomaticSpecialEnabled = true;
    public float cultivationIdleSeconds = 3.2f;
    private float cycleIdleStarted = -1;
    private bool automaticStarting;
    public bool CanPlaySpecial {
        get {
            var binding = GetComponent<CardArtImage>();
            var panel = GetComponentInParent<CardCultivationPanel>();
            return isActiveAndEnabled && binding != null && binding.AnimationActivated && binding.usage == CardArtImage.Usage.Portrait && panel != null && panel.isOpened && (rig != null || medivac != null);
        }
    }
    public bool IsSpecialPlaying => (medivac != null && medivac.IsThrustActive) || Burst != null || (rig != null && rig.GetComponent<HydraliskMouthRig>() != null && rig.GetComponent<HydraliskMouthRig>().IsRoaring);

    private async void BeginAutomaticSpecial() {
        if (automaticStarting) { return; }
        automaticStarting = true; cycleIdleStarted = -1;
        try { await PlaySpecialAsync(); }
        finally { automaticStarting = false; }
    }
    private void AdvanceCultivationCycle() {
        if (!CanPlaySpecial) { StopSpecial(); cycleIdleStarted = -1; return; }
        if (!AutomaticSpecialEnabled || (medivac != null ? medivac.paused : rig != null && rig.paused) || automaticStarting || IsSpecialPlaying) { cycleIdleStarted = -1; return; }
        float now = RookieEngine.timer.curTime;
        if (cycleIdleStarted < 0 || now < cycleIdleStarted) { cycleIdleStarted = now; }
        if (now - cycleIdleStarted >= cultivationIdleSeconds * 1000) { BeginAutomaticSpecial(); }
    }

    public Task<bool> PlaySpecialAsync() {
        var binding = GetComponent<CardArtImage>();
        if (!CanPlaySpecial || binding == null || !binding.AnimationActivated || (rig == null && medivac == null)) { return Task.FromResult(false); }
        if (binding.artId == 2102 && medivac != null) { medivac.PlayThrust(); return Task.FromResult(true); }
        if (binding.artId == 2101) { return PlayBurstAsync(); }
        var mouth = rig.GetComponent<HydraliskMouthRig>();
        return Task.FromResult(binding.artId == 2202 && mouth != null && mouth.PlayRoar());
    }

    public void StopSpecial() {
        if (medivac != null && medivac.IsThrustActive) { medivac.CancelThrust(); }
        if (Burst != null) { Burst.Cancel(); }
        if (rig != null) { var mouth = rig.GetComponent<HydraliskMouthRig>(); if (mouth != null) { mouth.StopRoar(); } }
    }

    public Task<bool> PlayBurstAsync() => CanPlaySpecial && GetComponent<CardArtImage>().artId == 2101 && GetComponent<CardArtImage>().AnimationActivated ? MarineBurstGraphic.PlayAsync(this) : Task.FromResult(false);

    /**便于验证共享持有与关闭回收，不能用于业务决策。*/
    public static int ActiveLeases { get { int count = 0; foreach (var item in leases.Values) { count += item.users; } return count; } }

    /**当前只批准两种页面中的陆战队员/刺蛇，不传播到牌库或队伍编辑。*/
    public static bool Supports(CardArtImage image, int id, CardArtImage.Usage usage) {
        if (!image.AnimationActivated || AddressFor(id) == null || (usage != CardArtImage.Usage.Formation && usage != CardArtImage.Usage.Portrait)) {
            return false;
        }
        return image.GetComponentInParent<CardCultivationPanel>() != null || (image.GetComponentInParent<MainPanel>() != null && usage == CardArtImage.Usage.Formation);
    }

    public async Task BindAsync(Sprite sprite) {
        Clear();
        int request = version;
        original = sprite;
        // 保留静态原图跨过一个主线程续体，关闭/换卡可在实例创建前使旧请求失效。
        await Task.Yield();
        if (this == null || !isActiveAndEnabled || request != version) {
            return;
        }
        var binding = GetComponent<CardArtImage>();
        if (!Supports(binding, binding.artId, binding.usage)) { return; }
        string address = AddressFor(binding.artId);
        if (!leases.TryGetValue(address, out Lease acquired)) {
            acquired = new Lease { address = address, owned = ResourceManager.GetLoadedPrefab(address) == null };
            acquired.loading = ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(address, ResType.Prefab) });
            leases.Add(address, acquired);
        }
        acquired.users++;
        bool assigned = false;
        try {
            await acquired.loading;
            if (this == null || !isActiveAndEnabled || request != version || !Supports(binding, binding.artId, binding.usage)) {
                return;
            }
            var prefab = ResourceManager.GetLoadedPrefab(acquired.address);
            if (prefab == null) {
                throw new InvalidOperationException("骨骼待机Prefab未就绪");
            }
            var instance = Instantiate(prefab, transform, false);
            if (binding.artId == 2102) {
                medivac = instance.GetComponent<MedivacDisplayRig>();
                bool valid = medivac != null && medivac.skins != null && medivac.skins.Length == 3;
                if (valid) foreach (var skin in medivac.skins) {
                    var layer = skin == null ? null : skin.GetComponent<SpriteRenderer>();
                    valid &= layer != null && layer.sprite != null && layer.sprite.rect.size == sprite.rect.size && Vector2.Distance(layer.sprite.pivot / layer.sprite.rect.size, sprite.pivot / sprite.rect.size) < .00001f;
                }
                if (!valid) { Destroy(instance); medivac = null; throw new InvalidOperationException("医疗运输机蒙皮输入不匹配"); }
                medivac.InitializeBind();
                foreach (var layer in instance.GetComponentsInChildren<SpriteRenderer>(true)) layer.forceRenderingOff = true;
                instance.SetActive(true);
                // Plumes behind rigid body; separate texture per Graphic, no atlas assumption.
                foreach (int layerIndex in new[] { 1, 2, 0 }) {
                    var go = new GameObject("MedivacLayer" + layerIndex, typeof(RectTransform), typeof(CanvasRenderer));
                    go.transform.SetParent(transform, false);
                    var view = go.AddComponent<MedivacSkinGraphic>(); medivacGraphics.Add(view);
                    view.owner = this; view.source = graphic as Image; view.skin = medivac.skins[layerIndex];
                    view.raycastTarget = false; view.material = graphic.material;
                    var rect = view.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.pivot = view.source.rectTransform.pivot; rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
            } else {
            rig = instance.GetComponent<MarineIdleRig>();
            if (rig == null || rig.skin == null || !CompatibleCanvas(binding.artId, rig.skin.GetComponent<SpriteRenderer>().sprite, sprite)) {
                Destroy(instance);
                rig = null;
                throw new InvalidOperationException("骨骼待机画布未登记兼容映射：" + binding.artId);
            }
            }
            heldLease = acquired;
            holding = true;
            assigned = true;
            graphic.SetVerticesDirty();
        } catch (Exception error) {
            if (this != null && request == version) {
                Debug.LogWarning("角色待机加载失败，保留静态立绘：" + error.GetType().Name + " " + error.Message, this);
            }
        } finally {
            if (!assigned) {
                Release(acquired);
            }
        }
    }

    // Approved source pairs are exact full-canvas resizes (CARD-SKIN-STATIC-01/R1).
    // Do not accept a new crop/composition merely because its aspect is similar.
    private static bool CompatibleCanvas(int id, Sprite skin, Sprite current) {
        if (skin == null || current == null) { return false; }
        if ((id == 2101 || id == 2202) && NativePortraitGeometry.TryGet(id, current, out var native)) {
            return skin.rect.size == (id == 2101 ? new Vector2(238, 294) : new Vector2(258, 280));
        }
        Vector2 sourceSize = id == 2101 ? new Vector2(238, 294) : new Vector2(258, 280);
        Vector2 currentSize = id == 2101 ? new Vector2(157, 194) : new Vector2(205, 223);
        return (id == 2101 || id == 2202) && skin.rect.size == sourceSize &&
            (current.rect.size == currentSize || current.rect.size == sourceSize);
    }

    public void Clear() {
        cycleIdleStarted = -1;
        if (graphic is Image originalImage) { originalImage.overrideSprite = null; }
        if (Burst != null) { Burst.Cancel(); Burst = null; }
        foreach (var layer in medivacGraphics) if (layer != null) { layer.gameObject.SetActive(false); Destroy(layer.gameObject); }
        medivacGraphics.Clear();
        if (medivac != null) { medivac.gameObject.SetActive(false); Destroy(medivac.gameObject); medivac = null; }
        version++;
        original = null;
        if (rig != null) {
            rig.gameObject.SetActive(false);
            Destroy(rig.gameObject);
            rig = null;
        }
        if (holding) {
            holding = false;
            Release(heldLease);
            heldLease = null;
        }
        if (graphic != null) {
            graphic.SetVerticesDirty();
        }
    }

    protected override void OnDisable() {
        Clear();
        base.OnDisable();
    }

    private static void Release(Lease acquired) {
        if (acquired == null) {
            return;
        }
        acquired.users--;
        if (acquired.users == 0) {
            if (acquired.owned) {
                ResourceManager.Release(acquired.address);
            }
            if (leases.TryGetValue(acquired.address, out Lease current) && ReferenceEquals(current, acquired)) { leases.Remove(acquired.address); }
        }
    }

    private void LateUpdate() {
        AdvanceCultivationCycle();
        if (medivac != null && graphic != null) { graphic.SetVerticesDirty(); }
        if (rig != null && graphic != null && graphic.IsActive()) {
            var mouth = rig.GetComponent<HydraliskMouthRig>();
            if (rig.skin.HasCurrentDeformedVertices() && (mouth == null || mouth.Ready) && graphic is Image image) {
                var body = rig.skin.GetComponent<SpriteRenderer>().sprite;
                if (image.overrideSprite != body) { image.overrideSprite = body; }
            }
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vertices) {
        if (MedivacReady) { vertices.Clear(); return; }
        if (Burst != null && Burst.IsReady) { vertices.Clear(); return; }
        var image = graphic as Image;
        if (!IsActive() || rig == null || image == null || !image.enabled || image.sprite != original || !rig.skin.HasCurrentDeformedVertices()) {
            return;
        }
        var mouth = rig.GetComponent<HydraliskMouthRig>();
        if (mouth != null && !mouth.Ready) { return; }
        var sprite = rig.skin.GetComponent<SpriteRenderer>().sprite;
        Rect drawn = image.GetPixelAdjustedRect();
        float aspect = original.rect.width / original.rect.height;
        if (image.preserveAspect && drawn.width > 0 && drawn.height > 0) {
            if (aspect > drawn.width / drawn.height) {
                float old = drawn.height;
                drawn.height = drawn.width / aspect;
                drawn.y += (old - drawn.height) * image.rectTransform.pivot.y;
            } else {
                float old = drawn.width;
                drawn.width = drawn.height * aspect;
                drawn.x += (old - drawn.width) * image.rectTransform.pivot.x;
            }
        }
        vertices.Clear();
        AppendSkin(vertices, rig.skin, drawn, image.color);
        if (mouth != null) { AppendSkin(vertices, mouth.jawSkin, drawn, image.color); }
    }

    private void AppendSkin(VertexHelper vertices, UnityEngine.U2D.Animation.SpriteSkin skin, Rect drawn, Color color) {
        var sprite = skin.GetComponent<SpriteRenderer>().sprite;
        var uv = sprite.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0);
        var indices = sprite.GetIndices();
        int start = vertices.currentVertCount;
        int index = 0;
        foreach (var point in skin.GetDeformedVertexPositionData()) {
            Vector2 pixel = (Vector2)point * sprite.pixelsPerUnit + sprite.pivot;
            Vector3 position = NativePortraitGeometry.MapSkinPixel(GetComponent<CardArtImage>().artId, original, sprite, pixel, drawn);
            vertices.AddVert(position, color, uv[index++]);
        }
        for (int i = 0; i < indices.Length; i += 3) { vertices.AddTriangle(start + indices[i], start + indices[i + 1], start + indices[i + 2]); }
    }
}
