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
    public int defaultAircraftLevel { get; private set; } = 3;

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
        EnemyWaveVO bossWave = CreateWave(config.BossWaveId);
        if (bossWave.enemyClass != EnemyClass.BOSS || bossWave.count != 1) {
            throw new InvalidOperationException($"关卡 {config.Id} 的 Boss 波次 {config.BossWaveId} 必须只配置一架 Boss 敌机");
        }
        return new StageConfigVO(config.Id, new Vector2(config.SelectPosition.X, config.SelectPosition.Y), CreateWaves(config.WaveIds), bossWave, config.SceneId, config.TwoStarScore, config.ThreeStarScore);
    }

    /**获取并转换普通敌机配置*/
    public EnemyConfigVO GetEnemyConfig(int enemyId) {
        EnemyResource enemy = CfgManager.tables.EnemyObj.GetOrDefault(enemyId);
        if (enemy == null) {
            return null;
        }
        List<PlayerBulletLauncherVO> launchers = CreateBulletLaunchers(enemy.Aircraft.BulletLaunchers, $"敌机 {enemyId}");
        return new EnemyConfigVO(enemy.Id, enemy.EnemyClass, enemy.Aircraft.Health, BattleConst.GetRaidenUnpackImagePath(enemy.Aircraft.AppearanceName), new Vector2(enemy.DisplaySize.X, enemy.DisplaySize.Y), AircraftCollisionVO.Create(enemy.Aircraft.CollisionShapes), enemy.Aircraft.MoveSpeed, enemy.Score, enemy.PoolCapacity, launchers, enemy.Aircraft.DeathExplosions, enemy.Aircraft.RemoveAfterDeathPresentation);
    }

    /**获取并转换敌机子弹配置*/
    public BulletConfigVO GetBulletConfig(int bulletType, int bulletLevel, int additionalLevel = 0) {
        int requestedLevel = Mathf.Max(1, bulletLevel + additionalLevel);
        BulletResource bullet = null;
        foreach (BulletResource candidate in CfgManager.tables.BulletObj.DataList) {
            if (candidate.Type != bulletType || candidate.Level > requestedLevel) {
                continue;
            }
            if (bullet == null || candidate.Level > bullet.Level) {
                bullet = candidate;
            }
        }
        if (bullet == null) {
            return null;
        }
        int collisionRadius = GetBulletCollisionRadius(bullet);
        Vector2 collisionSize = Vector2.one * collisionRadius * 2f;
        string appearancePath = bullet.EffectId > 0 ? null : BattleConst.GetRaidenUnpackImagePath(bullet.AppearancePath);
        return new BulletConfigVO(bullet.Id, bullet.Type, bullet.Level, appearancePath, bullet.EffectId, collisionSize, collisionRadius, bullet.Speed, bullet.Damage, bullet.HitEffectId, bullet.LaunchEffectId, bullet.MotionType, bullet.RotationSpeed, bullet.TrackingDelayMs, bullet.TrackingTurnSpeed);
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
                List<PlayerBulletLauncherVO> launchers = CreateBulletLaunchers(candidate.Aircraft.BulletLaunchers, $"玩家飞机等级 {candidate.Id}");
                return new PlayerAircraftBattleLevelVO(candidate.AircraftId, candidate.Level, BattleConst.GetRaidenUnpackImagePath(candidate.Aircraft.AppearanceName), new Vector2(candidate.DisplaySize.X, candidate.DisplaySize.Y), AircraftCollisionVO.Create(candidate.Aircraft.CollisionShapes), candidate.Aircraft.Health, candidate.BaseBulletCount, launchers, candidate.Aircraft.DeathExplosions, candidate.Aircraft.RemoveAfterDeathPresentation);
            }
        }
        throw new InvalidOperationException($"玩家飞机 {aircraftId} 缺少等级 {level} 配置");
    }

    private static List<PlayerBulletLauncherVO> CreateBulletLaunchers(IReadOnlyList<BulletLauncher> configs, string ownerName) {
        List<PlayerBulletLauncherVO> result = new List<PlayerBulletLauncherVO>();
        foreach (BulletLauncher launcher in configs) {
            BulletResource bullet = null;
            foreach (BulletResource candidate in CfgManager.tables.BulletObj.DataList) {
                if (candidate.Type == launcher.BulletType && candidate.Level <= launcher.BulletLevel && (bullet == null || candidate.Level > bullet.Level)) {
                    bullet = candidate;
                }
            }
            if (bullet == null) {
                throw new InvalidOperationException($"{ownerName} 引用了不存在的子弹：type={launcher.BulletType}, level={launcher.BulletLevel}");
            }
            result.Add(new PlayerBulletLauncherVO(new Vector2(launcher.Offset.X, launcher.Offset.Y), launcher.BulletType, launcher.BulletLevel, Mathf.Max(1, launcher.BulletCount), Mathf.Max(0.001f, launcher.FireIntervalMs / 1000f), Mathf.Max(0, launcher.BulletIntervalMs), launcher.Direction, launcher.SpreadType, Mathf.Max(0f, launcher.SpreadAngle)));
        }
        return result;
    }

    private static int GetBulletCollisionRadius(BulletResource bullet) {
        if (bullet.CollisionRadius <= 0) {
            throw new InvalidOperationException($"子弹 {bullet.Id} 缺少有效碰撞半径");
        }
        return bullet.CollisionRadius;
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
        defaultAircraftLevel = 3;
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
        return new PlayerAircraftVO(aircraft.Id, aircraft.Code, aircraft.DisplayName, aircraft.MaxLevel, previewLevel, aircraft.DefaultUnlocked, aircraft.UnlockStarCost, levelConfig.BasePower, BattleConst.GetRaidenUnpackImagePath(levelConfig.Aircraft.AppearanceName), new Vector2(levelConfig.DisplaySize.X, levelConfig.DisplaySize.Y));
    }

    /**将关卡引用的 Luban 波次配置转换为运行时业务数据*/
    private EnemyWaveVO[] CreateWaves(IReadOnlyList<int> waveIds) {
        EnemyWaveVO[] waves = new EnemyWaveVO[waveIds.Count];
        for (int index = 0; index < waveIds.Count; index++) {
            EnemyWaveVO wave = CreateWave(waveIds[index]);
            if (wave.enemyClass == EnemyClass.BOSS) {
                throw new InvalidOperationException($"普通波次列表不允许引用 Boss 波次：{waveIds[index]}");
            }
            waves[index] = wave;
        }
        return waves;
    }

    /**将单条 Luban 波次配置转换为运行时业务数据。*/
    private EnemyWaveVO CreateWave(int waveId) {
        StageWaveResource wave = CfgManager.tables.StageWaveObj.GetOrDefault(waveId);
        if (wave == null) {
            throw new InvalidOperationException($"关卡引用了不存在的波次配置：{waveId}");
        }
        EnemyConfigVO enemy = GetEnemyConfig(wave.EnemyId);
        if (enemy == null) {
            throw new InvalidOperationException($"波次 {wave.Id} 引用了不存在的敌机配置：{wave.EnemyId}");
        }
        return new EnemyWaveVO(enemy, wave.MotionType, wave.FormationType, wave.EnemyCount, new Vector2(wave.SpawnCenter.X, wave.SpawnCenter.Y), wave.Spacing, wave.MotionDirection);
    }

}
