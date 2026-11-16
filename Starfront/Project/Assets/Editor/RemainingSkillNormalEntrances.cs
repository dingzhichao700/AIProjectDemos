#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static partial class CardCultivationRuntimeValidation {
 private static async Task RunRemainingNormalEntrances(MainPanel main){
  CardSkillDetailControl.Close();PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);await ComparisonFrames(true,20);
  var deck=main.viewDeck.GetComponentInChildren<DeckPresentationView>();var scroll=deck.library.GetComponent<ScrollRect>();var field=typeof(DeckCardView).GetField("current",BindingFlags.Instance|BindingFlags.NonPublic);
  string evidence=SessionState.GetString(Key+".evidence","");Directory.CreateDirectory(evidence);
  foreach(int cardId in new[]{2201,2202,2203,2204,2205,2301,2302,2303,2304,2305,2306}){
   DeckCardView card=null;for(int step=0;step<=40&&card==null;step++){scroll.verticalNormalizedPosition=1f-step/40f;await ComparisonFrames(true,4);card=deck.library.GetComponentsInChildren<DeckCardView>().FirstOrDefault(v=>!v.formation&&(field.GetValue(v) as DeckCardView.DisplayData)?.cardId==cardId);}
   Check(card!=null,"正常牌库找到"+cardId);ComparisonClick(card.button.GetComponent<RectTransform>());
   for(int n=0;n<200&&!PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId);n++)await ComparisonFrames(true,1);await ComparisonFrames(true,30);
   var source=PanelMgr.ins.GetPanel(CardCultivationControl.PanelId) as CardCultivationPanel;Check(source!=null&&source.CurrentCardId==cardId,"牌库真实按钮打开培养"+cardId);
   foreach(var state in CardSkillQuery.ReadAll(CfgManager.tables,cardId,8)){
    var item=source.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(v=>v.txtSkillKind.text==CardSkillQuery.TypeName(state.slot));ComparisonClick(item.btnSkill.GetComponent<RectTransform>());
    for(int n=0;n<200&&!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId);n++)await ComparisonFrames(true,1);await Task.Delay(350);await ComparisonFrames(true,12);
    var detail=PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;Check(detail!=null&&detail.txtTitle.text==state.name&&detail.imgSkillIcon.enabled&&detail.imgSkillIcon.sprite.name=="skill-"+state.skillId,"正常入口技能和图标"+state.skillId);
    var rows=detail.listStages.GetComponentsInChildren<CardSkillStageItem>().Where(v=>v.gameObject.activeInHierarchy).ToArray();Check(rows.Length==state.ranks.Count&&rows.All(row=>state.ranks.Any(rank=>row.txtRank.text==rank.rank+"阶"&&row.txtEffect.text==rank.effect&&row.imgLock.enabled==!rank.unlocked&&row.imgStageCurrent.enabled==rank.current)),"正常窗口阶数/文字/锁/当前"+state.skillId);
    string path=Path.Combine(evidence,"normal-skill-"+state.skillId+"-level-8.png");ScreenCapture.CaptureScreenshot(path);for(int n=0;n<100&&!File.Exists(path);n++)await ComparisonFrames(true,2);await ComparisonFrames(true,4);Check(File.Exists(path),"正常游戏截图"+state.skillId);
    ComparisonClick(detail.btnClose.GetComponent<RectTransform>());
    await Task.Delay(300);await ComparisonFrames(true,8);Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId)&&source.CurrentCardId==cardId,"正常窗口返回原卡"+state.skillId);
   }
   PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);await ComparisonFrames(true,20);
  }
 }
}
#endif

