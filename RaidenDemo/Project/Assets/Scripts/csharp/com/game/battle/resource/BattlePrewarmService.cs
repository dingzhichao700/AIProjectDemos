using System.Collections.Generic;
using UnityEngine;

/// <summary>集中预热本关可能使用的战斗视觉对象。</summary>
internal static class BattlePrewarmService {
    public static void Prewarm(StageConfigVO stage, BattleVisualPool pool,
        RectTransform layer) {
        pool.Prewarm(BattleConst.PlayerLaserPath, BattleConst.PlayerLaserSize,
            BattleConst.PlayerProjectilePoolCapacity, layer);
        HashSet<int> enemyIds = new HashSet<int>();
        HashSet<int> bulletIds = new HashSet<int>();
        foreach (EnemyWaveVO wave in stage.enemyWaves) {
            PrewarmEnemy(wave.enemy, enemyIds, bulletIds, pool, layer);
        }
        PrewarmEnemy(RaidenControl.ins.model.GetEnemyConfig(4), enemyIds, bulletIds,
            pool, layer);
        PrewarmEnemy(RaidenControl.ins.model.GetEnemyConfig(5), enemyIds, bulletIds,
            pool, layer);
        pool.Prewarm(BattleConst.HealthDropPath, BattleConst.UpgradeDropSize,
            BattleConst.UpgradeDropPoolCapacity, layer);
        pool.Prewarm(BattleConst.UpgradeDropPath, BattleConst.UpgradeDropSize,
            BattleConst.UpgradeDropPoolCapacity, layer);
        pool.Prewarm(BattleConst.WingmanUpgradeDropPath, BattleConst.UpgradeDropSize,
            BattleConst.UpgradeDropPoolCapacity, layer);
        pool.Prewarm(BattleConst.LifeDropPath, BattleConst.UpgradeDropSize,
            BattleConst.UpgradeDropPoolCapacity, layer);
        pool.Prewarm(BattleConst.EliteInterceptorPath, BattleConst.EliteInterceptorSize,
            BattleConst.ElitePoolCapacity, layer);
        pool.Prewarm(BattleConst.BossStage01Path, BattleConst.BossStage01Size,
            BattleConst.BossPoolCapacity, layer);
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
