#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {

    private const string PulseTrialPath = "Assets/Art/unpack/default/cardCultivation/pulseRareCTrial/BlueLocalPulse";

    private static void SetupBluePulseTrial() {
        var importer = (TextureImporter)AssetImporter.GetAtPath(PulseTrialPath + ".png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        foreach (string ext in new[] { ".png", ".json" }) {
            string path = PulseTrialPath + ext;
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.FindGroup("Default Local Group")).SetAddress(path);
        }
        const string prefab = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try {
            var palette = root.GetComponent<CultivationExhibitPalette>();
            Check(palette.rareGlow != null, "原静态蓝光回退保留");
            palette.previewRarePulse = true;
            palette.rarePulseAnimation = PulseTrialPath;
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            AssetDatabase.SaveAssets();
            Check(root.GetComponentsInChildren<UIBinder>(true).Single(b => b.csharpAssetPath.EndsWith("/CardCultivationSkillItem.cs")).uiList.Count == 7, "技能七绑定和已验收按压结构不动");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static async Task<FrameAnimationView> WaitBluePulse(CardCultivationPanel panel) {
        for (int i = 0; i < 350; i++) {
            var view = panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).SingleOrDefault();
            if (view != null && view.animationData != null) {
                return view;
            }
            await Frames(2);
        }
        throw new TimeoutException("蓝色局部脉冲试装加载超时");
    }

    private static async Task PulseSwitchTo(CardCultivationPanel panel, int id) {
        var request = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
        int from = Array.IndexOf(request.orderedCardIds, panel.CurrentCardId);
        int to = Array.IndexOf(request.orderedCardIds, id);
        Check(to >= 0, "牌库来源包含目标卡=" + id);
        for (int i = from; i != to; i += Math.Sign(to - from)) {
            Click((to > from ? panel.btnNext : panel.btnPrevious).GetComponent<RectTransform>());
            await Frames(2);
        }
        await WaitPodiumCard(panel, id);
        await Frames(8);
    }

    private static async Task RunBluePulseTrial(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var field = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunction = field.GetValue(null);
        string folder = "Library/AIUI/BluePulseTrial/" + Guid.NewGuid().ToString("N");
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
            Check(pulse.animationData.sprites.Length == 24 && pulse.animationData.totalDuration == 2000, "正式帧动画链24帧/2000ms");
            Check(pulse.animationData.sourceSizes.All(s => s == new Vector2(660, 220)), "裁剪帧保留完整660×220画布");
            Check(!panel.imgQualityGlow.enabled && panel.imgQualityGlow.sprite != null, "动画显示时原静态蓝光保留但不重复叠加");
            Check(pulse.trans.parent == panel.imgQualityGlow.transform && pulse.trans.anchoredPosition == Vector2.zero, "动画居中叠在原固定光效容器");
            Check(Mathf.Abs(pulse.scale - 370f / 660) < .0001f, "动画等比缩放到原370px容器");
            Check(panel.imgPodium.rectTransform.anchoredPosition == new Vector2(175, -233.7f), "静态座台坐标不变");
            await PrepareStageCapture(panel);
            var seen = new HashSet<Sprite>();
            float begin = RookieEngine.timer.curTime;
            for (int i = 0; i < 25; i++) {
                while (RookieEngine.timer.curTime - begin < i * 80) {
                    await Frames(1);
                }
                seen.Add(pulse.GetComponentInChildren<Image>().sprite);
                Capture("runtime-blue-pulse-" + i.ToString("D2") + ".png");
            }
            Check(seen.Count >= 20, "共用计时器实际推进至少20个不同动画帧");
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
            observations.Add("本轮仅稀有品质蓝色局部脉冲C试装；复用FrameAnimationView/Manager，共用图集，未造动画框架。25张实际运行帧来自共用计时器，首尾未闭合问题保留。其他品质静态、未激活隐藏、静态回退和回池均验证；隔离玩家会话，不发GM。");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            field.SetValue(null, oldFunction);
        }
    }
}
#endif
