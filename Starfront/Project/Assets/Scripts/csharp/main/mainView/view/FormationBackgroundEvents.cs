using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>阵容远景的低频互斥事件；只读取既有雾/星光组件状态，不改变其资源或时间轴。</summary>
public sealed class FormationBackgroundEvents : MonoBehaviour {

    public Sprite frigateAtlas;
    public TextAsset frigateJson;
    public Sprite meteorAtlas;
    public TextAsset meteorJson;
    public TextAsset spawnFootprints;
    public Vector2 meteorInterval = new Vector2(15, 30);
    public Vector2 frigateInterval = new Vector2(40, 70);
    public Vector2 dwellInterval = new Vector2(6, 12);
    public float forwardSpeed = 1;
    public int randomSeed = 20261112;

    public bool Ready => clips != null;
    public bool EventsEnabled { get; private set; } = true;
    public bool Running => subscribed;
    public int ActiveEvent { get; private set; }
    public int EventStarts { get; private set; }
    public int TickCount { get; private set; }
    public float EventTime { get; private set; }
    public float HoldDuration { get; private set; }
    public float Heading { get; private set; }
    public Vector2 BirthPoint { get; private set; }
    public int DisplayedFrame { get; private set; } = -1;
    public string LoadError { get; private set; }

    private const float BaseShipHeading = -15.7252615f;
    private const float BaseMeteorHeading = 6.70984f;
    private FormationBackgroundAnimation background;
    private CanvasGroup visibility;
    private RectTransform sourceStage;
    private RectTransform layer;
    private RectTransform eventAnchor;
    private Image image;
    private FrameAnimationRes[] clips;
    private Vector2[][] footprints;
    private System.Random random;
    private RectTransform viewport;
    private RectTransform header;
    private DeckPresentationView deck;
    private bool subscribed;
    private bool loading;
    private bool destroyed;
    private bool applicationPaused;
    private float clock;
    private float nextMeteor;
    private float nextFrigate;
    private Vector2 direction;
    private int lastFrame = -1;
    private static readonly Vector2[] ShipPoints = { new Vector2(580, 375), new Vector2(400, 260), new Vector2(530, 220), new Vector2(330, 285), new Vector2(600, 160), new Vector2(330, 130), new Vector2(430, 330), new Vector2(550, 300) };
    private static readonly Vector2[] MeteorPoints = { new Vector2(300, 438), new Vector2(625, 372), new Vector2(560, 354), new Vector2(430, 260), new Vector2(540, 200), new Vector2(360, 150), new Vector2(430, 335) };
    private static readonly float[] RimX = { 200, 220, 260, 300, 350, 400, 500, 600, 720 };
    private static readonly float[] RimY = { 640, 600, 550, 510, 475, 450, 415, 395, 388 };

    private void Awake() {
        background = GetComponent<FormationBackgroundAnimation>();
        random = new System.Random(randomSeed);
    }

    private async void OnEnable() {
        if (clips != null || loading) {
            return;
        }
        loading = true;
        FrameAnimationRes[] generated = new FrameAnimationRes[2];
        try {
            generated[0] = await FrameAnimationManager.GenerateAsync(SimpleJSON.JSON.Parse(frigateJson.text), frigateAtlas, "FormationEvent/Frigate");
            generated[1] = await FrameAnimationManager.GenerateAsync(SimpleJSON.JSON.Parse(meteorJson.text), meteorAtlas, "FormationEvent/Meteor");
            if (destroyed) {
                DestroyClips(generated);
                return;
            }
            var json = SimpleJSON.JSON.Parse(spawnFootprints.text);
            footprints = new Vector2[2][];
            string[] keys = { "ship", "meteor" };
            for (int k = 0; k < 2; k++) {
                var values = json[keys[k]].AsArray;
                footprints[k] = new Vector2[values.Count];
                for (int i = 0; i < values.Count; i++) {
                    footprints[k][i] = new Vector2(values[i][0].AsFloat, values[i][1].AsFloat);
                }
            }
            clips = generated;
        } catch (Exception error) {
            DestroyClips(generated);
            if (!destroyed) {
                LoadError = error.Message;
                EventsEnabled = false;
                Debug.LogWarning("阵容远景事件保持关闭：" + error.Message);
            }
        } finally {
            loading = false;
        }
    }

    private void LateUpdate() {
        if (visibility == null) {
            visibility = GetComponentInParent<CanvasGroup>();
        }
        if (Ready && layer == null && background != null && background.Ready) {
            BuildLayer();
        }
        bool canRun = Ready && layer != null && EventsEnabled && background != null && background.MotionEnabled && !applicationPaused && (visibility == null || visibility.alpha > .001f) && RookieEngine.timer != null;
        if (canRun && !subscribed) {
            ScheduleFresh();
            RookieEngine.timer.AddUpdateListener(this, Tick);
            subscribed = true;
        } else if (!canRun && subscribed) {
            StopEvents();
        }
    }

