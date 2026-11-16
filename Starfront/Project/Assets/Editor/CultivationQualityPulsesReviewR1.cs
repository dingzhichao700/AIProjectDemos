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
    private const string QualityPulseFolder = "Assets/Art/unpack/default/cardCultivation/pulseQualitiesR1/";

    private static void SetupQualityPulsesR1() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        foreach (string name in new[] { "GreenLocalPulse", "PurpleLocalPulse", "GoldLocalPulse" }) {
            string path = QualityPulseFolder + name;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            foreach (string ext in new[] { ".png", ".json" }) {
                string address = path + ext;
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(address), settings.FindGroup("Default Local Group")).SetAddress(address);
            }
            Check(importer.maxTextureSize == 2048, "品质图集保持2048无二次缩放=" + name);
        }
        const string prefab = "Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try {
            var palette = root.GetComponent<CultivationExhibitPalette>();
            Check(palette.previewRarePulse && palette.rarePulseAnimation == PulseTrialPath, "已通过蓝色开关及路径保留");
            Check(palette.normalGlow && palette.rareGlow && palette.epicGlow && palette.legendGlow, "四品质原静态回退保留");
            palette.previewOtherQualityPulses = true;
            palette.normalPulseAnimation = QualityPulseFolder + "GreenLocalPulse";
            palette.epicPulseAnimation = QualityPulseFolder + "PurpleLocalPulse";
            palette.legendPulseAnimation = QualityPulseFolder + "GoldLocalPulse";
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            AssetDatabase.SaveAssets();
            Check(root.GetComponentsInChildren<UIBinder>(true).Single(b => b.csharpAssetPath.EndsWith("/CardCultivationSkillItem.cs")).uiList.Count == 7, "原技能七绑定保持");
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static async Task<FrameAnimationView> WaitQualityPulseR1(CardCultivationPanel panel, string path) {
        for (int i=0; i<350; i++) {
            var view = panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).SingleOrDefault();
            if(view != null && view.path == path && view.animationData != null) { return view; }
            await Frames(2);
        }
        throw new TimeoutException("品质光效加载超时="+path);
    }

    private static async Task RunQualityPulsesR1(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var function = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousFunction = function.GetValue(null);
        string folder = "Library/AIUI/QualityPulsesR1/"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        function.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var team = new TeamSchemeSaveVO();team.schemes[0]=new[]{2101,0,0,0};
        var previousTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,team,ignored=>{}));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(15);
            var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(8);
            var deck=main.GetComponentInChildren<DeckPresentationView>(true);deck.RequestCardInfo(2101);
            var panel=await CultivationOpened();await WaitPodiumCard(panel,2101);
            var palette=panel.GetComponent<CultivationExhibitPalette>();
            int[] ids={2101,2103,2104,2105};
            string[] names={"green","blue","purple","gold"};
            string[] paths={QualityPulseFolder+"GreenLocalPulse",PulseTrialPath,QualityPulseFolder+"PurpleLocalPulse",QualityPulseFolder+"GoldLocalPulse"};
            for(int n=0;n<ids.Length;n++) {
                await PulseSwitchTo(panel,ids[n]);
                var pulse=await WaitQualityPulseR1(panel,paths[n]);
                Check(pulse.animationData.sprites.Length==24 && pulse.animationData.totalDuration==2000,"24帧/2秒="+names[n]);
                Check(pulse.animationData.sourceSizes.All(s=>s==new Vector2(660,220)),"统一完整画布="+names[n]);
                Check(pulse.trans.parent==panel.imgQualityGlow.transform && pulse.trans.anchoredPosition==Vector2.zero,"共同容器居中="+names[n]);
                Check(Mathf.Abs(pulse.scale-370f/660)<.0001f,"共同等比显示倍率="+names[n]);
                Check(panel.imgPodium.rectTransform.anchoredPosition==new Vector2(175,-233.7f),"静态座台坐标保持="+names[n]);
                Check(!panel.imgQualityGlow.enabled && panel.imgQualityGlow.sprite==palette.GetGlow(CfgManager.tables.CardObj.Get(ids[n]).Quality),"原静态图保留且不叠双光="+names[n]);
                await PrepareStageCapture(panel);
                var seen=new HashSet<Sprite>();float begin=RookieEngine.timer.curTime;
                for(int i=0;i<25;i++) {
                    while(RookieEngine.timer.curTime-begin<i*80) { await Frames(1); }
                    seen.Add(pulse.GetComponentInChildren<Image>().sprite);
                    Capture("runtime-"+names[n]+"-"+i.ToString("D2")+".png");
                }
                Check(seen.Count>=18,"共用计时器实际推进="+names[n]);
                var images=panel.GetComponentsInChildren<CultivationImageLease>(false).Select(l=>l.GetComponent<Image>()).Where(i=>i.enabled && i.sprite).ToDictionary(i=>i,i=>i.sprite);
                images[panel.imgPortrait]=panel.imgPortrait.sprite;
                int fragment=CfgManager.tables.CardObj.Get(ids[n]).FragmentItemId;
                Check(session.Inventory.TryGrantItem(fragment,1,out string grantReason),"隔离背包变更刷新="+names[n]+":"+grantReason);
                Check(images.All(p=>p.Key.enabled && p.Key.sprite==p.Value),"同步刷新无静态图清空="+names[n]);
                await Frames(5);
                Check(panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Single()==pulse,"刷新不重启/复制动画="+names[n]);
                GlobalToast.Clear();
            }
            // 正式全部牌库按各自品质映射；哨兵保留未激活作为反例。
            foreach(var card in CfgManager.tables.CardObj.DataList.Where(c=>c.ShowInLibrary && c.Id!=2303)) {
                await PulseSwitchTo(panel,card.Id);
                string expected=(string)typeof(CultivationExhibitPalette).GetMethod("PulsePath",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(palette,new object[]{card.Quality});
                var view=await WaitQualityPulseR1(panel,expected);
                Check(panel.CurrentCardId==card.Id && view.path==expected,"全部已激活卡正确品质映射="+card.Id);
            }
            await PulseSwitchTo(panel,2303);
            Check(!panel.imgQualityGlow.enabled && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length==0,"未激活隐藏全部光效");
            Check(panel.imgPortrait.material==palette.inactivePortraitMaterial,"未激活立绘灰化保持");
            foreach(int id in new[]{2101,2104,2105}) {
                await PulseSwitchTo(panel,id);palette.previewOtherQualityPulses=false;
                typeof(CardCultivationPanel).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);await Frames(3);
                Check(panel.imgQualityGlow.enabled && panel.imgQualityGlow.sprite==palette.GetGlow(CfgManager.tables.CardObj.Get(id).Quality) && panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length==0,"关闭扩展开关恢复原静态="+id);
                palette.previewOtherQualityPulses=true;
            }
            await PulseSwitchTo(panel,2101);
            await PulseSwitchTo(panel,2105);var oldPulse=await WaitQualityPulseR1(panel,paths[3]);
            Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(350);
            Check(oldPulse.path==null && !oldPulse.gameObject.activeInHierarchy,"关闭回池停止并回收");
            deck.RequestCardInfo(2105);panel=await CultivationOpened();await WaitQualityPulseR1(panel,paths[3]);
            Check(panel.imgQualityGlow.GetComponentsInChildren<FrameAnimationView>(false).Length==1,"回池重开只保留一个光效");
            observations.Add("三种品质暂准采用用于功能验证，美观不足及蓝色源首尾跳变待后续优化。四种品质100张实际运行帧；全部已激活卡映射、未激活、静态回退、正式背包变更刷新不重启和回池检查。隔离进度回调，不给真实玩家发GM。本轮不绕过框架新GM开发开关；无闪R1此前真实GM验收不重开。未改成长/配置/共享动画框架。");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previousTeam);function.SetValue(null,previousFunction);
        }
    }
}
#endif
