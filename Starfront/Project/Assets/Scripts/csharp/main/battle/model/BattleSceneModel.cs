using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>一局的逻辑地图；复用 SceneModel 的元素登记和 Timer 分发。</summary>
public sealed class BattleSceneModel : SceneModel {
    private readonly Dictionary<(int, int), BattleCellVO> byCoordinate = new Dictionary<(int, int), BattleCellVO>();
    private readonly Dictionary<long, BattleCellVO> byId = new Dictionary<long, BattleCellVO>();
    private long nextId;
    private readonly Dictionary<BattleSceneCamp, BattleCampVO> campMap = new Dictionary<BattleSceneCamp, BattleCampVO>();
    private readonly Dictionary<long, BattleBuildingVO> buildingMap = new Dictionary<long, BattleBuildingVO>();
    public IReadOnlyDictionary<BattleSceneCamp, BattleCampVO> camps { get; }
    public IReadOnlyDictionary<long, BattleBuildingVO> buildings { get; }
    public event Action<BattleCampVO> CurrencyChanged;
    public event Action InformationChanged;

    public event Action StructureChanged;

    public BattleNavigation navigation { get; private set; }

    public BattleUnlockModel unlocks { get; private set; }
    public event Action<BattleSceneModel> TimeExpired;
    public double remainingSeconds { get; private set; }
    public bool expired { get; private set; }
    public int displayRemainingSeconds => (int)Math.Ceiling(remainingSeconds);
    public BattleSceneResource config { get; }
    public IReadOnlyDictionary<long, BattleCellVO> cells { get; }

    public BattleSceneModel(BattleSceneResource scene, IEnumerable<BattleSceneCellResource> source) {
        config = scene ?? throw new ArgumentNullException(nameof(scene));
        if (scene.Width <= 0 || scene.Height <= 0) {
            throw new InvalidOperationException("场景尺寸必须为正。");
        }
        camps = new ReadOnlyDictionary<BattleSceneCamp, BattleCampVO>(campMap);
        buildings = new ReadOnlyDictionary<long, BattleBuildingVO>(buildingMap);
        var configIds = new HashSet<int>();
        foreach (var record in source) {
            if (record.SceneId != scene.Id) {
                continue;
            }
            if ((record.K - record.Q) % 2 != 0 || !configIds.Add(record.Id) || byCoordinate.ContainsKey((record.Q, record.K))) {
                throw new InvalidOperationException("重复或非法格子配置：" + record.Id);
            }
            if (record.Type != BattleSceneCellType.NORMAL && record.Camp != BattleSceneCamp.ABSOLUTE_NEUTRAL) {
                throw new InvalidOperationException("障碍格必须永久中立：" + record.Id);
            }
            var cell = new BattleCellVO(++nextId, record, BattleConst.CellCenter(scene.OriginX, scene.OriginY, record.Q, record.K));
            byCoordinate.Add((cell.q, cell.k), cell);
            byId.Add(cell.id, cell);
            AddElement(cell);
        }
        cells = new ReadOnlyDictionary<long, BattleCellVO>(byId);
        ValidateBase(scene.FriendlyBaseQ, scene.FriendlyBaseK, BattleSceneCamp.FRIENDLY);
        ValidateBase(scene.EnemyBaseQ, scene.EnemyBaseK, BattleSceneCamp.ENEMY);
    }

    public void InitializeBattle(Tables tables, int? randomSeed = null) {
        if (campMap.Count != 0) {
            throw new InvalidOperationException("本局已初始化。");
        }
        int duration = BattleConst.DurationSeconds;
        if (duration <= 0 || string.IsNullOrWhiteSpace(BattleConst.FriendlyName) || string.IsNullOrWhiteSpace(BattleConst.EnemyName)) {
            throw new InvalidOperationException("对局时长或首版名称常量无效。");
        }
        remainingSeconds = duration;
        sceneTimeUpdated += OnBattleTimeUpdated;
        int startCount = 0;
        foreach (var start in tables.BattleStartObj.DataList) {
            if (start.SceneId == config.Id) {
                startCount++;
                if (start.Camp != BattleSceneCamp.FRIENDLY && start.Camp != BattleSceneCamp.ENEMY) {
                    throw new InvalidOperationException("开局不支持中立阵营。");
                }
            }
        }
        if (startCount != 2) {
            throw new InvalidOperationException("开局须且仅须配置双方阵营。");
        }
        AddCamp(tables, BattleSceneCamp.FRIENDLY, config.FriendlyBaseQ, config.FriendlyBaseK);
        AddCamp(tables, BattleSceneCamp.ENEMY, config.EnemyBaseQ, config.EnemyBaseK);
        navigation = new BattleNavigation(this);
        unlocks = new BattleUnlockModel(this, tables, randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random());
    }

