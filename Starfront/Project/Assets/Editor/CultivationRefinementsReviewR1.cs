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
    private static void ExportRefinementBinder(UIBinder binder) {
        var inspector=UnityEditor.Editor.CreateEditor(binder);
        try {
            inspector.GetType().GetMethod("GenerateUiBind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(inspector,new object[]{binder.csharpAssetPath});
        } finally { UnityEngine.Object.DestroyImmediate(inspector); }
    }
    private static void AddRefinementBinding(UIBinder binder,GameObject go) {
        if(!binder.uiList.Any(v=>v.uiName==go.name))binder.uiList.Add(new UIBindComponentData{id=binder.uiList.Max(v=>v.id)+1,uiName=go.name,uiTypeName="Image",go=go});
    }
    private static void SetupRefinementsR1() {
        var shadeImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/atlasSource/default/cardCultivation/skill-locked-shade-55.png");
        shadeImporter.textureType=TextureImporterType.Sprite;shadeImporter.spriteImportMode=SpriteImportMode.Single;shadeImporter.mipmapEnabled=false;shadeImporter.alphaIsTransparency=true;shadeImporter.textureCompression=TextureImporterCompression.Uncompressed;shadeImporter.SaveAndReimport();
        const string path="Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try {
            var main=root.GetComponentsInChildren<UIBinder>(true).Single(b=>b.csharpAssetPath.EndsWith("/CardCultivationPanel.cs"));
            var plate=root.GetComponentsInChildren<Image>(true).Single(i=>i.name=="imgProfession");
            plate.type=Image.Type.Sliced;
            if(plate.sprite.border.x<1) {
                string source=AssetDatabase.GetAssetPath(plate.sprite);var importer=(TextureImporter)AssetImporter.GetAtPath(source);
                importer.spriteBorder=new Vector4(16,0,16,0);importer.SaveAndReimport();plate.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(source);
            }
            AddRefinementBinding(main,plate.gameObject);
            var skill=root.GetComponentsInChildren<UIBinder>(true).Single(b=>b.csharpAssetPath.EndsWith("/CardCultivationSkillItem.cs"));
            var icon=skill.GetComponentsInChildren<Image>(true).Single(i=>i.name=="imgSkillIcon");
            var mask=icon.transform.parent.GetComponent<Mask>();Check(mask!=null,"保留原共同开口Mask");
            var old=icon.transform.parent.Find("imgSkillLockedShade");
            var shade=old?old.GetComponent<Image>():new GameObject("imgSkillLockedShade",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            var rt=shade.rectTransform;rt.SetParent(icon.transform.parent,false);rt.anchorMin=icon.rectTransform.anchorMin;rt.anchorMax=icon.rectTransform.anchorMax;rt.pivot=icon.rectTransform.pivot;rt.sizeDelta=icon.rectTransform.sizeDelta;rt.anchoredPosition=icon.rectTransform.anchoredPosition;rt.SetSiblingIndex(icon.transform.GetSiblingIndex()+1);
            shade.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/atlasSource/default/cardCultivation/skill-locked-shade-55.png");shade.color=Color.white;shade.raycastTarget=false;shade.enabled=false;
            AddRefinementBinding(skill,shade.gameObject);
            Check(rt.sizeDelta==new Vector2(55,55),"黑层与图标同55尺寸");
            Check(skill.uiList.Count==8,"技能保留原七绑定并增加锁定黑层");
            ExportRefinementBinder(main);ExportRefinementBinder(skill);
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            Check(shade.sprite!=null,"半透明黑图片引用就绪");
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static async Task RunRefinementsR1(PlayerProgressControl session) {
        const string mainId="default/mainView/MainPanel";
        var field=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);object prior=field.GetValue(null);
        string folder="Library/AIUI/RefinementsR1/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        field.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var team=new TeamSchemeSaveVO();team.schemes[0]=new[]{2101,0,0,0};var oldTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,team,ignored=>{}));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(15);var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(8);
            var deck=main.GetComponentInChildren<DeckPresentationView>(true);deck.RequestCardInfo(2101);var panel=await CultivationOpened();await WaitPodiumCard(panel,2101);await Frames(16);
            var plate=panel.GetComponentsInChildren<Image>(true).Single(i=>i.name=="imgProfession");
            float ink=panel.txtProfession.textBounds.size.x;
            Check(Mathf.Abs(plate.rectTransform.rect.width-Mathf.Ceil(ink+28))<.01f,"类型底板按当前实际字形宽度加左右14计算");
            Check(plate.type==Image.Type.Sliced && plate.sprite.border.x>0,"底板端部切片保持");
            string profession=panel.txtProfession.text;
            foreach(string value in new[]{"先锋","重装","支援","职业：—"}) {
                panel.txtProfession.text=value;
                typeof(CardCultivationPanel).GetMethod("FitProfessionBackground",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
                var b=panel.txtProfession.textBounds;var t=panel.txtProfession.rectTransform;
                var left=plate.rectTransform.InverseTransformPoint(t.TransformPoint(new Vector3(b.min.x,0,0))).x-plate.rectTransform.rect.xMin;
                var right=plate.rectTransform.rect.xMax-plate.rectTransform.InverseTransformPoint(t.TransformPoint(new Vector3(b.max.x,0,0))).x;
                Check(Mathf.Abs(left-14)<.1f && right>=13.9f && right<15.1f,"不同文字实际字形左右内距="+value);
            }
            panel.txtProfession.text=profession;typeof(CardCultivationPanel).GetMethod("FitProfessionBackground",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
            var cells=panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).OrderBy(c=>c.transform.GetSiblingIndex()).ToArray();Check(cells.Length==4,"四技能依序完整");
            await PrepareStageCapture(panel);Capture("runtime-full.png");
            foreach(var cell in cells) {
                var shade=cell.GetComponentsInChildren<Image>(true).Single(i=>i.name=="imgSkillLockedShade");
                bool locked=cell.imgSkillLock.enabled;
                Check(shade.enabled==locked,"黑层只在未解锁显示="+cell.txtSkillKind.text);
                Check(shade.rectTransform.rect.size==cell.imgSkillIcon.rectTransform.rect.size && shade.transform.parent==cell.imgSkillIcon.transform.parent,"黑层同尺寸同Mask="+cell.txtSkillKind.text);
                Check(shade.transform.GetSiblingIndex()>cell.imgSkillIcon.transform.GetSiblingIndex() && !shade.raycastTarget,"黑层在内图上且不拦点击="+cell.txtSkillKind.text);
                Check(cell.imgSkillIcon.sprite!=null,"新技能图已实际加载="+cell.txtSkillKind.text);
                var rect=cell.btnSkill.GetComponent<RectTransform>();var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};float sw=SkillWorldWidth(shade.rectTransform),kw=SkillWorldWidth(cell.txtSkillKind.rectTransform);
                ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject,pointer,ExecuteEvents.pointerDownHandler);await Task.Delay(180);await Frames(4);
                Check(Mathf.Abs(SkillWorldWidth(shade.rectTransform)/sw-.95f)<.003f && Mathf.Abs(SkillWorldWidth(cell.txtSkillKind.rectTransform)-kw)<.01f,"遮层随图按压而文字静止="+cell.txtSkillKind.text);
                ExecuteEvents.ExecuteHierarchy(cell.btnSkill.gameObject,pointer,ExecuteEvents.pointerExitHandler);await Task.Delay(180);
                Click(rect);for(int i=0;i<150&&!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId);i++)await Frames(2);
                Check(PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId),"含未解锁技能仍打开详情="+cell.txtSkillKind.text);await Frames(12);
                var detail=(CardSkillDetailPanel)PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId);
                var detailIcon=detail.GetComponentsInChildren<Image>(true).Single(i=>i.name=="imgSkillIcon");
                Check(detailIcon.sprite != null && detailIcon.sprite == cell.imgSkillIcon.sprite,"详情与培养共享配置权威Sprite="+cell.txtSkillKind.text);
                Capture("runtime-detail-"+Array.IndexOf(cells,cell)+".png");CardSkillDetailControl.Close();await Task.Delay(350);await Frames(6);
                Check(panel.CurrentCardId==2101 && shade.enabled==locked,"详情返回保留当前卡和锁层="+cell.txtSkillKind.text);
            }
            Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(350);deck.RequestCardInfo(2101);panel=await CultivationOpened();await Frames(15);
            Check(panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).All(c=>c.GetComponentsInChildren<Image>(true).Single(i=>i.name=="imgSkillLockedShade").enabled==c.imgSkillLock.enabled),"回池重开无旧黑层残留");
            await PrepareStageCapture(panel);Capture("runtime-reopened.png");
            observations.Add("仅既有资源和Prefab局部修订，用户本轮明确不要Figma；四图原路径/GUID保持，详情消费同图，无技能/成长表改动。类型读取TMP字形网格，黑层55同Mask，独立锁标和按压反馈保持。隔离会话不保存真实玩家数据。");
        }finally{CardSkillDetailControl.Close();PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);TeamEditControl.InstallValidationSession(oldTeam);field.SetValue(null,prior);}
    }
}
#endif
