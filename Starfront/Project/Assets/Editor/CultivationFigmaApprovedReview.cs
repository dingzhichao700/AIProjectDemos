#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {

    // 已审结构的持久接入通过原培养请求队列执行，不创建临时编辑器或预览替代物。
    private static void ApplyApprovedFigma143() {
        const string prefab = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try {
            var binders = root.GetComponentsInChildren<UIBinder>(true);
            var stageBinder = binders.Single(b => b.csharpAssetPath.EndsWith("/CardCultivationStageItem.cs"));
            var skillBinder = binders.Single(b => b.csharpAssetPath.EndsWith("/CardCultivationSkillItem.cs"));
            var stage = stageBinder.transform;
            var list = stage.GetComponentInParent<ScrollList>();
            var scroll = list.GetComponent<ScrollRect>();
            ApprovedRect(list.transform,38,738,640,244);
            var vp = scroll.viewport;
            var oldRectMask = vp.GetComponent<RectMask2D>();
            if (oldRectMask != null) UnityEngine.Object.DestroyImmediate(oldRectMask);
            var hit = vp.GetComponent<Image>() ?? vp.gameObject.AddComponent<Image>();
            hit.color = Color.white; hit.raycastTarget = true;
            var clip = vp.GetComponent<Mask>() ?? vp.gameObject.AddComponent<Mask>();
            clip.showMaskGraphic = false;
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.pivot = new Vector2(0,1);
            vp.offsetMin = vp.offsetMax = Vector2.zero;
            ApprovedRect(scroll.content,0,0,640,0);
            ApprovedRect(stage,0,0,640,44); list.horiLimitNum = 1; list.vertGap = 6;
            foreach (string name in new[]{"imgGained","imgNotGained"}) ApprovedRect(stage.Find(name),0,0,640,44);
            var oldText = stage.Find("txtStage").GetComponent<TextMeshProUGUI>();
            var level = ApprovedStageText(stage,oldText,"txtStageLevel",6,100,24,true,TextAlignmentOptions.Center);
            var effect = ApprovedStageText(stage,oldText,"txtStageEffect",136,386,20,false,TextAlignmentOptions.MidlineLeft);
            var state = ApprovedStageText(stage,oldText,"txtStageStatus",532,90,18,true,TextAlignmentOptions.MidlineRight);
            UnityEngine.Object.DestroyImmediate(oldText.gameObject);
            stageBinder.uiList.RemoveAll(b => b.uiName == "txtStage");
            foreach (var txt in new[]{level,effect,state}) {
                stageBinder.uiList.Add(new UIBindComponentData { id=stageBinder.uiList.Max(b=>b.id)+1, uiName=txt.name, go=txt.gameObject,uiTypeName="TextMeshProUGUI" });
            }
            // 内容高度由ScrollList计算，原生ScrollRect驱动滑块与拖动，空态自动隐藏。
            var barRoot = ApprovedChild(list.transform.parent,"stageScrollbar");
            ApprovedRect(barRoot,685,738,6,244);
            var barHit=barRoot.gameObject.AddComponent<Image>();barHit.color=Color.clear;barHit.raycastTarget=true;
            var handle=ApprovedChild(barRoot,"imgStageScrollbarThumb");
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var hi=handle.gameObject.AddComponent<Image>();hi.color=new Color32(106,247,255,255);hi.raycastTarget=true;
            hi.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");hi.type=Image.Type.Sliced;
            var bar=barRoot.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=hi;bar.direction=Scrollbar.Direction.BottomToTop;bar.transition=Selectable.Transition.None;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.verticalScrollbarSpacing=0;

            var skill=skillBinder.transform;
            var art=ApprovedChild(skill,"boxSkillArtwork");ApprovedRect(art,23.5f,34,79,79);
            var opening=ApprovedChild(art,"boxSkillOpening");ApprovedRect(opening,0,0,79,79);
            var maskImage=opening.gameObject.AddComponent<Image>();
            maskImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/unpack/default/skillControls/skillOpeningSquareMask.png");
            maskImage.raycastTarget=false;
            var mask=opening.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var backing=skill.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="imgSkillBlackBacking");backing.SetParent(opening,false);ApprovedRect(backing,0,0,79,79);
            var black=backing.GetComponent<Image>();black.color=Color.black;black.sprite=null;black.raycastTarget=false;
            var icon=skill.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="imgSkillIcon");icon.SetParent(opening,false);ApprovedRect(icon,12,12,55,55);icon.GetComponent<Image>().raycastTarget=false;
            var rim=skill.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="imgSkillFrame");rim.SetParent(art,false);ApprovedRect(rim,0,0,79,79);rim.GetComponent<Image>().raycastTarget=false;
            rim.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/unpack/default/skillControls/skillFrameSquareRim.png");
            var lockIcon=skill.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="imgSkillLock");lockIcon.SetParent(art,false);ApprovedRect(lockIcon,24.5f,18.5f,30,42);lockIcon.GetComponent<Image>().raycastTarget=false;
            Check(maskImage.sprite!=null && rim.GetComponent<Image>().sprite!=null,"复用已审79px框和真实Alpha开口");
            Check(skillBinder.uiList.Count==7,"技能原七个成员引用保持，不把静态Mask加入绑定");
            Check(stageBinder.uiList.Count==5 && stageBinder.uiList.All(b=>b.go!=null),"阶段五个动态成员引用完整");
            string staged=File.ReadAllText("Library/AIUI/cultivation-approved-stage-source.txt");
            File.WriteAllText(stageBinder.csharpAssetPath,staged);
            var editor=UnityEditor.Editor.CreateEditor(stageBinder,typeof(UIBinderInspector));
            try {
                typeof(UIBinderInspector).GetMethod("GenerateUiBind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,new object[]{stageBinder.csharpAssetPath});
            } finally { UnityEngine.Object.DestroyImmediate(editor); }
            PrefabUtility.SaveAsPrefabAsset(root,prefab);
            AssetDatabase.SaveAssets();
            observations.Add("Figma v14.3实际结构已获用户批准。仅阶段区及已审技能格变更；成员使用项目UIBinderInspector生成，无业务/数据/持久化改动。");
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.Refresh();
    }

    private static RectTransform ApprovedChild(Transform parent,string name) {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;
    }

    private static void ApprovedRect(Transform t,float x,float y,float w,float h) {
        var rt=(RectTransform)t;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;
    }

    private static TextMeshProUGUI ApprovedStageText(Transform parent,TextMeshProUGUI source,string name,float x,float width,float size,bool bold,TextAlignmentOptions align) {
        var text=UnityEngine.Object.Instantiate(source,parent);text.name=name;ApprovedRect(text.transform,x,0,width,44);
        text.text="";text.fontSize=size;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        text.enableAutoSizing=false;text.enableWordWrapping=false;text.alignment=align;text.color=new Color32(242,247,255,255);text.raycastTarget=false;
        return text;
    }

    private static async Task ValidateApprovedCultivationLayout(CardCultivationPanel panel) {
        var scroll=panel.listStages.GetComponent<ScrollRect>();
        Check(((RectTransform)panel.listStages.transform).sizeDelta==new Vector2(640,244),"阶段区640×244符合实际Figma");
        Check(scroll.viewport.GetComponent<Mask>()!=null && !scroll.viewport.GetComponent<Mask>().showMaskGraphic,"阶段使用真实Viewport Mask，底图不显示");
        Check(scroll.viewport.GetComponent<RectMask2D>()==null,"无运行时叠加旧RectMask2D");
        Check(scroll.verticalScrollbar!=null && !scroll.verticalScrollbar.gameObject.activeSelf,"五行无需滚动时滑块自动隐藏");
        foreach(var row in panel.listStages.GetComponentsInChildren<CardCultivationStageItem>(false)) {
            var texts=row.GetComponentsInChildren<TextMeshProUGUI>();
            Check(texts.Length==3,"独立等级、正文、获得状态三个文本");
            var effect=texts.Single(t=>t.name=="txtStageEffect");
            Check(effect.fontSize==20 && effect.rectTransform.anchoredPosition==new Vector2(136,0),"正文20px与136px起点");
            Check(effect.preferredWidth<=effect.rectTransform.rect.width,"正文一行完整容纳无溢出");
            Check(texts.Single(t=>t.name=="txtStageLevel").alignment==TextAlignmentOptions.Center,"等级在色块内居中");
            Check(texts.Single(t=>t.name=="txtStageStatus").alignment==TextAlignmentOptions.MidlineRight,"状态靠右且垂直居中");
            Check(row.GetComponent<UIBinder>().uiList.Count==5,"运行条目五个成员绑定");
        }
        var skills=panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).OrderBy(t=>t.transform.position.x).ToArray();
        Check(skills.Length==4,"四个已审技能复用实例");
        foreach(var cell in skills) {
            var icon=cell.imgSkillIcon.rectTransform;
            Check(cell.imgSkillFrame.rectTransform.sizeDelta==new Vector2(79,79) && icon.sizeDelta==new Vector2(55,55),"技能79/55实际尺寸");
            Check(icon.anchoredPosition==new Vector2(12,-12) && icon.parent.name=="boxSkillOpening","内图按共同开口定位");
            var mask=icon.parent.GetComponent<Mask>();
            Check(mask!=null && !mask.showMaskGraphic && mask.GetComponent<Image>().sprite!=null,"技能真实Alpha遮罩生效");
            Check(cell.imgSkillFrame.material==Graphic.defaultGraphicMaterial,"框体默认UI材质");
            Check(cell.GetComponent<UIBinder>().uiList.Count==7,"技能七成员未扩大");
        }
        var records=CfgManager.tables.CardGrowthStageObj.DataList;
        var extra=new[]{
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":6,\"unlockLevel\":10,\"description\":\"单位攻击范围 +X\",\"sortOrder\":6}")),
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":7,\"unlockLevel\":10,\"description\":\"建筑生命值 +X%\",\"sortOrder\":7}")),
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":8,\"unlockLevel\":10,\"description\":\"单位攻击力 +X\",\"sortOrder\":8}"))
        };
        records.AddRange(extra);
        try {
            typeof(CardCultivationPanel).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);await Frames(12);
            scroll.verticalNormalizedPosition=1;await Frames(8);Canvas.ForceUpdateCanvases();
            Check(scroll.content.rect.height==394,"八条测试内容高394，正式表不写入");
            Check(scroll.verticalScrollbar.gameObject.activeSelf && Mathf.Abs(scroll.verticalScrollbar.size-244f/394)<.01f,"滑块随内容长度计算并显示");
            await PrepareStageCapture(panel);Capture("runtime-approved-v14.3-eight-top.png");
            scroll.verticalNormalizedPosition=0;await Frames(12);
            Check(Mathf.Abs(scroll.content.anchoredPosition.y-150)<1,"滚动到底覆盖后续三条");
            Capture("runtime-approved-v14.3-eight-bottom.png");
            var previousScroll=scroll.content.anchoredPosition;
            Click(skills[0].btnSkill.GetComponent<RectTransform>());await Frames(12);
            Check(UnityEngine.Object.FindObjectOfType<CardSkillDetailPanel>()!=null,"新Mask结构下按钮仍可打开技能详情");
            CardSkillDetailControl.Close();await Task.Delay(400);await Frames(10);
            Check(panel.CurrentCardId==2101 && Vector2.Distance(previousScroll,scroll.content.anchoredPosition)<.1f,"关闭技能详情恢复当前卡和阶段滚动位置");
            Click(panel.btnNext.GetComponent<RectTransform>());await WaitPodiumCard(panel,2102);await Frames(12);
            Canvas.ForceUpdateCanvases();await Frames(3);
            observations.Add("empty-card="+panel.CurrentCardId+";rows="+panel.listStages.arraySource.Count+";content="+scroll.content.rect+";position="+scroll.content.anchoredPosition+";viewport="+scroll.viewport.rect+";scrollbar="+scroll.verticalScrollbar.gameObject.activeSelf);
            Capture("runtime-empty-card-after-switch.png");
            Check(panel.listStages.arraySource.Count==0,"切到无效果卡不残留行");
            Check(!scroll.verticalScrollbar.gameObject.activeSelf,"切到无效果卡滑块隐藏");
            Click(panel.btnPrevious.GetComponent<RectTransform>());await WaitPodiumCard(panel,2101);await Frames(12);
            Check(panel.listStages.arraySource.Count==8 && Vector2.Distance(scroll.content.anchoredPosition,Vector2.zero)<.1f,"切回正确卡，列表回顶且复用状态正确");
        } finally {
            foreach(var row in extra)records.Remove(row);
            typeof(CardCultivationPanel).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);await Frames(8);
        }
        observations.Add("八条X占位只在隔离内存，真实配置不由夹具写入。截图为实际正式MainPanel牌库入口和生产Prefab的隔离运行相机，不是真机或发布构建。");
    }
}
#endif
