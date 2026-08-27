using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配置表测试用例
/// </summary>
public class TestCaseConfigs {

    public TestCaseConfigs() {
        // 验证 Luban 配置已完成 Addressables 加载，并能按生成接口正确读取。
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_gmobj") != null, "GM JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_settingoptionobj") != null, "设置选项 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_stageobj") != null, "雷电关卡 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_stagewaveobj") != null, "雷电关卡波次 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_enemyobj") != null, "普通敌机 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_playeraircraftobj") != null, "玩家飞机类型 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_playeraircraftlevelobj") != null, "玩家飞机等级 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_bulletobj") != null, "通用子弹 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_effectobj") != null, "通用特效 JSON 未进入 ResourceManager 缓存");
        Require(ResourceManager.GetJsonNode(ResourceConst.PATH_CONFIG + "cfgobj_scenebgobj") != null, "场景背景 JSON 未进入 ResourceManager 缓存");

        Tables tables = CfgManager.tables;
        Require(tables != null, "CfgManager.tables 未成功初始化");

        Require(tables.GmObj.DataList.Count > 0, "GM 表没有有效数据");
        Require(tables.GmObj.DataList.Count == tables.GmObj.DataMap.Count, "GM 表的 DataList 与 DataMap 数量不一致");

        GMResource gm = tables.GmObj.Get(3);
        Require(gm.Content == "addItem", "GM 记录 3 的 Content 解析错误");
        Require(gm.ParamFormat.Count == 2 && gm.ParamFormat[0] == "1001" && gm.ParamFormat[1] == "1", "GM 记录 3 的列表参数解析错误");
        Require(gm.ParamIntro.Count == 2 && gm.ParamIntro[0] == "道具id" && gm.ParamIntro[1] == "道具数量", "GM 记录 3 的参数说明解析错误");
        Require(tables.GmObj.GetOrDefault(int.MinValue) == null, "GM 表不存在的 ID 应返回 null");

        Require(tables.SettingOptionObj.DataList.Count > 0, "设置选项表没有有效数据");
        Require(tables.SettingOptionObj.DataList.Count == tables.SettingOptionObj.DataMap.Count, "设置选项表的 DataList 与 DataMap 数量不一致");

        SettingOptionResource setting = tables.SettingOptionObj.Get(1001);
        Require(setting.Type == SettingType.GRAPHIC, "设置项 1001 的 SettingType 解析错误");
        Require(setting.OptionSelection == SettingOptionSelection.GRAPHIC_RESOLUTION, "设置项 1001 的 SettingOptionSelection 解析错误");
        Require(setting.OptionType == SettingOptionType.SWITCH_OPTIONS, "设置项 1001 的 SettingOptionType 解析错误");
        Require(tables.SettingOptionObj.GetOrDefault(int.MinValue) == null, "设置选项表不存在的 ID 应返回 null");

        Require(tables.StageObj.DataList.Count == 10, "雷电关卡表数量应为 10");
        Require(tables.StageObj.DataList.Count == tables.StageObj.DataMap.Count, "雷电关卡表的 DataList 与 DataMap 数量不一致");
        StageResource stage = tables.StageObj.Get(1);
        Require(stage.SelectPosition.X == 55 && stage.SelectPosition.Y == 925, "雷电关卡 1 的选择坐标解析错误");
        Require(stage.WaveIds.Count > 0, "雷电关卡 1 应配置普通敌机波次");
        Require(stage.BossWaveId == 1101, "雷电关卡 1 的 Boss 波次引用解析错误");
        Require(stage.TwoStarScore == 402 && stage.ThreeStarScore == 1002, "雷电关卡 1 的星级分数线解析错误");
        SceneBgResource sceneBackground = tables.SceneBgObj.GetOrDefault(stage.SceneId);
        Require(sceneBackground != null && !string.IsNullOrWhiteSpace(sceneBackground.BackgroundRes), "雷电关卡 1 的场景背景引用无效");
        Require(tables.StageObj.GetOrDefault(int.MinValue) == null, "雷电关卡表不存在的 ID 应返回 null");

        Require(tables.StageWaveObj.DataList.Count == 52, "雷电关卡波次表数量应为 52");
        Require(tables.StageWaveObj.DataList.Count == tables.StageWaveObj.DataMap.Count, "雷电关卡波次表的 DataList 与 DataMap 数量不一致");
        StageWaveResource firstWave = tables.StageWaveObj.Get(101);
        Require(firstWave.Id == 101 && firstWave.EnemyCount == 5, "关卡 1 首波基础字段解析错误");
        Require(firstWave.EnemyId == 1 && firstWave.MotionType == EnemyMotionType.STRAIGHT && firstWave.FormationType == EnemyFormationType.HORIZONTAL, "关卡 1 首波结构字段解析错误");
        Require(firstWave.SpawnCenter.X == 360 && firstWave.SpawnCenter.Y == 90 && firstWave.Spacing == 165, "关卡 1 首波编队字段解析错误");
        StageWaveResource bossWave = tables.StageWaveObj.Get(1101);
        Require(bossWave.EnemyId == 5 && bossWave.EnemyCount == 1 && bossWave.SpawnCenter.X == 360 && bossWave.SpawnCenter.Y == 120, "Boss 波次配置解析错误");
        Require(tables.StageWaveObj.GetOrDefault(int.MinValue) == null, "雷电关卡波次表不存在的 ID 应返回 null");

        Require(tables.EnemyObj.DataList.Count == 5, "敌机表数量应为 5");
        EnemyResource scout = tables.EnemyObj.Get(1);
        Require(scout.EnemyClass == EnemyClass.NORMAL && scout.Aircraft.Health == 30 && scout.Aircraft.AppearanceName == "aircraft/enemy/battleEnemyScout" && scout.Aircraft.MoveSpeed == 187.5f && scout.BulletId == 1, "侦察机配置解析错误");
        EnemyResource elite = tables.EnemyObj.Get(4);
        Require(elite.EnemyClass == EnemyClass.ELITE && elite.Aircraft.Health == 1600, "精英截击机配置解析错误");
        EnemyResource boss = tables.EnemyObj.Get(5);
        Require(boss.EnemyClass == EnemyClass.BOSS && boss.Aircraft.Health == 500, "Boss 临时测试配置解析错误");
        foreach (EnemyResource enemy in tables.EnemyObj.DataList) {
            int expectedExplosionCount = enemy.EnemyClass == EnemyClass.BOSS ? 5 : 1;
            Require(enemy.Aircraft.DeathExplosions.Count == expectedExplosionCount, $"敌机 {enemy.Id} 死亡爆炸数量解析错误");
            foreach (ExplosionEffect explosion in enemy.Aircraft.DeathExplosions) {
                Require(explosion.EffectId == 1002, $"敌机 {enemy.Id} 死亡爆炸特效解析错误");
            }
            Require(enemy.Aircraft.RemoveAfterDeathPresentation == (enemy.EnemyClass != EnemyClass.BOSS), $"敌机 {enemy.Id} 死亡后移除规则解析错误");
        }
        Require(boss.Aircraft.DeathExplosions[0].DelayMs == 0 && boss.Aircraft.DeathExplosions[0].Position.X == 0 && boss.Aircraft.DeathExplosions[0].Position.Y == 40 && boss.Aircraft.DeathExplosions[4].DelayMs == 560 && boss.Aircraft.DeathExplosions[4].Position.X == 55 && boss.Aircraft.DeathExplosions[4].Position.Y == -110, "Boss 死亡爆炸时序或坐标解析错误");
        BulletResource enemyBullet = tables.BulletObj.Get(1);
        Require(enemyBullet.Code == "enemy_small" && enemyBullet.Bullet.AppearancePath == "bullet/enemy/battleEnemyBullet" && enemyBullet.Bullet.Speed == 420 && enemyBullet.Bullet.Damage == 12, "敌机子弹配置解析错误");
        foreach (BulletResource bullet in tables.BulletObj.DataList) {
            if (bullet.Bullet.LaunchEffectId <= 0) {
                continue;
            }
            EffectResource launchEffect = tables.EffectObj.GetOrDefault(bullet.Bullet.LaunchEffectId);
            Require(launchEffect != null && launchEffect.Type == EffectType.BULLET_LAUNCH, $"子弹 {bullet.Id} 发射特效引用无效");
        }

        Require(tables.PlayerAircraftObj.DataList.Count == 5, "玩家飞机类型表数量应为 5");
        Require(tables.PlayerAircraftObj.DataList.Count == tables.PlayerAircraftObj.DataMap.Count, "玩家飞机类型表的 DataList 与 DataMap 数量不一致");
        PlayerAircraftResource assaultAircraft = tables.PlayerAircraftObj.Get(1);
        PlayerAircraftResource heavyAircraft = tables.PlayerAircraftObj.Get(2);
        PlayerAircraftResource swiftAircraft = tables.PlayerAircraftObj.Get(3);
        PlayerAircraftResource spreadAircraft = tables.PlayerAircraftObj.Get(4);
        PlayerAircraftResource beamAircraft = tables.PlayerAircraftObj.Get(5);
        Require(assaultAircraft.Code == "player_aircraft_assault_001" && assaultAircraft.DisplayName == "突击型战机" && assaultAircraft.MaxLevel == 3 && assaultAircraft.DefaultUnlocked && assaultAircraft.UnlockStarCost == 0, "突击型玩家飞机配置解析错误");
        Require(heavyAircraft.Code == "player_aircraft_heavy_001" && heavyAircraft.DisplayName == "重装型战机" && heavyAircraft.MaxLevel == 3, "重装型玩家飞机配置解析错误");
        Require(swiftAircraft.Code == "player_aircraft_swift_001" && swiftAircraft.DisplayName == "迅捷型战机" && swiftAircraft.MaxLevel == 4, "迅捷型玩家飞机配置解析错误");
        Require(spreadAircraft.Code == "player_aircraft_spread_001" && spreadAircraft.DisplayName == "散射型战机" && spreadAircraft.MaxLevel == 4, "散射型玩家飞机配置解析错误");
        Require(beamAircraft.Code == "player_aircraft_beam_001" && beamAircraft.DisplayName == "聚能型战机" && beamAircraft.MaxLevel == 4, "聚能型玩家飞机配置解析错误");
        Require(tables.PlayerAircraftLevelObj.DataList.Count == 18, "玩家飞机等级表数量应为 18");
        Require(tables.PlayerAircraftLevelObj.DataList.Count == tables.PlayerAircraftLevelObj.DataMap.Count, "玩家飞机等级表的 DataList 与 DataMap 数量不一致");
        PlayerAircraftLevelResource aircraftLevel1 = tables.PlayerAircraftLevelObj.Get(101);
        PlayerAircraftLevelResource aircraftLevel3 = tables.PlayerAircraftLevelObj.Get(103);
        PlayerAircraftLevelResource heavyLevel3 = tables.PlayerAircraftLevelObj.Get(203);
        PlayerAircraftLevelResource swiftLevel4 = tables.PlayerAircraftLevelObj.Get(304);
        PlayerAircraftLevelResource spreadLevel4 = tables.PlayerAircraftLevelObj.Get(404);
        PlayerAircraftLevelResource beamLevel4 = tables.PlayerAircraftLevelObj.Get(504);
        Require(aircraftLevel1.AircraftId == 1 && aircraftLevel1.Level == 1 && aircraftLevel1.Aircraft.AppearanceName == "aircraft/self/player_aircraft_assault_001_lv01" && aircraftLevel1.Aircraft.Health == 100 && aircraftLevel1.BaseBulletCount == 1 && aircraftLevel1.BasePower == 1000, "突击型玩家飞机 1 级配置解析错误");
        Require(aircraftLevel3.AircraftId == 1 && aircraftLevel3.Level == 3 && aircraftLevel3.Aircraft.AppearanceName == "aircraft/self/player_aircraft_assault_001_lv03" && aircraftLevel3.Aircraft.Health == 150 && aircraftLevel3.BaseBulletCount == 6, "突击型玩家飞机 3 级配置解析错误");
        Require(heavyLevel3.AircraftId == 2 && heavyLevel3.Level == 3 && heavyLevel3.Aircraft.AppearanceName == "aircraft/self/player_aircraft_heavy_001_lv03" && heavyLevel3.Aircraft.Health == 240 && heavyLevel3.BaseBulletCount == 3, "重装型玩家飞机 3 级配置解析错误");
        Require(swiftLevel4.AircraftId == 3 && swiftLevel4.Level == 4 && swiftLevel4.Aircraft.AppearanceName == "aircraft/self/player_aircraft_swift_001_lv04" && swiftLevel4.Aircraft.Health == 140 && swiftLevel4.BaseBulletCount == 5, "迅捷型玩家飞机 4 级配置解析错误");
        Require(spreadLevel4.AircraftId == 4 && spreadLevel4.Level == 4 && spreadLevel4.Aircraft.AppearanceName == "aircraft/self/player_aircraft_spread_001_lv04" && spreadLevel4.Aircraft.Health == 165 && spreadLevel4.BaseBulletCount == 9, "散射型玩家飞机 4 级配置解析错误");
        Require(beamLevel4.AircraftId == 5 && beamLevel4.Level == 4 && beamLevel4.Aircraft.AppearanceName == "aircraft/self/player_aircraft_beam_001_lv04" && beamLevel4.Aircraft.Health == 180 && beamLevel4.BaseBulletCount == 2 && beamLevel4.BasePower == 2400, "聚能型玩家飞机 4 级配置解析错误");
        foreach (PlayerAircraftLevelResource level in tables.PlayerAircraftLevelObj.DataList) {
            Require(level.Aircraft.DeathExplosions.Count == 1 && level.Aircraft.DeathExplosions[0].EffectId == 1002 && level.Aircraft.RemoveAfterDeathPresentation, $"玩家飞机等级 {level.Id} 死亡表现配置解析错误");
        }

        Require(tables.EffectObj.DataList.Count > 0, "特效表不应为空");
        Require(tables.EffectObj.DataList.Count == tables.EffectObj.DataMap.Count,
            "特效表的 DataList 与 DataMap 数量不一致");
        EffectResource bulletHitEffect = tables.EffectObj.Get(1);
        EffectResource missileHitEffect = tables.EffectObj.Get(2);
        EffectResource diamondHitEffect = tables.EffectObj.Get(4);
        EffectResource explosionEffect = tables.EffectObj.Get(1002);
        EffectResource levelUpEffect = tables.EffectObj.Get(10001);
        EffectResource levelUpEffect04 = tables.EffectObj.Get(10004);
        EffectResource bossBornEffect = tables.EffectObj.Get(11001);
        Require(bulletHitEffect.Type == EffectType.BULLET_HIT && bulletHitEffect.Res.StartsWith(BattleConst.BulletHitEffectPrefix, StringComparison.Ordinal),
            "默认子弹命中特效配置解析错误");
        Require(missileHitEffect.Type == EffectType.BULLET_HIT && missileHitEffect.Res.StartsWith(BattleConst.BulletHitEffectPrefix, StringComparison.Ordinal),
            "默认追踪弹命中特效配置解析错误");
        Require(diamondHitEffect.Type == EffectType.BULLET_HIT && diamondHitEffect.Res.StartsWith(BattleConst.BulletHitEffectPrefix, StringComparison.Ordinal),
            "默认菱形直线弹命中特效配置解析错误");
        Require(explosionEffect.Type == EffectType.AIRCRAFT_EXPLOSION && explosionEffect.Res == "explosion_common",
            "默认飞机爆炸特效配置解析错误");
        Require(levelUpEffect.Type == EffectType.OTHER && levelUpEffect.Res == "levelup_01",
            "玩家飞机升级特效配置解析错误");
        Require(levelUpEffect04.Type == EffectType.OTHER && levelUpEffect04.Res == "levelup_04",
            "玩家飞机升级特效 04 配置解析错误");
        Require(bossBornEffect.Type == EffectType.OTHER && bossBornEffect.Res == "bossBorn_1",
            "Boss 出生特效配置解析错误");

        Require(RaidenControl.ins.selectedAircraftId == 1 && RaidenControl.ins.defaultAircraftLevel == 1 && RaidenControl.ins.IsPlayerAircraftUnlocked(1), "默认玩家飞机没有正确解锁并出战");
        Require(!RaidenControl.ins.IsPlayerAircraftUnlocked(2) && !RaidenControl.ins.UnlockPlayerAircraft(2), "星数不足时不应解锁重装型玩家飞机");
        RaidenControl.ins.CompleteStage(1, stage.ThreeStarScore);
        Require(RaidenControl.ins.availableStarCount == 3 && RaidenControl.ins.UnlockPlayerAircraft(2), "累计三星后未能解锁重装型玩家飞机");
        Require(RaidenControl.ins.availableStarCount == 0 && RaidenControl.ins.SelectPlayerAircraft(2) && RaidenControl.ins.selectedAircraftId == 2, "重装型玩家飞机出战切换失败");
        RaidenControl.ins.Reset();

        StageConfigVO stageConfig = RaidenControl.ins.GetStageConfig(1);
        Require(stageConfig != null && stageConfig.stageId == 1, "RaidenModel 未正确转换关卡 1");
        int configuredEnemyCount = 0;
        foreach (int waveId in stage.WaveIds) {
            configuredEnemyCount += tables.StageWaveObj.Get(waveId).EnemyCount;
        }
        Require(stageConfig.enemyWaves.Length == stage.WaveIds.Count && stageConfig.enemyCount == configuredEnemyCount,
            "第一关波次配置转换结果与 Luban 配置不一致");
        Require(stageConfig.bossWave != null && stageConfig.bossWave.enemy.id == 5 && stageConfig.bossWave.count == 1 && stageConfig.bossWave.spawnCenter == new Vector2(360, 120),
            "第一关 Boss 波次配置转换错误");

        PlayerAircraftBattleLevelVO assaultBattleLevel = RaidenControl.ins.GetSelectedPlayerAircraftBattleLevel(3);
        Require(assaultBattleLevel != null && assaultBattleLevel.aircraftId == 1 && assaultBattleLevel.level == 3,
            "当前出战玩家飞机等级配置查询错误");
        Require(assaultBattleLevel.appearancePath == "Assets/Art/unpack/default/raiden/aircraft/self/player_aircraft_assault_001_lv03.png" &&
            assaultBattleLevel.displaySize == new Vector2(256, 256) &&
            assaultBattleLevel.collision.shapes.Count == 1 &&
            assaultBattleLevel.collision.boundsSize == new Vector2(54, 130) &&
            assaultBattleLevel.collision.boundsCenterOffset == new Vector2(0, -15.6f),
            "玩家飞机战斗外观或尺寸转换错误");
        Require(assaultBattleLevel.baseHealth == 150 && assaultBattleLevel.baseBulletCount == 6,
            "玩家飞机战斗属性转换错误");
        Require(RaidenControl.ins.GetPlayerAircraftBattleLevel(1, 0) == null &&
            RaidenControl.ins.GetPlayerAircraftBattleLevel(1, 4) == null &&
            RaidenControl.ins.GetPlayerAircraftBattleLevel(int.MaxValue, 1) == null,
            "玩家飞机战斗等级查询未正确拒绝越界或未知机型");

        List<SettingOptionResource> graphicSettings = SettingOptionCfgMgr.GetCfgByType(SettingType.GRAPHIC);
        Require(graphicSettings.Count > 0, "画面设置业务查询结果为空");
        Require(graphicSettings.TrueForAll(item => item.Type == SettingType.GRAPHIC), "画面设置业务查询混入了其他类型");

        Debug.Log($"[LubanTest] PASS：GM={tables.GmObj.DataList.Count}，SettingOption={tables.SettingOptionObj.DataList.Count}，GraphicSetting={graphicSettings.Count}，Stage={tables.StageObj.DataList.Count}，StageWave={tables.StageWaveObj.DataList.Count}，Enemy={tables.EnemyObj.DataList.Count}，Bullet={tables.BulletObj.DataList.Count}，PlayerAircraft={tables.PlayerAircraftObj.DataList.Count}，PlayerAircraftLevel={tables.PlayerAircraftLevelObj.DataList.Count}");
    }

    /**配置测试断言失败时抛出包含具体原因的异常*/
    private void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("[LubanTest] FAIL：" + message);
        }
    }

}
