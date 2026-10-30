using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public static class BattleProjectileValidation {
    private static void Check(bool value, string label) {
        if (!value) {
            throw new InvalidOperationException("Projectile validation: " + label);
        }
    }
    private static object Call(object target, string method, params object[] args) {
        return target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
    private static BattleSceneModel Scene() {
        var t = CfgManager.tables;
        var scene = new BattleSceneModel(t.BattleSceneObj.Get(1001), t.BattleSceneCellObj.DataList);
        scene.InitializeBattle(t, 7);
        return scene;
    }
    private static BattleUnitVO Unit(BattleSceneModel scene, long id, BattleSceneCamp camp, Vector2 point) {
        var unit = new BattleUnitVO(id, CfgManager.tables.UnitObj.Get(1101), camp, point);
        scene.AddElement(unit);
        return unit;
    }
    private static void Advance(BattleSceneModel scene, float seconds) {
        Call(scene.combat, "Advance", BattleSceneCamp.FRIENDLY, seconds);
    }
    private static void Move(BattleUnitVO unit, Vector2 point) {
        typeof(SceneElementVO).GetProperty("position").SetValue(unit, point);
    }
    public static void Validate() {
        Check(CfgManager.tables.UnitObj.DataList.Where(u => u.Id >= 1101 && u.Id <= 1104).All(u => u.SearchRange == 240), "search range 240");
        Check(CfgManager.tables.SkillObj.Get(1101).CastRange == 180, "shooting range 180");
        foreach (int mode in Enumerable.Range(0, 6)) {
            var scene = Scene();
            try {
                var origin = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY).position;
                var a = Unit(scene, 300001, BattleSceneCamp.FRIENDLY, origin);
                var b = Unit(scene, 300002, BattleSceneCamp.ENEMY, scene.cells.Values.First(c => c.groundPassable && Vector2.Distance(c.position, origin) > 140 && Vector2.Distance(c.position, origin) < 180).position);
                if (mode == 0) {
                    Call(a, "SetAltitude", 40f);
                    Call(b, "SetAltitude", 80f);
                }
                int hits = 0;
                Vector2 impactPosition = Vector2.zero;
                scene.combat.projectiles.Hit += hit => impactPosition = hit.position;
                scene.combat.projectiles.Hit += _ => hits++;
                Advance(scene, .01f);
                Check(a.activeCast != null && a.targetId == b.id && !a.moving && b.hp == 100, "ranged cast starts before closing distance");
                Advance(scene, .081f);
                Check(scene.combat.projectiles.projectiles.Count == 1 && b.hp == 100, "windup launches without damage: bullets=" + scene.combat.projectiles.projectiles.Count + ", hp=" + b.hp + ", target=" + a.targetId + ", stage=" + a.activeCast?.stage);
                if (mode == 0) {
                    Check(((Vector2)typeof(BattleProjectileVO).GetField("launchOffset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scene.combat.projectiles.projectiles.Values.Single())).y < -40, "elevated muzzle offset");
                    var blocker = Unit(scene, 300003, BattleSceneCamp.ENEMY, origin + Vector2.right * 60);
                    Advance(scene, .1f);
                    Check(b.hp == 100 && blocker.hp == 100, "flight and no interception");
                    Advance(scene, .2f);
                    Check(b.hp == 80 && blocker.hp == 100 && hits == 1 && scene.combat.projectiles.projectiles.Count == 0, "segment sweep hits exactly once");
                    Check(Vector2.Distance(impactPosition, b.position + new Vector2(b.config.HitOffset.X, b.config.HitOffset.Y - 80)) < .001f, "impact follows target altitude while logical damage unchanged");
                } else if (mode == 1) {
                    Call(scene.combat, "Damage", b, a, 100d);
                    Advance(scene, .4f);
                    Check(b.hp == 80 && hits == 1, "bullet survives shooter death");
                } else if (mode == 2) {
                    scene.RemoveElement(b.id);
                    Advance(scene, .4f);
                    Check(hits == 0 && scene.combat.projectiles.projectiles.Count == 0, "lost target destroys without retarget");
                } else if (mode == 3) {
                    Move(b, origin + Vector2.right * 350);
                    Advance(scene, .6f);
                    Check(b.hp == 80 && hits == 1, "tracks target beyond cast/search range");
                } else if (mode == 4) {
                    Move(b, origin + Vector2.right * 5000);
                    Advance(scene, 3f);
                    Check(b.hp == 100 && hits == 0 && scene.combat.projectiles.projectiles.Count == 0, "lifetime expires before unreachable hit");
                } else {
                    Advance(scene, 4.5f);
                    Check(b.destroyed && hits == 5 && scene.combat.projectiles.projectiles.Count == 0, "long frame gives each new bullet only elapsed time since firing");
                }
            } finally {
                scene.Dispose();
                Check(scene.combat == null || scene.combat.projectiles.projectiles.Count == 0, "dispose clears bullets");
            }
        }
        File.WriteAllText("Library/battle-projectile-validation.txt", "passed: range180/search240; windup/no instant damage; swept contact; no interception; shooter death; target removal; tracking beyond range; lifetime; long-frame single hit; disposal");
    }

    public static void ImportAssets() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("scene");
        foreach (string path in new[] { "Assets/Art/unpack/scene/battle/projectile/RifleProjectile.png", "Assets/Art/frameAnimation/scene/battle/effect/RifleImpact.png", "Assets/Art/frameAnimation/scene/battle/effect/RifleImpact.json" }) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (path.EndsWith(".png")) {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                var config = new TextureImporterSettings();
                importer.ReadTextureSettings(config);
                config.spriteAlignment = (int)SpriteAlignment.Custom;
                config.spritePivot = path.Contains("RifleProjectile") ? new Vector2(.75f, .5f) : new Vector2(.5f, .5f);
                importer.SetTextureSettings(config);
                importer.SaveAndReimport();
                Check(AssetDatabase.LoadAssetAtPath<Sprite>(path) != null, "sprite import");
            }
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).SetAddress(path);
        }
        EditorUtility.SetDirty(group);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    public static void ValidateViews() {
        var scene = Scene();
        var root = new GameObject("ProjectileViewFixture", typeof(RectTransform)).GetComponent<RectTransform>();
        var view = new BattleProjectileView(scene, root);
        try {
            var origin = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY).position;
            var a = Unit(scene, 310001, BattleSceneCamp.FRIENDLY, origin);
            var b = Unit(scene, 310002, BattleSceneCamp.ENEMY, scene.cells.Values.First(c => c.groundPassable && c.position.x < origin.x && Vector2.Distance(c.position, origin) > 140 && Vector2.Distance(c.position, origin) < 180).position);
            Advance(scene, .01f);
            Advance(scene, .081f);
            view.Refresh(new Rect(0, 0, 3000, 3000));
            Check(view.visibleCount == 1 && b.hp == 100, "visible left projectile does not damage");
            var image = root.GetComponentInChildren<UnityEngine.UI.Image>();
            Check(Mathf.Abs(image.rectTransform.pivot.x - .75f) < .001f && image.rectTransform.localScale == Vector3.one, "tip anchor and no duplicate zoom");
            view.Refresh(new Rect(-10000, -10000, 1, 1));
            Check(view.visibleCount == 0 && scene.combat.projectiles.projectiles.Count == 1, "culling preserves bullet");
            Advance(scene, .28f);
            view.Refresh(new Rect(0, 0, 3000, 3000));
            Check(b.hp == 80 && view.visibleCount == 1 && root.GetComponentsInChildren<UnityEngine.UI.Image>().Single().name.StartsWith("impact_"), "offscreen hit displays impact");
        } finally {
            view.Dispose();
            Check(root.GetComponentsInChildren<UnityEngine.UI.Image>(true).All(i => i.sprite == null), "pool cleanup");
            UnityEngine.Object.Destroy(root.gameObject);
            scene.Dispose();
        }
        File.WriteAllText("Library/battle-projectile-view-validation.txt", "passed: preload; left projectile tip pivot; culling; offscreen hit; impact frames; pooled reference cleanup");
    }
}


