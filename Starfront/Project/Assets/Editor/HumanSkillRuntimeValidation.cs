#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static partial class CardCultivationRuntimeValidation {
 private static async Task RunHumanSkills() {
  CardCultivationConfigValidation.Validate(CfgManager.tables);
  string[] files={"playerProgress.json","cardCollection.json"};var hashes=files.Select(ProgressFileDigest).ToArray();
  Check(CfgManager.tables.CardSkillEntryObj.DataList.Count(v=>v.CardId>=2101&&v.CardId<=2105)==20&&CfgManager.tables.CardSkillRankObj.DataList.Count(v=>v.CardId>=2101&&v.CardId<=2105)==55,"人族20技能55阶正式配置");
  foreach(int card in new[]{2102,2103,2104,2105}){
   int writes=0;var player=new PlayerProgressControl(CfgManager.tables,new PlayerProgressSaveVO{initialized=true},ignored=>writes++);var previous=PlayerProgressControl.InstallValidationSession(player);
   try{
    await CardCultivationControl.OpenAsync(new CardCultivationOpenVO{cardId=card,orderedCardIds=new[]{2102,2103,2104,2105},owner=canvas,source="human-r1-isolated-progress"});var source=await CultivationOpened();
    for(int level=0;level<=10;level++){
     Check(player.Cards.model.GetState(card).level==level,card+"实际等级"+level);
     var states=CardSkillQuery.ReadAll(CfgManager.tables,card,level);Check(states.Count==4,"四技能完整");
     foreach(var state in states){
      int[] levels=state.slot==cfg.SkillDisplaySlot.Basic?new[]{1,5,10}:state.slot==cfg.SkillDisplaySlot.Passive?new[]{2,6,9}:state.slot==cfg.SkillDisplaySlot.Active?new[]{3,7,10}:new[]{4,8};int rank=levels.Count(v=>v<=level);
      Check(state.currentRank==(rank==0?(int?)null:rank)&&state.unlocked==(rank>0)&&state.ranks.Select(v=>v.unlockLevel).SequenceEqual(levels)&&state.missing.Count==0,state.skillId+"Lv"+level+"自动解锁当前阶正确");
      var expected=ExpectedHumanEffects(state.skillId);Check(state.ranks.Select(v=>v.effect.Replace(" ","").Replace("\n","，")).SequenceEqual(expected),state.skillId+"各阶效果符合审核稿");
      var item=source.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(v=>v.txtSkillKind.text==CardSkillQuery.TypeName(state.slot));for(int n=0;n<100&&!item.imgSkillIcon.enabled;n++)await Frames(2);
      Check(item.imgSkillIcon.enabled&&item.imgSkillIcon.sprite!=null&&item.imgSkillIcon.sprite.name=="skill-"+state.skillId&&item.imgSkillLock.enabled==(rank==0),state.skillId+"培养图标及锁刷新");
      if(level!=0&&!levels.Contains(level))continue;
      Click(item.btnSkill.GetComponent<RectTransform>());for(int n=0;n<100&&!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId);n++)await Frames(2);await Task.Delay(300);await Frames(8);
      var detail=PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;Check(detail!=null,"培养按钮打开技能详情");for(int n=0;n<100&&!detail.imgSkillIcon.enabled;n++)await Frames(2);
      Check(detail.txtTitle.text==state.name&&detail.txtType.text==(state.kind==cfg.SkillKind.Passive?"被动":"主动")&&detail.imgSkillIcon.enabled&&detail.imgSkillIcon.sprite.name=="skill-"+state.skillId&&!detail.txtMissing.gameObject.activeSelf,state.skillId+"名称类型图标正确且无缺项");
      var rows=detail.listStages.GetComponentsInChildren<CardSkillStageItem>(true).Where(v=>v.gameObject.activeInHierarchy).ToArray();Check(rows.Length==levels.Length,"阶数列表完整");
      foreach(var row in rows){int i=int.Parse(row.txtRank.text.Replace("阶",""))-1;row.txtEffect.ForceMeshUpdate();string condition=level<levels[i]?levels[i]+"级解锁":i+1==rank?"当前":"已解锁";Check(row.txtEffect.text.Replace(" ","").Replace("\n","，")==expected[i]&&row.imgLock.enabled==(level<levels[i])&&row.imgStageCurrent.enabled==(rank==i+1)&&row.txtCondition.text==condition&&!row.txtEffect.isTextOverflowing,state.skillId+"Lv"+level+"阶"+(i+1)+"文字锁定当前不溢出");}
      GlobalToast.Clear();await Frames(4);Capture("skill-"+state.skillId+"-level-"+level+".png");
      if(level==0){long rev=player.model.Revision;var hit=Click(source.btnUpgrade.GetComponent<RectTransform>());Check(hit!=null&&hit.transform.IsChildOf(detail.transform)&&rev==player.model.Revision,"遮罩拦截底层升级");}else Click(detail.btnClose.GetComponent<RectTransform>());
      await Task.Delay(250);await Frames(4);Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId)&&ReferenceEquals(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId),source)&&source.CurrentCardId==card,"关闭返回原卡");
     }
     if(new[]{0,4,8,10}.Contains(level))Capture("card-"+card+"-level-"+level+".png");
     if(level<10){foreach(var cost in player.Cards.model.GetState(card).costs)Check(player.Inventory.TryGrantItem(cost.Key,cost.Value,out _),"仅补隔离升级材料");long rev=player.model.Revision;Click(source.btnUpgrade.GetComponent<RectTransform>());await Task.Delay(400);await Frames(8);Check(player.Cards.model.GetState(card).level==level+1&&player.model.Revision==rev+1,card+"实际按钮"+level+"→"+(level+1)+"单次升级事务");GlobalToast.Clear();}
    }
    Check(writes>0,"升级只写隔离内存回调");
   }finally{CardSkillDetailControl.Close();PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PlayerProgressControl.InstallValidationSession(previous);await Frames(8);}
  }
  for(int n=0;n<files.Length;n++)Check(hashes[n]==ProgressFileDigest(files[n]),"真实存档哈希不变"+files[n]);
 }
 private static string[] ExpectedHumanEffects(int id){switch(id){
case 210201:return new[]{"为1名友军恢复其最大生命值的3%","为1名友军恢复其最大生命值的4%","为1名友军恢复其最大生命值的5%"};
case 210202:return new[]{"治疗效果提升10%","治疗效果提升20%","治疗效果提升30%"};
case 210203:return new[]{"移动速度提升50%，持续3秒","移动速度提升50%，持续4秒","移动速度提升50%，持续5秒"};
case 210204:return new[]{"为最多3名友军各恢复其最大生命值的15%","为最多3名友军各恢复其最大生命值的20%"};
case 210301:return new[]{"造成100%技能伤害","造成120%技能伤害","造成140%技能伤害"};
case 210302:return new[]{"生命值提升10%","生命值提升20%","生命值提升30%"};
case 210303:return new[]{"攻击力提升25%，持续3秒","攻击力提升25%，持续4秒","攻击力提升25%，持续5秒"};
case 210304:return new[]{"对最多3个敌方目标各造成150%技能伤害","对最多3个敌方目标各造成200%技能伤害"};
case 210401:return new[]{"造成100%技能伤害","造成120%技能伤害","造成140%技能伤害"};
case 210402:return new[]{"攻击范围提升10%","攻击范围提升15%","攻击范围提升20%"};
case 210403:return new[]{"移动速度提升50%，持续3秒","移动速度提升50%，持续4秒","移动速度提升50%，持续5秒"};
case 210404:return new[]{"对1个敌方目标造成200%技能伤害","对1个敌方目标造成250%技能伤害"};
case 210501:return new[]{"造成100%技能伤害","造成120%技能伤害","造成140%技能伤害"};
case 210502:return new[]{"攻击力提升10%","攻击力提升20%","攻击力提升30%"};
case 210503:return new[]{"攻击速度提升50%，持续3秒","攻击速度提升50%，持续4秒","攻击速度提升50%，持续5秒"};
case 210504:return new[]{"对1个敌方目标造成250%技能伤害","对1个敌方目标造成300%技能伤害"};
 default:throw new ArgumentException("未知人族试配技能"+id);
 }}
}
#endif
