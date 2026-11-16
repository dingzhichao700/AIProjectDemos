using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>本轮占领迁移的定向导入与独立规则回归；不构建或发布。</summary>
[InitializeOnLoad]
public static class BattleOccupationMigration {
    [Serializable] private class Request { public string requestId; public int processId; public string action; }
    [Serializable] private class Result { public string requestId; public string status; public string error; }
    private sealed class EmptyRoll : System.Random {
        public override int Next(int maxValue) { return 0; }
    }
    static BattleOccupationMigration() { EditorApplication.update += Poll; }
    private static void Poll() {
        const string path = "Library/battle-occupation-request.json";
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
            return;
        }
        var r = new Result { status = "passed" };
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            r.requestId = request.requestId;
            if (string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("Request identity mismatch.");
            }
            File.Delete(path);
            if (request.action == "runtime") {
                BattleOccupationRuntimeValidation.Start(request.requestId);
                return;
            }
            if (request.action == "buildAndRuntime") {
                AddressablesAutoBuilder.BuildPlayerContent();
                BattleOccupationRuntimeValidation.Start(request.requestId);
                return;
            }
            Install();
            ValidateRules();
        } catch (Exception e) {
            r.status = "failed";
            r.error = e.ToString();
            Debug.LogException(e);
        }
        File.WriteAllText("Library/battle-occupation-result.json", JsonUtility.ToJson(r, true));
    }
    private static void Install() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.DefaultGroup;
        foreach (string name in new[] { "camp_blue", "camp_orange" }) {
            string path = "Assets/Art/unpack/default/battle/effects/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            var config = new TextureImporterSettings();
            importer.ReadTextureSettings(config);
            config.spriteMeshType = SpriteMeshType.FullRect;
            config.spriteAlignment = (int)SpriteAlignment.Center;
            config.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(config);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite.rect.size == new Vector2(112, 98) && sprite.pivot == new Vector2(56, 49), "coverage geometry");
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            entry.address = path;
            entry.SetLabel("main-content", true, true);
        }
        const string matPath = "Assets/Art/material/default/battle/coverage.mat";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/shader/BattleCoverage.shader");
        Require(shader != null && shader.isSupported, "coverage shader supported");
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null) {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, matPath);
        }
        material.shader = shader;
        EditorUtility.SetDirty(material);
        var matEntry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(matPath), group);
        matEntry.address = matPath;
        matEntry.SetLabel("main-content", true, true);
        foreach (string name in ConfigManifest.Names) {
            string path = ResourceConst.PATH_CONFIG + name + ".json";
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.FindGroup("configData"));
            entry.address = path;
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
    private static object Call(object target, string method, params object[] args) {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
    private static void Require(bool value, string message) {
        if (!value) { throw new InvalidOperationException("Occupation: " + message); }
    }
    private static int Distance(BattleCellVO a, BattleCellVO b) {
        int q = a.q - b.q, k = a.k - b.k;
        return Math.Max(Math.Abs(q), Math.Max(Math.Abs((k-q)/2), Math.Abs((k+q)/2)));
    }
    public static void ValidateRules() {
        var field = typeof(CfgManager).GetField("_tables", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = field.GetValue(null);
        var tables = new Tables(name => JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json")));
        field.SetValue(null, tables);
        typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        
        try {
            foreach (int id in new[] { 1001, 1002 }) {
                var scene = new BattleSceneModel(tables.BattleSceneObj.Get(id), tables.BattleSceneCellObj.DataList);
                scene.InitializeBattle(tables, 7);
                try {
                    Require(scene.buildings.Count == 2, "only bases at start");
                    foreach (var building in scene.buildings.Values) {
                        Require(building.cell.unlocked && building.cell.camp == building.camp, "base unlocked");
                        foreach (var cell in scene.cells.Values.Where(c => c.camp == building.camp)) {
                            Require(Distance(cell, building.cell) <= 1, "initial ownership only immediate neighbors");
                        }
                    }
                    Require(scene.cells.Values.Any(c => c.camp == BattleSceneCamp.UNOWNED), "explicit unowned cells");
                    Require(scene.cells.Values.Where(c => c.capturable && !c.unlocked).All(c => c.unlockConfig != null), "hidden types assigned to all ordinary cells");
                    foreach (var cell in scene.cells.Values.Where(c => c.unlockConfig != null)) {
                        Require(scene.GetCell(-cell.q, -cell.k).unlockConfig.Id == cell.unlockConfig.Id, "rotational type pairing");
                    }
                    var frontier = scene.cells.Values.First(c => c.camp == BattleSceneCamp.FRIENDLY && scene.unlocks.IsRevealed(c)
                        && scene.GetNeighbors(c).Any(n => n.camp == BattleSceneCamp.UNOWNED));
                    var neighbors = scene.GetNeighbors(frontier).ToArray();
                    var outside = scene.cells.Values.Where(c => Distance(c, frontier) > 1).ToDictionary(c => c.id, c => c.camp);
                    Call(frontier, "SetUnlockConfig", tables.CellUnlockObj.Get(2));
                    typeof(BattleUnlockModel).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scene.unlocks, new EmptyRoll());
                    long balance = scene.camps[BattleSceneCamp.FRIENDLY].currency;
                    object[] unlockArgs = { frontier.id, BattleSceneCamp.FRIENDLY, null };
                    Require((bool)Call(scene.unlocks, "TryUnlock", unlockArgs), "successful empty unlock");
                    Require(frontier.unlocked && !frontier.unlockedCardId.HasValue && scene.buildings.Count == 2, "empty is unlocked, no building");
                    Require(scene.camps[BattleSceneCamp.FRIENDLY].currency == balance - 25, "deduct once");
                    Require(neighbors.Where(c => c.capturable).All(c => c.camp == BattleSceneCamp.FRIENDLY), "immediate expansion");
                    Require(outside.All(pair => scene.GetCell(pair.Key).camp == pair.Value), "no chain expansion");
                    Require(!scene.unlocks.CanUnlock(frontier.id, BattleSceneCamp.FRIENDLY, out _), "no repeated unlock");
                    Call(frontier, "TrySetCamp", BattleSceneCamp.ENEMY);
                    Require(frontier.unlocked && frontier.camp == BattleSceneCamp.ENEMY, "empty land can change camp without relock");
                    var enemyBase = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY);
                    var adjacent = scene.GetNeighbors(enemyBase.cell).First(c => c.capturable);
                    Call(scene.territory, "ExpandFrom", adjacent, BattleSceneCamp.FRIENDLY);
                    Require(enemyBase.cell.camp == BattleSceneCamp.ENEMY, "living enemy building protected");
                    var ruin = scene.cells.Values.First(c => c.capturable && !c.unlocked && c.camp == BattleSceneCamp.UNOWNED);
                    Call(ruin, "TrySetCamp", BattleSceneCamp.ENEMY);
                    var collectorCard = tables.CardObj.DataList.First(c => c.Content is BuildingCardContent);
                    var defeated = (BattleBuildingVO)Call(scene, "PrepareCardBuilding", ruin, collectorCard);
                    Call(scene, "CommitUnlock", ruin, (int?)collectorCard.Id, defeated);
                    Call(defeated, "ApplyDamage", defeated.maxHp);
                    Call(scene, "RemoveDefeated", defeated, BattleSceneCamp.FRIENDLY);
                    Require(!scene.buildings.ContainsKey(defeated.id) && defeated.destroyed, "destroyed building removed from combat and production");
                    Require(ruin.ruined && !ruin.capturable && ruin.camp == BattleSceneCamp.FRIENDLY, "ruin locked to lethal camp");
                    Require(!(bool)Call(ruin, "TrySetCamp", BattleSceneCamp.ENEMY), "ruin cannot be recaptured");
                    foreach (var neighbor in scene.GetNeighbors(ruin)) {
                        Call(scene.territory, "ExpandFrom", neighbor, BattleSceneCamp.ENEMY);
                    }
                    Require(ruin.camp == BattleSceneCamp.FRIENDLY && !scene.unlocks.CanUnlock(ruin.id, BattleSceneCamp.FRIENDLY, out _), "ruin immune to expansion and rebuilding");
                    var neutral = scene.cells.Values.First(c => c.camp == BattleSceneCamp.ABSOLUTE_NEUTRAL);
                    Require(!(bool)Call(neutral, "TrySetCamp", BattleSceneCamp.FRIENDLY), "permanent neutral protected");
                    var contested = scene.cells.Values.First(c => c.camp == BattleSceneCamp.UNOWNED && c.capturable);
                    scene.AddElement(new BattleUnitVO(900001, tables.UnitObj.Get(1101), BattleSceneCamp.FRIENDLY, contested.position));
                    scene.AddElement(new BattleUnitVO(900002, tables.UnitObj.Get(1101), BattleSceneCamp.ENEMY, contested.position));
                    Call(scene.territory, "Resolve");
                    Require(contested.camp == BattleSceneCamp.UNOWNED, "both camps preserve unowned");
                    scene.RemoveElement(900002);
                    Call(scene.territory, "Resolve");
                    Require(contested.camp == BattleSceneCamp.FRIENDLY, "survivor captures unowned");
                    Require(scene.GetTerritoryCount(BattleSceneCamp.UNOWNED) == 0, "unowned excluded from territory statistics");
                } finally { scene.Dispose(); }
            }
            File.WriteAllText("Library/battle-occupation-rules.txt", "passed: both scenes; bases/neighbors; hidden types/rotation; empty unlock/cost/no repeat; immediate expansion/no chain; living building/permanent neutral/ruin protection; contested unowned/survivor; teardown");
        } finally {
            field.SetValue(null, previous);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            
        }
    }
}




