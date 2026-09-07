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
    private float waveCooldown;
    private EnemyFormationPathVO currentFormationPath;

    public float victoryDelay => stageConfig.victoryDelay;
    public int GetWaveRewardItemId(int index) => stageConfig.enemyWaves[index].rewardItemId;

    public bool bossSpawned { get; private set; }

    /**重置当前关卡的全部波次推进状态。*/
    public void Initialize(StageConfigVO config) {
        stageConfig = config;
        currentWaveIndex = 0;
        spawnedInWave = 0;
        defeatedInWave = 0;
        waveCooldown = 0f;
        currentFormationPath = null;
        bossSpawned = false;
    }

    /**推进配置化普通波次，并在全部结束后请求创建 Boss。*/
    public void Update(float deltaTime, int activeEnemyCount,
        Action<EnemyWaveVO, int, EnemyFormationPathVO> spawnNormal,
        Action<EnemyWaveVO> spawnSpecial) {
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
            currentFormationPath = null;
            if (currentWaveIndex >= stageConfig.enemyWaves.Length) {
                TryRequestBoss(activeEnemyCount, spawnSpecial);
                return;
            }
            wave = stageConfig.enemyWaves[currentWaveIndex];
        }
        if (wave.enemyClass != cfg.EnemyClass.NORMAL) {
            spawnSpecial(wave);
        } else {
            currentFormationPath = new EnemyFormationPathVO(wave);
            for (int formationIndex = 0; formationIndex < wave.count; formationIndex++) {
                spawnNormal(wave, formationIndex, currentFormationPath);
            }
        }
        spawnedInWave = wave.count;
        waveCooldown = stageConfig.waveInterval;
    }

    /**仅使用 enemyTimer 推进共享路径一次，波次调度仍归 sceneTimer。*/
    public void UpdateEnemyMovement(float deltaTime) {
        currentFormationPath?.Update(deltaTime);
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
    public void TryRequestBoss(int activeEnemyCount, Action<EnemyWaveVO> spawnSpecial) {
        if (stageConfig == null || currentWaveIndex < stageConfig.enemyWaves.Length || bossSpawned || activeEnemyCount > 0) {
            return;
        }
        bossSpawned = true;
        if (stageConfig.bossWave != null) {
            spawnSpecial(stageConfig.bossWave);
        }
    }

    public void Clear() {
        Initialize(null);
    }
}
