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

    private static void ApplyPodiumActivation15() {
        const string path = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            var binder = root.GetComponentsInChildren<UIBinder>(true).Single(b => b.csharpAssetPath.EndsWith("/CardCultivationPanel.cs"));
            var images = root.GetComponentsInChildren<Image>(true);
            var podiumImage = images.Single(i => i.name == "imgPodium");
            var hero = images.Single(i => i.name == "imgPortrait");
            var oldGlow = images.SingleOrDefault(i => i.name == "imgQualityGlow");
            var glow = oldGlow ?? new GameObject("imgQualityGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(podiumImage.transform.parent, false);
            glow.transform.SetSiblingIndex(podiumImage.transform.GetSiblingIndex() + 1);
            ApprovedRect(podiumImage.transform, 175, 233.7f, 370, 123.39f);
            ApprovedRect(glow.transform, 175, 233.7f, 370, 123.39f);
            podiumImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/atlasSource/default/cardCultivation/podium-metal.png");
            podiumImage.material = null;
            podiumImage.color = Color.white;
            podiumImage.enabled = true;
            podiumImage.raycastTarget = false;
            podiumImage.preserveAspect = true;
            glow.raycastTarget = false;
            glow.preserveAspect = true;
            glow.material = null;
            glow.enabled = false;
            var palette = root.GetComponent<CultivationExhibitPalette>() ?? root.AddComponent<CultivationExhibitPalette>();
            palette.normalGlow = LoadActivationGlow("green");
            palette.rareGlow = LoadActivationGlow("blue");
            palette.epicGlow = LoadActivationGlow("purple");
            palette.legendGlow = LoadActivationGlow("gold");
            palette.inactivePortraitMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/material/default/sharedRender/DeckLockedFrame.mat");
            Check(podiumImage.sprite != null && palette.inactivePortraitMaterial != null, "共用静态底图与既有灰度材质引用完整");
            // 原成员保持，仅新增获准动态光效Image；仍用项目原生生成器。
            int before = binder.uiList.Count;
            if (!binder.uiList.Any(b => b.uiName == "imgQualityGlow")) {
                binder.uiList.Add(new UIBindComponentData { id = binder.uiList.Max(b => b.id) + 1, uiName = "imgQualityGlow", go = glow.gameObject, uiTypeName = "Image" });
            }
            Check(binder.uiList.Count == before + (oldGlow == null ? 1 : 0), "原绑定集合保留，仅新增品质光效");
            File.WriteAllText(binder.csharpAssetPath, File.ReadAllText("Library/AIUI/cultivation-activation-panel-source.txt"));
            var editor = UnityEditor.Editor.CreateEditor(binder, typeof(UIBinderInspector));
            try {
                typeof(UIBinderInspector).GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { binder.csharpAssetPath });
            } finally {
                UnityEngine.Object.DestroyImmediate(editor);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            observations.Add("Figma v15.1 actual55:1864已按R3样件156项自主核验；用户明确授权检查通过接入Unity。静态座台、品质光效独立层；原成长/技能/背包/保存不改。");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.Refresh();
    }

    private static Sprite LoadActivationGlow(string quality) {
        string path = "Assets/Art/atlasSource/default/cardCultivation/glow-" + quality + ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var common = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/atlasSource/default/cardCultivation/podium-metal.png");
        Check(sprite != null && sprite.texture.width == common.texture.width && sprite.texture.height == common.texture.height, "独立光效与本体同导入尺寸=" + quality);
        return sprite;
    }

    private static async Task WaitActivationCard(CardCultivationPanel panel, int id, bool owned) {
        var state = CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, id);
        for (int i = 0; i < 500; i++) {
            if (panel.CurrentCardId == id && panel.imgPortrait.enabled && panel.imgPortrait.sprite != null && panel.imgPortrait.sprite == ResourceManager.GetUnpackImage(state.portraitAddress)) {
                await Frames(10);
                var palette = panel.GetComponent<CultivationExhibitPalette>();
                var light = panel.GetComponentsInChildren<Image>(true).Single(v => v.name == "imgQualityGlow");
                if (light.enabled == owned && panel.imgPortrait.material == (owned ? Graphic.defaultGraphicMaterial : palette.inactivePortraitMaterial)) {
                    return;
                }
            }
            await Frames(2);
        }
        throw new TimeoutException("培养新座台状态加载=" + id + "/" + owned);
    }

    private static async Task RunPodiumActivationReview(PlayerProgressControl outerSession) {
        const string mainId = "default/mainView/MainPanel";
        var tables = CfgManager.tables;
        var cards = tables.CardObj.DataList.Where(c => c.ShowInLibrary).OrderBy(c => c.Id).ToArray();
        var function = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunction = function.GetValue(null);
        string folder = "Library/AIUI/PodiumActivation/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        function.SetValue(null, new FunctionControl(new FunctionModel(tables.FunctionObj.DataList, folder + "/functions.json")));
        try {
            // 两状态各自使用独立Play，避免测试中销毁整个主界面后留下旧列表的摊帧任务。
            foreach (bool owned in new[] { SessionState.GetString(Key + ".state", "").EndsWith("-owned", StringComparison.Ordinal) }) {
                var seed = new PlayerProgressSaveVO { initialized = true };
                foreach (var card in cards) {
                    if (owned) {
                        seed.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = card.Id, level = 6 });
                    }
                    seed.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = card.FragmentItemId, count = 9999 });
                }
                int writes = 0;
                var memory = new PlayerProgressControl(tables, seed, ignored => writes++);
                writes = 0;
                var oldPlayer = PlayerProgressControl.InstallValidationSession(memory);
                var scheme = new TeamSchemeSaveVO();
                scheme.schemes[0] = new[] { 2101, 0, 0, 0 };
                var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(tables, memory.Cards.model, scheme, ignored => { }));
                try {
                    await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
                    PanelMgr.ins.OpenPanel(mainId);
                    await Frames(15);
                    var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
                    Click(main.btnDeck.GetComponent<RectTransform>());
                    await Task.Delay(350);
                    await Frames(8);
                    var deck = main.GetComponentInChildren<DeckPresentationView>(true);
                    deck.RequestCardInfo(2101);
                    var panel = await CultivationOpened();
                    await WaitActivationCard(panel, 2101, owned);
                    var request = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                    int index = Array.IndexOf(request.orderedCardIds, 2101);
                    while (index-- > 0) {
                        Click(panel.btnPrevious.GetComponent<RectTransform>());
                        await Frames(2);
                    }
                    foreach (int id in request.orderedCardIds) {
                        await WaitActivationCard(panel, id, owned);
                        var current = tables.CardObj.Get(id);
                        var palette = panel.GetComponent<CultivationExhibitPalette>();
                        var light = panel.GetComponentsInChildren<Image>(true).Single(v => v.name == "imgQualityGlow");
                        Check(Vector2.Distance(panel.imgPodium.rectTransform.anchoredPosition, new Vector2(175, -233.7f)) < .01f && panel.imgPodium.rectTransform.sizeDelta == new Vector2(370, 123.39f), "16卡共用固定座台几何=" + id + "/" + owned);
                        Check(panel.imgPodium.sprite.name == "podium-metal" && panel.imgPodium.color == Color.white && panel.imgPodium.material == Graphic.defaultGraphicMaterial, "本体无品质替换/灰化/材质变化=" + id + "/" + owned);
                        Check(light.rectTransform.anchoredPosition == panel.imgPodium.rectTransform.anchoredPosition && light.rectTransform.sizeDelta == panel.imgPodium.rectTransform.sizeDelta, "光效与本体同坐标同尺寸=" + id + "/" + owned);
                        Check(light.transform.GetSiblingIndex() == panel.imgPodium.transform.GetSiblingIndex() + 1 && panel.imgPortrait.transform.GetSiblingIndex() > light.transform.GetSiblingIndex(), "本体/光效/立绘层序=" + id + "/" + owned);
                        Check(owned ? light.enabled && light.sprite == palette.GetGlow(current.Quality) : !light.enabled && light.sprite == null, "品质映射与未激活隐藏=" + id + "/" + owned);
                        Check(panel.imgPortrait.color == Color.white && panel.imgPortrait.material == (owned ? Graphic.defaultGraphicMaterial : palette.inactivePortraitMaterial), "仅立绘随拥有状态切换灰度=" + id + "/" + owned);
                        await PrepareStageCapture(panel);
                        Capture("runtime-" + id + (owned ? "-owned" : "-locked") + ".png");
                        if (panel.btnNext.interactable) {
                            Click(panel.btnNext.GetComponent<RectTransform>());
                        }
                    }
                    Check(!panel.btnNext.interactable, "尾卡不循环=" + owned);
                    Check(writes == 0, "32卡状态查看不写入隔离回调=" + owned);
                    Click(panel.btnReturn.GetComponent<RectTransform>());
                    await Task.Delay(350);
                    deck.RequestCardInfo(2101);
                    panel = await CultivationOpened();
                    await WaitActivationCard(panel, 2101, owned);
                    Check(panel.CurrentCardId == 2101, "回池重开无上一卡状态=" + owned);
                    if (!owned) {
                        Check(panel.btnUpgrade.interactable, "隔离碎片足够可验证解锁");
                        Click(panel.btnUpgrade.GetComponent<RectTransform>());
                        await WaitActivationCard(panel, 2101, true);
                        Check(memory.Cards.model.GetState(2101).owned && writes == 1, "实际解锁一次事务即刷新颜色/光效，保存仅隔离回调一次");
                        await PrepareStageCapture(panel);
                        Capture("runtime-2101-unlocked-live.png");
                    }
                } finally {
                    PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
                    PanelMgr.ins.ClosePanelByType(mainId);
                    TeamEditControl.InstallValidationSession(oldTeam);
                    PlayerProgressControl.InstallValidationSession(oldPlayer);
                }
            }
            observations.Add("正式MainPanel→牌库→培养入口，16卡×未拥有/拥有，独立内存会话不改真实存档；一次解锁验证即时刷新。未执行GM、未替换正式技能图。截图为实际720×1280渲染，非游戏设备发布。");
        } finally {
            function.SetValue(null, oldFunction);
        }
    }
}
#endif