    private void OnBattleTimeUpdated(float deltaTime) {
        if (expired || deltaTime == 0) {
            return;
        }
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
        int before = displayRemainingSeconds;
        remainingSeconds = Math.Max(0, remainingSeconds - deltaTime);
        if (remainingSeconds == 0) {
            expired = true;
        }
        if (before != displayRemainingSeconds) {
            InformationChanged?.Invoke();
        }
        if (expired) {
            TimeExpired?.Invoke(this);
        }
    }

    /**归属统计包含未解锁普通格；不包含中立障碍。*/
    public int GetTerritoryCount(BattleSceneCamp camp) {
        int count = 0;
        foreach (var cell in byId.Values) {
            if (cell.groundPassable && cell.camp == camp && camp != BattleSceneCamp.ABSOLUTE_NEUTRAL) {
                count++;
            }
        }
        return count;
    }

    /**只统计登记且未销毁的作战单位，建筑不占人口。*/
    public int GetUnitCount(BattleSceneCamp camp) {
        int count = 0;
        foreach (var element in elements.Values) {
            if (element is BattleUnitVO unit && !unit.destroyed && unit.camp == camp) {
                count++;
            }
        }
        return count;
    }

    private void AddCamp(Tables tables, BattleSceneCamp camp, int q, int k) {
        var owner = BattleRoster.CreateCamp(tables, config.Id, camp);
        var baseConfig = tables.BuildingObj.Get(owner.config.BaseBuildingId);
        var cell = GetCell(q, k);
        var building = new BattleBuildingVO(++nextId, baseConfig, cell, owner, tables.CurrencyProductionObj.Get(baseConfig.Id));
        campMap.Add(camp, owner);
        owner.CurrencyChanged += OnCurrencyChanged;
        buildingMap.Add(building.id, building);
        AddElement(building);
        cell.MarkUnlocked();
    }

    /**先构造并校验候选建筑，尚不登记或扣费。*/
    internal BattleBuildingVO PrepareCardBuilding(BattleCellVO cell, CardResource card) {
        var tables = CfgManager.tables;
        UnitProductionResource units = null;
        int buildingId;
        if (card.Content is BuildingCardContent collector) {
            buildingId = collector.BuildingId;
        } else if (card.Content is UnitCardContent unit) {
            units = tables.UnitProductionObj.Get(unit.UnitId);
            buildingId = units.BuildingId;
        } else {
            throw new InvalidOperationException("不支持的解锁卡牌：" + card.Id);
        }
        return new BattleBuildingVO(nextId + 1, tables.BuildingObj.Get(buildingId), cell, campMap[cell.camp], tables.CurrencyProductionObj.GetOrDefault(buildingId), units, TryProduceUnit);
    }

    /**只在资格、余额及全部结果引用已校验后提交，稳定ID不会与现有元素冲突。*/
    internal void CommitUnlock(BattleCellVO cell, int? cardId, BattleBuildingVO building) {
        if (building != null) {
            AddElement(building);
            buildingMap.Add(building.id, building);
            nextId = building.id;
        }
        cell.SetUnlockResult(cardId);
    }

    internal void NotifyStructureChanged() {
        StructureChanged?.Invoke();
        InformationChanged?.Invoke();
    }

