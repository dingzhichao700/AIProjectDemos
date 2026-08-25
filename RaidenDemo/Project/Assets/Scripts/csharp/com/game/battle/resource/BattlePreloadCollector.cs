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
        AddPreloadResource(preload, resourceKeys, BattleConst.BackgroundPath,
            ResType.UnpackImage);
        AddPreloadResource(preload, resourceKeys, BattleConst.WingmanPath,
            ResType.UnpackImage);
        AddPreloadResource(preload, resourceKeys, BattleConst.HealthDropPath,
            ResType.UnpackImage);
        AddPreloadResource(preload, resourceKeys, BattleConst.UpgradeDropPath,
            ResType.UnpackImage);
        AddPreloadResource(preload, resourceKeys, BattleConst.WingmanUpgradeDropPath,
            ResType.UnpackImage);
        AddPreloadResource(preload, resourceKeys, BattleConst.LifeDropPath,
            ResType.UnpackImage);
    
        foreach (PlayerAircraftLevelResource levelConfig in
            CfgManager.tables.PlayerAircraftLevelObj.DataList) {
            if (levelConfig.AircraftId != selectedAircraft.id ||
                levelConfig.Level < initialLevel || levelConfig.Level > selectedAircraft.maxLevel) {
                continue;
            }
            AddAircraftResources(preload, resourceKeys, levelConfig.Aircraft);
        }
    
        StageResource stage = CfgManager.tables.StageObj.GetOrDefault(stageId);
        if (stage == null) {
            throw new InvalidOperationException($"关卡 {stageId} 不存在，无法收集预加载资源");
        }
        HashSet<int> enemyIds = new HashSet<int> { 4, 5 };
        foreach (int waveId in stage.WaveIds) {
            StageWaveResource wave = CfgManager.tables.StageWaveObj.GetOrDefault(waveId);
            if (wave == null) {
                throw new InvalidOperationException($"关卡 {stageId} 引用了不存在的波次 {waveId}");
            }
            enemyIds.Add(wave.EnemyId);
        }
        foreach (int enemyId in enemyIds) {
            EnemyResource enemy = CfgManager.tables.EnemyObj.GetOrDefault(enemyId);
            if (enemy == null) {
                throw new InvalidOperationException($"关卡 {stageId} 引用了不存在的敌机 {enemyId}");
            }
            AddAircraftResources(preload, resourceKeys, enemy.Aircraft);
            AddBulletResources(preload, resourceKeys, enemy.BulletId);
        }
        return preload;
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
            AddBulletResources(preload, resourceKeys, launcher.BulletId);
        }
        foreach (ExplosionEffect explosion in aircraft.DeathExplosions) {
            AddEffectResource(preload, resourceKeys, explosion.EffectId);
        }
    }
    
    /**收集子弹外观及命中特效资源*/
    private static void AddBulletResources(List<ResLoadInfo> preload,
        HashSet<string> resourceKeys, int bulletId) {
        BulletResource bulletConfig = CfgManager.tables.BulletObj.GetOrDefault(bulletId);
        if (bulletConfig == null) {
            throw new InvalidOperationException($"子弹配置 {bulletId} 不存在");
        }
        Bullet bullet = bulletConfig.Bullet;
        if (bullet.AppearanceType == BulletAppearanceType.FRAME_ANIMATION) {
            AddPreloadResource(preload, resourceKeys,
                ResourceConst.GetFrameAnimationPath("default/raiden/" + bullet.AppearancePath),
                ResType.FrameAnim);
        } else {
            AddPreloadResource(preload, resourceKeys,
                BattleConst.GetRaidenUnpackImagePath(bullet.AppearancePath),
                ResType.UnpackImage);
        }
        AddEffectResource(preload, resourceKeys, bullet.HitEffectId);
    }
    
    /**按特效配置收集帧动画资源*/
    private static void AddEffectResource(List<ResLoadInfo> preload,
        HashSet<string> resourceKeys, int effectId) {
        if (effectId <= 0) {
            return;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null) {
            throw new InvalidOperationException($"特效配置 {effectId} 不存在");
        }
        string path = GetEffectResourcePath(effect);
        AddPreloadResource(preload, resourceKeys, path, ResType.FrameAnim);
    }
    
    /**根据特效分类拼装完整资源路径*/
    public static string GetEffectResourcePath(EffectResource effect) {
        string typeDirectory;
        switch (effect.Type) {
            case EffectType.BULLET_HIT:
                typeDirectory = "bulletHit";
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
    
    
}
