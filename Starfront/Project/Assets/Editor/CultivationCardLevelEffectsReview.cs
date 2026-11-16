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

    private static async Task RunCardLevelEffectsReview(PlayerProgressControl session) {
        var tables = CfgManager.tables;
        var records = tables.CardGrowthStageObj.DataList;
        var original = records.ToArray();
        Check(original.Length == 128 && original.GroupBy(row=>row.CardId).All(group=>group.Count()==8), "读取实际配置生成的16卡128条临时验收记录");
        // 空表和夹具语义仍单独覆盖；finally恢复本轮源表加载的展示数据。
        records.Clear();
        var empty = CardCultivationQuery.Read(tables,session,2101);
        Check(empty.skills.Count == 4 && empty.skills.Sum(skill => skill.ranks.Count) == 11, "四技能和11阶仍存在于技能查询");
        Check(empty.stages.Count == 0 && empty.missing.Contains("卡牌独立等级效果未配置"), "有技能阶数也不虚构等级效果");
        var fixture = new[] {
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":5,\"unlockLevel\":9,\"description\":\"单位移动速度 +X\",\"sortOrder\":5}")),
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":1,\"unlockLevel\":1,\"description\":\"单位生命值 +X\",\"sortOrder\":1}")),
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":4,\"unlockLevel\":7,\"description\":\"建筑生产时间 −X%\",\"sortOrder\":4}")),
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":2,\"unlockLevel\":3,\"description\":\"建筑生命值 +X\",\"sortOrder\":2}")),
            new cfg.resource.CardGrowthStageResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"id\":3,\"unlockLevel\":5,\"description\":\"单位攻击力 +X%\",\"sortOrder\":3}"))
        };
        const string mainId = "default/mainView/MainPanel";
        var functionsField = typeof(FunctionControl).GetField("instance",BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunctions = functionsField.GetValue(null);
        string folder="Library/AIUI/CardLevelEffects/"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionsField.SetValue(null,new FunctionControl(new FunctionModel(tables.FunctionObj.DataList,folder+"/functions.json")));
        var scheme=new TeamSchemeSaveVO();scheme.schemes[0]=new[]{2101,0,0,0};
        var oldTeam=TeamEditControl.InstallValidationSession(new TeamEditControl(tables,session.Cards.model,scheme,ignored=>{}));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>{new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId),ResType.Prefab)});
            PanelMgr.ins.OpenPanel(mainId);await Frames(15);
            var main=(MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());await Task.Delay(350);
            var view=main.GetComponentInChildren<DeckPresentationView>(true);
            view.RequestCardInfo(2101);var panel=await CultivationOpened();await WaitPodiumCard(panel,2101);
            Check(panel.listStages.arraySource.Cast<CultivationStageVO>().Count()==0,"真实牌库入口正式空表没有技能效果行");
            await PrepareStageCapture(panel);Capture("runtime-formal-empty-level-effects.png");
            records.AddRange(fixture);
            foreach(int level in new[]{0,1,3,6,10}) {
                int writes=0;
                var save=new PlayerProgressSaveVO{initialized=true};
                if(level>0){save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry{cardId=2101,level=level});}
                var player=new PlayerProgressControl(tables,save,ignored=>writes++);writes=0;
                var snapshot=CardCultivationQuery.Read(tables,player,2101);
                Check(snapshot.stages.Count==5,"独立表五条不依赖技能阶数：等级"+level);
                Check(snapshot.stages.Select(row=>row.stageId).SequenceEqual(new[]{1,2,3,4,5}),"独立ID与等级排序稳定：等级"+level);
                foreach(var stage in snapshot.stages) {
                    var source=fixture.Single(row=>row.Id==stage.stageId);
                    Check(stage.cardId==2101&&stage.skillId==0&&stage.rank==0&&stage.skillName==null,"等级效果不携带技能身份="+level+"/"+stage.stageId);
                    Check(stage.description==source.Description&&stage.gained==(level>0&&level>=source.UnlockLevel),"效果文案及等级获得条件="+level+"/"+stage.stageId);
                }
                Check(writes==0,"等级效果只读零写入="+level);
            }
            var other=CardCultivationQuery.Read(tables,session,2102);
            Check(other.stages.Count==0,"其他卡无配置时不串入2101效果");
            typeof(CardCultivationPanel).GetMethod("Refresh",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel,null);
            await Frames(12);
            var shown=panel.listStages.arraySource.Cast<CultivationStageVO>().ToArray();
            Check(shown.Length==5&&shown.Count(row=>row.gained)==3,"实际列表三已获得两未获得，独立于技能阶");
            await ValidateApprovedCultivationLayout(panel);
            foreach(var item in panel.listStages.GetComponentsInChildren<CardCultivationStageItem>(false)) {
                Check(item.GetComponentsInChildren<TMPro.TextMeshProUGUI>().All(text=>!text.enableWordWrapping&&!text.text.Contains("\n")&&!text.text.Contains("阶")),"正文单行无技能名或阶数");
            }
            Check(panel.listSkills.arraySource.Cast<CardSkillStateVO>().Select(row=>row.skillId).SequenceEqual(new[]{210101,210102,210103,210104}),"技能列表顺序不受独立效果影响");
            Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(400);await Frames(8);
            view.RequestCardInfo(2101);panel=await CultivationOpened();await Frames(8);
            Check(panel.listStages.arraySource.Cast<CultivationStageVO>().Select(row=>row.stageId).SequenceEqual(new[]{1,2,3,4,5}),"回池重开保留独立效果身份");
            records.Clear();records.AddRange(original);
            typeof(CardCultivationPanel).GetMethod("Refresh",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel,null);await Frames(8);
            Check(panel.listStages.arraySource.Cast<CultivationStageVO>().Count()==original.Count(row=>row.CardId==2101),"夹具移除后恢复实际源表的八条临时验收数据");
            var configured=panel.listStages.arraySource.Cast<CultivationStageVO>().ToArray();
            Check(configured.Count(row=>row.gained)==3 && configured.Select(row=>row.description).SequenceEqual(original.Where(row=>row.CardId==2101).OrderBy(row=>row.UnlockLevel).ThenBy(row=>row.SortOrder).ThenBy(row=>row.Id).Select(row=>row.Description)),"真实临时配置文案及6级三已获得五未获得正确");
            await PrepareStageCapture(panel);Capture("runtime-configured-level-effects.png");
            observations.Add("独立查询与v14.3已审布局均验证。等级和X占位仅隔离内存，不是正式内容；未执行实际属性增幅。");
        } finally {
            records.Clear();records.AddRange(original);
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);functionsField.SetValue(null,oldFunctions);
        }
    }
}
#endif
