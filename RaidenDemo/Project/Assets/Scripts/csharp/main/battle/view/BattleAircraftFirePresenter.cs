using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飞行器着火表现
/// </summary>
/// <remarks>
/// 按单位的着火点播放附着火焰和独立烟雾；死亡期间保留绑定，销毁时释放监听和火焰。
/// </remarks>
internal sealed class BattleAircraftFirePresenter {

    /// <summary>
    /// 单个飞行器的火焰节点和播放句柄
    /// </summary>
    private sealed class AircraftFireViewState {

        /**拥有着火点的逻辑单位*/
        public AircraftVO aircraft;

        /**始终位于实体层的机身根节点*/
        public RectTransform root;

        /**用于同步机身隐藏和闪烁的图像节点*/
        public RectTransform visual;

        /**统一控制附着火焰的可见性*/
        public RectTransform fireRoot;

        /**按着火点持有对应的循环动画*/
        public readonly Dictionary<AircraftFirePointVO, FrameAnimationView> flames = new Dictionary<AircraftFirePointVO, FrameAnimationView>();

    }

    /**通过特效服务创建已经预加载的帧动画*/
    private readonly BattleEffectPresenter effects;

    /**保持存活、死亡和保留机身的单位绑定*/
    private readonly Dictionary<long, AircraftFireViewState> aircraftViews = new Dictionary<long, AircraftFireViewState>();

    /**已经离开单位的烟雾播放至结束，退出战斗时统一取消*/
    private readonly HashSet<FrameAnimationView> smokeEffects = new HashSet<FrameAnimationView>();

    /// <summary>
    /// 创建着火表现协调器。
    /// </summary>
    /// <param name="effects">战斗特效播放服务</param>
    public BattleAircraftFirePresenter(BattleEffectPresenter effects) {
        this.effects = effects;
    }

    /// <summary>
    /// 绑定飞行器的着火表现。
    /// </summary>
    /// <param name="aircraft">拥有着火点的单位</param>
    /// <param name="root">单位在实体层中的根节点</param>
    public void Bind(AircraftVO aircraft, RectTransform root) {
        if (aircraft == null || aircraft.destroyed || root == null) {
            return;
        }
        Unbind(aircraft.id);
        AircraftFireViewState state = new AircraftFireViewState { aircraft = aircraft, root = root, visual = root.Find("imgVisual") as RectTransform };
        aircraftViews.Add(aircraft.id, state);
        aircraft.firePointsChanged += OnFirePointsChanged;
        aircraft.smokeRequested += OnSmokeRequested;
        SyncFlames(state);
    }

    /// <summary>
    /// 解除单位绑定并清除附着火焰。
    /// </summary>
    /// <param name="id">单位ID</param>
    /// <remarks>
    /// 必须在机身放回对象池前执行，防止旧监听操作被复用的机身。
    /// </remarks>
    public void Unbind(long id) {
        if (!aircraftViews.TryGetValue(id, out AircraftFireViewState state)) {
            return;
        }
        aircraftViews.Remove(id);
        state.aircraft.firePointsChanged -= OnFirePointsChanged;
        state.aircraft.smokeRequested -= OnSmokeRequested;
        ClearFlames(state);
    }

    /**同步所属计时器内的机身可见性，不推进着火逻辑*/
    public void Sync(TimerType timerType) {
        foreach (AircraftFireViewState state in aircraftViews.Values) {
            if (state.aircraft.timerType == timerType) {
                SyncVisibility(state);
            }
        }
    }

    /// <summary>
    /// 清理本局全部火焰和未播放完的烟雾。
    /// </summary>
    /// <remarks>
    /// 先解绑单位再回收机身，烟雾的动画计时和完成回调同时取消。
    /// </remarks>
    public void Clear() {
        foreach (AircraftFireViewState state in aircraftViews.Values) {
            state.aircraft.firePointsChanged -= OnFirePointsChanged;
            state.aircraft.smokeRequested -= OnSmokeRequested;
            ClearFlames(state);
        }
        aircraftViews.Clear();
        foreach (FrameAnimationView smoke in smokeEffects) {
            if (smoke != null) {
                smoke.Recover();
            }
        }
        smokeEffects.Clear();
    }

