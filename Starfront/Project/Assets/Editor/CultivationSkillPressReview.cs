#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {

    private static void ApplySkillPressFix() {
        const string path = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            var binder = root.GetComponentsInChildren<UIBinder>(true).Single(b => b.csharpAssetPath.EndsWith("/CardCultivationSkillItem.cs"));
            var item = binder.transform;
            var button = item.Find("btnSkill").GetComponent<RectTransform>();
            var art = item.Find("boxSkillArtwork").GetComponent<RectTransform>();
            var staticVisuals = new[] { "imgSkillKind", "txtSkillKind", "txtSkillState" }.Select(name => item.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == name)).ToArray();
            // 标签不属于按压图组，保留世界坐标和原点击矩形。
            foreach (var visual in staticVisuals) {
                visual.SetParent(item, true);
                foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true)) {
                    graphic.raycastTarget = false;
                }
            }
            Vector3 oldTopLeft = button.TransformPoint(new Vector3(button.rect.xMin, button.rect.yMax));
            button.pivot = new Vector2(.5f, .475f);
            Vector3 newTopLeft = button.TransformPoint(new Vector3(button.rect.xMin, button.rect.yMax));
            button.position += oldTopLeft - newTopLeft;
            art.SetParent(button, true);
            var feedback = new SerializedObject(button.GetComponent<GameButton>());
            feedback.FindProperty("label").objectReferenceValue = null;
            feedback.ApplyModifiedPropertiesWithoutUndo();
            Check(binder.uiList.Count == 7 && binder.uiList.All(b => b.go != null), "原技能七绑定不变");
            Check(button.sizeDelta == new Vector2(126, 140), "完整126×140点击范围保持");
            Check(art.parent == button && staticVisuals.All(t => t.parent == item), "图标组属于按钮，类型/状态文字静止");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static float SkillWorldWidth(RectTransform rect) {
        var points = new Vector3[4];
        rect.GetWorldCorners(points);
        return Vector3.Distance(points[0], points[3]);
    }

    private static async Task RunSkillPressReview(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var function = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunction = function.GetValue(null);
        string folder = "Library/AIUI/SkillPress/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        function.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
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
            deck.RequestCardInfo(2101);
            var panel = await CultivationOpened();
            await WaitPodiumCard(panel, 2101);
            await Frames(15);
            var cells = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).OrderBy(v => v.transform.GetSiblingIndex()).ToArray();
            Check(cells.Length == 4, "实际牌库培养入口四技能完整");
            await PrepareStageCapture(panel);
            Capture("runtime-skill-normal.png");
            foreach (var cell in cells) {
                var rect = cell.btnSkill.GetComponent<RectTransform>();
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
                float iconWidth = SkillWorldWidth(cell.imgSkillIcon.rectTransform);
                float frameWidth = SkillWorldWidth(cell.imgSkillFrame.rectTransform);
                float kindWidth = SkillWorldWidth(cell.txtSkillKind.rectTransform);
                float stateWidth = SkillWorldWidth(cell.txtSkillState.rectTransform);
                ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                await Task.Delay(180);
                await Frames(12);
                float iconRatio = SkillWorldWidth(cell.imgSkillIcon.rectTransform) / iconWidth;
                Check(Mathf.Abs(iconRatio - .95f) < .003f, "技能图标按压缩至95%=" + cell.txtSkillKind.text);
                Check(Mathf.Abs(SkillWorldWidth(cell.imgSkillFrame.rectTransform) / frameWidth - .95f) < .003f, "框体/开口随图标一致缩放=" + cell.txtSkillKind.text);
                Check(Mathf.Abs(SkillWorldWidth(cell.txtSkillKind.rectTransform) - kindWidth) < .01f && Mathf.Abs(SkillWorldWidth(cell.txtSkillState.rectTransform) - stateWidth) < .01f, "类型和阶数/未解锁文字不缩放=" + cell.txtSkillKind.text);
                Check(cell.imgSkillIcon.transform.parent.GetComponent<Mask>() != null, "真实开口Mask保持=" + cell.txtSkillKind.text);
                await PrepareStageCapture(panel);
                Capture("runtime-skill-pressed-" + Array.IndexOf(cells, cell) + ".png");
                ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                await Task.Delay(180);
                await Frames(12);
                Check(Mathf.Abs(SkillWorldWidth(cell.imgSkillIcon.rectTransform) / iconWidth - 1) < .003f, "滑出取消后恢复原大小=" + cell.txtSkillKind.text);
                Click(rect);
                for (int i = 0; i < 150 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) {
                    await Frames(2);
                }
                Check(PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId), "点击仍打开对应技能详情=" + cell.txtSkillKind.text);
                CardSkillDetailControl.Close();
                await Task.Delay(350);
                await Frames(10);
                Check(panel.CurrentCardId == 2101 && Mathf.Abs(SkillWorldWidth(cell.imgSkillIcon.rectTransform) / iconWidth - 1) < .003f, "详情返回原卡且无残留缩放=" + cell.txtSkillKind.text);
            }
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Task.Delay(350);
            deck.RequestCardInfo(2101);
            panel = await CultivationOpened();
            await Frames(12);
            Check(panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).All(c => c.btnSkill.transform.localScale == Vector3.one), "回池重开四按钮基准恢复");
            observations.Add("通过正式MainPanel→牌库→培养验证指针按压、滑出取消、四技能详情往返和池复用。仅Prefab父子归属及旋转中心修复，复用GameButton既有95%规则；无技能、成长、背包、存档业务改动。");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            function.SetValue(null, oldFunction);
        }
    }
}
#endif
