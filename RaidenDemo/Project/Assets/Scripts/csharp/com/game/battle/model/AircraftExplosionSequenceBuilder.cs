using cfg;
using System;
using System.Collections.Generic;

/// <summary>
/// 飞机死亡爆炸序列构建器
/// </summary>
/// <remarks>
/// 根据飞机体型与死亡方式决定爆炸规模和数量，并在实际碰撞组合区域内生成位置。
/// </remarks>
internal static class AircraftExplosionSequenceBuilder {

    /**构建一次死亡开始时播放的爆炸序列*/
    public static IReadOnlyList<AircraftExplosionVO> Build(AircraftSizeType aircraftSize, AircraftDeathType deathType, AircraftCollisionVO collision, System.Random random) {
        if (collision == null) {
            throw new ArgumentNullException(nameof(collision), "飞机死亡爆炸需要实际碰撞区域");
        }
        List<AircraftExplosionSize> sizes = GetExplosionSizes(aircraftSize, deathType);
        List<AircraftExplosionVO> result = new List<AircraftExplosionVO>(sizes.Count);
        int normalIntervalMs = BattleAircraftDeathConst.NormalExplosionIntervalMs;
        int lastIntervalMs = BattleAircraftDeathConst.LastExplosionIntervalMs;
        if (normalIntervalMs < 0 || lastIntervalMs < 0) {
            throw new InvalidOperationException("飞机死亡爆炸间隔不能为负数");
        }
        int delayMs = 0;
        for (int index = 0; index < sizes.Count; index++) {
            AircraftExplosionSize explosionSize = sizes[index];
            IReadOnlyList<int> effectIds = BattleAircraftDeathConst.GetExplosionEffectIds(explosionSize);
            int effectId = BattleAircraftDeathConst.GetRandom(effectIds, random, $"{explosionSize}规模爆炸特效");
            result.Add(new AircraftExplosionVO(delayMs, collision.GetRandomPoint(random), effectId));
            if (index + 1 < sizes.Count) {
                bool nextIsDelayedLastExplosion = aircraftSize >= AircraftSizeType.LARGE && index + 1 == sizes.Count - 1;
                delayMs += nextIsDelayedLastExplosion ? lastIntervalMs : normalIntervalMs;
            }
        }
        return result;
    }

    /**按体型与死亡方式返回有序爆炸规模*/
    private static List<AircraftExplosionSize> GetExplosionSizes(AircraftSizeType aircraftSize, AircraftDeathType deathType) {
        List<AircraftExplosionSize> result = new List<AircraftExplosionSize>();
        if (deathType == AircraftDeathType.DISINTEGRATE) {
            AddDisintegrationSizes(result, aircraftSize);
        } else {
            AddFallingSizes(result, aircraftSize);
        }
        return result;
    }

    /**添加爆炸解体所需的爆炸规模*/
    private static void AddDisintegrationSizes(List<AircraftExplosionSize> result, AircraftSizeType aircraftSize) {
        switch (aircraftSize) {
            case AircraftSizeType.SMALL:
                Add(result, AircraftExplosionSize.SMALL, 1);
                break;
            case AircraftSizeType.MEDIUM:
                Add(result, AircraftExplosionSize.SMALL, 1);
                Add(result, AircraftExplosionSize.MEDIUM, 1);
                break;
            case AircraftSizeType.LARGE:
                Add(result, AircraftExplosionSize.MEDIUM, 2);
                Add(result, AircraftExplosionSize.LARGE, 1);
                break;
            case AircraftSizeType.GIANT:
                Add(result, AircraftExplosionSize.MEDIUM, 3);
                Add(result, AircraftExplosionSize.LARGE, 2);
                Add(result, AircraftExplosionSize.GIANT, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(aircraftSize), aircraftSize, "未知飞机体型");
        }
    }

    /**添加开始坠落时所需的爆炸规模*/
    private static void AddFallingSizes(List<AircraftExplosionSize> result, AircraftSizeType aircraftSize) {
        switch (aircraftSize) {
            case AircraftSizeType.MEDIUM:
                Add(result, AircraftExplosionSize.SMALL, 2);
                break;
            case AircraftSizeType.LARGE:
                Add(result, AircraftExplosionSize.MEDIUM, 3);
                break;
            case AircraftSizeType.GIANT:
                Add(result, AircraftExplosionSize.MEDIUM, 4);
                Add(result, AircraftExplosionSize.LARGE, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(aircraftSize), aircraftSize, "小型飞机不会进入坠落死亡方式");
        }
    }

    /**按数量追加同一爆炸规模*/
    private static void Add(List<AircraftExplosionSize> result, AircraftExplosionSize size, int count) {
        for (int index = 0; index < count; index++) {
            result.Add(size);
        }
    }

}
