using cfg;
using UnityEngine;

/// <summary>
/// 普通敌机编队共享飞行路径
/// </summary>
/// <remarks>
/// 统一推进编队中心并按槽位计算成员位置，保证队形、转向和视窗边界一致。
/// </remarks>
internal sealed class EnemyFormationPathVO {

    private readonly EnemyWaveVO wave;

    private readonly float moveSpeed;

    private readonly float safeMinCenterX;

    private readonly float safeMaxCenterX;

    private readonly float originCenterX;

    private readonly float originCenterY;

    private readonly int formationColumns;

    private readonly float effectiveSpacing;

    private float elapsed;

    public EnemyFormationPathVO(EnemyWaveVO wave) {
        this.wave = wave;
        moveSpeed = Mathf.Max(1f, wave.enemy.moveSpeed);
        float memberHalfWidth = wave.enemy.displaySize.x * 0.5f;
        float availableCenterSpan = BattleConst.BattleViewportWidth -
                                    2f * (memberHalfWidth +
                                          BattleConst.EnemyFormationViewportPadding);
        float minimumSpacing = wave.enemy.displaySize.x +
                               BattleConst.EnemyFormationMemberGap;
        int columnsByWidth = Mathf.FloorToInt(availableCenterSpan /
            Mathf.Max(1f, minimumSpacing)) + 1;
        formationColumns = Mathf.Clamp(columnsByWidth, 1,
            Mathf.Min(wave.count, BattleConst.EnemyFormationMaxColumns));
        effectiveSpacing = formationColumns <= 1
            ? 0f
            : Mathf.Min(wave.spacing, availableCenterSpan /
                (formationColumns - 1));
        float formationHalfWidth = 0f;
        float lowestMemberOffsetY = 0f;
        float highestMemberOffsetY = 0f;
        for (int i = 0; i < wave.count; i++) {
            Vector2 memberOffset = GetMemberOffset(i);
            formationHalfWidth = Mathf.Max(formationHalfWidth,
                Mathf.Abs(memberOffset.x));
            lowestMemberOffsetY = Mathf.Min(lowestMemberOffsetY,
                memberOffset.y);
            highestMemberOffsetY = Mathf.Max(highestMemberOffsetY,
                memberOffset.y);
        }
        float safeHalfWidth = memberHalfWidth + formationHalfWidth +
                              BattleConst.EnemyFormationViewportPadding;
        safeMinCenterX = safeHalfWidth;
        safeMaxCenterX = BattleConst.BattleViewportWidth - safeHalfWidth;
        if (wave.motionType == EnemyMotionType.SNAKE) {
            originCenterX = wave.direction > 0f
                ? -safeHalfWidth
                : BattleConst.BattleViewportWidth + safeHalfWidth;
            float memberHalfHeight = wave.enemy.displaySize.y * 0.5f;
            float minimumCenterY = -BattleConst.BattleViewportHeight +
                                   memberHalfHeight +
                                   BattleConst.EnemyFormationViewportPadding -
                                   lowestMemberOffsetY +
                                   BattleConst.EnemyHorizontalPassVerticalAmplitude;
            float maximumCenterY = -memberHalfHeight -
                                   BattleConst.EnemyFormationViewportPadding -
                                   highestMemberOffsetY -
                                   BattleConst.EnemyHorizontalPassVerticalAmplitude;
            originCenterY = Mathf.Clamp(BattleConst.EnemyHorizontalPassHeight,
                minimumCenterY, maximumCenterY);
        } else {
            originCenterX = Mathf.Clamp(wave.spawnCenter.x,
                safeMinCenterX, safeMaxCenterX);
            float minimumOutsideCenterY = wave.enemy.displaySize.y * 0.5f +
                                          BattleConst.EnemyFormationViewportPadding -
                                          lowestMemberOffsetY;
            originCenterY = Mathf.Max(wave.spawnCenter.y, minimumOutsideCenterY);
        }
    }

