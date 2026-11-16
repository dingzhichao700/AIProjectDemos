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
    [Serializable] private sealed class PowerAlignmentR2 {
        public float centerY, iconOriginalY, titleOriginalY, iconDown, titleDown;
    }
    private static Rect ActualPowerAlphaR2(string name) {
        var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(Path.Combine(SessionState.GetString(Key+".evidence",""),name+".png")));
        try {
            var pixels=image.GetPixels32();int minX=image.width,minY=image.height,maxX=-1,maxY=-1;
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)if(pixels[y*image.width+x].a>=32){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(maxX<0)throw new InvalidOperationException("Isolated layer contains no alpha: "+name);
            return new Rect(minX,image.height-1-maxY,maxX-minX+1,maxY-minY+1);
        } finally {UnityEngine.Object.Destroy(image);}
    }
    private static float ProbePowerY_R2(Image image,string name,float center) {
        var original=image.rectTransform.anchoredPosition;float best=0,error=float.MaxValue;
        var rows=new List<string>();
        for(int i=0;i<=24;i++) {
            float down=i*.25f;image.rectTransform.anchoredPosition=new Vector2(original.x,original.y-down);
            string sample=name+"-probe-"+i;CapturePowerLayerR2(sample,new[]{image});var bounds=ActualPowerAlphaR2(sample);float next=Mathf.Abs(bounds.center.y-center);
            rows.Add("down="+down+";alpha32="+bounds.ToString("F3")+";centerError="+next);
            if(next<error-.001f){error=next;best=down;}
        }
        File.WriteAllLines(Path.Combine(SessionState.GetString(Key+".evidence",""),name+"-probes.txt"),rows);
        image.rectTransform.anchoredPosition=new Vector2(original.x,original.y-best);
        CapturePowerLayerR2(name+"-aligned",new[]{image});
        Check(Mathf.Abs(ActualPowerAlphaR2(name+"-aligned").center.y-center)<=.51f,"独立实际Alpha中心误差不超过半像素="+name);
        return best;
    }
    private static void ApplyPowerPixelAlignmentR2() {
        var proposal=JsonUtility.FromJson<PowerAlignmentR2>(File.ReadAllText(Path.Combine(SessionState.GetString(Key+".evidence",""),"alignment-proposal.json")));
        const string path="Assets/Prefab/default/mainView/CardCultivationPanel.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try {
            var display=root.GetComponentInChildren<PowerDisplay>(true);
            var icon=display.transform.Find("imgPowerSymbol").GetComponent<RectTransform>();var title=display.transform.Find("imgPowerTitle").GetComponent<RectTransform>();
            Check(icon.anchoredPosition.y==proposal.iconOriginalY && title.anchoredPosition.y==proposal.titleOriginalY,"原位与实际像素采样输入一致");
            icon.anchoredPosition=new Vector2(icon.anchoredPosition.x,proposal.iconOriginalY-proposal.iconDown);
            title.anchoredPosition=new Vector2(title.anchoredPosition.x,proposal.titleOriginalY-proposal.titleDown);
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon);PrefabUtility.RecordPrefabInstancePropertyModifications(title);
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    [Serializable] private sealed class PowerPixelInfoR2 {
        public string name, spriteName, spritePath, textureName, material, rectScreen, spriteRect, textureRect, pivot, padding;
        public bool preserveAspect, packed;
        public int textureWidth, textureHeight;
        public Vector3[] meshVertices;
        public Vector2[] meshUV;
    }

    private static void CapturePowerLayerR2(string name, Image[] keep) {
        var all=UnityEngine.Object.FindObjectsOfType<Graphic>();
        var graphics=all.Select(g=>g.enabled).ToArray();
        var renderers=UnityEngine.Object.FindObjectsOfType<Renderer>();
        var rendererStates=renderers.Select(r=>r.enabled).ToArray();
        var flags=camera.clearFlags;var color=camera.backgroundColor;
        try {
            foreach(var g in all) g.enabled=keep.Contains(g);
            foreach(var r in renderers) r.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            Canvas.ForceUpdateCanvases();
            Capture(name+".png");
            var metadata=new List<string>();
            foreach(var g in keep) {
                Sprite sprite=g.overrideSprite!=null?g.overrideSprite:g.sprite;
                var mesh=g.canvasRenderer.GetMesh();
                var data=new PowerPixelInfoR2 {name=g.name,spriteName=sprite.name,spritePath=AssetDatabase.GetAssetPath(sprite),textureName=sprite.texture.name,
                    material=g.material.shader.name,rectScreen=ScreenBounds(g.rectTransform).ToString("F4"),spriteRect=sprite.rect.ToString("F4"),
                    textureRect=sprite.textureRect.ToString("F4"),pivot=sprite.pivot.ToString("F4"),padding=UnityEngine.Sprites.DataUtility.GetPadding(sprite).ToString("F4"),
                    preserveAspect=g.preserveAspect,packed=sprite.packed,textureWidth=sprite.texture.width,textureHeight=sprite.texture.height,meshVertices=mesh.vertices,meshUV=mesh.uv};
                metadata.Add(JsonUtility.ToJson(data,true));
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key+".evidence",""),name+".json"),"["+string.Join(",",metadata)+"]");
        } finally {
            for(int i=0;i<all.Length;i++) if(all[i])all[i].enabled=graphics[i];
            for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=rendererStates[i];
            camera.clearFlags=flags;camera.backgroundColor=color;Canvas.ForceUpdateCanvases();
        }
    }

    private static async Task RunPowerPixelsR2(PlayerProgressControl session, bool confirm = false) {
        const string mainId="default/mainView/MainPanel";
        var f=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);var old=f.GetValue(null);
        string folder="Library/AIUI/PowerPixelsR2/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        f.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var scheme=new TeamSchemeSaveVO();scheme.schemes[0]=new[]{2205,2101,2301,2302};
        var oldTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,scheme,ignored=>{}));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(20);var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(12);
            var deck=main.GetComponentInChildren<DeckPresentationView>(true);deck.RequestCardInfo(2205);
            var panel=await CultivationOpened();await WaitPodiumCard(panel,2205);await PrepareStageCapture(panel);
            var display=panel.GetComponentInChildren<PowerDisplay>(true);
            var icon=display.transform.Find("imgPowerSymbol").GetComponent<Image>();
            var title=display.transform.Find("imgPowerTitle").GetComponent<Image>();
            var backing=panel.GetComponentsInChildren<Image>(false).Single(i=>i.name=="imgUpgradeChrome");
            Check(display.DisplayedValue=="468","真实查询雷兽6级战力468");
            Capture("runtime-current.png");
            CapturePowerLayerR2("icon-current",new[]{icon});
            CapturePowerLayerR2("title",new[]{title});
            CapturePowerLayerR2("digits-468",display.digitsArea.GetComponentsInChildren<Image>(false));
            CapturePowerLayerR2("backing",new[]{backing});
            float center=target.height-ScreenBounds(display.GetComponent<RectTransform>()).center.y;
            if(confirm) {
                Check(Mathf.Abs(ActualPowerAlphaR2("icon-current").center.y-center)<=.51f,"正式Prefab图标实际Alpha纵向对齐");
                Check(Mathf.Abs(ActualPowerAlphaR2("title").center.y-center)<=.51f,"正式Prefab艺术字实际Alpha纵向对齐");
            } else {
                var iconOld=icon.rectTransform.anchoredPosition;var titleOld=title.rectTransform.anchoredPosition;
                try {
                    var proposal=new PowerAlignmentR2 {centerY=center,iconOriginalY=iconOld.y,titleOriginalY=titleOld.y};
                    proposal.iconDown=ProbePowerY_R2(icon,"icon",center);proposal.titleDown=ProbePowerY_R2(title,"title",center);
                    File.WriteAllText(Path.Combine(SessionState.GetString(Key+".evidence",""),"alignment-proposal.json"),JsonUtility.ToJson(proposal,true));
                    Capture("runtime-aligned-candidate.png");
                } finally {icon.rectTransform.anchoredPosition=iconOld;title.rectTransform.anchoredPosition=titleOld;}
            }
            var position=icon.rectTransform.anchoredPosition;
            try {
                icon.rectTransform.anchoredPosition=new Vector2(0,position.y);await PrepareStageCapture(panel);
                Capture("runtime-without-x-compensation.png");CapturePowerLayerR2("icon-without-x-compensation",new[]{icon});
            } finally {icon.rectTransform.anchoredPosition=position;}
            display.SetValue(999999);await Frames(2);CapturePowerLayerR2("digits-6",display.digitsArea.GetComponentsInChildren<Image>(false));
            display.SetValue(9);await Frames(2);CapturePowerLayerR2("digits-1",display.digitsArea.GetComponentsInChildren<Image>(false));
            display.SetValue(468);await PrepareStageCapture(panel);Capture("runtime-restored.png");
            Check(icon.rectTransform.anchoredPosition==position && display.DisplayedValue=="468","隔离采样后恢复原图标及真实数值");
            if(confirm) {
                foreach(int id in CfgManager.tables.CardObj.DataList.Where(c=>c.ShowInLibrary).Select(c=>c.Id)) {
                    Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(250);await Frames(4);
                    deck.RequestCardInfo(id);panel=await CultivationOpened();await WaitPodiumCard(panel,id);await Frames(4);
                    display=panel.GetComponentInChildren<PowerDisplay>(true);icon=display.transform.Find("imgPowerSymbol").GetComponent<Image>();title=display.transform.Find("imgPowerTitle").GetComponent<Image>();
                    float cardCenter=target.height-ScreenBounds(display.GetComponent<RectTransform>()).center.y;
                    CapturePowerLayerR2("card-"+id+"-icon",new[]{icon});CapturePowerLayerR2("card-"+id+"-title",new[]{title});
                    Check(Mathf.Abs(ActualPowerAlphaR2("card-"+id+"-icon").center.y-cardCenter)<=.51f && Mathf.Abs(ActualPowerAlphaR2("card-"+id+"-title").center.y-cardCenter)<=.51f,"16卡实际Alpha逐卡对齐="+id);
                }
                Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(250);await Frames(4);
                var oldScreen=UIScreenRegions.EditorScreenOverride;var oldRegions=UIScreenRegions.EditorOverride;
                try {
                    ResizeViewportStage(720,1600);await Frames(5);deck.RequestCardInfo(2205);panel=await CultivationOpened();await WaitPodiumCard(panel,2205);await PrepareStageCapture(panel);
                    display=panel.GetComponentInChildren<PowerDisplay>(true);icon=display.transform.Find("imgPowerSymbol").GetComponent<Image>();title=display.transform.Find("imgPowerTitle").GetComponent<Image>();
                    CapturePowerLayerR2("long-icon",new[]{icon});CapturePowerLayerR2("long-title",new[]{title});Capture("runtime-long.png");
                    float longCenter=target.height-ScreenBounds(display.GetComponent<RectTransform>()).center.y;
                    Check(Mathf.Abs(ActualPowerAlphaR2("long-icon").center.y-longCenter)<=.51f && Mathf.Abs(ActualPowerAlphaR2("long-title").center.y-longCenter)<=.51f,"长屏实际Alpha中心对齐");
                } finally {UIScreenRegions.EditorScreenOverride=oldScreen;UIScreenRegions.EditorOverride=oldRegions;}
            }
            observations.Add("真实界面逐元素透明渲染，只读诊断；输出Sprite实际rect/UV/网格及Alpha图，不使用源PNG公式自证。候选位移只在隔离内存中切换，未写Prefab。");
        } finally {PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);TeamEditControl.InstallValidationSession(oldTeam);f.SetValue(null,old);}
    }
}
#endif