    private void BuildLayer() {
        sourceStage = (RectTransform)transform.Find("FormationMotionLayers");
        if (sourceStage == null) {
            return;
        }
        layer = new GameObject("FormationOccasionalEvents", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(sourceStage, false);
        layer.anchorMin = layer.anchorMax = new Vector2(0, 1);
        layer.pivot = new Vector2(0, 1);
        layer.sizeDelta = new Vector2(720, 720);
        layer.SetSiblingIndex(5);
        eventAnchor = new GameObject("EventAnchor", typeof(RectTransform)).GetComponent<RectTransform>();
        eventAnchor.SetParent(layer, false);
        eventAnchor.anchorMin = eventAnchor.anchorMax = new Vector2(0, 1);
        eventAnchor.pivot = new Vector2(.5f, .5f);
        image = new GameObject("EventFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(eventAnchor, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
        image.rectTransform.pivot = Vector2.zero;
        image.raycastTarget = false;
        image.enabled = false;
        viewport = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        var main = GetComponentInParent<MainPanel>();
        if (main != null) {
            header = main.GetComponent<MainPanelLayout>().powerHeader;
            deck = main.GetComponentInChildren<DeckPresentationView>(true);
        }
    }

    private float Between(Vector2 range) {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return min + (max - min) * (float)random.NextDouble();
    }

    private void ScheduleFresh() {
        nextMeteor = clock + Between(new Vector2(5, 10));
        nextFrigate = clock + Between(new Vector2(12, 22));
    }

    private void Tick(float deltaTime) {
        if (!isActiveAndEnabled || !EventsEnabled || applicationPaused || !background.MotionEnabled || (visibility != null && visibility.alpha <= .001f)) {
            return;
        }
        TickCount++;
        clock += Mathf.Max(0, deltaTime);
        if (ActiveEvent == 0) {
            if (clock >= nextFrigate) {
                if (!TryStart(2)) {
                    nextFrigate = clock + 5;
                }
            }
            if (ActiveEvent == 0 && clock >= nextMeteor) {
                if (!TryStart(1)) {
                    nextMeteor = clock + 5;
                }
            }
        }
        if (ActiveEvent == 0) {
            return;
        }
        EventTime += Mathf.Max(0, deltaTime);
        float end = ActiveEvent == 2 ? HoldDuration + 2.22f : 1f;
        if (EventTime >= end) {
            FinishEvent();
            return;
        }
        RenderEvent();
    }

    /// <summary>一个共用显示实例保证流星和远舰不同时出现；无可见安全区域时稍后重试。</summary>
    private bool TryStart(int kind) {
        if (!Ready || layer == null || ActiveEvent != 0) {
            return false;
        }
        float dwell = kind == 2 ? Between(dwellInterval) : 0;
        if (!FindSpawn(kind, dwell, out Vector2 point, out float heading)) {
            return false;
        }
        HoldDuration = dwell;
        Heading = heading;
        BirthPoint = point;
        direction = new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
        ActiveEvent = kind;
        EventTime = 0;
        EventStarts++;
        lastFrame = -1;
        eventAnchor.localScale = Vector3.one;
        eventAnchor.localEulerAngles = new Vector3(0, 0, -(heading - (kind == 2 ? BaseShipHeading : BaseMeteorHeading)));
        if (kind == 2) {
            nextFrigate = float.PositiveInfinity;
        } else {
            nextMeteor = float.PositiveInfinity;
        }
        RenderEvent();
        return true;
    }

    private bool FindSpawn(int kind, float dwell, out Vector2 point, out float heading) {
        Vector2[] candidates = kind == 2 ? ShipPoints : MeteorPoints;
        int first = random.Next(candidates.Length);
        for (int pass = 0; pass < 2; pass++) {
            for (int i = 0; i < candidates.Length; i++) {
                point = candidates[(first + i) % candidates.Length];
                heading = kind == 2 ? (pass == 0 ? (random.Next(2) == 0 ? -1 : 1) * Between(new Vector2(8, 20)) : BaseShipHeading) : (pass == 0 ? Between(new Vector2(-14, 14)) : BaseMeteorHeading);
                if (IsClearFootprint(point, heading, dwell, kind)) {
                    return true;
                }
            }
        }
        point = Vector2.zero;
        heading = 0;
        return false;
    }

    private bool IsClearFootprint(Vector2 point, float heading, float dwell, int kind) {
        float correction = (heading - (kind == 2 ? BaseShipHeading : BaseMeteorHeading)) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(correction);
        float sin = Mathf.Sin(correction);
        Vector2 travel = kind == 2 ? new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad)) * ((dwell + .23f) * forwardSpeed) : Vector2.zero;
        foreach (Vector2 sample in footprints[kind == 2 ? 0 : 1]) {
            Vector2 rotated = new Vector2(sample.x * cos - sample.y * sin, sample.x * sin + sample.y * cos);
            for (int step = 0; step < (kind == 2 ? 3 : 1); step++) {
                if (!ClearPoint(point + rotated + travel * (step * .5f))) {
                    return false;
                }
            }
        }
        return true;
    }

