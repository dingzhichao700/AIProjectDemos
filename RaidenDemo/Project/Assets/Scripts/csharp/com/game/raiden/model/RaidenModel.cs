using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷电模块数据管理
/// </summary>
public sealed class RaidenModel {

    /**当前运行周期内的关卡进度表*/
    private readonly Dictionary<int, StageProgressVO> stageProgressMap = new Dictionary<int, StageProgressVO>();

    /**当前运行周期内已经解锁的玩家飞机*/
    private readonly HashSet<int> unlockedAircraftIds = new HashSet<int>();

    /**当前运行周期内已经消费的星数*/
    private int spentStarCount;

    /**当前出战玩家飞机类型*/
    public int selectedAircraftId { get; private set; }

    /**关卡外飞机默认等级，后续由永久强化系统提升*/
    public int defaultAircraftLevel { get; private set; } = 1;

    /**关卡配置总数*/
    public int stageCount => CfgManager.tables.StageObj.DataList.Count;

    /**当前累计获得的关卡星数*/
    public int totalStarCount {
        get {
            int count = 0;
            foreach (StageProgressVO progress in stageProgressMap.Values) {
                count += progress.highestStar;
            }
            return count;
        }
    }

    /**扣除本次运行期消费后的可用星数*/
    public int availableStarCount => Mathf.Max(0, totalStarCount - spentStarCount);

    /**初始化雷电模块数据*/
    public RaidenModel() {
        Reset();
    }

    /**获取指定关卡在当前运行周期内的进度*/
    public StageProgressVO GetStageProgress(int stageId) {
        return stageProgressMap.TryGetValue(stageId, out StageProgressVO progress) ? progress : null;
    }

    /**获取并转换指定关卡配置*/
    public StageConfigVO GetStageConfig(int stageId) {
        StageResource config = CfgManager.tables.StageObj.GetOrDefault(stageId);
        if (config == null) {
            return null;
        }
        return new StageConfigVO(config.Id, new Vector2(config.SelectPosition.X, config.SelectPosition.Y), CreateWaves(config.WaveIds), config.TwoStarScore, config.ThreeStarScore);
    }

    /**获取并转换普通敌机配置*/
    public EnemyConfigVO GetEnemyConfig(int enemyId) {
        EnemyResource enemy = CfgManager.tables.EnemyObj.GetOrDefault(enemyId);
        if (enemy == null) {
            return null;
        }
        EnemyBulletConfigVO bullet = GetEnemyBulletConfig(enemy.BulletId);
        if (bullet == null) {
            throw new InvalidOperationException($"敌机 {enemyId} 引用了不存在的子弹配置：{enemy.BulletId}");
        }
        return new EnemyConfigVO(enemy.Id, enemy.EnemyClass, enemy.BaseHealth, BattleConst.GetRaidenUnpackImagePath(enemy.AppearanceName), new Vector2(enemy.DisplaySize.X, enemy.DisplaySize.Y), AircraftCollisionVO.Create(enemy.Aircraft.CollisionShapes), enemy.MoveSpeed, enemy.FireInterval, enemy.FireType, enemy.Score, enemy.PoolCapacity, bullet);
    }

    /**获取并转换敌机子弹配置*/
    public EnemyBulletConfigVO GetEnemyBulletConfig(int bulletId) {
        EnemyBulletResource bullet = CfgManager.tables.EnemyBulletObj.GetOrDefault(bulletId);
        if (bullet == null) {
            return null;
        }
        BulletResource commonBullet = CfgManager.tables.BulletObj.GetOrDefault(bulletId);
        string appearanceName = commonBullet != null
            ? commonBullet.Bullet.AppearancePath
            : bullet.AppearanceName;
        return new EnemyBulletConfigVO(bullet.Id,
            BattleConst.GetRaidenUnpackImagePath(appearanceName),
            new Vector2(bullet.DisplaySize.X, bullet.DisplaySize.Y),
            new Vector2(bullet.HitSize.X, bullet.HitSize.Y), bullet.Speed, bullet.Damage,
            commonBullet?.Bullet.HitEffectId ?? 0,
            bullet.PoolCapacity);
    }

    /**获取全部玩家飞机类型*/
    public List<PlayerAircraftVO> GetAllPlayerAircraft() {
        List<PlayerAircraftVO> aircraftList = new List<PlayerAircraftVO>();
        foreach (PlayerAircraftResource aircraft in CfgManager.tables.PlayerAircraftObj.DataList) {
            aircraftList.Add(CreatePlayerAircraft(aircraft));
        }
        return aircraftList;
    }

