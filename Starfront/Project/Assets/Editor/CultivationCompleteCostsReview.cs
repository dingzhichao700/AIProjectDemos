#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static partial class CultivationMaxStateReviewValidation {
    private static async Task RunCompleteCostsReview(PlayerProgressControl session) {
        const string mainId="default/mainView/MainPanel";
        var function=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        object oldFunction=function.GetValue(null);string folder="Library/AIUI/CompleteCosts/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        function.SetValue(null,new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList,folder+"/functions.json")));
        var scheme=new TeamSchemeSaveVO();scheme.schemes[0]=new[]{2101,0,0,0};
        var oldTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables,session.Cards.model,scheme,ignored=>{}));
        var levels=(Dictionary<string,cfg.resource.CardLevelResource>)typeof(CardCollectionModel).GetField("levels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.Cards.model);
        var original=levels["2101:2"];
        try {
            levels["2101:2"]=new cfg.resource.CardLevelResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"level\":2,\"fragmentCount\":"+original.FragmentCount+",\"extraCosts\":[{\"itemId\":2001,\"count\":30},{\"itemId\":2002,\"count\":20},{\"itemId\":3101,\"count\":5}]}"));
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(15);
            var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(8);
            var deck=main.GetComponentInChildren<DeckPresentationView>(true);deck.RequestCardInfo(2101);
            var panel=await CultivationOpened();await WaitPodiumCard(panel,2101);await Frames(12);
            var snapshot=CardCultivationQuery.Read(CfgManager.tables,session,2101);
            var expected=snapshot.materials;
            Check(!snapshot.missing.Any(message=>message.Contains("3槽容量")),"超过三项不再报告失效的容量缺项");
            Check(expected.Count==4 && expected.Any(r=>r.itemId==12101 && r.required>0),"对应碎片与三个额外成本完整查询");
            Check(panel.listMaterials.arraySource.Cast<CultivationMaterialVO>().Select(r=>r.itemId).SequenceEqual(expected.Select(r=>r.itemId)),"四项传入ScrollList，不因容量或图标缺失过滤");
            var scroll=panel.listMaterials.GetComponent<ScrollRect>();Canvas.ForceUpdateCanvases();
            Check(scroll.horizontal && !scroll.vertical && scroll.viewport.rect.width==365,"保持365px视窗并支持横向滚动");
            Check(scroll.content.rect.width==464,"四格完整内容宽464px，不压缩截断");
            Check(panel.btnUpgrade.interactable && panel.txtHint.text=="","不因四项成本阻断升级或显示容量报错");
            foreach(float position in new[]{0f,1f}) {
                scroll.horizontalNormalizedPosition=position;await Frames(12);
                var cells=panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>(false);
                Check(cells.Length>0,"横向两端均有真实材料格="+position);
                foreach(var cell in cells) {
                    var cost=expected.Single(r=>r.itemId==cell.CurrentItemId);
                    Check(cell.txtMaterialRequired.text==cost.required.ToString(),"缺正式图标也显示实际所需数量="+cost.itemId);
                }
                Capture("runtime-four-costs-"+(position==0?"left":"right")+".png");
            }
            Check(Mathf.Abs(scroll.content.anchoredPosition.x+99)<1,"可滑至第四项完整进入视窗");
            long revision=session.model.Revision;int level=session.Cards.model.GetState(2101).level;
            var balances=CfgManager.tables.ItemObj.DataList.ToDictionary(r=>r.Id,r=>session.Inventory.model.GetQuantity(r.Id));
            Click(panel.btnUpgrade.GetComponent<RectTransform>());await GlobalToast.EnsureReadyAsync();await Frames(8);
            Check(GlobalToast.Host.ActiveItems.Any(r=>r.Message=="材料不足"),"四项仍调用正式升级事务并正常提示缺料");
            Check(session.model.Revision==revision && session.Cards.model.GetState(2101).level==level && balances.All(r=>session.Inventory.model.GetQuantity(r.Key)==r.Value),"拒绝时全部余额/等级/版本保持");
            GlobalToast.Clear();Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(400);await Frames(8);
            deck.RequestCardInfo(2101);panel=await CultivationOpened();await Frames(12);
            Check(panel.listMaterials.arraySource.Count==4 && Vector2.Distance(panel.listMaterials.GetComponent<ScrollRect>().content.anchoredPosition,Vector2.zero)<.1f,"回池重开四项完整且材料列表回到左侧");
            observations.Add("四项成本只替换隔离CardCollectionModel等级记录，finally恢复；未填正式extraCosts，未执行GM，未改任何玩家进度或Prefabs/Figma结构。");
        } finally {
            levels["2101:2"]=original;GlobalToast.Clear();PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);function.SetValue(null,oldFunction);
        }
    }
}
#endif