    /**着火点改变时同步火焰，单位销毁时立即解绑*/
    private void OnFirePointsChanged(AircraftVO aircraft) {
        if (aircraft.destroyed) {
            Unbind(aircraft.id);
            return;
        }
        if (aircraftViews.TryGetValue(aircraft.id, out AircraftFireViewState state)) {
            SyncFlames(state);
        }
    }

    /**在逻辑发烟位置播放独立烟雾，后续移动不再带动已生成的烟雾*/
    private void OnSmokeRequested(AircraftVO aircraft, AircraftFirePointVO point) {
        if (!aircraftViews.TryGetValue(aircraft.id, out AircraftFireViewState state) || state.root == null || !state.root.gameObject.activeInHierarchy || (state.visual != null && !state.visual.gameObject.activeSelf)) {
            return;
        }
        FrameAnimationView handle = null;
        bool completedSynchronously = false;
        handle = effects.PlayAircraftSmoke(point.smokeEffectId, aircraft.GetFirePointWorldPosition(point), aircraft.timerType, () => {
            completedSynchronously = true;
            if (handle != null) {
                smokeEffects.Remove(handle);
            }
        });
        if (handle != null && !completedSynchronously) {
            smokeEffects.Add(handle);
        }
    }

    /**按着火点差额增删循环火焰，已有着火点继续沿用原播放句柄*/
    private void SyncFlames(AircraftFireViewState state) {
        if (state.root == null) {
            ClearFlames(state);
            return;
        }
        HashSet<AircraftFirePointVO> activePoints = new HashSet<AircraftFirePointVO>(state.aircraft.firePoints);
        List<AircraftFirePointVO> removedPoints = new List<AircraftFirePointVO>();
        foreach (KeyValuePair<AircraftFirePointVO, FrameAnimationView> entry in state.flames) {
            if (!activePoints.Contains(entry.Key)) {
                entry.Value?.Recover();
                removedPoints.Add(entry.Key);
            }
        }
        foreach (AircraftFirePointVO point in removedPoints) {
            state.flames.Remove(point);
        }
        if (activePoints.Count == 0) {
            ClearFireRoot(state);
            return;
        }
        EnsureFireRoot(state);
        foreach (AircraftFirePointVO point in state.aircraft.firePoints) {
            if (!state.flames.ContainsKey(point)) {
                state.flames.Add(point, effects.PlayAircraftFire(point.fireEffectId, state.fireRoot, point.localPosition, state.aircraft.timerType));
            }
        }
        SyncVisibility(state);
    }

    /**创建承载附着火焰的机身子节点*/
    private static void EnsureFireRoot(AircraftFireViewState state) {
        if (state.fireRoot != null) {
            return;
        }
        state.fireRoot = new GameObject("aircraftFire", typeof(RectTransform)).GetComponent<RectTransform>();
        state.fireRoot.SetParent(state.root, false);
        state.fireRoot.anchorMin = state.fireRoot.anchorMax = new Vector2(0.5f, 0.5f);
        state.fireRoot.pivot = new Vector2(0.5f, 0.5f);
        state.fireRoot.anchoredPosition = Vector2.zero;
        state.fireRoot.sizeDelta = Vector2.zero;
        state.fireRoot.localScale = Vector3.one;
        state.fireRoot.localEulerAngles = Vector3.zero;
    }

    /**附着火焰随机身隐藏，位置保持在实际碰撞坐标中，不叠加图像的装饰性转向*/
    private static void SyncVisibility(AircraftFireViewState state) {
        if (state.fireRoot != null) {
            state.fireRoot.gameObject.SetActive(state.visual == null || state.visual.gameObject.activeSelf);
        }
    }

    /**停止全部循环动画，防止销毁节点后仍有计时回调*/
    private static void ClearFlames(AircraftFireViewState state) {
        foreach (FrameAnimationView flame in state.flames.Values) {
            if (flame != null) {
                flame.Recover();
            }
        }
        state.flames.Clear();
        ClearFireRoot(state);
    }

    /**销毁空的火焰承载节点*/
    private static void ClearFireRoot(AircraftFireViewState state) {
        if (state.fireRoot == null) {
            return;
        }
        state.fireRoot.gameObject.SetActive(false);
        Object.Destroy(state.fireRoot.gameObject);
        state.fireRoot = null;
    }


}
