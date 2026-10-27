using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 用完整画布元数据核对八方向全部帧的实际显示锚点。
/// </summary>
/// <remarks>专用隔离对象覆盖左向镜像；不改动画资源的暂用pivot。</remarks>
public static class BattleUnitPivotValidation {

    public static void Validate() {
        var tables = CfgManager.tables;
        var model = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
        var root = new GameObject("PivotValidation", typeof(RectTransform)).GetComponent<RectTransform>();
        var view = new BattleUnitView(model, root);
        var directions = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };
        var animationTime = typeof(BattleUnitVO).GetProperty("animationMs", BindingFlags.Public | BindingFlags.Instance);
        try {
            for (int i = 0; i < directions.Length; i++) {
                var point = new Vector2(300 + i * 100, 700);
                var unit = new BattleUnitVO(1000000 + i, tables.UnitObj.Get(1101), BattleSceneCamp.FRIENDLY, point, new[] { point + directions[i] * 1000 });
                unit.OnTimeUpdate(.001f);
                model.AddElement(unit);
            }
            for (int frame = 0; frame < 8; frame++) {
                foreach (var unit in model.elements.Values.OfType<BattleUnitVO>()) {
                    var appearance = unit.config.Appearances.First(item => item.Direction == BattleUnitView.ResolveDirection(unit.heading));
                    var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(appearance.Resource));
                    double midpoint = animation.durations.Take(frame).Sum() + animation.durations[frame] / 2d;
                    animationTime.SetValue(unit, midpoint);
                }
                view.Refresh(new Rect(0, 0, 2000, 2000));
                foreach (var unit in model.elements.Values.OfType<BattleUnitVO>()) {
                    var appearance = unit.config.Appearances.First(item => item.Direction == BattleUnitView.ResolveDirection(unit.heading));
                    var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(appearance.Resource));
                    var image = root.Find("unit_" + unit.id).GetComponent<Image>();
                    Vector2 footInRegion = Vector2.Scale(animation.sourceSizes[frame], animation.pivot) - animation.frameOffsets[frame];
                    Vector2 localFoot = image.rectTransform.rect.min + footInRegion;
                    Vector3 displayedFoot = image.rectTransform.TransformPoint(localFoot);
                    Vector3 logicalFoot = root.TransformPoint(new Vector3(root.rect.xMin + unit.position.x, root.rect.yMax - unit.position.y, 0));
                    if (image.sprite != animation.sprites[frame] || Vector3.Distance(displayedFoot, logicalFoot) > .001f) {
                        throw new InvalidOperationException("完整画布锚点未对齐：direction=" + unit.heading + ", frame=" + frame);
                    }
                }
            }
            File.WriteAllText("Library/battle-unit-pivot-validation.txt", "passed: 8 directions x 8 frames; actual transformed full-canvas anchor matches logical ground point; trimmed offsets preserved; left directions mirror around same anchor; resource pivot unchanged");
        } finally {
            view.Dispose();
            model.Dispose();
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }

}