    /**获取指定玩家飞机类型*/
    public PlayerAircraftVO GetPlayerAircraft(int aircraftId) {
        PlayerAircraftResource aircraft = CfgManager.tables.PlayerAircraftObj.GetOrDefault(aircraftId);
        if (aircraft == null) {
            return null;
        }
        return CreatePlayerAircraft(aircraft);
    }

    /**获取指定玩家飞机在指定等级的关卡内战斗配置*/
    public PlayerAircraftBattleLevelVO GetPlayerAircraftBattleLevel(int aircraftId, int level) {
        PlayerAircraftResource aircraft = CfgManager.tables.PlayerAircraftObj.GetOrDefault(aircraftId);
        if (aircraft == null || level < 1 || level > aircraft.MaxLevel) {
            return null;
        }
        foreach (PlayerAircraftLevelResource candidate in CfgManager.tables.PlayerAircraftLevelObj.DataList) {
            if (candidate.AircraftId == aircraftId && candidate.Level == level) {
                List<PlayerBulletLauncherVO> launchers = CreatePlayerBulletLaunchers(candidate);
                return new PlayerAircraftBattleLevelVO(candidate.AircraftId, candidate.Level,
                    BattleConst.GetRaidenUnpackImagePath(candidate.AppearanceName),
                    new Vector2(candidate.DisplaySize.X, candidate.DisplaySize.Y),
                    AircraftCollisionVO.Create(candidate.Aircraft.CollisionShapes),
                    candidate.BaseHealth, candidate.BaseBulletCount, launchers);
            }
        }
        throw new InvalidOperationException($"玩家飞机 {aircraftId} 缺少等级 {level} 配置");
    }

    private static List<PlayerBulletLauncherVO> CreatePlayerBulletLaunchers(PlayerAircraftLevelResource level) {
        List<PlayerBulletLauncherVO> result = new List<PlayerBulletLauncherVO>();
        foreach (BulletLauncher launcher in level.Aircraft.BulletLaunchers) {
            BulletResource bullet = CfgManager.tables.BulletObj.GetOrDefault(launcher.BulletId);
            if (bullet == null) {
                throw new InvalidOperationException($"玩家飞机等级 {level.Id} 引用了不存在的子弹配置：{launcher.BulletId}");
            }
            Vector2 size = GetBulletShapeSize(bullet.Bullet.Shape);
            Vector2 pivot = GetBulletShapePivot(bullet.Bullet.Shape);
            result.Add(new PlayerBulletLauncherVO(
                new Vector2(launcher.Offset.X, launcher.Offset.Y),
                Mathf.Max(1, launcher.BulletCount),
                Mathf.Max(0.001f, launcher.FireIntervalMs / 1000f),
                Mathf.Max(0, launcher.BulletIntervalMs),
                launcher.Direction, launcher.SpreadType, Mathf.Max(0f, launcher.SpreadAngle),
                BattleConst.GetRaidenUnpackImagePath(bullet.Bullet.AppearancePath),
                size, size, pivot, bullet.Bullet.Speed, bullet.Bullet.Damage,
                bullet.Bullet.HitEffectId,
                bullet.Bullet.MotionType, bullet.Bullet.Rotate,
                bullet.Bullet.RotationSpeed, Mathf.Max(0, bullet.Bullet.TrackingDelayMs),
                Mathf.Max(0f, bullet.Bullet.TrackingTurnSpeed)));
        }
        return result;
    }

    private static Vector2 GetBulletShapeSize(Shape shape) {
        if (shape is RectangleShape rectangle) {
            return new Vector2(rectangle.Rect.X, rectangle.Rect.Y);
        }
        if (shape is CircleShape circle) {
            float diameter = circle.Radius * 2f;
            return new Vector2(diameter, diameter);
        }
        throw new InvalidOperationException("子弹配置缺少有效形状");
    }

    private static Vector2 GetBulletShapePivot(Shape shape) {
        if (shape is RectangleShape rectangle) {
            return new Vector2(rectangle.Pivot.X, rectangle.Pivot.Y);
        }
        if (shape is CircleShape) {
            return new Vector2(0.5f, 0.5f);
        }
        throw new InvalidOperationException("子弹配置缺少有效形状");
    }

    /**判断玩家飞机是否已经解锁*/
    public bool IsPlayerAircraftUnlocked(int aircraftId) {
        return unlockedAircraftIds.Contains(aircraftId);
    }

