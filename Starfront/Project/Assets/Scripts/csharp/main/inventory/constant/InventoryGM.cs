using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 背包业务拥有的GM查询与发放指令。
/// </summary>
/// <remarks>跟随正式主界面注册和注销，执行时取得当前共享玩家会话。</remarks>
public static class InventoryGM {

    private static bool registered;

    public static void Init() {
        if (registered) {
            return;
        }
        GMControl.ins.RegisterCommand("inventoryGrant", Grant);
        GMControl.ins.RegisterCommand("inventoryBalance", Balance);
        GMControl.ins.RegisterCommand("inventoryClear", Clear);
        registered = true;
    }

    public static void Shutdown() {
        if (!registered) {
            return;
        }
        GMControl.ins.UnregisterCommand("inventoryGrant");
        GMControl.ins.UnregisterCommand("inventoryBalance");
        GMControl.ins.UnregisterCommand("inventoryClear");
        registered = false;
    }

    private static bool Clear(IReadOnlyList<string> values) {
        if (values.Count != 0) {
            throw new ArgumentException("清空背包不需要参数");
        }
        if (!InventoryControl.ins.TryClear(out string reason)) {
            Debug.LogError(reason);
            return false;
        }
        Debug.Log("背包已清空，卡牌拥有状态和等级保持不变");
        return true;
    }

    private static bool Grant(IReadOnlyList<string> values) {
        if (values.Count != 2 || !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || !long.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long count) || count <= 0) {
            throw new ArgumentException("格式：道具ID,正整数数量");
        }
        Debug.Log("[GMGrant] request item=" + id + ", count=" + count);
        if (!InventoryControl.ins.TryGrantItem(id, count, out string reason)) {
            GMControl.ins.ReportResult(reason);
            Debug.LogError(reason);
            return false;
        }
        long total = InventoryControl.ins.model.GetQuantity(id);
        GMControl.ins.ReportResult(CfgManager.tables.ItemObj.Get(id).Name + " (" + id + ") +" + count + "，现有 " + total);
        PrintBalance(id);
        return true;
    }

    private static bool Balance(IReadOnlyList<string> values) {
        if (values.Count != 1 || !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) {
            throw new ArgumentException("格式：道具ID");
        }
        PrintBalance(id);
        return true;
    }

    private static void PrintBalance(int id) {
        long quantity = InventoryControl.ins.model.GetQuantity(id);
        Debug.Log("背包：" + CfgManager.tables.ItemObj.Get(id).Name + " (" + id + ") = " + quantity);
    }

}
