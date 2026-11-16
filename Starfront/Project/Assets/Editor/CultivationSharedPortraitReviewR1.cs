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
    [Serializable] private sealed class SharedPortraitGeometry { public string consumer; public int id; public float[] size,pivot,position,scale; }
    [Serializable] private sealed class SharedPortraitGeometrySet { public SharedPortraitGeometry[] items; }
    private static void SetupSharedPortraitDedupR1() {
        var settings=AddressableAssetSettingsDefaultObject.Settings;
        var records=SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_cardartobj.json")).Children.Where(a=>a["id"].AsInt>=2100).ToArray();
        Check(records.Length==16,"精确16卡迁移范围");
        foreach(var art in records) {
            string relative="Assets/Art/unpack/default/card/rosterV3/"+art["id"].AsInt+"/formation.png";
            string archived="E:/Codex_Projects/异星军团/outputs/shared-portraits-dedup-r1/archive/Project/"+relative;
            Check(File.Exists(archived) && File.ReadAllBytes(relative).SequenceEqual(File.ReadAllBytes(archived)) && File.ReadAllBytes(relative+".meta").SequenceEqual(File.ReadAllBytes(archived+".meta")),"PNG/meta可恢复归档已核验="+art["id"].AsInt);
        }
        foreach(var art in records) {
            int id=art["id"].AsInt;
            Check((string)art["portrait"]==(string)art["formation"],"两个用途指向同一资源="+id);
            string duplicate="Assets/Art/unpack/default/card/rosterV3/"+id+"/formation.png";
            string oldGuid=AssetDatabase.AssetPathToGUID(duplicate);
            Check(!string.IsNullOrEmpty(oldGuid),"待清理资产存在="+id);
            settings.RemoveAssetEntry(oldGuid);
        }
        AssetDatabase.SaveAssets();
        foreach(var art in records) {
            int id=art["id"].AsInt;
            string duplicate="Assets/Art/unpack/default/card/rosterV3/"+id+"/formation.png";
            string resolved=Path.GetFullPath(duplicate).Replace('\\','/');
            string allowed=Path.GetFullPath("Assets/Art/unpack/default/card/rosterV3/").Replace('\\','/').TrimEnd('/')+"/";
            Check(resolved.StartsWith(allowed,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved)=="formation.png","删除路径限定于批准清单="+id);
            Check(AssetDatabase.DeleteAsset(duplicate),"删除已归档重复资产和meta="+id);
            Check(AssetDatabase.LoadAssetAtPath<Sprite>((string)art["portrait"])!=null,"权威立绘保留="+id);
        }
        AssetDatabase.SaveAssets();
    }
    private static async Task RunSharedPortraitDedupR1(PlayerProgressControl session) {
        bool after=SessionState.GetString(Key+".state","")=="shared-portraits-after";
        var geometries=new List<SharedPortraitGeometry>();
        Action<string,int,Image> record=(consumer,id,image)=>{
            Check(image.enabled && image.sprite!=null,"已显示="+consumer+":"+id);
            var r=image.rectTransform;
            geometries.Add(new SharedPortraitGeometry{consumer=consumer,id=id,size=new[]{r.rect.width,r.rect.height},pivot=new[]{r.pivot.x,r.pivot.y},position=new[]{r.anchoredPosition.x,r.anchoredPosition.y},scale=new[]{r.localScale.x,r.localScale.y}});
        };
        const string mainId="default/mainView/MainPanel";
        var function=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);object oldFunction=function.GetValue(null);
        string folder="Library/AIUI/SharedPortraitsR1/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        function.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var saved=new TeamSchemeSaveVO();
        saved.schemes[0]=new[]{2101,2105,2205,2104};saved.schemes[1]=new[]{2102,2103,2304,2301};saved.schemes[2]=new[]{2303,2204,2202,2201};saved.schemes[3]=new[]{2203,2302,2305,2306};
        var oldTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,saved,ignored=>{}));
        try {
            // 同时持有两个用途绑定，直接检查真实共享加载身份，而非只比较磁盘文件。
            foreach(var card in CfgManager.tables.CardObj.DataList.Where(c=>c.ShowInLibrary)) {
                var art=CfgManager.tables.CardArtObj.Get(card.ArtId);
                var a=new GameObject("SharedPortraitProbe",typeof(RectTransform),typeof(Image)).AddComponent<CardArtImage>();a.autoLoadOnEnable=false;
                var b=new GameObject("SharedFormationProbe",typeof(RectTransform),typeof(Image)).AddComponent<CardArtImage>();b.autoLoadOnEnable=false;
                try {
                    await Task.WhenAll(a.BindAsync(art.Id,CardArtImage.Usage.Portrait),b.BindAsync(art.Id,CardArtImage.Usage.Formation));
                    Check((art.Portrait==art.Formation)==after,"配置迁移状态="+card.Id);
                    Check((a.GetComponent<Image>().sprite==b.GetComponent<Image>().sprite)==after,"同时持有两用途Sprite共享身份="+card.Id);
                }finally{a.Clear();b.Clear();UnityEngine.Object.Destroy(a.gameObject);UnityEngine.Object.Destroy(b.gameObject);}
            }
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});PanelMgr.ins.OpenPanel(mainId);await Frames(15);
            var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(12);var deck=main.GetComponentInChildren<DeckPresentationView>(true);
            for(int group=0;group<4;group++) {
                Click(deck.editTeam.GetComponent<RectTransform>());await Task.Delay(350);await Frames(20);
                var edit=(TeamEditPanel)PanelMgr.ins.GetPanel(TeamEditControl.PanelId);var buttons=new[]{edit.btnScheme1,edit.btnScheme2,edit.btnScheme3,edit.btnScheme4};Click(buttons[group].GetComponent<RectTransform>());await Frames(15);
                await Task.WhenAll(edit.GetComponentsInChildren<CardArtImage>().Select(a=>a.Loading));await Frames(5);
                var imgs=new[]{edit.imgSlot1,edit.imgSlot2,edit.imgSlot3,edit.imgSlot4};
                for(int i=0;i<4;i++){int id=saved.schemes[group][i];record("team",id,imgs[i]);Check(imgs[i].sprite==ResourceManager.GetUnpackImage(CfgManager.tables.CardArtObj.Get(CfgManager.tables.CardObj.Get(id).ArtId).Formation),"队伍编辑加载配置路径="+id);}
                Capture("team-"+group+".png");Click(edit.btnSave.GetComponent<RectTransform>());await Task.Delay(350);await Frames(15);
                await Task.WhenAll(deck.GetComponentsInChildren<CardArtImage>().Select(a=>a.Loading));await Frames(5);
                for(int i=0;i<4;i++)record("formation",saved.schemes[group][i],deck.slots[i].artwork);
                Capture("formation-"+group+".png");
                for(int i=0;i<4;i++) {
                    int id=saved.schemes[group][i];Click(deck.slots[i].artwork.rectTransform);var panel=await CultivationOpened();await WaitPodiumCard(panel,id);
                    for(int j=0;j<150;j++) {var cells=panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false);if(cells.Length==4&&cells.All(c=>c.imgSkillIcon.enabled&&c.imgSkillIcon.sprite))break;await Frames(2);}
                    await Frames(8);record("cultivation",id,panel.imgPortrait);
                    Check(panel.CurrentCardId==id,"从阵容打开所点卡="+id);
                    if(id==2101||id==2105||id==2205){await PrepareStageCapture(panel);Capture("cultivation-"+id+".png");}
                    Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(350);await Frames(8);
                    Check(PanelMgr.ins.GetPanel(mainId)==main && deck.slots[i].artwork.sprite!=null,"返回原阵容且立绘仍显示="+id);
                }
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key+".evidence",""),"geometry.json"),JsonUtility.ToJson(new SharedPortraitGeometrySet{items=geometries.ToArray()},true));
            Check(geometries.Count==48,"16卡三消费端几何记录完整");
        }finally{PanelMgr.ins.ClosePanelByType(TeamEditControl.PanelId);PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);TeamEditControl.InstallValidationSession(oldTeam);function.SetValue(null,oldFunction);}
    }
}
#endif
