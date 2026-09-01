using System;
using UnityEngine;

/// <summary>
/// 战斗关卡数据管理
/// </summary>
/// <remarks>
/// 管理配置化敌机波次与 Boss 波次推进。
/// </remarks>
internal sealed class BattleStageModel {

    private StageConfigVO stageConfig;
    private int currentWaveIndex;
    private int spawnedInWave;
    private int defeatedInWave;
    private float spawnCooldown;
    private float waveCooldown;

    public bool bossSpawned { get; private set; }

    /**重置当前关卡的全部波次推进状态。*/
    public void Initialize(StageConfigVO config) {
        stageConfig = config;
        currentWaveIndex = 0;
        spawnedInWave = 0;
        defeatedInWave = 0;
        spawnCooldown = 0f;
        waveCooldown = 0f;
        bossSpawned = false;
    }

    /**推进配置化普通波次，并在全部结束后请求创建 Boss。*/
    public void Update(float deltaTime, int activeEnemyCount, Action<EnemyWaveVO, int> spawnNormal, Action<EnemyConfigVO, Vector2> spawnSpecial) {
        if (stageConfig == null) {
            return;
        }
        if (currentWaveIndex >= stageConfig.enemyWaves.Length) {
            TryRequestBoss(activeEnemyCount, spawnSpecial);
            return;
        }
        EnemyWaveVO wave = stageConfig.enemyWaves[currentWaveIndex];
        if (spawnedInWave >= wave.count) {
            if (activeEnemyCount > 0) {
                return;
            }
            waveCooldown -= deltaTime;
            if (waveCooldown > 0f) {
                return;
            }
            currentWaveIndex++;
            spawnedInWave = 0;
            defeatedInWave = 0;
            spawnCooldown = 0f;
            if (currentWaveIndex >= stageConfig.enemyWaves.Length) {
                TryRequestBoss(activeEnemyCount, spawnSpecial);
                return;
            }
            wave = stageConfig.enemyWaves[currentWaveIndex];
        }
        spawnCooldown -= deltaTime;
        if (spawnCooldown > 0f) {
            return;
        }
        spawnCooldown += BattleConst.EnemySpawnInterval;
        int formationIndex = spawnedInWave++;
        spawnNormal(wave, formationIndex);
        if (spawnedInWave >= wave.count) {
            waveCooldown = BattleConst.EnemyWaveInterval;
        }
    }

    /**记录普通敌机结算，并返回刚完成击毁的波次索引。*/
    public int RecordNormalEnemyResolved(bool defeated, int activeEnemyCount) {
        if (!defeated || stageConfig == null || currentWaveIndex >= stageConfig.enemyWaves.Length) {
            return -1;
        }
        defeatedInWave++;
        int waveEnemyCount = stageConfig.enemyWaves[currentWaveIndex].count;
        bool completed = activeEnemyCount == 0 && spawnedInWave >= waveEnemyCount;
        return completed && defeatedInWave >= waveEnemyCount ? currentWaveIndex : -1;
    }

    /**全部普通波次清除后请求生成 Boss。*/
    public void TryRequestBoss(int activeEnemyCount, Action<EnemyConfigVO, Vector2> spawnSpecial) {
        if (bossSpawned || activeEnemyCount > 0) {
            return;
        }
        bossSpawned = true;
        if (stageConfig.bossWave != null) {
            spawnSpecial(stageConfig.bossWave.enemy, stageConfig.bossWave.spawnCenter);
        }
    }

    public void Clear() {
        Initialize(null);
    }
}
