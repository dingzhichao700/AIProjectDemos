using System.Collections.Generic;
using cfg.resource;
using UnityEngine;

/// <summary>集中预热本关可能使用的战斗视觉对象。</summary>
internal static class BattlePrewarmService {
    public static void Prewarm(StageConfigVO stage, BattleVisualPool pool,
        RectTransform layer) {
        PrewarmPlayerProjectiles(pool, layer);
        HashSet<int> enemyIds = new HashSet<int>();
        HashSet<string> bulletKeys = new HashSet<string>();
        foreach (EnemyWaveVO wave in stage.enemyWaves) {
            PrewarmEnemy(wave.enemy, enemyIds, bulletKeys, pool, layer);
        }
        PrewarmEnemy(stage.bossWave.enemy, enemyIds, bulletKeys, pool, layer);
        foreach (StageItemResource item in CfgManager.tables.StageItemObj.DataList) {
            pool.Prewarm(BattleConst.GetRaidenUnpackImagePath(item.Res), BattleConst.UpgradeDropSize, BattleConst.UpgradeDropPoolCapacity, layer);
        }
    }

    /**按当前出战机型的全部可用等级预热玩家子弹。*/
    private static void PrewarmPlayerProjectiles(BattleVisualPool pool, RectTransform layer) {
        PlayerAircraftVO aircraft = RaidenControl.ins.GetSelectedPlayerAircraft();
        if (aircraft == null) {
            return;
        }
        HashSet<string> projectilePaths = new HashSet<string>();
        for (int level = RaidenControl.ins.defaultAircraftLevel; level <= aircraft.maxLevel; level++) {
            PlayerAircraftBattleLevelVO config = RaidenControl.ins.GetPlayerAircraftBattleLevel(aircraft.id, level);
            if (config == null) {
                continue;
            }
            foreach (PlayerBulletLauncherVO launcher in config.bulletLaunchers) {
                PrewarmBulletType(launcher, projectilePaths, pool, layer);
            }
        }
    }

    private static void PrewarmEnemy(EnemyConfigVO enemy, HashSet<int> enemyIds,
        HashSet<string> bulletKeys, BattleVisualPool pool, RectTransform layer) {
        if (enemy == null || !enemyIds.Add(enemy.id)) {
            return;
        }
        pool.Prewarm(enemy.appearancePath, enemy.displaySize, enemy.poolCapacity, layer);
        foreach (PlayerBulletLauncherVO launcher in enemy.bulletLaunchers) {
            string key = $"{launcher.bulletType}:{launcher.bulletLevel}";
            if (bulletKeys.Add(key)) PrewarmBulletType(launcher, bulletKeys, pool, layer);
        }
    }

    /**预热同类型从基础等级起的全部静态图片弹体。*/
    private static void PrewarmBulletType(PlayerBulletLauncherVO launcher, HashSet<string> paths, BattleVisualPool pool, RectTransform layer) {
        foreach (BulletResource candidate in CfgManager.tables.BulletObj.DataList) {
            if (candidate.Type != launcher.bulletType || candidate.Level < launcher.bulletLevel || candidate.EffectId > 0) continue;
            string path = BattleConst.GetRaidenUnpackImagePath(candidate.AppearancePath);
            if (paths.Add(path)) pool.Prewarm(path, Vector2.one * candidate.CollisionRadius * 2f, BattleConst.PlayerProjectilePoolCapacity, layer);
        }
    }
}
