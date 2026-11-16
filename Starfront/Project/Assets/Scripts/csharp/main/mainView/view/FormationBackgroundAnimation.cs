using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>阵容专属背景序列；原Image保持静态回退，动画不参与输入。</summary>
public sealed class FormationBackgroundAnimation : MonoBehaviour {

    public Sprite space;
    public Sprite floor;
    public Sprite fogAAtlas;
    public Sprite fogBAtlas;
    public Sprite starAtlas;
    public TextAsset fogAJson;
    public TextAsset fogBJson;
    public TextAsset starJson;

    public bool MotionEnabled { get; private set; } = true;
    public bool Ready => clips != null;
    public int TickCount { get; private set; }
    public int FogFrame { get; private set; }
    public float Elapsed => elapsed;
    public string LoadError { get; private set; }

    private Image fallback;
    private RectTransform stage;
    private Image fog;
    private Image[] stars;
    private FrameAnimationRes[] clips;
    private bool loading;
    private bool destroyed;
    private bool subscribed;
    private bool applicationPaused;
    private bool visible = true;
    private CanvasGroup visibilityGroup;
    private float elapsed;
    private float nextStar = .3f;
    private float slowSeconds;
    private float[] born = { -10, -10, -10, -10 };
    private Vector2[] positions;
    private System.Random random;
    private int fogKey = -1;
    private int[] starKeys = { -1, -1, -1, -1 };
    private static readonly Vector2[] AllowedStars = { new Vector2(441, 47), new Vector2(541, 25), new Vector2(612, 196), new Vector2(360, 112), new Vector2(283, 65), new Vector2(502, 280), new Vector2(250, 445), new Vector2(285, 460), new Vector2(335, 432), new Vector2(380, 412), new Vector2(260, 510), new Vector2(310, 485) };

    private void Awake() {
        fallback = GetComponent<Image>();
        random = new System.Random(20261112);
        positions = new Vector2[4];
    }

    private async void OnEnable() {
        if (clips != null) {
            StartMotion();
            return;
        }
        if (loading) {
            return;
        }
        loading = true;
        FrameAnimationRes[] generated = new FrameAnimationRes[3];
        try {
            Sprite[] atlases = { fogAAtlas, fogBAtlas, starAtlas };
            TextAsset[] json = { fogAJson, fogBJson, starJson };
            for (int i = 0; i < 3; i++) {
                generated[i] = await FrameAnimationManager.GenerateAsync(SimpleJSON.JSON.Parse(json[i].text), atlases[i], "FormationBackground/" + i);
            }
            if (destroyed) {
                DestroyClips(generated);
                return;
            }
            clips = generated;
            BuildStage();
            StartMotion();
        } catch (Exception error) {
            DestroyClips(generated);
            if (!destroyed) {
                LoadError = error.Message;
                SetMotionEnabled(false);
                Debug.LogWarning("阵容动态背景回退静态：" + error.Message);
            }
        } finally {
            loading = false;
        }
    }

    private void BuildStage() {
        stage = new GameObject("FormationMotionLayers", typeof(RectTransform)).GetComponent<RectTransform>();
        stage.SetParent(transform, false);
        stage.anchorMin = stage.anchorMax = new Vector2(0, 1);
        stage.pivot = new Vector2(0, 1);
        stage.sizeDelta = new Vector2(720, 720);
        Image sky = CreateImage("DistantSpace", stage);
        sky.sprite = space;
        PlaceFull(sky, new Vector2(720, 720), Vector2.zero);
        stars = new Image[4];
        for (int i = 0; i < stars.Length; i++) {
            stars[i] = CreateImage("LocalStar" + i, stage);
            stars[i].enabled = false;
        }
        fog = CreateImage("HorizonFog", stage);
        Image front = CreateImage("MechanicalForeground", stage);
        front.sprite = floor;
        PlaceFull(front, new Vector2(720, 720), Vector2.zero);
    }

    private static Image CreateImage(string name, Transform parent) {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.gameObject.layer = parent.gameObject.layer;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
        image.rectTransform.pivot = new Vector2(0, 1);
        return image;
    }

    private static void PlaceFull(Image image, Vector2 size, Vector2 topLeft) {
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
    }

    private void StartMotion() {
        if (!isActiveAndEnabled || clips == null || !MotionEnabled || !visible || applicationPaused || RookieEngine.timer == null) {
            return;
        }
        stage.gameObject.SetActive(true);
        fallback.enabled = false;
        fogKey = -1;
        for (int i = 0; i < starKeys.Length; i++) {
            starKeys[i] = -1;
        }
        if (!subscribed) {
            RookieEngine.timer.AddUpdateListener(this, Tick);
            subscribed = true;
        }
        Render();
    }

    private void StopMotion() {
        if (subscribed && RookieEngine.timer != null) {
            RookieEngine.timer.RemoveUpdateListener(this, Tick);
        }
        subscribed = false;
        if (stage != null) {
            stage.gameObject.SetActive(false);
        }
        if (fallback != null) {
            fallback.enabled = true;
        }
        if (fog != null) {
            fog.sprite = null;
        }
        if (stars != null) {
            foreach (Image star in stars) {
                star.sprite = null;
            }
        }
    }

