using System.Collections.Generic;

/// <summary>
/// 近期伤害统计器
/// </summary>
/// <remarks>
/// 使用所属单位计时器累计逻辑时间，只保留统计窗口内实际扣除的伤害。
/// </remarks>
internal sealed class RecentDamageTracker {

    private readonly struct DamageRecord {

        public readonly double expireTimeMs;
        public readonly int value;

        public DamageRecord(double expireTimeMs, int value) {
            this.expireTimeMs = expireTimeMs;
            this.value = value;
        }

    }

    private readonly Queue<DamageRecord> records = new Queue<DamageRecord>();
    private double elapsedTimeMs;
    private long totalDamage;

    /**当前统计窗口内的实际伤害总值*/
    public long value => totalDamage;

    /**记录一次实际伤害*/
    public void Record(int damage, int durationMs) {
        if (damage <= 0 || durationMs <= 0) {
            return;
        }
        records.Enqueue(new DamageRecord(elapsedTimeMs + durationMs, damage));
        totalDamage += damage;
    }

    /**按所属计时器移除已经过期的伤害*/
    public void Update(float deltaTime) {
        if (deltaTime <= 0f || records.Count == 0) {
            return;
        }
        elapsedTimeMs += deltaTime * 1000d;
        while (records.Count > 0 && records.Peek().expireTimeMs <= elapsedTimeMs) {
            totalDamage -= records.Dequeue().value;
        }
    }

    /**清空全部伤害记录*/
    public void Clear() {
        records.Clear();
        elapsedTimeMs = 0d;
        totalDamage = 0L;
    }

}
