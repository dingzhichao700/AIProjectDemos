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

    private static async Task RunAllCardLevelEffectsReview(PlayerProgressControl session) {
        var tables=CfgManager.tables;
        var cards=tables.CardObj.DataList.Where(c=>c.ShowInLibrary).OrderBy(c=>c.Id).ToArray();
        Check(cards.Length==16 && tables.CardGrowthStageObj.DataList.Count==128,"实际生成配置16卡各8条，无历史卡补造");
        foreach(var card in cards) {
            foreach(int level in new[]{0,1,6,10}) {
                int writes=0;
                var seed=new PlayerProgressSaveVO { initialized=true };
                if(level>0)seed.cards.Add(new PlayerProgressSaveVO.CardLevelEntry{cardId=card.Id,level=level});
                var memory=new PlayerProgressControl(tables,seed,ignored=>writes++);writes=0;
                var state=CardCultivationQuery.Read(tables,memory,card.Id);
                Check(state.stages.Count==8 && state.stages.All(r=>r.cardId==card.Id),"本卡八条身份="+card.Id+"/"+level);
                Check(state.stages.Select(r=>r.stageId).SequenceEqual(new[]{1,2,3,4,5,6,7,8}),"等级/序号排序稳定="+card.Id+"/"+level);
                Check(state.stages.Count(r=>r.gained)==(level==0?0:level==1?1:level==6?3:8),"获得数0/1/3/8="+card.Id+"/"+level);
                Check(state.stages.All(r=>r.skillId==0 && r.rank==0 && r.skillName==null),"独立效果无技能身份="+card.Id+"/"+level);
                Check(writes==0,"查询无保存回调="+card.Id+"/"+level);
            }
        }
        const string mainId="default/mainView/MainPanel";
        var functionField=typeof(FunctionControl).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        object oldFunction=functionField.GetValue(null);
        string folder="Library/AIUI/AllLevelEffects/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
        functionField.SetValue(null,new FunctionControl(new FunctionModel(tables.FunctionObj.DataList,folder+"/functions.json")));
        var scheme=new TeamSchemeSaveVO();scheme.schemes[0]=new[]{2101,0,0,0};
        var oldTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(tables,session.Cards.model,scheme,ignored=>{}));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(15);
            var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);await Frames(8);
            var deck=main.GetComponentInChildren<DeckPresentationView>(true);
            var sourceScroll=deck.library.GetComponent<ScrollRect>().content.anchoredPosition;
            deck.RequestCardInfo(2101);var panel=await CultivationOpened();
            var request=(CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
            int[] order=request.orderedCardIds;
            Check(order.Length==16 && order.Distinct().Count()==16,"真实牌库来源包含16卡且无重复");
            int first=Array.IndexOf(order,2101);
            for(int step=0;step<first;step++){Click(panel.btnPrevious.GetComponent<RectTransform>());await WaitPodiumCard(panel,order[first-step-1]);}
            Check(!panel.btnPrevious.interactable,"来源首位不向前循环");
            for(int i=0;i<order.Length;i++) {
                int id=order[i];await WaitPodiumCard(panel,id);await Frames(8);
                var state=CardCultivationQuery.Read(tables,session,id);
                var shown=panel.listStages.arraySource.Cast<CultivationStageVO>().ToArray();
                Check(shown.Length==8 && shown.All(r=>r.cardId==id),"实际切卡无串数据="+id);
                Check(shown.Count(r=>r.gained)==3,"实际6级三条已获得="+id);
                var scroll=panel.listStages.GetComponent<ScrollRect>();Canvas.ForceUpdateCanvases();
                Check(scroll.content.rect.height==394 && scroll.verticalScrollbar.gameObject.activeSelf,"八条内容与滚动条正确="+id);
                Check(Vector2.Distance(scroll.content.anchoredPosition,Vector2.zero)<.1f,"每卡初次切换回顶="+id);
                foreach(var item in panel.listStages.GetComponentsInChildren<CardCultivationStageItem>(false)) {
                    Check(!item.txtStageEffect.enableWordWrapping && !item.txtStageEffect.text.Contains("阶") && item.txtStageEffect.preferredWidth<=item.txtStageEffect.rectTransform.rect.width,"单行效果文案适配="+id);
                }
                await PrepareStageCapture(panel);Capture("runtime-"+id+"-level6.png");
                scroll.verticalNormalizedPosition=0;await Frames(8);
                Check(Mathf.Abs(scroll.content.anchoredPosition.y-150)<1,"各卡底部三条可滚动显示="+id);
                if(i==0 || i==order.Length-1)Capture("runtime-"+id+"-bottom.png");
                if(i<order.Length-1)Click(panel.btnNext.GetComponent<RectTransform>());
            }
            Check(!panel.btnNext.interactable,"来源末位不向后循环");
            Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(400);await Frames(8);
            Check(Vector2.Distance(sourceScroll,deck.library.GetComponent<ScrollRect>().content.anchoredPosition)<.1f,"16卡核验后返回原牌库滚动位置");
            deck.RequestCardInfo(2101);panel=await CultivationOpened();await WaitPodiumCard(panel,2101);await Frames(8);
            Check(panel.listStages.arraySource.Cast<CultivationStageVO>().All(r=>r.cardId==2101),"回池重开恢复陆战自身效果");
            observations.Add("128条均实际从Luban生成配置读取，不注入等级效果表。玩家等级6只在隔离内存，0保存回调。仅UI描述，不执行单位/建筑属性加成；不补历史卡和技能配置。");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);functionField.SetValue(null,oldFunction);
        }
    }
}
#endif
