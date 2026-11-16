#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

public static partial class CultivationMaxStateReviewValidation {
    private static void ApplyPowerMicroShiftR3() {
        const string path="Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try {
            var power=root.GetComponentInChildren<PowerDisplay>(true);
            var icon=power.transform.Find("imgPowerSymbol").GetComponent<RectTransform>();
            var title=power.transform.Find("imgPowerTitle").GetComponent<RectTransform>();
            var digits=power.digitsArea;
            if(icon.anchoredPosition.y==-9 && title.anchoredPosition.y==-8 && digits.anchoredPosition.y==-1) {
                Check(true,"R3三处磁盘增量已在位，无需重复写入");return;
            }
            Check(icon.anchoredPosition.y==-7 && title.anchoredPosition.y==-6 && digits.anchoredPosition.y==0,"当前装配对应已审核R3预览的R2输入");
            icon.anchoredPosition=new Vector2(icon.anchoredPosition.x,-9);
            title.anchoredPosition=new Vector2(title.anchoredPosition.x,-8);
            digits.anchoredPosition=new Vector2(digits.anchoredPosition.x,-1);
            foreach(var rect in new[]{icon,title,digits}) PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            Check(icon.anchoredPosition.y==-9 && title.anchoredPosition.y==-8 && digits.anchoredPosition.y==-1,"按用户明确位移+2/+2/+1接入培养实例");
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    // 只在内存读取真实Alpha统计，不保存或向模型传递截图。
    private static Rect MeasurePowerAlphaR3(Image[] keep) {
        var graphics=UnityEngine.Object.FindObjectsOfType<Graphic>();var enabled=graphics.Select(g=>g.enabled).ToArray();
        var renderers=UnityEngine.Object.FindObjectsOfType<Renderer>();var rendererEnabled=renderers.Select(r=>r.enabled).ToArray();
        var oldColor=camera.backgroundColor;var oldFlags=camera.clearFlags;var previous=RenderTexture.active;Texture2D image=null;
        try {
            foreach(var graphic in graphics)graphic.enabled=keep.Contains(graphic);foreach(var renderer in renderers)renderer.enabled=false;
            camera.backgroundColor=Color.clear;camera.clearFlags=CameraClearFlags.SolidColor;Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            var pixels=image.GetPixels32();int minX=target.width,minY=target.height,maxX=-1,maxY=-1;
            for(int y=0;y<target.height;y++)for(int x=0;x<target.width;x++)if(pixels[y*target.width+x].a>=32){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(maxX<0)throw new InvalidOperationException("Actual Alpha empty");
            return new Rect(minX,target.height-1-maxY,maxX-minX+1,maxY-minY+1);
        } finally {
            RenderTexture.active=previous;if(image)UnityEngine.Object.Destroy(image);
            for(int i=0;i<graphics.Length;i++)if(graphics[i])graphics[i].enabled=enabled[i];for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=rendererEnabled[i];
            camera.backgroundColor=oldColor;camera.clearFlags=oldFlags;Canvas.ForceUpdateCanvases();
        }
    }
    private static void CheckPowerMicroPixelsR3(CardCultivationPanel panel, int down) {
        var display=panel.GetComponentInChildren<PowerDisplay>(true);var icon=display.transform.Find("imgPowerSymbol").GetComponent<Image>();var title=display.transform.Find("imgPowerTitle").GetComponent<Image>();
        var ib=MeasurePowerAlphaR3(new[]{icon});var tb=MeasurePowerAlphaR3(new[]{title});
        Check(ib==new Rect(242,361+down,25,21),"实际图标Alpha相对已批准R2下移2px");
        Check(tb==new Rect(276,359+down,58,25),"实际艺术字Alpha相对已批准R2下移2px");
        display.SetValue(468);Canvas.ForceUpdateCanvases();var db=MeasurePowerAlphaR3(display.digitsArea.GetComponentsInChildren<Image>(false));
        Check(db==new Rect(391,356+down,87,30),"实际数字468 Alpha相对R2下移1px");
        foreach(double? value in new double?[]{0,9,999999,999999999999,null,468}) {
            display.SetValue(value);var active=display.digitsArea.GetComponentsInChildren<Image>(false);
            Check(active.Length==(value.HasValue?value.Value.ToString("0").Length:1),"位数/缺值刷新正确="+value);
            Check(display.digitsArea.anchoredPosition.y==-1,"数值变化保持R3数值区Y");
            foreach(var item in active){var corners=new Vector3[4];item.rectTransform.GetWorldCorners(corners);Check(corners.All(p=>display.digitsArea.rect.Contains((Vector2)display.digitsArea.InverseTransformPoint(p))),"数字保持原区域内="+value);}
        }
        display.SetValue(CardCultivationQuery.Read(CfgManager.tables,PlayerProgressControl.ins,panel.CurrentCardId).power);
        observations.Add("screen="+target.width+"x"+target.height+";actualAlpha32 icon="+ib+";title="+tb+";digits468="+db+";PNG files produced=0");
    }
    private static async Task RunPowerMicroShiftR3(PlayerProgressControl session) {
        const string mainId="default/mainView/MainPanel";var f=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);var old=f.GetValue(null);
        string folder="Library/AIUI/PowerMicroR3/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);f.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var team=new TeamSchemeSaveVO();team.schemes[0]=new[]{2205,2101,2301,2302};var previousTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,team,ignored=>{}));
        var oldScreen=UIScreenRegions.EditorScreenOverride;var oldRegions=UIScreenRegions.EditorOverride;
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});PanelMgr.ins.OpenPanel(mainId);await Frames(20);
            var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(10);var deck=main.GetComponentInChildren<DeckPresentationView>(true);
            foreach(int id in CfgManager.tables.CardObj.DataList.Where(c=>c.ShowInLibrary).Select(c=>c.Id)) {
                deck.RequestCardInfo(id);var panel=await CultivationOpened();await Frames(8);var display=panel.GetComponentInChildren<PowerDisplay>(true);
                var icon=display.transform.Find("imgPowerSymbol").GetComponent<RectTransform>();var title=display.transform.Find("imgPowerTitle").GetComponent<RectTransform>();
                Check(icon.anchoredPosition.y==-9 && title.anchoredPosition.y==-8 && display.digitsArea.anchoredPosition.y==-1,"16卡三处Y保持="+id);
                if(id==2205)CheckPowerMicroPixelsR3(panel,0);
                Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(250);await Frames(4);
            }
            ResizeViewportStage(720,1600);await Frames(5);deck.RequestCardInfo(2205);var longPanel=await CultivationOpened();await Frames(8);CheckPowerMicroPixelsR3(longPanel,320);
            Check(main.GetComponentInChildren<PowerDisplay>(true).digitsArea.anchoredPosition.y==0,"Main消费端数值区保持不动");
        } finally {PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);TeamEditControl.InstallValidationSession(previousTeam);f.SetValue(null,old);UIScreenRegions.EditorScreenOverride=oldScreen;UIScreenRegions.EditorOverride=oldRegions;}
    }
}
#endif