    /// <summary>显式静态回退开关；恢复时保持原构图和资源，不另建实例。</summary>
    public void SetMotionEnabled(bool value) {
        MotionEnabled = value;
        if (value) {
            slowSeconds = 0;
            StartMotion();
        } else {
            StopMotion();
        }
    }

    private void Tick(float deltaTime) {
        if (!isActiveAndEnabled || !MotionEnabled || !visible || applicationPaused) {
            return;
        }
        TickCount++;
        elapsed += Mathf.Max(0, deltaTime);
        // 持续慢于20FPS才降级；一次切页/加载尖峰不会触发。
        slowSeconds = deltaTime > .05f ? slowSeconds + deltaTime : Mathf.Max(0, slowSeconds - deltaTime * 2);
        if (slowSeconds > 5) {
            SetMotionEnabled(false);
            return;
        }
        if (elapsed >= nextStar) {
            for (int i = 0; i < 4; i++) {
                if (elapsed - born[i] > 1.333f) {
                    born[i] = elapsed;
                    positions[i] = PickVisibleStar();
                    stars[i].color = new Color(1, 1, 1, .6f + (float)random.NextDouble() * .3f);
                    break;
                }
            }
            nextStar = elapsed + 1.6f + (float)random.NextDouble() * 1.2f;
        }
        Render();
    }

    private Vector2 PickVisibleStar() {
        var rootCanvas = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        var main = GetComponentInParent<MainPanel>();
        RectTransform header = main != null ? main.GetComponent<MainPanelLayout>().powerHeader : null;
        int first = random.Next(AllowedStars.Length);
        for (int i = 0; i < AllowedStars.Length; i++) {
            Vector2 point = AllowedStars[(first + i) % AllowedStars.Length];
            Vector3 world = stage.TransformPoint(new Vector3(point.x, -point.y, 0));
            Vector2 local = rootCanvas.InverseTransformPoint(world);
            if (!rootCanvas.rect.Contains(local)) {
                continue;
            }
            if (header != null && header.rect.Contains((Vector2)header.InverseTransformPoint(world))) {
                continue;
            }
            return point;
        }
        return new Vector2(285, 460);
    }

    private void Render() {
        stage.localScale = Vector3.one * (((RectTransform)transform).rect.width / 720f);
        float ms = (elapsed * 1000) % 8000;
        int segment = ms < 4000 ? 0 : 1;
        int index = clips[segment].GetIndexByTime(ms % 4000);
        FogFrame = segment * 32 + index;
        if (fogKey != FogFrame) {
            ApplyFrame(fog, clips[segment], index, new Vector2(360, 576));
            fogKey = FogFrame;
        }
        for (int i = 0; i < 4; i++) {
            float age = elapsed - born[i];
            if (age < 0 || age >= 1.333f) {
                stars[i].enabled = false;
                stars[i].sprite = null;
                starKeys[i] = -1;
                continue;
            }
            int frame = clips[2].GetIndexByTime(age * 1000);
            if (starKeys[i] != frame) {
                ApplyFrame(stars[i], clips[2], frame, positions[i]);
                starKeys[i] = frame;
            }
        }
    }

    /// <summary>复用框架原画布/pivot/左下裁剪偏移契约，显示坐标使用母图左上原点。</summary>
    private static void ApplyFrame(Image image, FrameAnimationRes clip, int index, Vector2 anchor) {
        Sprite sprite = clip.sprites[index];
        Vector2 source = clip.sourceSizes[index];
        Vector2 offset = clip.frameOffsets[index];
        Vector2 topLeft = anchor - Vector2.Scale(source, new Vector2(clip.pivot.x, 1 - clip.pivot.y)) + new Vector2(offset.x, source.y - offset.y - sprite.rect.height);
        PlaceFull(image, sprite.rect.size, topLeft);
        image.sprite = sprite;
        image.enabled = true;
    }

    private void LateUpdate() {
        if (visibilityGroup == null) {
            visibilityGroup = GetComponentInParent<CanvasGroup>();
        }
        bool next = visibilityGroup == null || visibilityGroup.alpha > .001f;
        if (next != visible) {
            visible = next;
            if (visible) {
                StartMotion();
            } else {
                StopMotion();
            }
        }
    }

    private void OnDisable() {
        StopMotion();
    }

    private void OnApplicationPause(bool paused) {
        applicationPaused = paused;
        if (paused) {
            StopMotion();
        } else {
            StartMotion();
        }
    }

    private void OnDestroy() {
        destroyed = true;
        StopMotion();
        DestroyClips(clips);
        clips = null;
    }

    private static void DestroyClips(FrameAnimationRes[] data) {
        if (data == null) {
            return;
        }
        foreach (FrameAnimationRes clip in data) {
            if (clip == null || clip.sprites == null) {
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

