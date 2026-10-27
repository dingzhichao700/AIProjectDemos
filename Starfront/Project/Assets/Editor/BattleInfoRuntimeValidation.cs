using System;
using System.IO;
using System.Linq;
using cfg;
using UnityEngine;

/// <summary>
/// HUD业务接口及场景计时边界的聚焦验证。
/// </summary>
/// <remarks>在真实对战开始前运行隔离模型，避免推进玩家当前局。</remarks>
public static class BattleInfoRuntimeValidation {

    public static void ValidateLoadedState() {
        var model = new BattleSceneModel(CfgManager.tables.BattleSceneObj.Get(BattleConst.InitialSceneId), CfgManager.tables.BattleSceneCellObj.DataList);
        float scale = RookieEngine.sceneTimer.scale;
        try {
            model.InitializeBattle(CfgManager.tables);
            Require(BattleConst.DurationSeconds == 180 && BattleConst.FriendlyName == "玩家" && BattleConst.EnemyName == "敌方AI", "three configuration constants");
            Require(model.GetTerritoryCount(BattleSceneCamp.FRIENDLY) == 73 && model.GetTerritoryCount(BattleSceneCamp.ENEMY) == 73, "all owned walkable cells count before unlock");
            Require(model.cells.Values.Count(cell => cell.unlocked) == 2, "only bases unlocked");
            Require(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0, "buildings do not occupy population");
            var unitConfig = CfgManager.tables.UnitObj.Get(1101);
            var friendly = new BattleUnitVO(1000000, unitConfig, BattleSceneCamp.FRIENDLY, Vector2.zero);
            var enemy = new BattleUnitVO(1000001, unitConfig, BattleSceneCamp.ENEMY, Vector2.zero);
            model.AddElement(friendly);
            model.AddElement(enemy);
            Require(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 1 && model.GetUnitCount(BattleSceneCamp.ENEMY) == 1, "population counts actual units per camp");
            friendly.Destroy();
            Require(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0, "destroyed unit frees population");
            model.StartTimeFlow();
            RookieEngine.sceneTimer.scale = 1;
            RookieEngine.playerTimer.SyncTime(1000);
            RookieEngine.enemyTimer.SyncTime(1000);
            Require(model.displayRemainingSeconds == 180, "camp timers do not change scene clock");
            RookieEngine.sceneTimer.SyncTime(1000);
            Require(model.displayRemainingSeconds == 179, "scene timer advances countdown");
            RookieEngine.sceneTimer.scale = 0;
            RookieEngine.sceneTimer.SyncTime(10000);
            Require(model.displayRemainingSeconds == 179, "scene pause holds countdown");
            RookieEngine.sceneTimer.scale = 1;
            int expired = 0;
            model.TimeExpired += source => expired++;
            RookieEngine.sceneTimer.SyncTime(179000);
            RookieEngine.sceneTimer.SyncTime(1000);
            Require(model.remainingSeconds == 0 && model.expired && expired == 1, "expiry clamps to zero and fires exactly once");
            model.StopTimeFlow();
            File.WriteAllText("Library/battle-info-logic-validation.txt", "passed: configured 180 seconds and names; ownership excludes neutral obstacles and ignores unlock; actual live-unit count; independent camp timers; scene pause; zero clamp and single expiry");
        } finally {
            model.Dispose();
            RookieEngine.sceneTimer.scale = scale;
        }
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Battle HUD validation: " + message);
        }
    }

}