    /**推进一次整支编队共用的路径时间。*/
    public void Update(float deltaTime) {
        elapsed += Mathf.Max(0f, deltaTime);
    }

    /**取得指定槽位在当前共享路径上的位置。*/
    public Vector2 GetMemberPosition(int memberIndex) {
        return GetCenterPosition() + GetMemberOffset(memberIndex);
    }

    private Vector2 GetCenterPosition() {
        float centerX = originCenterX;
        float distance = GetTravelDistance();
        if (wave.motionType == EnemyMotionType.SNAKE) {
            float centerY = originCenterY +
                            Mathf.Sin(elapsed *
                                BattleConst.EnemyHorizontalPassVerticalFrequency) *
                            BattleConst.EnemyHorizontalPassVerticalAmplitude;
            return new Vector2(originCenterX + wave.direction * distance,
                centerY);
        }
        switch (wave.motionType) {
            case EnemyMotionType.STRAIGHT:
                float straightAvailable = wave.direction > 0f
                    ? safeMaxCenterX - originCenterX
                    : originCenterX - safeMinCenterX;
                float driftAmplitude = Mathf.Min(
                    BattleConst.EnemyStraightDriftAmplitude, straightAvailable);
                float driftProgress = Mathf.Clamp01(elapsed /
                    BattleConst.EnemyStraightDriftDuration);
                centerX += wave.direction *
                           Mathf.Sin(driftProgress * Mathf.PI) * driftAmplitude;
                break;
            case EnemyMotionType.DIAGONAL:
                float available = wave.direction > 0f
                    ? safeMaxCenterX - originCenterX
                    : originCenterX - safeMinCenterX;
                centerX += wave.direction * Mathf.Min(distance *
                    BattleConst.EnemyDiagonalHorizontalRatio, available);
                break;
            case EnemyMotionType.FORMATION_TURN:
                float turnAvailable = wave.direction > 0f
                    ? safeMaxCenterX - originCenterX
                    : originCenterX - safeMinCenterX;
                float turnAmplitude = Mathf.Min(BattleConst.EnemyFormationTurnAmplitude,
                    turnAvailable);
                float progress = Mathf.Clamp01(elapsed /
                    BattleConst.EnemyFormationTurnDuration);
                centerX += wave.direction * Mathf.Sin(progress * Mathf.PI) *
                           turnAmplitude;
                break;
        }
        return new Vector2(centerX, originCenterY - distance);
    }

    /**直线编队在入场阶段由慢到快，其他路径保持配置速度。*/
    private float GetTravelDistance() {
        if (wave.motionType != EnemyMotionType.STRAIGHT) {
            return moveSpeed * elapsed;
        }
        float accelerationProgress = Mathf.Clamp01(elapsed /
            BattleConst.EnemyStraightEntryAccelerationDuration);
        float speedRatio = Mathf.Lerp(BattleConst.EnemyStraightEntrySpeedRatio,
            BattleConst.EnemyStraightCruiseSpeedRatio, accelerationProgress);
        return moveSpeed * elapsed * speedRatio;
    }

    private Vector2 GetMemberOffset(int memberIndex) {
        int row = memberIndex / formationColumns;
        int column = memberIndex % formationColumns;
        int rowStartIndex = row * formationColumns;
        int rowMemberCount = Mathf.Min(formationColumns,
            wave.count - rowStartIndex);
        float centeredColumn = column - (rowMemberCount - 1) * 0.5f;
        float offsetX = centeredColumn * effectiveSpacing;
        float offsetY = row * (wave.enemy.displaySize.y +
            BattleConst.EnemyFormationRowGap);
        if (wave.formationType == EnemyFormationType.DIAGONAL) {
            offsetY -= centeredColumn *
                       BattleConst.EnemyDiagonalFormationVerticalGap *
                       wave.direction;
        }
        return new Vector2(offsetX, offsetY);
    }

}
