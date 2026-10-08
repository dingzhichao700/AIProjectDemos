using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗背景表现
/// </summary>
/// <remarks>
/// 创建并同步关卡循环滚动背景，以及固定在地面层上的飞机残骸。
/// </remarks>
internal sealed class BattleBackgroundPresenter {

    private sealed class WreckageViewState {

        public RectTransform hitHole;
        public RectTransform aircraft;

    }

    private const float BackgroundWidth = 720f;
    private const float BackgroundHeight = 2560f;
    private readonly RectTransform layer;
    private readonly RectTransform highLayer;
    private readonly BattleVisualPool visualPool;
    private readonly List<RectTransform[]> backgroundLayers = new List<RectTransform[]>();
    private readonly List<float> scrollSpeeds = new List<float>();
    private readonly Dictionary<long, WreckageViewState> wreckages = new Dictionary<long, WreckageViewState>();

    public BattleBackgroundPresenter(RectTransform layer, RectTransform highLayer, BattleVisualPool visualPool) {
        this.layer = layer;
        this.highLayer = highLayer;
        this.visualPool = visualPool;
    }

    /**按远近顺序创建四层循环背景*/
    public void Initialize(BattleSceneBackgroundVO config) {
        backgroundLayers.Clear();
        scrollSpeeds.Clear();
        AddLayer("Far", config.backgroundRes, config.backgroundScrollSpeed, true, layer);
        AddLayer("Low", config.lowRes, config.lowScrollSpeed, false, layer);
        AddLayer("Middle", config.middleRes, config.middleScrollSpeed, false, layer);
        AddLayer("High", config.highRes, config.highScrollSpeed, false, highLayer);
    }

    /**使用场景计时器增量同步各层视差滚动*/
    public void Update(float deltaTime) {
        for (int layerIndex = 0; layerIndex < backgroundLayers.Count; layerIndex++) {
            RectTransform[] pair = backgroundLayers[layerIndex];
            float distance = scrollSpeeds[layerIndex] * deltaTime;
            foreach (RectTransform background in pair) {
                background.anchoredPosition += Vector2.down * distance;
            }
            foreach (RectTransform background in pair) {
                if (background.anchoredPosition.y <= -BackgroundHeight) {
                    RectTransform other = pair[0] == background ? pair[1] : pair[0];
                    background.anchoredPosition = new Vector2(0f, other.anchoredPosition.y + BackgroundHeight);
                }
            }
        }
    }

    /**在地面层创建撞击坑与战损飞机组成的残骸*/
    public void AddWreckage(AircraftWreckageVO wreckage) {
        if (wreckage == null || wreckages.ContainsKey(wreckage.id)) {
            return;
        }
        Vector2 hitHoleSize = GetImageSize(wreckage.hitHolePath);
        RectTransform hitHole = visualPool.Create($"aircraftWreckage{wreckage.id}", layer, hitHoleSize, wreckage.position, wreckage.hitHolePath, 0f, true);
        RectTransform aircraft = visualPool.Create("damagedAircraft", hitHole, wreckage.aircraftDisplaySize, Vector2.zero, wreckage.damagedAppearancePath, wreckage.aircraftRotation, true);
        aircraft.anchorMin = aircraft.anchorMax = new Vector2(0.5f, 0.5f);
        aircraft.localScale = Vector3.one * BattleAircraftDeathConst.GroundAircraftDisplayScale;
        wreckages.Add(wreckage.id, new WreckageViewState { hitHole = hitHole, aircraft = aircraft });
    }

    /**同步一个地面残骸的滚动位置*/
    public void SyncWreckage(AircraftWreckageVO wreckage) {
        if (wreckage != null && wreckages.TryGetValue(wreckage.id, out WreckageViewState state) && state.hitHole != null) {
            state.hitHole.anchoredPosition = wreckage.position;
        }
    }

    /**回收一个已经离开地面层范围的残骸*/
    public void RemoveWreckage(long id) {
        if (!wreckages.Remove(id, out WreckageViewState state)) {
            return;
        }
        visualPool.Recycle(state.aircraft);
        visualPool.Recycle(state.hitHole);
    }

    /**清理背景和全部残骸表现*/
    public void Clear() {
        foreach (long id in new List<long>(wreckages.Keys)) {
            RemoveWreckage(id);
        }
        backgroundLayers.Clear();
        scrollSpeeds.Clear();
        for (int i = highLayer.childCount - 1; i >= 0; i--) {
            GameObject child = highLayer.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    /**获取已经预加载的散图原始尺寸*/
    private static Vector2 GetImageSize(string path) {
        BattlePreloadCollector.RequireUnpackImagePreloaded(path);
        Sprite sprite = ResourceManager.GetUnpackImage(path);
        if (sprite == null) {
            throw new System.InvalidOperationException($"飞机残骸图片不存在：{path}");
        }
        return sprite.rect.size;
    }

    /**创建同一视差层首尾衔接的两张图片*/
    private void AddLayer(string layerName, string resourceName, float scrollSpeed, bool required, RectTransform targetLayer) {
        if (string.IsNullOrWhiteSpace(resourceName)) {
            if (required) {
                throw new System.InvalidOperationException("场景背景必须配置远景地表资源");
            }
            return;
        }
        string path = BattleConst.GetSceneBackgroundImagePath(resourceName);
        Vector2 size = new Vector2(BackgroundWidth, BackgroundHeight);
        RectTransform first = BattleViewFactory.CreateImage($"imgBattleBackground{layerName}A", targetLayer, size, Vector2.zero, path);
        RectTransform second = BattleViewFactory.CreateImage($"imgBattleBackground{layerName}B", targetLayer, size, new Vector2(0f, BackgroundHeight), path);
        backgroundLayers.Add(new[] { first, second });
        scrollSpeeds.Add(scrollSpeed);
    }

}