    private bool ClearPoint(Vector2 point) {
        if (point.x < 205 || point.x > 708 || point.y < 12) {
            return false;
        }
        float rim = 388;
        for (int i = 1; i < RimX.Length; i++) {
            if (point.x <= RimX[i]) {
                rim = Mathf.Lerp(RimY[i - 1], RimY[i], Mathf.InverseLerp(RimX[i - 1], RimX[i], point.x));
                break;
            }
        }
        if (point.y > rim - 2) {
            return false;
        }
        Vector3 world = sourceStage.TransformPoint(new Vector3(point.x, -point.y, 0));
        if (!viewport.rect.Contains((Vector2)viewport.InverseTransformPoint(world))) {
            return false;
        }
        if (header != null && header.rect.Contains((Vector2)header.InverseTransformPoint(world))) {
            return false;
        }
        if (deck != null) {
            foreach (DeckCardView slot in deck.slots) {
                if (slot.cellCostBadge == null || !slot.cellCostBadge.activeInHierarchy) {
                    continue;
                }
                var rect = (RectTransform)slot.cellCostBadge.transform;
                Vector2 local = (Vector2)rect.InverseTransformPoint(world) - rect.rect.center;
                float x = local.x / (rect.rect.width * .5f + 2);
                float y = local.y / (rect.rect.height * .5f + 2);
                if (x * x + y * y < 1) {
                    return false;
                }
            }
        }
        return true;
    }

    private void RenderEvent() {
        bool ship = ActiveEvent == 2;
        FrameAnimationRes clip = clips[ship ? 0 : 1];
        float time = EventTime;
        if (ship && time >= .84f) {
            float depart = .84f + HoldDuration;
            time = time >= depart - .08f ? 4.82f + time - (depart - .08f) : .9f + ((time - .84f) % 3.8f);
        }
        int frame = clip.GetIndexByTime(Mathf.Min(time * 1000, clip.totalDuration - 1));
        Vector2 position = BirthPoint;
        if (ship) {
            position += direction * (Mathf.Clamp(EventTime - .84f, 0, HoldDuration + .23f) * forwardSpeed);
        }
        eventAnchor.anchoredPosition = new Vector2(position.x, -position.y);
        if (lastFrame != frame) {
            Vector2 source = clip.sourceSizes[frame];
            // Root anchor is animation pivot in full source canvas; child restores bottom-left crop offset.
            eventAnchor.sizeDelta = source;
            eventAnchor.pivot = clip.pivot;
            image.rectTransform.sizeDelta = clip.sprites[frame].rect.size;
            image.rectTransform.anchoredPosition = clip.frameOffsets[frame];
            image.sprite = clip.sprites[frame];
            image.enabled = true;
            lastFrame = frame;
            DisplayedFrame = frame;
        }
    }

    private void FinishEvent() {
        if (ActiveEvent == 2) {
            nextFrigate = clock + Between(frigateInterval);
            nextMeteor = Mathf.Max(nextMeteor, clock + Between(new Vector2(2, 5)));
        } else if (ActiveEvent == 1) {
            nextMeteor = clock + Between(meteorInterval);
            nextFrigate = Mathf.Max(nextFrigate, clock + Between(new Vector2(2, 5)));
        }
        CancelDisplay();
    }

    private void CancelDisplay() {
        ActiveEvent = 0;
        EventTime = 0;
        lastFrame = -1;
        DisplayedFrame = -1;
        if (image != null) {
            image.enabled = false;
            image.sprite = null;
        }
    }

    private void StopEvents() {
        if (subscribed && RookieEngine.timer != null) {
            RookieEngine.timer.RemoveUpdateListener(this, Tick);
        }
        subscribed = false;
        CancelDisplay();
    }

    public void SetEventsEnabled(bool value) {
        EventsEnabled = value;
        if (!value) {
            StopEvents();
        }
    }

    private void OnDisable() {
        StopEvents();
    }

    private void OnApplicationPause(bool paused) {
        applicationPaused = paused;
        if (paused) {
            StopEvents();
        }
    }

    private void OnDestroy() {
        destroyed = true;
        StopEvents();
        DestroyClips(clips);
        clips = null;
        if (layer != null) {
            Destroy(layer.gameObject);
        }
    }

    private static void DestroyClips(FrameAnimationRes[] values) {
        if (values == null) {
            return;
        }
        foreach (FrameAnimationRes clip in values) {
            if (clip == null) {
                continue;
            }
            foreach (Sprite sprite in clip.sprites) {
                if (sprite != null) {
                    Destroy(sprite);
                }
            }
        }
    }
}

