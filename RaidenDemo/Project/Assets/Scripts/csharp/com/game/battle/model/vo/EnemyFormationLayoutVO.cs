using cfg;
using UnityEngine;

/// <summary>不可变的编队槽位与合法活动区域。</summary>
/// <remarks>由关卡配置转换时创建，路径运行时复用；不推进时间。</remarks>
public sealed class EnemyFormationLayoutVO {
    private readonly Vector2[] offsets;
    public readonly Vector2 minCenter;
    public readonly Vector2 maxCenter;
    public readonly float outsideTopY;
    public Vector2 GetOffset(int index) => offsets[index];
    public EnemyFormationLayoutVO(EnemyWaveVO wave) {
        float direction = wave.direction;
        // 保守外接范围覆盖机身倾斜后的尺寸，不依赖视图对象。
        float angle = BattleConst.EnemyFormationBankAngle * Mathf.Deg2Rad;
        float halfWidth = (wave.enemy.displaySize.x + wave.enemy.displaySize.y * Mathf.Sin(angle)) * 0.5f;
        float halfHeight = (wave.enemy.displaySize.y + wave.enemy.displaySize.x * Mathf.Sin(angle)) * 0.5f;
        float padding = BattleConst.EnemyFormationViewportPadding;
        float availableWidth = BattleConst.BattleViewportWidth - 2f * (halfWidth + padding);
        int columns = Mathf.Clamp(Mathf.FloorToInt((availableWidth + 2f * halfWidth + BattleConst.EnemyFormationMemberGap) /
            (2f * halfWidth + BattleConst.EnemyFormationMemberGap)), 1, Mathf.Min(wave.count, BattleConst.EnemyFormationMaxColumns));
        float spacing = columns == 1 ? 0f : Mathf.Min(Mathf.Max(wave.spacing,
            2f * halfWidth + BattleConst.EnemyFormationMemberGap), availableWidth / (columns - 1));
        int rows = Mathf.CeilToInt(wave.count / (float)columns);
        float availableHeight = -padding - BattleConst.EnemyActivityBottom - 2f * halfHeight;
        float diagonalSpan = wave.formationType == EnemyFormationType.DIAGONAL
            ? (columns - 1) * BattleConst.EnemyDiagonalFormationVerticalGap : 0f;
        float rowStep = rows <= 1 ? 0f : Mathf.Min(2f * halfHeight + BattleConst.EnemyFormationRowGap,
            (availableHeight - diagonalSpan) / (rows - 1));
        if (availableWidth < 0f || availableHeight < diagonalSpan || rows > 1 && rowStep < 2f * halfHeight) {
            throw new System.InvalidOperationException($"敌机编队无法容纳于上半屏：enemy={wave.enemy.id}, count={wave.count}");
        }
        offsets = new Vector2[wave.count];
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float extentX = 0f;
        for (int i = 0; i < wave.count; i++) {
            int row = i / columns;
            int rowCount = Mathf.Min(columns, wave.count - row * columns);
            float column = i % columns - (rowCount - 1) * 0.5f;
            float y = row * rowStep;
            if (wave.formationType == EnemyFormationType.DIAGONAL) {
                y -= column * BattleConst.EnemyDiagonalFormationVerticalGap * direction;
            }
            offsets[i] = new Vector2(column * spacing, y);
            extentX = Mathf.Max(extentX, Mathf.Abs(offsets[i].x));
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }
        float minX = extentX + halfWidth + padding;
        float maxX = BattleConst.BattleViewportWidth - minX;
        float lowerY = BattleConst.EnemyActivityBottom + halfHeight - minY;
        float upperY = -padding - halfHeight - maxY;
        minCenter = new Vector2(minX, lowerY);
        maxCenter = new Vector2(maxX, upperY);
        outsideTopY = halfHeight + padding - minY;
    }
}
