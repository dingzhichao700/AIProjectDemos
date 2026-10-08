using cfg;
using UnityEngine;

/// <summary>普通敌机编队共享飞行路径</summary>
/// <remarks>使用已验证布局及配置推进上半屏入场、攻击和离场。</remarks>
internal sealed class EnemyFormationPathVO {
    private readonly EnemyWaveVO wave;
    private readonly Vector2 origin;
    private readonly Vector2 station;
    private readonly Vector2 departure;
    private readonly float speed;
    private readonly float entryDuration;
    private readonly float attackDuration;
    private readonly float patrolAmplitude;
    private readonly float direction;
    private float elapsed;

    private bool horizontalPass => wave.motionType == EnemyMotionType.HORIZONTAL_PASS;
    private bool patrol => wave.motionType == EnemyMotionType.PATROL;
    public bool isLeaving => elapsed >= entryDuration + attackDuration;
    public bool canFire => elapsed >= entryDuration + wave.prepareDuration && !isLeaving;

    public EnemyFormationPathVO(EnemyWaveVO wave) {
        this.wave = wave;
        speed = wave.enemy.moveSpeed;
        direction = wave.direction;
        EnemyFormationLayoutVO layout = wave.layout;
        if (layout == null) {
            throw new System.InvalidOperationException($"波次 {wave.id} 缺少已验证的编队布局");
        }
        float minX = layout.minCenter.x;
        float maxX = layout.maxCenter.x;
        float lowerY = layout.minCenter.y;
        float upperY = layout.maxCenter.y;
        float stationY = Mathf.Lerp(lowerY, upperY, wave.stationHeightRatio);
        float stationX = Mathf.Clamp(wave.spawnCenter.x, minX, maxX);
        if (patrol) {
            stationX = (minX + maxX) * 0.5f;
        }
        patrolAmplitude = Mathf.Min(wave.patrolAmplitude, (maxX - minX) * 0.5f);
        station = new Vector2(stationX, stationY);
        if (horizontalPass) {
            origin = new Vector2(direction > 0f ? -minX : BattleConst.BattleViewportWidth + minX, stationY);
            station = new Vector2(direction > 0f ? minX : maxX, stationY);
            entryDuration = Vector2.Distance(origin, station) / (speed * wave.entrySpeedMultiplier);
            // 宽编队完整入场后适当减速，保留可辨识的射击窗口。
            attackDuration = Mathf.Max(wave.attackDuration, (maxX - minX) / speed);
            departure = new Vector2(direction > 0f ? maxX : minX, stationY);
        } else {
            origin = new Vector2(stationX, Mathf.Max(wave.spawnCenter.y, layout.outsideTopY));
            // 入场单独加速并保留减速曲线；不改变攻击与撤离阶段的配置移速。
            entryDuration = 2f * Vector2.Distance(origin, station) / (speed * wave.entrySpeedMultiplier);
            attackDuration = wave.attackDuration;
            departure = station;
        }
    }

    public void Update(float deltaTime) {
        elapsed += Mathf.Max(0f, deltaTime);
    }

    public Vector2 GetMemberPosition(int memberIndex) {
        return GetCenterPosition() + wave.layout.GetOffset(memberIndex);
    }

    private Vector2 GetCenterPosition() {
        if (elapsed < entryDuration) {
            float progress = Mathf.Clamp01(elapsed / entryDuration);
            float ratio = horizontalPass ? progress : 1f - (1f - progress) * (1f - progress);
            return Vector2.Lerp(origin, station, ratio);
        }
        float attackTime = elapsed - entryDuration;
        if (attackTime < attackDuration) {
            if (horizontalPass) {
                return Vector2.Lerp(station, departure, attackTime / attackDuration);
            }
            if (patrol) {
                float movingTime = Mathf.Max(0f, attackTime - wave.prepareDuration);
                float progress = movingTime / (attackDuration - wave.prepareDuration);
                float amplitude = Mathf.Min(patrolAmplitude,
                    speed * (attackDuration - wave.prepareDuration) / (2f * Mathf.PI));
                return station + Vector2.right * (direction * amplitude * Mathf.Sin(progress * 2f * Mathf.PI));
            }
            return station;
        }
        float exitTime = attackTime - attackDuration;
        Vector2 exitDirection = wave.exitDirection;
        return departure + exitDirection * (speed * exitTime);
    }
}
