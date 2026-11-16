#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {
    private const string SharedPowerArt = "Assets/Art/unpack/default/sharedPower/";
    private const string SharedPowerPrefab = "Assets/Prefab/default/common/PowerDisplay.prefab";

    private static RectTransform PowerRect(Transform parent, string name, Vector2 position, Vector2 size) {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image PowerImage(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite) {
        var image = PowerRect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void SetupPowerSharedR1() {
        foreach (string file in Enumerable.Range(0, 10).Select(i => SharedPowerArt + "power-digit-" + i + ".png").Concat(new[] { SharedPowerArt + "power-missing.png", SharedPowerArt + "power-title.png", "Assets/Art/atlasSource/default/sharedPower/skill-cell-backing.png" })) {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(SharedPowerPrefab));
        var root = new GameObject("PowerDisplay", typeof(RectTransform));
        try {
            var r = root.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(254, 40);
            r.pivot = new Vector2(0, 1);
            var display = root.AddComponent<PowerDisplay>();
            display.digits = Enumerable.Range(0, 10).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(SharedPowerArt + "power-digit-" + i + ".png")).ToArray();
            display.missing = AssetDatabase.LoadAssetAtPath<Sprite>(SharedPowerArt + "power-missing.png");
            display.digitsArea = PowerRect(root.transform, "boxPowerDigits", new Vector2(97.48f, 0), new Vector2(156.52f, 40));
            PowerImage(root.transform, "imgPowerSymbol", new Vector2(0, -3), new Vector2(33, 33), AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/unpack/default/sharedPower/ui-power-icon.png"));
            PowerImage(root.transform, "imgPowerTitle", new Vector2(37, -5), new Vector2(59.5f, 31.3f), AssetDatabase.LoadAssetAtPath<Sprite>(SharedPowerArt + "power-title.png"));
            display.SetValue(249);
            Check(display.digits.All(s => s != null) && display.missing != null, "0—9及缺值图片引用齐全");
            PrefabUtility.SaveAsPrefabAsset(root, SharedPowerPrefab);
        } finally { UnityEngine.Object.DestroyImmediate(root); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedPowerPrefab);
        foreach (string path in new[] { "Assets/Prefab/default/mainView/CardCultivationPanel.prefab", "Assets/Prefab/default/mainView/MainPanel.prefab" }) {
            var panel = PrefabUtility.LoadPrefabContents(path);
            try {
                bool cultivation = path.Contains("CardCultivation");
                var parent = cultivation ? panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == "txtPowerValue").parent : panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == "boxPowerHeader");
                var prior = parent.Find("sharedPowerDisplay");
                if (prior) UnityEngine.Object.DestroyImmediate(prior.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = "sharedPowerDisplay";
                var rt = instance.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = cultivation ? new Vector2(238, -350) : new Vector2(53, -15);
                rt.localScale = cultivation ? Vector3.one : Vector3.one * 1.1f;
                if (!cultivation) panel.GetComponentInChildren<DeckPresentationView>(true).powerDisplay = instance.GetComponent<PowerDisplay>();
                var legacyNames = cultivation ? new[] { "txtPower", "txtPowerValue", "imgPowerSymbol" } : new[] { "txtPowerTitle", "txtPowerValue", "imgPowerIcon" };
                foreach (string name in legacyNames) {
                    foreach (var legacy in parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == name && !t.IsChildOf(instance.transform)).ToArray()) legacy.gameObject.SetActive(false);
                }
                if (cultivation) {
                    var skill = panel.GetComponentsInChildren<UIBinder>(true).Single(b => b.csharpAssetPath.EndsWith("/CardCultivationSkillItem.cs"));
                    var plate = skill.transform.Find("imgSkillCellBacking");
                    if (plate) UnityEngine.Object.DestroyImmediate(plate.gameObject);
                    var backing = PowerImage(skill.transform, "imgSkillCellBacking", Vector2.zero, new Vector2(126, 140), AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/atlasSource/default/sharedPower/skill-cell-backing.png"));
                    backing.preserveAspect = false;
                    backing.transform.SetAsFirstSibling();
                    var artwork = skill.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "boxSkillArtwork");
                    artwork.anchoredPosition = new Vector2(18, -32);
                    artwork.localScale = Vector3.one * (90f / 79f);
                    foreach (string name in new[] { "imgSkillKind", "txtSkillKind" }) {
                        var rect = skill.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == name);
                        rect.anchoredPosition = new Vector2(13, -4);
                        rect.sizeDelta = new Vector2(100, 28);
                    }
                    var state = skill.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "txtSkillState");
                    state.anchoredPosition = new Vector2(4, -120);
                    state.sizeDelta = new Vector2(118, 20);
                    Check(skill.uiList.Count == 8 && skill.uiList.All(b => b.go), "技能原八绑定保持，无新增动态底板绑定");
                    Check(backing.transform.parent == skill.transform && artwork.parent.name == "btnSkill", "底板静态、图标组仍参与原按压");
                }
                PrefabUtility.SaveAsPrefabAsset(panel, path);
                Check(panel.GetComponentsInChildren<PowerDisplay>(true).Length == 1, "唯一共用战力实例=" + path);
            } finally { PrefabUtility.UnloadPrefabContents(panel); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void CheckPowerDisplayR1(PowerDisplay display) {
        Check(display != null && display.digits.Length == 10 && display.digits.All(s => s), "共用战力组件及十图有效");
        foreach (double? value in new double?[] { 671, 19571, 999999, 0, null, 999999999999, 9, null, 671 }) {
            display.SetValue(value);
            string expected = value.HasValue ? value.Value.ToString("0", CultureInfo.InvariantCulture) : "—";
            Check(display.DisplayedValue == expected, "图片战力数值准确=" + expected);
            var active = display.digitsArea.GetComponentsInChildren<Image>(false);
            Check(active.Length == expected.Length, "位数减少及缺值无旧图片=" + expected);
            foreach (var image in active) {
                Check(image.sprite != null && !image.raycastTarget, "图片引用且不拦截点击=" + expected);
                var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                Check(corners.All(p => display.digitsArea.rect.Contains((Vector2)display.digitsArea.InverseTransformPoint(p))), "长数字不溢出=" + expected);
            }
        }
    }

    private static async Task RunPowerSharedR1(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var f = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic); object oldFunction = f.GetValue(null);
        string folder = "Library/AIUI/PowerSharedR1/" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(folder);
        f.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var team = new TeamSchemeSaveVO(); team.schemes[0] = new[] { 2101, 2105, 2205, 2306 }; team.schemes[1] = new[] { 2301, 2302, 2303, 2304 };
        var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, team, ignored => { }));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId); await Frames(20); var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(12);
            var deck = main.GetComponentInChildren<DeckPresentationView>(true); var mainPower = main.GetComponentInChildren<PowerDisplay>(true);
            Check(mainPower.DisplayedValue == TeamEditControl.ins.Query.FormationPower(team.schemes[0]).Value.ToString("0", CultureInfo.InvariantCulture), "阵容总战力继续读取原查询");
            string total = mainPower.DisplayedValue; CheckPowerDisplayR1(mainPower); mainPower.SetValue(double.Parse(total, CultureInfo.InvariantCulture));
            Capture("runtime-main.png");
            foreach (int id in CfgManager.tables.CardObj.DataList.Where(c => c.ShowInLibrary).Select(c => c.Id)) {
                deck.RequestCardInfo(id); var panel = await CultivationOpened(); await WaitPodiumCard(panel, id); await Frames(14);
                var power = panel.GetComponentInChildren<PowerDisplay>(true);
                var value = CardCultivationQuery.Read(CfgManager.tables, session, id).power;
                Check(power.DisplayedValue == value.Value.ToString("0", CultureInfo.InvariantCulture), "培养单卡继续读取原查询=" + id);
                Check(power.digits.SequenceEqual(mainPower.digits), "两处共用相同十张Sprite=" + id);
                var cells = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false);
                Check(cells.Length == 4, "四个技能底板实例完整=" + id);
                foreach (var cell in cells) {
                    var plate = cell.transform.Find("imgSkillCellBacking");
                    Check(plate && !plate.GetComponent<Image>().raycastTarget && plate.parent == cell.transform, "独立静态底板=" + id);
                    var art = cell.GetComponentsInChildren<RectTransform>().Single(t => t.name == "boxSkillArtwork");
                    Check(Mathf.Abs(art.rect.width * art.localScale.x - 90) < .01f, "图框占幅90且保留原内图比例=" + id);
                }
                if (id == 2101 || id == 2306) { await PrepareStageCapture(panel); Capture("runtime-cultivation-" + id + ".png"); }
                if (id == 2101) {
                    CheckPowerDisplayR1(power); power.SetValue(value);
                    var cell = cells[0]; var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    var plate = cell.transform.Find("imgSkillCellBacking").GetComponent<RectTransform>();
                    float beforePlate = SkillWorldWidth(plate), beforeKind = SkillWorldWidth(cell.txtSkillKind.rectTransform), beforeArt = SkillWorldWidth(cell.imgSkillFrame.rectTransform);
                    ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject, pointer, ExecuteEvents.pointerDownHandler); await Task.Delay(180); await Frames(4);
                    Check(Mathf.Abs(SkillWorldWidth(cell.imgSkillFrame.rectTransform) / beforeArt - .95f) < .004f && Mathf.Abs(SkillWorldWidth(plate) - beforePlate) < .01f && Mathf.Abs(SkillWorldWidth(cell.txtSkillKind.rectTransform) - beforeKind) < .01f, "按压仅图标缩小，底板和类型静止");
                    ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject, pointer, ExecuteEvents.pointerUpHandler); await Task.Delay(150);
                    ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    for (int i = 0; i < 150 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) await Frames(2);
                    Check(PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId), "技能详情实际打开");
                    await Frames(12);
                    CardSkillDetailControl.Close(); await Task.Delay(350); await Frames(8);
                    Check(panel.CurrentCardId == id && power.DisplayedValue == value.Value.ToString("0", CultureInfo.InvariantCulture), "技能详情返回保持卡牌及战力");
                }
                Click(panel.btnReturn.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(8);
                Check(mainPower.DisplayedValue == total, "培养返回阵容总战力不变=" + id);
            }
            foreach (int index in new[] { 1, 0 }) {
                Click(deck.editTeam.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(16);
                var edit = (TeamEditPanel)PanelMgr.ins.GetPanel(TeamEditControl.PanelId);
                Click((index == 1 ? edit.btnScheme2 : edit.btnScheme1).GetComponent<RectTransform>()); await Frames(8);
                Click(edit.btnSave.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(12);
                Check(mainPower.DisplayedValue == TeamEditControl.ins.Query.FormationPower(team.schemes[index]).Value.ToString("0", CultureInfo.InvariantCulture), "实际切方案保存后共用总战力刷新=" + index);
            }
            deck.RequestCardInfo(2101); var upgradePanel = await CultivationOpened(); await WaitPodiumCard(upgradePanel, 2101); await Frames(8);
            var beforeUpgrade = CardCultivationQuery.Read(CfgManager.tables, session, 2101);
            foreach (var cost in beforeUpgrade.growth.costs) Check(session.Inventory.TryGrantItem(cost.Key, cost.Value, out string reason), "隔离背包补本档真实所需材料=" + cost.Key + ":" + reason);
            await Frames(10); Check(upgradePanel.btnUpgrade.interactable, "真实材料到账后升级按钮可用");
            var samePower = upgradePanel.GetComponentInChildren<PowerDisplay>(true);
            Click(upgradePanel.btnUpgrade.GetComponent<RectTransform>()); await Task.Delay(500); await Frames(15);
            var afterUpgrade = CardCultivationQuery.Read(CfgManager.tables, session, 2101);
            Check(afterUpgrade.growth.level == beforeUpgrade.growth.level + 1, "实际点击只升级一级");
            Check(upgradePanel.GetComponentInChildren<PowerDisplay>(true) == samePower && samePower.DisplayedValue == afterUpgrade.power.Value.ToString("0", CultureInfo.InvariantCulture), "升级原位复用组件且图片战力刷新");
            await PrepareStageCapture(upgradePanel); Capture("runtime-cultivation-upgraded.png");
            Click(upgradePanel.btnReturn.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(12);
            Check(mainPower.DisplayedValue == TeamEditControl.ins.Query.FormationPower(TeamEditControl.ins.ReadSelected(out _)).Value.ToString("0", CultureInfo.InvariantCulture), "升级返回后阵容总战力按原查询刷新");
            Capture("runtime-main-after-upgrade.png");
            observations.Add("16卡正常MainPanel/牌库/培养入口与技能详情返回、实际切方案保存、材料补足后升级及双端战力刷新；隔离玩家/方案，未构建发布，未改战力计算/成长配置/真实存档。图片字与技能底板仅表现层更新。");
        } finally { CardSkillDetailControl.Close(); PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId); PanelMgr.ins.ClosePanelByType(mainId); TeamEditControl.InstallValidationSession(oldTeam); f.SetValue(null, oldFunction); }
    }
}
#endif
