using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// 收集进入关卡前需要预加载的完整战斗资源依赖。
/// </summary>
public static class BattlePreloadCollector {

    /**返回当前关卡进入战斗前需要预加载的全部动态资源*/
    public static List<ResLoadInfo> GetStagePreloadList(int stageId) {
        PlayerAircraftVO selectedAircraft = RaidenControl.ins.GetSelectedPlayerAircraft();
        if (selectedAircraft == null) {
            throw new InvalidOperationException("当前没有可用于关卡预加载的出战玩家飞机");
        }
        int initialLevel = Mathf.Clamp(RaidenControl.ins.defaultAircraftLevel, 1,
            selectedAircraft.maxLevel);
        List<ResLoadInfo> preload = new List<ResLoadInfo>();
        HashSet<string> resourceKeys = new HashSet<string>();
        StageResource stage = CfgManager.tables.StageObj.GetOrDefault(stageId);
        if (stage == null) {
            throw new InvalidOperationException($"关卡 {stageId} 不存在，无法收集预加载资源");
        }
        AddFixedStageResources(preload, resourceKeys);
        AddSceneBackgroundResources(preload, resourceKeys, stage.SceneId);

        foreach (PlayerAircraftLevelResource levelConfig in
            CfgManager.tables.PlayerAircraftLevelObj.DataList) {
            if (levelConfig.AircraftId != selectedAircraft.id ||
                levelConfig.Level < initialLevel || levelConfig.Level > selectedAircraft.maxLevel) {
                continue;
            }
            AddAircraftResources(preload, resourceKeys, levelConfig.Aircraft);
        }

        HashSet<int> enemyIds = new HashSet<int>();
        foreach (int waveId in stage.WaveIds) {
            StageWaveResource wave = CfgManager.tables.StageWaveObj.GetOrDefault(waveId);
            if (wave == null) {
                throw new InvalidOperationException($"关卡 {stageId} 引用了不存在的波次 {waveId}");
            }
            enemyIds.Add(wave.EnemyId);
        }
        StageWaveResource bossWave = CfgManager.tables.StageWaveObj.GetOrDefault(stage.BossWaveId);
        if (bossWave == null) {
            throw new InvalidOperationException($"关卡 {stageId} 引用了不存在的 Boss 波次 {stage.BossWaveId}");
        }
        EnemyResource bossEnemy = CfgManager.tables.EnemyObj.GetOrDefault(bossWave.EnemyId);
        if (bossEnemy == null || bossEnemy.EnemyClass != EnemyClass.BOSS || bossWave.EnemyCount != 1) {
            throw new InvalidOperationException($"关卡 {stageId} 的 Boss 波次 {stage.BossWaveId} 配置无效");
        }
        enemyIds.Add(bossWave.EnemyId);
        foreach (int enemyId in enemyIds) {
            EnemyResource enemy = CfgManager.tables.EnemyObj.GetOrDefault(enemyId);
            if (enemy == null) {
                throw new InvalidOperationException($"关卡 {stageId} 引用了不存在的敌机 {enemyId}");
            }
            AddAircraftResources(preload, resourceKeys, enemy.Aircraft);
        }
        return preload;
    }

    /**收集关卡流程必然可能使用、但不由实体配置直接引用的固定资源。*/
    private static void AddFixedStageResources(List<ResLoadInfo> preload, HashSet<string> resourceKeys) {
        AddPreloadResource(preload, resourceKeys, BattleConst.WingmanPath,
            ResType.UnpackImage);
        foreach (StageItemResource item in CfgManager.tables.StageItemObj.DataList) {
            AddPreloadResource(preload, resourceKeys, BattleConst.GetRaidenUnpackImagePath(item.Res), ResType.UnpackImage);
            AddEffectResource(preload, resourceKeys, item.EffectId, EffectType.OTHER);
        }
        AddPreloadResource(preload, resourceKeys, BattleConst.EliteHealthBarBackgroundPath, ResType.UnpackImage);
        AddPreloadResource(preload, resourceKeys, BattleConst.EliteHealthBarFillPath, ResType.UnpackImage);
        foreach (int effectId in BattleConst.FixedStageEffectIds) {
            AddEffectResource(preload, resourceKeys, effectId);
        }
    }

