using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cfg;

/// <summary>管理本局准备与结束；唯一的格子解锁请求入口，不依赖界面节点。</summary>
public sealed class BattleControl {
    public static BattleControl ins { get; } = new BattleControl();
    public BattleSceneModel model { get; private set; }
    public bool preparing => pending != null;
    private Task pending;
    private int generation;
    private readonly HashSet<string> ownedResources = new HashSet<string>();
    public event Action<long, string> UnlockRejected;
    public event Action Finished;
    public BattleResultVO lastResult { get; private set; }

    public Task PrepareAsync(int sceneId) {
        if (pending != null || model != null) {
            throw new InvalidOperationException("战斗已开始或正在准备。");
        }
        lastResult = null;
        int version = ++generation;
        pending = PrepareCoreAsync(sceneId, version);
        return AwaitPreparationAsync();
    }

    private async Task AwaitPreparationAsync() {
        try {
            await pending;
        } finally {
            pending = null;
        }
    }

    private async Task PrepareCoreAsync(int sceneId, int version) {
        BattleSceneModel candidate = null;
        var acquired = new HashSet<string>();
        try {
            await CfgManager.EnsureLoadedAsync();
            candidate = new BattleSceneModel(CfgManager.tables.BattleSceneObj.Get(sceneId), CfgManager.tables.BattleSceneCellObj.DataList);
            candidate.InitializeBattle(CfgManager.tables);
            var requests = new List<ResLoadInfo>();
            foreach (string path in BattleSceneResources.Collect(candidate)) {
                if (!ResourceManager.HasLoadedUnpackImage(path)) {
                    acquired.Add(path);
                }
                requests.Add(new ResLoadInfo(path, ResType.UnpackImage));
            }
            string groundMaterial = BattleSceneResources.GroundMaterialPath(candidate);
            if (ResourceManager.GetMaterial(groundMaterial) == null) {
                acquired.Add(groundMaterial);
            }
            requests.Add(new ResLoadInfo(groundMaterial, ResType.Material));
            foreach (string path in BattleSceneResources.CollectAnimations(candidate)) {
                requests.Add(new ResLoadInfo(path, ResType.FrameAnim));
            }
            await ResourceLoader.LoadListAsync(requests, maxConcurrent: 4);
            if (version != generation) {
                throw new OperationCanceledException();
            }
            model = candidate;
            model.BattleEnded += OnBattleEnded;
            ownedResources.UnionWith(acquired);
        } catch {
            candidate?.Dispose();
            foreach (string path in acquired) {
                ResourceManager.Release(path);
            }
            throw;
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
        if (model != null) {
            model.BattleEnded -= OnBattleEnded;
            model.Dispose();
        }
        model = null;
        foreach (string path in ownedResources) {
            ResourceManager.Release(path);
        }
        ownedResources.Clear();
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