    /**消耗当前运行期星数解锁玩家飞机*/
    public bool TryUnlockPlayerAircraft(int aircraftId) {
        PlayerAircraftResource aircraft = CfgManager.tables.PlayerAircraftObj.GetOrDefault(aircraftId);
        if (aircraft == null || unlockedAircraftIds.Contains(aircraftId) || availableStarCount < aircraft.UnlockStarCost) {
            return false;
        }
        spentStarCount += aircraft.UnlockStarCost;
        unlockedAircraftIds.Add(aircraftId);
        return true;
    }

    /**设置当前出战玩家飞机*/
    public bool TrySelectPlayerAircraft(int aircraftId) {
        if (!unlockedAircraftIds.Contains(aircraftId)) {
            return false;
        }
        selectedAircraftId = aircraftId;
        return true;
    }

    /**获取全部普通敌机配置，用于战斗资源预加载和对象池预热*/
    public List<EnemyConfigVO> GetAllEnemyConfigs() {
        List<EnemyConfigVO> configs = new List<EnemyConfigVO>();
        foreach (EnemyResource enemy in CfgManager.tables.EnemyObj.DataList) {
            configs.Add(GetEnemyConfig(enemy.Id));
        }
        return configs;
    }

    /**清空本次运行数据，并恢复为仅第一关解锁的初始状态*/
    public void Reset() {
        stageProgressMap.Clear();
        for (int stageId = 1; stageId <= stageCount; stageId++) {
            stageProgressMap.Add(stageId, new StageProgressVO {
                stageId = stageId,
                unlocked = stageId == 1
            });
        }
        spentStarCount = 0;
        defaultAircraftLevel = 1;
        unlockedAircraftIds.Clear();
        selectedAircraftId = 0;
        foreach (PlayerAircraftResource aircraft in CfgManager.tables.PlayerAircraftObj.DataList) {
            if (aircraft.DefaultUnlocked) {
                unlockedAircraftIds.Add(aircraft.Id);
                if (selectedAircraftId == 0) {
                    selectedAircraftId = aircraft.Id;
                }
            }
        }
        if (selectedAircraftId == 0) {
            throw new InvalidOperationException("玩家飞机配置中至少需要一种默认解锁机型");
        }
    }

    /**将玩家飞机配置及其一级外观转换为运行时展示数据*/
    private PlayerAircraftVO CreatePlayerAircraft(PlayerAircraftResource aircraft) {
        PlayerAircraftLevelResource levelConfig = null;
        int previewLevel = Mathf.Clamp(defaultAircraftLevel, 1, aircraft.MaxLevel);
        foreach (PlayerAircraftLevelResource candidate in CfgManager.tables.PlayerAircraftLevelObj.DataList) {
            if (candidate.AircraftId == aircraft.Id && candidate.Level == previewLevel) {
                levelConfig = candidate;
                break;
            }
        }
        if (levelConfig == null) {
            throw new InvalidOperationException($"玩家飞机 {aircraft.Id} 缺少等级 {previewLevel} 配置");
        }
        return new PlayerAircraftVO(aircraft.Id, aircraft.Code, aircraft.DisplayName, aircraft.MaxLevel, previewLevel, aircraft.DefaultUnlocked, aircraft.UnlockStarCost, levelConfig.BasePower, BattleConst.GetRaidenUnpackImagePath(levelConfig.AppearanceName), new Vector2(levelConfig.DisplaySize.X, levelConfig.DisplaySize.Y));
    }

    /**将关卡引用的 Luban 波次配置转换为运行时业务数据*/
    private EnemyWaveVO[] CreateWaves(IReadOnlyList<int> waveIds) {
        EnemyWaveVO[] waves = new EnemyWaveVO[waveIds.Count];
        for (int index = 0; index < waveIds.Count; index++) {
            StageWaveResource wave = CfgManager.tables.StageWaveObj.GetOrDefault(waveIds[index]);
            if (wave == null) {
                throw new InvalidOperationException($"关卡引用了不存在的波次配置：{waveIds[index]}");
            }
            EnemyConfigVO enemy = GetEnemyConfig(wave.EnemyId);
            if (enemy == null) {
                throw new InvalidOperationException($"波次 {wave.Id} 引用了不存在的敌机配置：{wave.EnemyId}");
            }
            waves[index] = new EnemyWaveVO(enemy, wave.MotionType, wave.FormationType, wave.EnemyCount, new Vector2(wave.SpawnCenter.X, wave.SpawnCenter.Y), wave.Spacing, wave.MotionDirection);
        }
        return waves;
    }

}
