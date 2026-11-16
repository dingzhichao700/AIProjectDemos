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
    private const float PowerIconShiftR1 = -3.78688525f;

    private static void ApplyPowerPositionR1() {
        const string path = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            var display = root.GetComponentInChildren<PowerDisplay>(true);
            var icon = display.transform.Find("imgPowerSymbol").GetComponent<RectTransform>();
            Check(icon.anchoredPosition == new Vector2(0, -3) && icon.sizeDelta == new Vector2(33, 33), "现有图标槽位符合测量输入");
            icon.anchoredPosition = new Vector2(PowerIconShiftR1, -3);
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            Check(Mathf.Abs(icon.anchoredPosition.x - PowerIconShiftR1) < .001f, "只在培养实例补偿图标X");
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void CheckPowerPositionR1(CardCultivationPanel panel, bool corrected) {
        var display = panel.GetComponentInChildren<PowerDisplay>(true);
        var icon = display.transform.Find("imgPowerSymbol").GetComponent<Image>();
        var rect = icon.rectTransform;
        Check(Mathf.Abs(rect.anchoredPosition.x - (corrected ? PowerIconShiftR1 : 0)) < .001f && rect.anchoredPosition.y == -3 && rect.sizeDelta == new Vector2(33,33), "仅培养图标X修正=" + panel.CurrentCardId);
        Check(icon.preserveAspect && !icon.raycastTarget && icon.color == Color.white, "图标保持比例与原Tint/点击行为");
        // 源PNG实测Alpha>=32主体[14,4,61,42]，不是Sprite整画布中心。
        var texture = new Texture2D(2,2);
        texture.LoadImage(File.ReadAllBytes("Assets/Art/unpack/default/sharedPower/ui-power-icon.png"));
        try {
            int minX=texture.width, maxX=-1;
            var pixels=texture.GetPixels32();
            for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++) if(pixels[y*texture.width+x].a>=32) { minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); }
            float scale=Mathf.Min(rect.rect.width/texture.width,rect.rect.height/texture.height);
            float visibleCenter=rect.anchoredPosition.x+(rect.rect.width-texture.width*scale)*.5f+(minX+maxX+1)*.5f*scale;
            Check(Mathf.Abs(visibleCenter-(corrected ? 16.5f : 20.286885f)) < .01f, "真实Alpha主体对槽位中心偏差="+(visibleCenter-16.5f).ToString("F4"));
        } finally { UnityEngine.Object.Destroy(texture); }
        foreach (double? value in new double?[] { 0, 9, 468, 999999, 999999999999, null, 468 }) {
            display.SetValue(value);
            Check(Mathf.Abs(rect.anchoredPosition.x-(corrected?PowerIconShiftR1:0))<.001f,"位数变化不重置图标X="+value);
            var active=display.digitsArea.GetComponentsInChildren<Image>(false);
            Check(active.Length==(value.HasValue?value.Value.ToString("0").Length:1),"动态数字和缺值位数不变="+value);
            foreach(var image in active) {
                var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);
                Check(corners.All(p=>display.digitsArea.rect.Contains((Vector2)display.digitsArea.InverseTransformPoint(p))),"数字仍在原数值区="+value);
            }
        }
        display.SetValue(CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, panel.CurrentCardId).power);
        observations.Add("card="+panel.CurrentCardId+";iconX="+rect.anchoredPosition.x+";rect="+ScreenBounds(rect)+";sourceAlphaThreshold=32");
    }

    private static async Task RunPowerPositionR1(PlayerProgressControl session, bool corrected) {
        const string mainId="default/mainView/MainPanel";
        var f=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);var old=f.GetValue(null);
        string folder="Library/AIUI/PowerPositionR1/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        f.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var team=new TeamSchemeSaveVO();team.schemes[0]=new[]{2205,2101,2301,2302};
        var previousTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,team,ignored=>{}));
        var oldScreen=UIScreenRegions.EditorScreenOverride; var oldRegions=UIScreenRegions.EditorOverride;
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(20);var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(12);
            var deck=main.GetComponentInChildren<DeckPresentationView>(true);
            foreach(int id in CfgManager.tables.CardObj.DataList.Where(c=>c.ShowInLibrary).Select(c=>c.Id)) {
                deck.RequestCardInfo(id);var panel=await CultivationOpened();await WaitPodiumCard(panel,id);await Frames(8);
                CheckPowerPositionR1(panel,corrected);
                if(id==2205||id==2101) { await PrepareStageCapture(panel);Capture("runtime-"+id+"-720x1280.png"); }
                Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(350);await Frames(6);
            }
            ResizeViewportStage(720,1600);await Frames(10);
            deck.RequestCardInfo(2205);var longPanel=await CultivationOpened();await WaitPodiumCard(longPanel,2205);await Frames(8);
            CheckPowerPositionR1(longPanel,corrected);await PrepareStageCapture(longPanel);Capture("runtime-2205-720x1600.png");
            Check(main.GetComponentInChildren<PowerDisplay>(true).transform.Find("imgPowerSymbol").GetComponent<RectTransform>().anchoredPosition.x==0,"阵容消费端图标X保持");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previousTeam);f.SetValue(null,old);
            UIScreenRegions.EditorScreenOverride=oldScreen;UIScreenRegions.EditorOverride=oldRegions;
        }
    }
}
#endif