    /**收集关卡配置引用的全部视差背景资源。*/
    private static void AddSceneBackgroundResources(List<ResLoadInfo> preload, HashSet<string> resourceKeys, int sceneId) {
        SceneBgResource scene = CfgManager.tables.SceneBgObj.GetOrDefault(sceneId);
        if (scene == null) {
            throw new InvalidOperationException($"场景背景 {sceneId} 不存在，无法收集预加载资源");
        }
        if (string.IsNullOrWhiteSpace(scene.BackgroundRes)) {
            throw new InvalidOperationException($"场景背景 {sceneId} 未配置远景地表资源");
        }
        AddSceneBackgroundResource(preload, resourceKeys, scene.BackgroundRes);
        AddSceneBackgroundResource(preload, resourceKeys, scene.LowRes);
        AddSceneBackgroundResource(preload, resourceKeys, scene.MiddleRes);
        AddSceneBackgroundResource(preload, resourceKeys, scene.HighRes);
    }

    /**按固定目录拼装并收集单层场景背景。*/
    private static void AddSceneBackgroundResource(List<ResLoadInfo> preload, HashSet<string> resourceKeys, string resourceName) {
        if (!string.IsNullOrWhiteSpace(resourceName)) {
            AddPreloadResource(preload, resourceKeys, BattleConst.GetSceneBackgroundImagePath(resourceName), ResType.UnpackImage);
        }
    }
    
    /**收集飞行物外观、子弹和死亡特效资源*/
    private static void AddAircraftResources(List<ResLoadInfo> preload,
        HashSet<string> resourceKeys, Aircraft aircraft) {
        if (aircraft == null) {
            return;
        }
        AddPreloadResource(preload, resourceKeys,
            BattleConst.GetRaidenUnpackImagePath(aircraft.AppearanceName),
            ResType.UnpackImage);
        foreach (BulletLauncher launcher in aircraft.BulletLaunchers) {
            AddBulletTypeResources(preload, resourceKeys, launcher.BulletType, launcher.BulletLevel);
        }
        foreach (ExplosionEffect explosion in aircraft.DeathExplosions) {
            AddEffectResource(preload, resourceKeys, explosion.EffectId);
        }
    }

    /**收集同类型从基础等级起可能被动态升级使用的全部子弹资源。*/
    private static void AddBulletTypeResources(List<ResLoadInfo> preload, HashSet<string> resourceKeys, int bulletType, int baseLevel) {
        bool found = false;
        foreach (BulletResource candidate in CfgManager.tables.BulletObj.DataList) {
            if (candidate.Type != bulletType || candidate.Level < baseLevel) continue;
            found = true;
            AddBulletResources(preload, resourceKeys, candidate.Type, candidate.Level);
        }
        if (!found) throw new InvalidOperationException($"子弹配置 type={bulletType}, level>={baseLevel} 不存在");
    }
    
    /**收集子弹外观、命中特效及发射特效资源*/
    private static void AddBulletResources(List<ResLoadInfo> preload,
        HashSet<string> resourceKeys, int bulletType, int bulletLevel) {
        BulletResource bulletConfig = ResolveBullet(bulletType, bulletLevel);
        if (bulletConfig == null) {
            throw new InvalidOperationException($"子弹配置 type={bulletType}, level={bulletLevel} 不存在");
        }
        if (bulletConfig.EffectId > 0) {
            AddEffectResource(preload, resourceKeys, bulletConfig.EffectId, EffectType.BULLET);
        } else {
            AddPreloadResource(preload, resourceKeys,
                BattleConst.GetRaidenUnpackImagePath(bulletConfig.AppearancePath),
                ResType.UnpackImage);
        }
        AddEffectResource(preload, resourceKeys, bulletConfig.HitEffectId, EffectType.BULLET_HIT);
        AddEffectResource(preload, resourceKeys, bulletConfig.LaunchEffectId, EffectType.BULLET_LAUNCH);
    }

