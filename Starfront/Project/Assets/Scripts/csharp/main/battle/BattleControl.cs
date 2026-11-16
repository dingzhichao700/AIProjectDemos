using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using cfg;

/// <summary>管理本局准备与结束；唯一的格子解锁请求入口，不依赖界面节点。</summary>
public sealed class BattleControl {
    public static BattleControl ins { get; } = new BattleControl();
    public BattleSceneModel model { get; private set; }
    public bool preparing => pending != null;
    private Task pending;
    private CancellationTokenSource activeCancellation;
    private BattlePreparedBattle preparedBattle;
    private int generation;
    private readonly List<IDisposable> cardImageLeases = new List<IDisposable>();
    private readonly HashSet<string> ownedResources = new HashSet<string>();
    public event Action<long, string> UnlockRejected;
    public event Action Finished;
    public BattleResultVO lastResult { get; private set; }

    /// <summary>场景战斗的阶段准入。UI预检与直接准备调用必须遵守同一入口。</summary>
    public bool CanEnterSceneBattle(out string reason) {
        int limit = BattleConst.DeckCardLimit;
        if (limit <= 0 || TeamEditControl.ins.ReadSelected(out _).Count(id => id != 0 && TeamEditControl.ins.Query.Level(id) > 0) > limit)
        {
            reason = "出战卡牌数量超过配置上限，或上限配置无效";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public async Task PrepareAsync(int sceneId)
    {
        if (!CanEnterSceneBattle(out string reason))
        {
            throw new InvalidOperationException(reason);
        }
        // 兼容旧入口也在调用当刻冻结，之后不再读取编组。
        var pair = new LocalBattleMatchBackend().CaptureLocal(sceneId);
        using (var result = await PrepareMatchAsync(pair, null, CancellationToken.None))
        {
            if (!result.TryCommit())
            {
                throw new InvalidOperationException("战斗准备结果已失效");
            }
        }
    }

    internal async Task<BattlePreparedBattle> PrepareMatchAsync(BattleMatchPairVO pair,
        Action<BattlePreparationProgressVO> progress, CancellationToken cancellation)
    {
        if (pending != null || model != null || preparedBattle != null)
        {
            throw new InvalidOperationException("战斗已开始或正在准备。");
        }
        var marker = new TaskCompletionSource<bool>();
        pending = marker.Task;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        activeCancellation = linked;
        int version = ++generation;
        lastResult = null;
        try
        {
            var result = await BattleMatchPreparation.PrepareAsync(this, pair, progress, linked.Token);
            if (version != generation || linked.IsCancellationRequested)
            {
                result.Dispose();
                throw new OperationCanceledException(linked.Token);
            }
            preparedBattle = result;
            return result;
        }
        finally
        {
            if (ReferenceEquals(pending, marker.Task))
            {
                pending = null;
            }
            if (ReferenceEquals(activeCancellation, linked))
            {
                activeCancellation = null;
            }
            linked.Dispose();
            marker.TrySetResult(true);
        }
    }

    internal bool CommitPrepared(BattlePreparedBattle prepared)
    {
        if (!ReferenceEquals(preparedBattle, prepared) || pending != null || model != null
            || prepared.Candidate == null)
        {
            return false;
        }
        preparedBattle = null;
        model = prepared.Candidate;
        prepared.Candidate = null;
        model.BattleEnded += OnBattleEnded;
        // 成功移交后的窗口Prefab沿用既有UIFactory/PanelMgr常驻缓存；
        // 未移交时仍由准备结果回收，不能在窗口池仍持有实例时卸载其母版。
        prepared.OwnedResources.Remove(ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL));
        prepared.OwnedResources.Remove(ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL));
        ownedResources.UnionWith(prepared.OwnedResources);
        prepared.OwnedResources.Clear();
        cardImageLeases.AddRange(prepared.Images);
        prepared.Images.Clear();
        return true;
    }

    internal void DetachPrepared(BattlePreparedBattle prepared)
    {
        if (ReferenceEquals(preparedBattle, prepared))
        {
            preparedBattle = null;
        }
    }

    private void OnBattleEnded(BattleSceneModel source) {
        if (source != model) {
            return;
        }
        lastResult = source.result;
        source.StopTimeFlow();
        UnityEngine.Debug.Log("战斗结束：" + lastResult.reason + "，胜方=" + lastResult.winner + "，占地=" + lastResult.friendlyTerritory + ":" + lastResult.enemyTerritory);
        Finished?.Invoke();
    }

    /**视图须先清空Sprite引用，再调用End释放本局独占资源。*/
    public void End() {
        generation++;
        activeCancellation?.Cancel();
        preparedBattle?.Dispose();
        if (model != null) {
            model.BattleEnded -= OnBattleEnded;
            model.Dispose();
        }
        model = null;
        foreach (string path in ownedResources) {
            ResourceManager.Release(path);
        }
        ownedResources.Clear();
        foreach (var lease in cardImageLeases)
        {
            lease.Dispose();
        }
        cardImageLeases.Clear();
    }

    /**供后续伤害/死亡逻辑统一释放人口，表现回收不能调用此接口。*/
    public bool SetUnitAltitude(long id, float altitude) {
        if (model == null || !model.elements.TryGetValue(id, out var element) || !(element is BattleUnitVO unit) || unit.destroyed) {
            return false;
        }
        unit.SetAltitude(altitude);
        return true;
    }

    public bool RemoveUnit(long id) {
        return model != null && model.RemoveCombatUnit(id);
    }

    /**玩家输入和电脑决策共用提交路径，最终校验与扣费仍由解锁模型负责。*/
    internal static bool TryUnlockForCamp(BattleSceneModel scene, BattleSceneCamp camp, long cellId, out string reason) {
        if (scene?.unlocks == null || !scene.camps.ContainsKey(camp)) {
            reason = "对战尚未就绪或阵营无效";
            return false;
        }
        return scene.unlocks.TryUnlock(cellId, camp, out reason);
    }

    public bool TryUnlockCell(long cellId, out string reason) {
        if (model?.unlocks == null) {
            reason = "对战尚未就绪";
        } else if (model.camps[BattleSceneCamp.FRIENDLY].config.ControlType != BattlePlayerControlType.HUMAN) {
            reason = "该阵营由电脑控制";
        } else if (TryUnlockForCamp(model, BattleSceneCamp.FRIENDLY, cellId, out reason)) {
            return true;
        }
        UnlockRejected?.Invoke(cellId, reason);
        return false;
    }
}
