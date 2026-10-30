using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>正式死亡配置、资源登记与隔离致死链路回归。仅显式请求时运行。</summary>
[InitializeOnLoad]
public static class BattleUnitDeathIntegrationValidation {
    const string Key = "Starfront.FormalDeathValidation";
    const string Pending = "Library/infantry-death-integration-request.json";
    const string ResultPath = "Library/infantry-death-integration-result.json";
    const string Root = "Assets/Art/frameAnimation/scene/Colonial/Infantry/";
    static readonly string[] Directions = { "Up", "UpRight", "Right", "DownRight", "Down" };
    [Serializable] class Request { public string requestId; public int processId; public string action; }
    [Serializable] class Result { public string requestId; public string status; public string details; }
    static double deadline;
    static BattleUnitDeathIntegrationValidation() { EditorApplication.update += Poll; }
    static void Report(string status, string details) {
        File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { requestId = SessionState.GetString(Key + ".id", ""), status = status, details = details }, true));
    }
    static void Poll() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try {
            if (File.Exists(Pending) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                var req = JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
                File.Delete(Pending);
                Check(req != null && !string.IsNullOrEmpty(req.requestId) && req.processId == System.Diagnostics.Process.GetCurrentProcess().Id, "request identity");
                SessionState.SetString(Key + ".id", req.requestId);
                if (req.action == "setup") {
                    Register();
                    AddressablesAutoBuilder.BuildPlayerContent();
                    Report("setup-passed", "10 formal DEATH assets moved with GUIDs preserved, registered in scene; local Addressables build passed. Source-table generation performed separately.");
                } else if (req.action == "validate") {
                    SessionState.SetBool(Key, true);
                    deadline = EditorApplication.timeSinceStartup + 120;
                    EditorApplication.isPlaying = true;
                } else throw new InvalidOperationException("Unknown death validation action");
            }
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
            Check(EditorApplication.timeSinceStartup < deadline, "startup timeout");
            Application.runInBackground = true;
            if (PanelMgr.ins == null) return;
            var login = PanelMgr.ins.GetPanel(UIEnum.LOGIN_PANEL) as LoginPanel;
            if (login != null && login.isOpened) {
                typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
                return;
            }
            var main = PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) as MainPanel;
            if (main != null && main.isOpened && BattleControl.ins.model == null && !BattleControl.ins.preparing) {
                main.GetComponent<MainPanelLayout>().battleButton.GetComponent<Button>().onClick.Invoke();
                return;
            }
            var panel = PanelMgr.ins.GetPanel(MainUIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
            if (panel == null || !panel.isOpened) return;
            SessionState.SetBool(Key, false);
            Validate();
            BattleUnitActionValidation.Validate();
            Report("passed", "Formal config loaded through Login/Addressables; 4 units x 20 appearances; 21 animations preloaded; 8 headings x 18 death frames x heights 0/40/80; death resource and mirror freeze; population immediate; hold/fade; offscreen pool restore; shadow-only culling; pool/dispose cleanup; cached sprites retained. Original 528 living-action frame regression also passed. No Editor preview adapter.");
        } catch (Exception e) {
            SessionState.SetBool(Key, false);
            Report("failed", e.ToString());
            Debug.LogException(e);
        }
    }
    static void Register() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Check(settings != null && settings.FindGroup("scene") != null, "scene group");
        foreach (string direction in Directions) foreach (string ext in new[] { ".png", ".json" }) {
            string target = Root + "Death_" + direction + ext;
            string source = Root + "DeathReview/Death_" + direction + ext;
            if (AssetDatabase.LoadMainAssetAtPath(target) == null) {
                Check(AssetDatabase.LoadMainAssetAtPath(source) != null, "approved source " + source);
                string error = AssetDatabase.MoveAsset(source, target);
                Check(string.IsNullOrEmpty(error), error);
            }
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(target), settings.FindGroup("scene"));
            entry.SetAddress(target); entry.SetLabel("Colonial", true, true);
            Check(AssetDatabase.LoadMainAssetAtPath(target) != null, "formal asset " + target);
        }
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(settings.FindGroup("scene"));
        AssetDatabase.SaveAssets();
    }
    static object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Formal death: " + message); }
    static float Clock(BattleSceneCamp camp) => camp == BattleSceneCamp.FRIENDLY ? RookieEngine.playerTimer.curTime : RookieEngine.enemyTimer.curTime;

    public static void Validate() {
        Check(typeof(BattleUnitDeathIntegrationValidation).Assembly.GetType("InfantryDeathPreview") == null, "legacy preview retired");
        Check(BattleConst.DeathHoldMs == 2000 && BattleConst.DeathFadeMs == 300, "configured approved timing");
        foreach (var config in CfgManager.tables.UnitObj.DataList) {
            Check(config.Appearances.Count == 20 && config.Appearances.Count(a => a.Action == UnitActionType.DEATH && !a.Loop) == 5, "20 explicit appearances " + config.Id);
        }
        Check(BattleSceneResources.CollectAnimations(BattleControl.ins.model).Count == 21, "21 preloaded animation dependencies");
        var scene = new BattleSceneModel(CfgManager.tables.BattleSceneObj.Get(1001), CfgManager.tables.BattleSceneCellObj.DataList);
        scene.InitializeBattle(CfgManager.tables, 7);
        var root = new GameObject("FormalDeathFixture", typeof(RectTransform)).GetComponent<RectTransform>();
        var shadowRoot = new GameObject("FormalDeathShadowFixture", typeof(RectTransform)).GetComponent<RectTransform>();
        var view = new BattleUnitView(scene, root, shadowRoot);
        var death = (BattleUnitDeathView)Get(view, "deaths");
        var entries = (IDictionary)Get(death, "entries");
        var full = new Rect(0, 0, 2000, 2000);
        var empty = new Rect(-10000, -10000, 1, 1);
        var headings = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };
        try {
            for (int i = 0; i < headings.Length; i++) {
                var camp = i % 2 == 0 ? BattleSceneCamp.FRIENDLY : BattleSceneCamp.ENEMY;
                var unit = new BattleUnitVO(990000 + i, CfgManager.tables.UnitObj.Get(1101), camp, new Vector2(500, 500));
                Call(unit, "Face", unit.position + headings[i]); scene.AddElement(unit);
                var appearance = unit.config.Appearances.Single(a => a.Action == UnitActionType.DEATH && a.Direction == BattleAim.ResolveDirection(headings[i]));
                var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(appearance.Resource));
                Check(animation != null && animation.sprites.Length == 18, "configured death preloaded");
                int population = scene.GetUnitCount(camp);
                var sourceCamp = camp == BattleSceneCamp.FRIENDLY ? BattleSceneCamp.ENEMY : BattleSceneCamp.FRIENDLY;
                Call(scene.combat, "ApplyDamage", 1L, sourceCamp, unit, 100d);
                Check(unit.hp == 0 && unit.destroyed && !scene.elements.ContainsKey(unit.id) && scene.GetUnitCount(camp) == population - 1 && death.count == 1, "death handoff and immediate population");
                var entry = entries[unit.id];
                Check(Get(entry, "animation") == animation && (bool)Get(entry, "mirror") == (headings[i].x < 0), "frozen source and mirror");
                Call(unit, "Face", unit.position - headings[i]);
                Check(Get(entry, "animation") == animation, "destroyed VO cannot change corpse animation");
                double elapsed = 0;
                for (int frame = 0; frame < animation.sprites.Length; frame++) {
                    foreach (float height in new[] { 0f, 40f, 80f }) {
                        view.Refresh(empty); // 为每种初始高度重新取得表现；正式死亡快照的高度不再变化。
                        Set(entry, "altitude", height); Set(entry, "started", Clock(camp) - (float)(elapsed + animation.durations[frame] / 2d));
                        view.Refresh(full);
                        var visual = Get(entry, "visual"); var image = (Image)Get(visual, "body"); var shadow = (BattleUnitShadowGraphic)Get(visual, "shadow"); var corpseRoot = (RectTransform)Get(visual, "root");
                        Check(image.sprite == animation.sprites[frame] && shadow.mainTexture == image.sprite.texture, "actual death frame and shared shadow");
                        Vector2 foot = Vector2.Scale(animation.sourceSizes[frame], animation.pivot) - animation.frameOffsets[frame];
                        Vector3 actual = image.rectTransform.TransformPoint(image.rectTransform.rect.min + foot);
                        Vector2 ground = (Vector2)Get(entry, "ground");
                        Vector3 expected = root.TransformPoint(new Vector3(root.rect.xMin + ground.x, root.rect.yMax - ground.y + height, 0));
                        Check(Vector3.Distance(actual, expected) < .001f && corpseRoot.anchoredPosition == new Vector2(ground.x, -ground.y), "trim/mirror anchor and ground depth heading=" + i + " frame=" + frame + " height=" + height + " actual=" + actual + " expected=" + expected);
                        Check(!image.raycastTarget && !shadow.raycastTarget && shadow.transform.parent == shadowRoot, "input and independent shadow layer");
                    }
                    elapsed += animation.durations[frame];
                }
                view.Refresh(empty);
                Set(entry, "altitude", 0f); Set(entry, "started", Clock(camp) - animation.totalDuration - 1000f); view.Refresh(full);
                var held = Get(entry, "visual");
                Check(((Image)Get(held, "body")).sprite == animation.sprites.Last(), "nonloop last frame hold");
                view.Refresh(empty);
                Check(death.count == 1 && death.visibleCount == 0 && Get(entry, "visual") == null && ((Image)Get(held, "body")).sprite == null, "offscreen pool clears references without losing time");
                view.Refresh(full);
                Check(((Image)Get(Get(entry, "visual"), "body")).sprite == animation.sprites.Last(), "pooled resume retains last frame");
                Set(entry, "started", Clock(camp) - animation.totalDuration - 2150f); view.Refresh(full);
                Check(Mathf.Abs(((Image)Get(Get(entry, "visual"), "body")).color.a - .5f) < .01f, "configured fade");
                Set(entry, "altitude", 4000f); Set(entry, "started", Clock(camp) - 100f); view.Refresh(full);
                Check(death.visibleCount == 0, "both body and shadow offscreen");
                Vector2 g = (Vector2)Get(entry, "ground"); Vector2 offset = BattleUnitShadowGraphic.Direction(BattleConst.ShadowAngle, BattleConst.ShadowLength) * 4000;
                view.Refresh(new Rect(g.x + offset.x - 100, g.y - offset.y - 100, 200, 200));
                Check(death.visibleCount == 1, "shadow-only culling retains corpse");
                Set(entry, "started", Clock(camp) - animation.totalDuration - BattleConst.DeathHoldMs - BattleConst.DeathFadeMs - 1f); view.Refresh(full);
                Check(death.count == 0 && death.visibleCount == 0, "expires and recovers both visuals");
            }
        } finally {
            view.Dispose();
            Check(death.count == 0 && root.GetComponentsInChildren<Image>(true).All(v => v.sprite == null), "dispose clears references");
            scene.Dispose(); UnityEngine.Object.Destroy(root.gameObject); UnityEngine.Object.Destroy(shadowRoot.gameObject);
        }
        foreach (var a in CfgManager.tables.UnitObj.Get(1101).Appearances.Where(a => a.Action == UnitActionType.DEATH))
            Check(FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(a.Resource)).sprites.All(s => s != null), "shared cached sprites retained");
    }
}

