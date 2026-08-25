using System;
using UnityEngine;

/// <summary>
/// 战斗关卡数据管理
/// </summary>
/// <remarks>
/// 管理敌机波次、精英与 Boss 推进以及本关公共敌弹配置。
/// </remarks>
internal sealed class BattleStageModel {

    private StageConfigVO stageConfig;
    private int resolvedEnemyCount;
    private int currentWaveIndex;
    private int spawnedInWave;
    private int defeatedInWave;
    private float spawnCooldown;
    private float waveCooldown;
    private bool eliteSpawned;

    public bool bossSpawned { get; private set; }
    public EnemyConfigVO eliteConfig { get; private set; }
    public EnemyConfigVO bossConfig { get; private set; }
    public EnemyBulletConfigVO defaultEnemyBullet { get; private set; }

    /**重置当前关卡的全部波次推进状态。*/
    public void Initialize(StageConfigVO config) {
        stageConfig = config;
        resolvedEnemyCount = 0;
        currentWaveIndex = 0;
        spawnedInWave = 0;
        defeatedInWave = 0;
        spawnCooldown = 0f;
        waveCooldown = 0f;
        eliteSpawned = false;
        bossSpawned = false;
    }

    /**设置精英、Boss 和公共敌弹配置。*/
    public void Configure(EnemyConfigVO elite, EnemyConfigVO boss, EnemyBulletConfigVO defaultBullet) {
        eliteConfig = elite;
        bossConfig = boss;
        defaultEnemyBullet = defaultBullet;
    }

    /**推进普通波次，并在满足条件时请求创建精英或 Boss。*/
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
                TryRequestElite(activeEnemyCount, spawnSpecial);
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
        resolvedEnemyCount++;
        if (!defeated || stageConfig == null || currentWaveIndex >= stageConfig.enemyWaves.Length) {
            return -1;
        }
        defeatedInWave++;
        int waveEnemyCount = stageConfig.enemyWaves[currentWaveIndex].count;
        bool completed = activeEnemyCount == 0 && spawnedInWave >= waveEnemyCount;
        return completed && defeatedInWave >= waveEnemyCount ? currentWaveIndex : -1;
    }

    /**精英敌机清除后请求生成 Boss。*/
    public void TryRequestBoss(int activeEnemyCount, Action<EnemyConfigVO, Vector2> spawnSpecial) {
        if (bossSpawned || activeEnemyCount > 0) {
            return;
        }
        bossSpawned = true;
        if (bossConfig != null) {
            spawnSpecial(bossConfig, new Vector2(360f, 120f));
        }
    }

    private void TryRequestElite(int activeEnemyCount, Action<EnemyConfigVO, Vector2> spawnSpecial) {
        if (eliteSpawned || currentWaveIndex < stageConfig.enemyWaves.Length || resolvedEnemyCount < stageConfig.enemyCount || activeEnemyCount > 0) {
            return;
        }
        eliteSpawned = true;
        if (eliteConfig != null) {
            spawnSpecial(eliteConfig, new Vector2(360f, 80f));
        }
    }

    public void Clear() {
        eliteConfig = null;
        bossConfig = null;
        defaultEnemyBullet = null;
        Initialize(null);
    }
}
