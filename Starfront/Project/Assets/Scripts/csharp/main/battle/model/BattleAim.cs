using System.Collections.Generic;
using cfg;
using UnityEngine;
public static class BattleAim
{
    public static IReadOnlyList<UnitAppearanceDirection> SourceDirections { get; } =
        System.Array.AsReadOnly(new[] { UnitAppearanceDirection.UP_RIGHT, UnitAppearanceDirection.DOWN_RIGHT });
    public static UnitAppearanceDirection ResolveDirection(Vector2 heading)
    {
        return heading.y < 0 ? UnitAppearanceDirection.UP_RIGHT : UnitAppearanceDirection.DOWN_RIGHT;
    }
    public static Vector2 UpdateDisplayHeading(Vector2 previous, Vector2 heading)
    {
        if (float.IsNaN(heading.x) || float.IsNaN(heading.y) || float.IsInfinity(heading.x) || float.IsInfinity(heading.y))
        {
            return previous;
        }
        return new Vector2(heading.x == 0 ? previous.x : Mathf.Sign(heading.x),
            heading.y == 0 ? previous.y : Mathf.Sign(heading.y));
    }
}
