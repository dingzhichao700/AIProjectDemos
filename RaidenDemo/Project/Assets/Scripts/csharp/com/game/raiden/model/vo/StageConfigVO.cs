using UnityEngine;

/// <summary>
/// 单个关卡的运行时配置数据
/// </summary>
public sealed class StageConfigVO {

    /**关卡编号*/
    public readonly int stageId;

    /**关卡选择界面中的节点坐标*/
    public readonly Vector2 selectPosition;

    /**普通敌机编队配置*/
    public readonly EnemyWaveVO[] enemyWaves;

    /**本关必须执行的 Boss 波次*/
    public readonly EnemyWaveVO bossWave;

    /**关卡场景背景编号*/
    public readonly int sceneId;

    /**达到二星所需的最低得分*/
    public readonly int twoStarScore;

    /**达到三星所需的最低得分*/
    public readonly int threeStarScore;

    /**普通敌机总数*/
    public int enemyCount {
        get {
            int count = 0;
            foreach (EnemyWaveVO wave in enemyWaves) count += wave.count;
            return count;
        }
    }

    /// <summary>
    /// 从 Luban 关卡配置创建运行时数据
    /// </summary>
    public StageConfigVO(int stageId, Vector2 selectPosition, EnemyWaveVO[] enemyWaves, EnemyWaveVO bossWave, int sceneId, int twoStarScore, int threeStarScore) {
        this.stageId = stageId;
        this.selectPosition = selectPosition;
        this.enemyWaves = enemyWaves;
        this.bossWave = bossWave;
        this.sceneId = sceneId;
        this.twoStarScore = twoStarScore;
        this.threeStarScore = threeStarScore;
    }

}
