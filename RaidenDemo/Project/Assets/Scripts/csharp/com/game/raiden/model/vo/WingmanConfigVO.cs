using cfg;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 僚机战斗配置
/// </summary>
/// <remarks>
/// 保存单种僚机的外观、编队槽位、跟随参数和发射器配置。
/// </remarks>
public sealed class WingmanConfigVO {

    public readonly int id;
    public readonly string code;
    public readonly string displayName;
    public readonly Vector2 displaySize;
    public readonly int maxCount;
    public readonly WingmanFormationType formationType;
    public readonly IReadOnlyList<Vector2> formationOffsets;
    public readonly float followSpeed;
    public readonly string appearancePath;
    public readonly IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers;

    public WingmanConfigVO(int id, string code, string displayName, Vector2 displaySize, int maxCount, WingmanFormationType formationType, IReadOnlyList<Vector2> formationOffsets, float followSpeed, string appearancePath, IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers) {
        this.id = id;
        this.code = code;
        this.displayName = displayName;
        this.displaySize = displaySize;
        this.maxCount = maxCount;
        this.formationType = formationType;
        this.formationOffsets = formationOffsets;
        this.followSpeed = followSpeed;
        this.appearancePath = appearancePath;
        this.bulletLaunchers = bulletLaunchers;
    }
}
