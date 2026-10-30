using cfg;
using UnityEngine;

/// <summary>场景X右Y下方向约定；逻辑与表现共用，左向镜像源朝向。</summary>
public static class BattleAim {
    public static UnitAppearanceDirection ResolveDirection(Vector2 heading) {
        float x = Mathf.Abs(heading.x);
        float y = Mathf.Abs(heading.y);
        float threshold = Mathf.Tan(Mathf.PI / 8);
        if (x <= y * threshold) {
            return heading.y < 0 ? UnitAppearanceDirection.UP : UnitAppearanceDirection.DOWN;
        }
        if (y <= x * threshold) {
            return UnitAppearanceDirection.RIGHT;
        }
        return heading.y < 0 ? UnitAppearanceDirection.UP_RIGHT : UnitAppearanceDirection.DOWN_RIGHT;
    }
}
