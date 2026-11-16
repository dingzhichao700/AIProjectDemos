#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
public static partial class CultivationMaxStateReviewValidation {
    private static async Task RunRefreshStabilityTrial(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var field = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunction = field.GetValue(null);
        string folder = "Library/AIUI/RefreshStability/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        field.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var scheme = new TeamSchemeSaveVO();
        scheme.schemes[0] = new[] { 2101, 0, 0, 0 };
        var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, scheme, ignored => { }));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            await Frames(8);
            var deck = main.GetComponentInChildren<DeckPresentationView>(true);
            deck.RequestCardInfo(2103);
            var panel = await CultivationOpened();
            await WaitPodiumCard(panel, 2103);
            var pulse = await WaitBluePulse(panel);
            await PrepareStageCapture(panel);
            var stable = panel.GetComponentsInChildren<CultivationImageLease>(false)
                .Select(l => l.GetComponent<Image>()).Where(i => i.enabled && i.sprite != null).ToDictionary(i => i, i => i.sprite);
            stable[panel.imgPortrait] = panel.imgPortrait.sprite;
            var pulseIdentity = pulse;
            bool before = UnityEditor.SessionState.GetString(Key + ".state", "").EndsWith("before");
            int gaps = 0;
            Action sample = () => {
                foreach (var pair in stable) { if (!pair.Key.enabled || pair.Key.sprite != pair.Value) { gaps++; } }
                if (panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).SingleOrDefault() != pulseIdentity) { gaps++; }
            };
            var costs = CardCultivationQuery.Read(CfgManager.tables, session, 2103).materials;
            int itemId = costs[0].itemId;
            GMControl.ins.OpenPanel();
            await Task.Delay(350); await Frames(8);
            var gm = (GmPanel)PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL);
            GMItem command = null;
            var scroll = gm.listItem.GetComponent<ScrollRect>();
            for (int step = 0; step <= 20 && command == null; step++) {
                scroll.verticalNormalizedPosition = 1 - step / 20f; await Frames(3);
                command = gm.GetComponentsInChildren<GMItem>(false).FirstOrDefault(c => {
                    var cfg = (cfg.resource.GMResource)typeof(GMItem).GetField("cfg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
                    return cfg != null && cfg.Content == "inventoryGrant";
                });
            }
            Check(command != null, "实际GM面板找到发放道具命令");
            await Frames(5);
            var inputs = command.listParam.GetComponentsInChildren<TMPro.TMP_InputField>(false);
            Check(inputs.Length == 2, "实际GM参数输入框完整");
            inputs[0].text = itemId.ToString(); inputs[1].text = "10000";
            Capture("gm-panel.png");
            long quantity = session.Inventory.model.GetQuantity(itemId);
            Click(command.btnExecute.GetComponent<RectTransform>()); sample();
            Check(session.Inventory.model.GetQuantity(itemId) == quantity + 10000, "实际GM执行按钮余额增加10000");
            Click(gm.btnClose.GetComponent<RectTransform>());
            for (int i=0; i<12; i++) { await Frames(1); sample(); Capture("gm-after-" + i.ToString("D2") + ".png"); }
            await Task.Delay(350); await Frames(5);
            foreach (var cost in costs.Skip(1)) { Check(GMControl.ins.ExecuteGM("inventoryGrant", new List<string>{cost.itemId.ToString(), "10000"}), "完整实际消耗发放=" + cost.itemId); sample(); }
            GlobalToast.Clear();
            // 面板关闭后仍走同一正式GM指令，记录培养页完整的前后连续帧。
            Capture("gm-visible-00.png");
            Check(GMControl.ins.ExecuteGM("inventoryGrant", new List<string>{itemId.ToString(), "7"}), "正式GM指令再次增加7"); sample();
            Capture("gm-visible-01.png");
            for (int i=2; i<18; i++) { await Frames(1); sample(); Capture("gm-visible-"+i.ToString("D2")+".png"); }
            GlobalToast.Clear(); await Frames(4);
            Capture("upgrade-visible-00.png");
            var balances = costs.ToDictionary(c=>c.itemId,c=>session.Inventory.model.GetQuantity(c.itemId));
            int level = session.Cards.model.GetState(2103).level;
            Click(panel.btnUpgrade.GetComponent<RectTransform>()); sample();
            Capture("upgrade-visible-01.png");
            for(int i=2;i<18;i++){ await Frames(1); sample(); Capture("upgrade-visible-"+i.ToString("D2")+".png"); }
            Check(session.Cards.model.GetState(2103).level == level + 1, "真实培养升级按钮等级增加1");
            Check(costs.All(c=>session.Inventory.model.GetQuantity(c.itemId)==balances[c.itemId]-c.required), "真实事务完整扣除配置材料");
            Check(panel.txtLevel.text.Contains((level+1).ToString()), "界面等级同步");
            Check(panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Single() == pulseIdentity, "GM与升级保持原动画实例不重启");
            observations.Add("same-resource image gaps="+gaps+"; baseline="+before);
            File.WriteAllText(Path.Combine(UnityEditor.SessionState.GetString(Key+".evidence", ""), "gap-observation.json"), "{\"baseline\":"+before.ToString().ToLowerInvariant()+",\"gaps\":"+gaps+"}");
            Check(before ? gaps > 0 : gaps == 0, before ? "修复前复现同资源图片清空" : "GM和升级同步/连续帧无静态图清空或动画重启");
            await PulseSwitchTo(panel, 2101);
            Check(panel.imgQualityGlow.enabled && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 0, "切普通卡恢复静态绿光并停止蓝色动画");
            await PulseSwitchTo(panel, 2104);
            Check(panel.imgQualityGlow.enabled && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 0, "史诗维持静态光效");
            await PulseSwitchTo(panel, 2105);
            Check(panel.imgQualityGlow.enabled && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 0, "传奇维持静态光效");
            await PulseSwitchTo(panel, 2303);
            Check(!session.Cards.model.GetState(2303).owned && !panel.imgQualityGlow.enabled && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 0, "未激活稀有卡隐藏动画和光效");
            await PulseSwitchTo(panel, 2103);
            pulse = await WaitBluePulse(panel);
            Check(panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 1, "快速跨品质切回仅一个动画实例");
            var palette = panel.GetComponent<CultivationExhibitPalette>();
            palette.previewRarePulse = false;
            typeof(CardCultivationPanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
            await Frames(4);
            Check(panel.imgQualityGlow.enabled && panel.imgQualityGlow.sprite == palette.rareGlow && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 0, "关闭试装立即回到原静态蓝光");
            palette.previewRarePulse = true;
            typeof(CardCultivationPanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
            pulse = await WaitBluePulse(panel);
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Task.Delay(350);
            Check(pulse.path == null && !pulse.gameObject.activeInHierarchy, "关闭后回收动画并停止计时");
            deck.RequestCardInfo(2103);
            panel = await CultivationOpened();
            pulse = await WaitBluePulse(panel);
            Check(panel.CurrentCardId == 2103 && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length == 1, "回池重开恢复当前卡单个动画");
            observations.Add("正式GM面板及指令/实际培养升级事务；隔离内存写入，非真实玩家存档；切卡、未激活隐藏、回退与回池复测。");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            field.SetValue(null, oldFunction);
        }
    }
}
#endif