    /**统一检查名额与路径，再登记唯一单位；同帧多建筑不能越过阵营上限。*/
    private bool TryProduceUnit(BattleBuildingVO building) {
        if (expired || building.destroyed || building.unitProduction == null || !buildingMap.TryGetValue(building.id, out var registered) || registered != building || GetUnitCount(building.owner.camp) >= building.owner.unitLimit) {
            return false;
        }
        var target = building.owner.camp == BattleSceneCamp.FRIENDLY ? GetCell(config.EnemyBaseQ, config.EnemyBaseK) : GetCell(config.FriendlyBaseQ, config.FriendlyBaseK);
        var path = navigation.FindGroundPath(building.cell, target);
        if (path == null || path.Count == 0) {
            return false;
        }
        var unitConfig = CfgManager.tables.UnitObj.Get(building.unitProduction.UnitId);
        if (unitConfig.MovementType != UnitMovementType.GROUND) {
            throw new NotSupportedException("本阶段尚未接入飞行路径。");
        }
        Vector2 birth = path[0];
        path.RemoveAt(0);
        var unit = new BattleUnitVO(++nextId, unitConfig, building.owner.camp, birth, path);
        AddElement(unit);
        InformationChanged?.Invoke();
        return true;
    }

    /**死亡移除释放名额后，立即尝试已满进度的等待建筑；不结束未完成周期。*/
    internal bool RemoveCombatUnit(long id) {
        if (!elements.TryGetValue(id, out var element) || !(element is BattleUnitVO unit)) {
            return false;
        }
        RemoveElement(id);
        foreach (var producer in buildingMap.Values) {
            if (producer.owner.camp == unit.camp) {
                producer.TryResumeProduction();
            }
        }
        InformationChanged?.Invoke();
        return true;
    }

    /**供解锁、出生和寻路共用六邻格拓扑。*/
    public IEnumerable<BattleCellVO> GetNeighbors(BattleCellVO cell) {
        for (int dq = -1; dq <= 1; dq++) {
            int dk = dq == 0 ? 2 : 1;
            var above = GetCell(cell.q + dq, cell.k - dk);
            var below = GetCell(cell.q + dq, cell.k + dk);
            if (above != null) {
                yield return above;
            }
            if (below != null) {
                yield return below;
            }
        }
    }

    private void OnCurrencyChanged(BattleCampVO camp) {
        CurrencyChanged?.Invoke(camp);
    }

    private void ValidateBase(int q, int k, BattleSceneCamp camp) {
        if (!byCoordinate.TryGetValue((q, k), out var cell) || cell.camp != camp || !cell.groundPassable) {
            throw new InvalidOperationException("基地坐标或阵营不合法。");
        }
    }

    public BattleCellVO GetCell(int q, int k) {
        byCoordinate.TryGetValue((q, k), out var cell);
        return cell;
    }

    public BattleCellVO GetCell(long id) {
        byId.TryGetValue(id, out var cell);
        return cell;
    }

    /**坐标查询不依赖格子图片是否存在或当前是否可见。*/
    public BattleCellVO FindCell(Vector2 scenePoint) {
        int q = Mathf.RoundToInt((scenePoint.x - config.OriginX) / BattleConst.ColumnStep);
        int k = Mathf.RoundToInt((scenePoint.y - config.OriginY) / BattleConst.RowStep);
        for (int x = q - 1; x <= q + 1; x++) {
            for (int y = k - 2; y <= k + 2; y++) {
                var cell = GetCell(x, y);
                if (cell != null && BattleConst.Contains(scenePoint - cell.position)) {
                    return cell;
                }
            }
        }
        return null;
    }

    public void Dispose() {
        StopTimeFlow();
        sceneTimeUpdated -= OnBattleTimeUpdated;
        InformationChanged = null;
        StructureChanged = null;
        unlocks = null;
        navigation = null;
        TimeExpired = null;
        foreach (var owner in campMap.Values) {
            owner.CurrencyChanged -= OnCurrencyChanged;
        }
        CurrencyChanged = null;
        Clear();
        buildingMap.Clear();
        campMap.Clear();
        byCoordinate.Clear();
        byId.Clear();
    }
}
