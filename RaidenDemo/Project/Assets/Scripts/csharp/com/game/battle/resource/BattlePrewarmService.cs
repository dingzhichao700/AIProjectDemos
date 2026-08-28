using System.Collections.Generic;
using cfg.resource;
using UnityEngine;

/// <summary>集中预热本关可能使用的战斗视觉对象。</summary>
internal static class BattlePrewarmService {
    public static void Prewarm(StageConfigVO stage, BattleVisualPool pool,
        RectTransform layer) {
        PrewarmPlayerProjectiles(pool, layer);
        HashSet<int> enemyIds = new HashSet<int>();
        HashSet<int> bulletIds = new HashSet<int>();
        foreach (EnemyWaveVO wave in stage.enemyWaves) {
            PrewarmEnemy(wave.enemy, enemyIds, bulletIds, pool, layer);
        }
        PrewarmEnemy(RaidenControl.ins.model.GetEnemyConfig(4), enemyIds, bulletIds,
            pool, layer);
        PrewarmEnemy(stage.bossWave.enemy, enemyIds, bulletIds, pool, layer);
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
                if (projectilePaths.Add(launcher.projectilePath)) {
                    pool.Prewarm(launcher.projectilePath, launcher.projectileSize, BattleConst.PlayerProjectilePoolCapacity, layer);
                }
            }
        }
    }

    private static void PrewarmEnemy(EnemyConfigVO enemy, HashSet<int> enemyIds,
        HashSet<int> bulletIds, BattleVisualPool pool, RectTransform layer) {
        if (enemy == null || !enemyIds.Add(enemy.id)) {
            return;
        }
        pool.Prewarm(enemy.appearancePath, enemy.displaySize, enemy.poolCapacity, layer);
        if (bulletIds.Add(enemy.bullet.id)) {
            pool.Prewarm(enemy.bullet.appearancePath, enemy.bullet.displaySize,
                enemy.bullet.poolCapacity, layer);
        }
    }
}
