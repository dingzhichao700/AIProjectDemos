using System;
using System.Collections.Generic;
using cfg.resource;
using UnityEngine;

/// <summary>已验证的关卡配置与资源引用。</summary>
public sealed class StageConfigVO {
    public readonly int stageId;
    public readonly Vector2 selectPosition;
    public readonly EnemyWaveVO[] enemyWaves;
    public readonly EnemyWaveVO bossWave;
    public readonly int sceneId;
    public readonly int twoStarScore;
    public readonly int threeStarScore;
    public readonly float supplyFirstDelay;
    public readonly float supplyInterval;
    public readonly float waveInterval;
    public readonly float victoryDelay;
    public readonly IReadOnlyList<StageItemResource> supplyItems;
    public int enemyCount {
        get {
            int count = 0;
            foreach (EnemyWaveVO wave in enemyWaves) {
                count += wave.count;
            }
            return count;
        }
    }
    public StageConfigVO(StageResource config, EnemyWaveVO[] enemyWaves, EnemyWaveVO bossWave, StageItemResource[] supplyItems) {
        stageId = config.Id;
        selectPosition = new Vector2(config.SelectPosition.X, config.SelectPosition.Y);
        this.enemyWaves = enemyWaves;
        this.bossWave = bossWave;
        sceneId = config.SceneId;
        twoStarScore = config.TwoStarScore;
        threeStarScore = config.ThreeStarScore;
        supplyFirstDelay = config.SupplyFirstDelayMs / 1000f;
        supplyInterval = config.SupplyIntervalMs / 1000f;
        waveInterval = config.WaveIntervalMs / 1000f;
        victoryDelay = config.VictoryDelayMs / 1000f;
        this.supplyItems = Array.AsReadOnly(supplyItems);
        if (supplyFirstDelay < 0f || supplyInterval <= 0f || waveInterval < 0f || victoryDelay < 0f) {
            throw new InvalidOperationException($"关卡 {stageId} 的时间配置无效");
        }
    }
}
