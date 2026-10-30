using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>仅调整当前存活单位的高度，供地面/离地阴影验收，不修改兵种配置。</summary>
public static class BattleShadowGM {
    public static void Init() {
        GMControl.ins.RegisterCommand("unitAltitude", Apply);
    }
    public static void Shutdown() {
        GMControl.ins.UnregisterCommand("unitAltitude");
    }
    private static bool Apply(IReadOnlyList<string> values) {
        if (values.Count != 1 || !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float height) || float.IsNaN(height) || float.IsInfinity(height) || height < 0) {
            throw new ArgumentException("请输入非负离地高度（场景像素）");
        }
        var model = BattleControl.ins.model;
        if (model == null) {
            throw new InvalidOperationException("请先进入战斗并产兵");
        }
        int count = 0;
        foreach (var unit in model.elements.Values.OfType<BattleUnitVO>()) {
            if (BattleControl.ins.SetUnitAltitude(unit.id, height)) {
                count++;
            }
        }
        UnityEngine.Debug.Log("已设置当前单位高度：" + height + "，数量：" + count + "；新出生单位仍用兵种表默认值");
        PanelMgr.ins.ClosePanelByType(MainUIEnum.GM_PANEL);
        return count > 0;
    }
}