    /**按类型与等级读取同类型中不高于请求等级的最高配置。*/
    private static BulletResource ResolveBullet(int bulletType, int bulletLevel) {
        BulletResource result = null;
        foreach (BulletResource candidate in CfgManager.tables.BulletObj.DataList) {
            if (candidate.Type == bulletType && candidate.Level <= bulletLevel && (result == null || candidate.Level > result.Level)) {
                result = candidate;
            }
        }
        return result;
    }
    
    /**按特效配置收集帧动画资源*/
    private static void AddEffectResource(List<ResLoadInfo> preload, HashSet<string> resourceKeys, int effectId, EffectType? expectedType = null) {
        if (effectId <= 0) {
            return;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null) {
            throw new InvalidOperationException($"特效配置 {effectId} 不存在");
        }
        if (expectedType.HasValue && effect.Type != expectedType.Value) {
            throw new InvalidOperationException($"特效配置 {effectId} 类型错误，应为 {expectedType.Value}，实际为 {effect.Type}");
        }
        string path = GetEffectResourcePath(effect);
        AddPreloadResource(preload, resourceKeys, path, ResType.FrameAnim);
    }
    
    /**根据特效分类拼装完整资源路径*/
    public static string GetEffectResourcePath(EffectResource effect) {
        string typeDirectory;
        switch (effect.Type) {
            case EffectType.BULLET_HIT:
                RequireResourcePrefix(effect.Res, BattleConst.BulletHitEffectPrefix, $"子弹命中特效 {effect.Id}");
                typeDirectory = "bullet";
                break;
            case EffectType.BULLET_LAUNCH:
                RequireResourcePrefix(effect.Res, BattleConst.BulletLaunchEffectPrefix, $"子弹发射特效 {effect.Id}");
                typeDirectory = "bullet";
                break;
            case EffectType.BULLET:
                typeDirectory = "bullet";
                break;
            case EffectType.AIRCRAFT_EXPLOSION:
                typeDirectory = "aircraftExplosion";
                break;
            case EffectType.OTHER:
                typeDirectory = "other";
                break;
            default:
                throw new InvalidOperationException($"未支持的特效类型：{effect.Type}");
        }
        return ResourceConst.GetFrameAnimationPath(
            $"default/{typeDirectory}/{effect.Res}");
    }

    /**校验资源名是否遵循所属业务分类的前缀约定*/
    private static void RequireResourcePrefix(string resourceName, string prefix, string resourceLabel) {
        if (string.IsNullOrWhiteSpace(resourceName) || !resourceName.StartsWith(prefix, StringComparison.Ordinal)) {
            throw new InvalidOperationException($"{resourceLabel} 资源名必须使用 {prefix} 前缀：{resourceName}");
        }
    }
    
    /**按资源类型和路径去重后加入预加载列表*/
    private static void AddPreloadResource(List<ResLoadInfo> preload,
        HashSet<string> resourceKeys, string path, ResType resType) {
        string key = $"{(int)resType}:{path}";
        if (resourceKeys.Add(key)) {
            preload.Add(new ResLoadInfo(path, resType));
        }
    }
    
    /**不打开加载界面，仅执行当前关卡资源预加载*/
    public static Task PreloadStageAssetsAsync(int stageId) {
        return ResourceLoader.LoadListAsync(GetStagePreloadList(stageId));
    }

    /**确认战斗散图已在进入关卡前加载完成。*/
    public static void RequireUnpackImagePreloaded(string path) {
        if (!ResourceManager.HasLoadedUnpackImage(path)) {
            throw new InvalidOperationException($"战斗散图资源尚未预加载：{path}");
        }
    }

    /**确认战斗帧动画已在进入关卡前解析完成。*/
    public static void RequireFrameAnimationPreloaded(string path) {
        if (!FrameAnimationManager.HasLoad(path)) {
            throw new InvalidOperationException($"战斗帧动画资源尚未预加载：{path}");
        }
    }
    
    
}
