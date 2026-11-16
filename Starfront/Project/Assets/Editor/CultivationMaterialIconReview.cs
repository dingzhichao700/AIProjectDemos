#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {

    private const string MaterialArtRoot = "Assets/Art/unpack/default/item/cardFragmentsB/";

    private static void SetupMaterialIcons() {
        foreach (string path in Directory.GetFiles("Assets/Art/unpack/default/itemIcons", "*.png", SearchOption.TopDirectoryOnly)) {
            string normalized = path.Replace('\\', '/');
            AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalized);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            var config = new TextureImporterSettings();
            importer.ReadTextureSettings(config);
            config.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(config);
            importer.SaveAndReimport();
        }
        string mapPath = "Assets/ResourceData/default/item/icon-map.json";
        AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceSynchronousImport);
        var map = AssetDatabase.LoadAssetAtPath<TextAsset>(mapPath);
        var rows = SimpleJSON.JSON.Parse(map.text)["entries"].AsArray;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        foreach (SimpleJSON.JSONNode row in rows) {
            string path = row["content"].Value;
            string guid = AssetDatabase.AssetPathToGUID(path);
            Check(!string.IsNullOrEmpty(guid) && AssetDatabase.LoadAssetAtPath<Sprite>(path) != null, "资源存在=" + row["itemId"].AsInt);
            settings.CreateOrMoveEntry(guid, settings.FindGroup("Default Local Group")).SetAddress(path);
        }
        const string prefabPath = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            var items = root.GetComponentsInChildren<CardCultivationMaterialItem>(true);
            Check(items.Length == 1, "仅一个材料复用模板，无运行Clone预制残留");
            var item = items.Single();
            var frame = item.GetComponentsInChildren<Image>(true).Single(image => image.name == "imgMaterialSlot");
            string existing = AssetDatabase.GetAssetPath(frame.sprite);
            Check(existing == "Assets/Art/atlasSource/default/cardCultivation/material-blue.png" || existing == MaterialArtRoot + "shared/complete-black-frame-97x101.png", "只替换现行明确蓝框，不新增品质回退");
            Check(frame.rectTransform.anchoredPosition == new Vector2(6, 0) && frame.rectTransform.sizeDelta == new Vector2(97, 101), "保留原框体位置和尺寸");
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MaterialArtRoot + "shared/complete-black-frame-97x101.png");
            item.itemResourceMap = map;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Check(rows.Count == 26, "保留16当前和10历史映射，不启用历史卡");
        observations.Add("资源已按B开口裁Alpha，原Image层级不变；数量和UIBinder成员不变；固定蓝框没有品质选框逻辑，其他品质框不自动制作或变色。");
    }

    private static CardCultivationMaterialItem[] VisibleMaterials(CardCultivationPanel panel) {
        return panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>(false).OrderBy(item => item.transform.position.x).ToArray();
    }

    private static async Task WaitMaterialIcons(CardCultivationPanel panel, int cardId) {
        for (int wait = 0; wait < 500; wait++) {
            var materials = CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, cardId).materials;
            var cells = VisibleMaterials(panel);
            if (panel.CurrentCardId == cardId && materials.Count == cells.Length && cells.Select((cell, i) => {
                var material = materials[i];
                if (cell.CurrentItemId != material.itemId || !cell.imgMaterialIcon.enabled || cell.imgMaterialIcon.sprite == null) { return false; }
                return !CultivationItemIconResources.TryGetAddress(cell.itemResourceMap, material.itemId, out string address) || cell.imgMaterialIcon.sprite == ResourceManager.GetUnpackImage(address);
            }).All(value => value)) { await Task.Delay(100); return; }
            await Frames(2);
        }
        throw new TimeoutException("正式材料图标加载未就绪=" + cardId);
    }

    private static void CheckMaterialIcons(CardCultivationPanel panel, PlayerProgressControl session, int cardId) {
        var materials = CardCultivationQuery.Read(CfgManager.tables, session, cardId).materials;
        var cells = VisibleMaterials(panel);
        Check(cells.Length == materials.Count, "实际成本数量与材料格一致=" + cardId);
        Check(Mathf.Abs(panel.listMaterials.GetComponent<ScrollRect>().viewport.anchoredPosition.x) < .01f, "实际列表靠左=" + cardId);
        for (int i = 0; i < cells.Length; i++) {
            var cell = cells[i];
            var material = materials[i];
            Check(cell.CurrentItemId == material.itemId, "格身份来自实际itemId=" + material.itemId);
            bool mapped = CultivationItemIconResources.TryGetAddress(cell.itemResourceMap, material.itemId, out string address);
            if (mapped) {
                var expected = AssetDatabase.LoadAssetAtPath<Sprite>(address);
                Check(cell.imgMaterialIcon.sprite.texture == expected.texture && cell.imgMaterialIcon.sprite == ResourceManager.GetUnpackImage(address), "正式Sprite身份无串卡=" + material.itemId);
                Check(cell.imgMaterialIcon.rectTransform.anchoredPosition == new Vector2(6,0) && cell.imgMaterialIcon.rectTransform.sizeDelta == new Vector2(97,101), "B内图原生画布贴合开口=" + material.itemId);
            } else {
                Check(cell.imgMaterialIcon.sprite == cell.temporaryIcon && cell.imgMaterialIcon.rectTransform.sizeDelta == new Vector2(64,64), "未交付通用道具保留原占位=" + material.itemId);
            }
            var frame = cell.GetComponentsInChildren<Image>().Single(image => image.name == "imgMaterialSlot");
            Check(AssetDatabase.GetAssetPath(frame.sprite) == MaterialArtRoot + "shared/complete-black-frame-97x101.png" && frame.color == Color.white, "只替换原固定蓝框无运行变色=" + material.itemId);
            Check(cell.txtMaterialOwned.text == material.owned.ToString() && cell.txtMaterialRequired.text == material.required.ToString(), "真实余额和成本不变=" + material.itemId);
            var container = (RectTransform)cell.txtMaterialRequired.transform.parent;
            var number = cell.txtMaterialRequired.rectTransform;
            Check(Mathf.Abs(container.anchoredPosition.x + number.anchoredPosition.x + number.sizeDelta.x - 94) < .01f && container.anchoredPosition.y == -70 && cell.txtMaterialRequired.fontSize == 22, "数量保持右界94上边70字号22=" + material.itemId);
        }
    }

    private static async Task RunMaterialIconsReview(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var functionField = typeof(FunctionControl).GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        object previousFunction = functionField.GetValue(null);
        string folder = "Library/AIUI/MaterialIcons/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var scheme = new TeamSchemeSaveVO();
        scheme.schemes[0] = new[] {2101,0,0,0};
        var previousTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, scheme, ignored => { }));
        try {
            await ResourceLoader.LoadListAsync(new System.Collections.Generic.List<ResLoadInfo> {new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            var sourcePosition = view.library.GetComponent<ScrollRect>().content.anchoredPosition;
            view.RequestCardInfo(2101);
            var panel = await CultivationOpened();
            var source = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(panel);
            Check(source.source == "library" && source.orderedCardIds.Length == 16, "MainPanel真实牌库来源16当前卡，无历史卡启用");
            foreach (int id in source.orderedCardIds) {
                await WaitMaterialIcons(panel,id);
                CheckMaterialIcons(panel,session,id);
                if (id == 2101) { await WaitPodiumCard(panel,id); await PrepareStageCapture(panel); Capture("runtime-marine-material.png"); }
                else if (id == 2203 || id == 2303) { Capture("runtime-material-" + id + ".png"); }
                if (id != source.orderedCardIds.Last()) { Click(panel.btnNext.GetComponent<RectTransform>()); await Frames(2); }
            }
            for (int i=1;i<source.orderedCardIds.Length;i++) { Click(panel.btnPrevious.GetComponent<RectTransform>()); await Frames(1); }
            await WaitMaterialIcons(panel,2101);
            CheckMaterialIcons(panel,session,2101);
            var oldCell = VisibleMaterials(panel).Single();
            int oldItemId = oldCell.CurrentItemId;
            string oldAddress;
            Check(CultivationItemIconResources.TryGetAddress(oldCell.itemResourceMap,oldCell.CurrentItemId,out oldAddress), "当前格资源地址可复核");
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Task.Delay(400); await Frames(8);
            Check(oldCell.CurrentItemId == 0 && oldCell.imgMaterialIcon.sprite == null && !oldCell.imgMaterialIcon.enabled, "回池清除itemId和Sprite");
            Check(ResourceManager.GetUnpackImage(oldAddress) == null, "最后图标租约关闭后释放加载资源");
            Check(Vector2.Distance(sourcePosition,view.library.GetComponent<ScrollRect>().content.anchoredPosition)<.1f, "返回恢复原牌库滚动位置");
            view.RequestCardInfo(2303);
            panel = await CultivationOpened(); await WaitMaterialIcons(panel,2303); CheckMaterialIcons(panel,session,2303);
            Check(VisibleMaterials(panel).Single().CurrentItemId != oldItemId, "回池重开不同卡不沿用陆战碎片身份");
            // 使用独立Item验证复用，既不改真实成本，也不将临时道具混入生产配置。
            var cell = VisibleMaterials(panel).Single();
            var current = CardCultivationQuery.Read(CfgManager.tables,session,2303).materials.Single();
            cell.SetData(0,new CultivationMaterialVO {itemId=2001,owned=987,required=20,name="隔离通用道具"});
            Check(cell.CurrentItemId == 2001 && cell.imgMaterialIcon.sprite == cell.temporaryIcon && cell.imgMaterialIcon.rectTransform.sizeDelta == new Vector2(64,64), "碎片→未知通用道具恢复原占位尺寸，不串图");
            cell.SetData(0,current);
            await WaitMaterialIcons(panel,2303); CheckMaterialIcons(panel,session,2303);
            observations.Add("16当前卡正式成本按itemId加载；快切、返回、不同卡重开、通用道具回退和资源租约释放。隔离内存fixture，不改变正式配置或存档。");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previousTeam);
            functionField.SetValue(null,previousFunction);
        }
    }
}
#endif
