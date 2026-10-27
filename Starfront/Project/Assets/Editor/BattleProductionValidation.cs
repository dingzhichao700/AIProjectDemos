using System;
using System.Linq;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>验证圆环只读绑定、定位、离屏、重用及销毁，不改正式对局状态。</summary>
public static class BattleProductionValidation {
    public static void Validate(BattleSceneView view) {
        var tables = CfgManager.tables;
        var model = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
        model.InitializeBattle(tables);
        var lod = new GameObject("ProductionValidation", typeof(RectTransform)).GetComponent<RectTransform>();
        lod.SetParent(view.lod, false);
        var presentation = new BattleProductionView(model, view.sceneRoot, lod);
        try {
            var building = model.buildings.Values.Single(item => item.owner.camp == BattleSceneCamp.FRIENDLY);
            var rect = new Rect(0, 0, model.config.Width, model.config.Height);
            presentation.Refresh(rect, view.zoom);
            Require(presentation.visibleCount == 1, "friendly producer only");
            var fill = lod.Find("production_" + building.id + "/fill").GetComponent<Image>();
            Require(fill.fillAmount == 0 && fill.fillMethod == Image.FillMethod.Radial360 && fill.fillClockwise, "initial radial progress");
            building.OnTimeUpdate(2.5f);
            presentation.Refresh(rect, view.zoom);
            Require(Mathf.Abs(fill.fillAmount - 0.5f) < 0.001f, "half cycle reads logical progress");
            building.OnTimeUpdate(2.5f);
            presentation.Refresh(rect, view.zoom);
            Require(fill.fillAmount == 0 && building.owner.currency == 60, "payout resets ring");
            presentation.Refresh(new Rect(-10000, -10000, 1, 1), view.zoom);
            Require(presentation.visibleCount == 0 && !fill.gameObject.activeInHierarchy, "offscreen ring recycled");
            building.OnTimeUpdate(1.5f);
            presentation.Refresh(rect, view.zoom);
            Require(presentation.visibleCount == 1 && Mathf.Abs(fill.fillAmount - 0.3f) < 0.001f, "reused ring restores current progress");
            building.Destroy();
            presentation.Refresh(rect, view.zoom);
            Require(presentation.visibleCount == 0, "destroyed producer removes ring");
            Require(lod.GetComponentsInChildren<Image>(true).All(image => !image.raycastTarget), "rings do not block input");
        } finally {
            presentation.Dispose();
            Require(lod.GetComponentsInChildren<Image>(true).All(image => image.sprite == null), "dispose clears sprite references");
            UnityEngine.Object.Destroy(lod.gameObject);
            model.Dispose();
        }
    }

    public static void ValidateAlignment(BattleSceneView view, BattleSceneModel model) {
        var rect = new Rect(view.cameraCenter - view.viewport.rect.size / (2 * view.zoom), view.viewport.rect.size / view.zoom);
        foreach (var building in model.buildings.Values) {
            Vector2 position = building.position + new Vector2(0, BattleConst.CellHeight / 3f - model.config.GroundThickness);
            bool expected = !building.destroyed && building.owner.camp == BattleSceneCamp.FRIENDLY && new Rect(position - Vector2.one * 12, Vector2.one * 24).Overlaps(rect, true);
            var ring = view.lod.Find("production_" + building.id);
            Require(expected == (ring != null && ring.gameObject.activeSelf), "viewport and camp visibility");
            if (expected) {
                Vector3 world = view.sceneRoot.TransformPoint(new Vector3(position.x, -position.y, 0));
                Require(Vector3.Distance(world, ring.position) < 0.05f, "pan and zoom anchor alignment");
                Require(Mathf.Abs(ring.localScale.x - view.zoom) < 0.001f, "zoom applied once");
                var fill = ring.Find("fill").GetComponent<Image>();
                Require(Mathf.Abs(fill.fillAmount - (float)building.productionProgress) < 0.001f, "real cycle binding");
            }
        }
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Production ring validation: " + message);
        }
    }
}
