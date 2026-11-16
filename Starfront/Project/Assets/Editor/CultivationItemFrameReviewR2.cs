#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {
    private static Image MaterialFrameR2(CardCultivationMaterialItem cell) {
        return cell.GetComponentsInChildren<Image>(true).Single(i => i.name == "imgMaterialSlot");
    }

    private static async Task CheckFramesR2(CardCultivationPanel panel) {
        foreach (var cell in VisibleMaterials(panel)) {
            int id = cell.CurrentItemId;
            int quality = (int)CfgManager.tables.ItemObj.Get(id).Quality;
            Check(CultivationItemIconResources.TryGetBackgroundAddress(cell.itemResourceMap, id, quality, out string address), "实际材料有品质背景=" + id);
            Check(CultivationItemIconResources.TryGetQualityBackgroundAddress(cell.itemResourceMap, id, quality, out string tipAddress) && address == tipAddress, "材料和Tips共用背景地址=" + id);
            var frame = MaterialFrameR2(cell);
            for (int i = 0; i < 300 && (frame.sprite == null || frame.sprite != ResourceManager.GetUnpackImage(address)); i++) { await Frames(2); }
            Check(frame.enabled && frame.sprite == ResourceManager.GetUnpackImage(address), "材料实际Sprite按品质加载=" + id);
            Click(cell.GetComponent<RectTransform>());
            ItemTipPanel tip = null;
            for (int i = 0; i < 300; i++) {
                tip = PanelMgr.ins.GetPanel(ItemTipControl.PanelId) as ItemTipPanel;
                if (tip != null && tip.imgQualityIcon.sprite == frame.sprite && tip.imgItemIcon.sprite == cell.imgMaterialIcon.sprite) { break; }
                await Frames(2);
            }
            Check(tip != null && ItemTipControl.Pending.itemId == id && tip.imgQualityIcon.sprite == frame.sprite, "点击真实材料后Tips同品质Sprite=" + id);
            Check(tip.imgItemIcon.sprite == cell.imgMaterialIcon.sprite, "Tips与材料同内容Sprite=" + id);
            if (panel.CurrentCardId == 2101) {
                await PrepareStageCapture(panel); Capture("runtime-marine-item-tip-r2.png");
            }
            ItemTipControl.Close(); await Task.Delay(250); await Frames(5);
        }
    }

    private static async Task RunItemFrameR2(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var field = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousFunction = field.GetValue(null);
        string folder = "Library/AIUI/ItemFrameR2/" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(folder);
        field.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var scheme = new TeamSchemeSaveVO(); scheme.schemes[0] = new[] { 2101, 0, 0, 0 };
        var previousTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, scheme, ignored => { }));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId); await Frames(20);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(12);
            var deck = main.GetComponentInChildren<DeckPresentationView>(true);
            deck.RequestCardInfo(2101); var panel = await CultivationOpened();
            var request = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var map = panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>(true).First().itemResourceMap;
            var document = SimpleJSON.JSON.Parse(map.text);
            foreach (SimpleJSON.JSONNode entry in document["entries"].AsArray) {
                int id = entry["itemId"].AsInt, q = (int)CfgManager.tables.ItemObj.Get(id).Quality;
                bool a = CultivationItemIconResources.TryGetBackgroundAddress(map, id, q, out string aa);
                bool b = CultivationItemIconResources.TryGetQualityBackgroundAddress(map, id, q, out string bb);
                Check(a && b && aa == bb && AssetDatabase.LoadAssetAtPath<Sprite>(aa) != null, "全部资源映射同品质且资源存在=" + id);
            }
            foreach (int id in request.orderedCardIds) {
                await WaitMaterialIcons(panel, id); await CheckFramesR2(panel);
                if (id == 2101) { await WaitPodiumCard(panel, id); await PrepareStageCapture(panel); Capture("runtime-marine-material-r2.png"); }
                if (id != request.orderedCardIds.Last()) { Click(panel.btnNext.GetComponent<RectTransform>()); await Frames(2); }
            }
            for (int i = 1; i < request.orderedCardIds.Length; i++) { Click(panel.btnPrevious.GetComponent<RectTransform>()); await Frames(1); }
            await WaitMaterialIcons(panel, 2101); await CheckFramesR2(panel);
            var before = CardCultivationQuery.Read(CfgManager.tables, session, 2101);
            foreach (var cost in before.growth.costs) { Check(session.Inventory.TryGrantItem(cost.Key, cost.Value, out string reason), "隔离补足真实成本=" + cost.Key); }
            await Frames(12); Click(panel.btnUpgrade.GetComponent<RectTransform>()); await Task.Delay(500); await Frames(12);
            var after = CardCultivationQuery.Read(CfgManager.tables, session, 2101);
            Check(after.growth.level == before.growth.level + 1, "升级后只增长一级");
            await WaitMaterialIcons(panel, 2101); await CheckFramesR2(panel);
            Click(panel.btnReturn.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(8);
            deck.RequestCardInfo(2101); panel = await CultivationOpened(); await WaitMaterialIcons(panel, 2101); await CheckFramesR2(panel);
            observations.Add("正常MainPanel牌库入口；全映射背景地址、16卡实际材料与点击Tips同Sprite、快速首尾往返、实际升级与回池重开。仅隔离会话，未写真实存档。");
        } finally {
            ItemTipControl.Close(); PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId); PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previousTeam); field.SetValue(null, previousFunction);
        }
    }
}
#endif
