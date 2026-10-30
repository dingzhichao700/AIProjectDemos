using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class BattleCardRevealValidation {
    private static void Check(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Card reveal: " + message);
        }
    }
    private static object Call(object target, string name, params object[] args) {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
    public static void Validate(TMP_FontAsset font) {
        var tables = CfgManager.tables;
        var scene = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
        scene.InitializeBattle(tables, 7);
        var world = new GameObject("CardRevealValidation", typeof(RectTransform)).GetComponent<RectTransform>();
        var root = new GameObject("Scene", typeof(RectTransform)).GetComponent<RectTransform>();
        var lod = new GameObject("LOD", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(world, false); lod.SetParent(world, false);
        var view = new BattleCardRevealView(scene, root, lod, font);
        float saved = RookieEngine.playerTimer.scale;
        var visible = new Rect(0, 0, 2000, 2000);
        try {
            view.Refresh(visible, 1);
            var cell = scene.cells.Values.First(c => c.camp == BattleSceneCamp.FRIENDLY && scene.unlocks.IsRevealed(c) && c.unlockConfig.CollectorWeight > 0);
            int events = 0;
            Action<BattleUnlockResultVO> observer = result => {
                events++;
                Check(scene.camps[result.camp].currency == 0 && result.cardId == cell.unlockedCardId && scene.buildings.Values.Any(b => b.cell == cell), "event after payment, result and building commit");
            };
            scene.UnlockCompleted += observer;
            object[] args = { cell.id, BattleSceneCamp.FRIENDLY, null };
            Check((bool)Call(scene.unlocks, "TryUnlock", args), "actual unlock");
            Check(events == 1 && view.visibleCount == 1, "one result, one visible card");
            var card = (RectTransform)lod.Find("UnlockCardResults/unlockCard_" + cell.id);
            Check(card.sizeDelta == new Vector2(72, 88) && card.pivot == new Vector2(.5f, 0), "configured size and bottom pivot");
            Check(card.Find("CardArt").GetComponent<Image>().sprite == ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(tables.CardObj.Get(cell.unlockedCardId.Value).Image)), "actual drawn card image");
            Check(card.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), "input passes through");
            var building = scene.buildings.Values.Single(b => b.cell == cell);
            building.OnTimeUpdate(1);
            Check(building.productionProgress > 0 && view.visibleCount == 1, "production advances during card display");
            Check(!(bool)Call(scene.unlocks, "TryUnlock", args) && events == 1, "duplicate request cannot replay");
            RookieEngine.playerTimer.scale = 0;
            RookieEngine.playerTimer.SyncTime(500);
            view.Refresh(visible, 1);
            Check(Math.Abs(card.localScale.x - .8f) < .001f, "pause freezes reveal");
            RookieEngine.playerTimer.scale = 1;
            RookieEngine.playerTimer.SyncTime(150);
            root.localScale = Vector3.one * 2;
            root.localPosition = new Vector3(23, -31, 0);
            view.Refresh(visible, 2);
            Vector3 expected = root.TransformPoint(new Vector3(cell.position.x, -cell.position.y + scene.config.GroundThickness, 0));
            Check(Vector3.Distance(card.position, expected) < .001f && Math.Abs(card.localScale.x - 2) < .001f, "pan and zoom once at ground anchor");
            RookieEngine.playerTimer.SyncTime(725);
            view.Refresh(visible, 2);
            Check(Math.Abs(card.GetComponent<CanvasGroup>().alpha - .5f) < .02f, "fade at875ms");
            RookieEngine.playerTimer.SyncTime(125);
            view.Refresh(visible, 2);
            Check(view.visibleCount == 0 && card.Find("CardArt").GetComponent<Image>().sprite == null, "one second recycle clears sprite");
            view.Refresh(visible, 1);
            Check(view.visibleCount == 0, "refresh never replays old unlock");
            scene.UnlockCompleted -= observer;
            // 敌方也显示；视窗离开后回收，返回不补播。
            var enemyCell = scene.cells.Values.First(c => c.camp == BattleSceneCamp.ENEMY && scene.unlocks.IsRevealed(c) && c.unlockConfig.CollectorWeight > 0);
            object[] enemyArgs = { enemyCell.id, BattleSceneCamp.ENEMY, null };
            Check((bool)Call(scene.unlocks, "TryUnlock", enemyArgs) && view.visibleCount == 1, "enemy result also displays using recycled visual");
            view.Refresh(new Rect(-10000, -10000, 1, 1), 1);
            view.Refresh(visible, 1);
            Check(view.visibleCount == 0, "leaving viewport discards without replay");
            Call(scene.camps[BattleSceneCamp.FRIENDLY], "AddCurrency", 10000L);
            var hidden = scene.cells.Values.First(c => scene.unlocks.CanUnlock(c.id, BattleSceneCamp.FRIENDLY, out _));
            view.Refresh(new Rect(-10000, -10000, 1, 1), 1);
            object[] hiddenArgs = { hidden.id, BattleSceneCamp.FRIENDLY, null };
            Check((bool)Call(scene.unlocks, "TryUnlock", hiddenArgs), "offscreen real unlock still completes");
            view.Refresh(visible, 1);
            Check(view.visibleCount == 0, "offscreen event not queued");
        } finally {
            RookieEngine.playerTimer.scale = saved;
            view.Dispose();
            Check(world.GetComponentsInChildren<Image>(true).All(i => i.sprite == null), "dispose clears card refs");
            scene.Dispose();
            UnityEngine.Object.Destroy(world.gameObject);
        }
        File.WriteAllText("Library/battle-card-reveal-validation.txt", "passed: real paid result; building immediate and producing; actual card; one-shot/duplicate; 72x88 bottom pivot; pause; pan/zoom; 150/600/250 timeline; fade; offscreen discard; teardown");
    }
}
