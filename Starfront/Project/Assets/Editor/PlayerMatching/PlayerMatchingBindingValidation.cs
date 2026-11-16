#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>匹配表现层隔离验证；使用编辑期显示样例，不修改业务、存档或登记。</summary>
public static class PlayerMatchingBindingValidation {
    const string Root="Assets/Prefab/default/playerMatching/PlayerMatchingPanel.prefab";
    const string Images="Assets/Art/unpack/uiBattle/battle/playerMatching/";
    static readonly List<string> Checks=new List<string>();
    [Serializable] public class Report { public string status="passed"; public string context="isolated Editor display inputs;not actual matching/loading business"; public string[] checks; public int binders,members,captures; }

    static void Check(bool condition, string name) {
        if (!condition) throw new InvalidOperationException("Matching view validation failed: "+name);
        Checks.Add(name);
    }

    static Sprite Sprite(string key) {
        // 默认头像已正式迁移；其余旧样例仅为Editor夹具，继续hold且不登记。
        string path=key=="avatar-portrait" ? PlayerMatchingFlow.PortraitAddress : Images+key+".png";
        Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite==null) throw new InvalidOperationException("Missing display fixture: "+key);
        return sprite;
    }

    static PlayerMatchingTeamDisplay Team(int count) {
        int[] ids={2101,2102,2103,2104}; string[] names={"陆战队员","医疗运输机","攻城坦克","幽灵"};
        string[] jobs={"vanguard","support","heavy","firepower"}; var cards=new List<PlayerMatchingCardDisplay>();
        for (int i=0;i<count;i++) cards.Add(new PlayerMatchingCardDisplay(Sprite("card-art-"+ids[i]),Sprite("card-frame-blue"),Sprite("profession-"+jobs[i]),names[i],30+i*10,1));
        return new PlayerMatchingTeamDisplay("我方指挥官","出战阵容 · "+count+"张卡牌",Sprite("avatar-portrait"),cards);
    }

    static PlayerMatchingPresentation Frame(string request, PlayerMatchingUiState state, float progress=.45f, int count=4, int completed=6, string item="7") {
        var friendly=Team(count); var enemy=new PlayerMatchingTeamDisplay("电脑对手","电脑模拟 · 阵容展示",Sprite("avatar-portrait"),new List<PlayerMatchingCardDisplay>(friendly.Cards));
        return new PlayerMatchingPresentation(request,state,friendly,enemy,completed,10,item,progress,3,13,state==PlayerMatchingUiState.Failed ? "战斗资源加载失败，请重试" : null,"1张卡因战斗资源未就绪暂不参战");
    }

    static void Capture(GameObject root, string state, int height) {
        ((RectTransform)root.transform).sizeDelta=new Vector2(720,height);
        var request=new AIUIExportQueue.Request {rootName="PlayerMatchingPanel",width=720,height=height,previewImagePath="Library/AIUI/PlayerMatchingBinding/"+state+"-"+height+".png"};
        typeof(AIUIExportQueue).GetMethod("CapturePreview",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[] {root,request});
    }


    sealed class SourceProbe : IPlayerMatchingPresentationSource {
        public PlayerMatchingPresentation Current { get; private set; }
        public event Action<PlayerMatchingPresentation> Changed;
        public int Listeners => Changed?.GetInvocationList().Length ?? 0;
        public int Closed,Retry,Cancel;
        public Func<bool> Cleared;
        public SourceProbe(PlayerMatchingPresentation frame) { Current=frame; }
        public void Emit(PlayerMatchingPresentation frame) { Current=frame; Changed?.Invoke(frame); }
        public Action<PlayerMatchingPresentation> CaptureListener() { return Changed; }
        public void Dispatch(PlayerMatchingUiAction action, string requestId) {
            Check(requestId==Current.RequestId,"source receives original request identity");
            if (action==PlayerMatchingUiAction.Closed) {
                Closed++; Check(Listeners==0,"source detached before closed intent");
                Check(Cleared(),"UI references cleared before provider releases assets");
            }
            if (action==PlayerMatchingUiAction.Retry) Retry++;
            if (action==PlayerMatchingUiAction.Cancel) Cancel++;
        }
    }

    static void ConsumerChecks(PlayerMatchingPanel panel, GameObject root) {
        Func<bool> cleared=()=> {
            foreach (PlayerMatchingCardView card in root.GetComponentsInChildren<PlayerMatchingCardView>(true))
                if (card.imgArtwork.sprite!=null || card.imgArtwork.enabled) return false;
            return true;
        };
        var a=new SourceProbe(Frame("source-A",PlayerMatchingUiState.Searching)) {Cleared=cleared};
        panel.openParams=new object[] {a}; panel.OnOpen();
        Check(a.Listeners==1,"normal OnOpen consumes presentation source");
        panel.state_search.btnCancel.onClick.Invoke(); Check(a.Cancel==1,"source cancel command forwarded without local transition");
        a.Emit(Frame("source-A",PlayerMatchingUiState.Preparing,.2f));
        Check(panel.state_preparing.txtPercent.text=="20%","source event refreshes actual display");
        var late=a.CaptureListener();
        var b=new SourceProbe(Frame("source-B",PlayerMatchingUiState.Preparing,.45f)) {Cleared=cleared};
        panel.BindSource(b);
        Check(a.Closed==1 && a.Listeners==0 && b.Listeners==1,"rebind retires previous provider exactly once");
        late(Frame("source-A",PlayerMatchingUiState.Preparing,1));
        Check(panel.state_preparing.txtPercent.text=="45%","captured old subscription callback cannot modify replacement");
        b.Emit(Frame("source-foreign",PlayerMatchingUiState.Preparing,1));
        Check(panel.state_preparing.txtPercent.text=="45%","foreign request frame rejected on active source");
        var failed=new PlayerMatchingPresentation("source-B",PlayerMatchingUiState.Failed,Team(1),null,0,0,"",0,0,0,"匹配未成功，请重试");
        b.Emit(failed);
        Check(panel.state_failure.txtStatus.text=="匹配未成功，请重试","pre-match failure requires no fabricated opponent");
        Check(panel.state_failure.enemyView.txtName.text==string.Empty,"unknown opponent clears previous identity");
        panel.state_failure.btnRetry.onClick.Invoke(); Check(b.Retry==1,"retry delegated to active source");
        panel.OnClose(); panel.OnClose();
        Check(b.Closed==1 && b.Listeners==0,"source close unsubscribes and releases once");
        panel.openParams=null; bool rejected=false;
        try { panel.OnOpen(); } catch (ArgumentException) { rejected=true; }
        Check(rejected,"normal open without request rejects serialized sample display");
        foreach (Transform child in panel.transform.Find("boxCenteredContent")) Check(!child.gameObject.activeSelf,"invalid open leaves no sample state visible");
    }

    public static string Run() {
        Checks.Clear(); GameObject root=PrefabUtility.LoadPrefabContents(Root);
        try {
            UIBinder binder=root.GetComponent<UIBinder>(); Check(binder!=null,"root binder exists");
            typeof(UIBinder).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(binder,null);
            PlayerMatchingPanel panel=root.GetComponent<PlayerMatchingPanel>(); Check(panel!=null && panel.isInit,"real binder runtime append and initialization");
            int members=0;
            foreach (UIBinder item in root.GetComponentsInChildren<UIBinder>(true)) {
                foreach (var member in item.uiList) {
                    Check(member.go!=null,"bound reference "+item.name+"."+member.uiName); members++;
                    Check(!member.uiName.Contains("Frame") && member.uiName!="imgBackground" && member.uiName!="txtTip","static art not bound "+member.uiName);
                }
            }
            panel.BindPresentation(Frame("A",PlayerMatchingUiState.Preparing),null);
            Check(panel.state_preparing.txtStatus.text=="准备战斗资源（6/10）" && panel.state_preparing.txtPercent.text=="45%","overall count separate from item percentage");
            Check(Mathf.Abs(panel.state_preparing.progressView.fillMask.rect.width-185)<.1f,"item45percent rounds from full412 width");
            foreach (float value in new[] {0f,.2f,.9f,.2f,1f}) {
                panel.Present(Frame("A",PlayerMatchingUiState.Preparing,value));
                Check(Mathf.Abs(panel.state_preparing.progressView.fillMask.rect.width-Mathf.Round(412*value))<.1f,"progress baseline value "+value);
                Check(panel.state_preparing.progressView.fillMask.gameObject.activeSelf==(value>0),"zero fill hidden without false minimum "+value);
                Capture(root,"progress-"+Mathf.RoundToInt(value*100),1280);
            }
            panel.Present(Frame("A",PlayerMatchingUiState.Preparing,0,4,7,"8"));
            Check(panel.state_preparing.txtStatus.text.Contains("7/10") && panel.state_preparing.txtPercent.text=="0%","new item reset independent from overall count");
            Capture(root,"next-item-zero",1280);
            for (int count=0;count<=4;count+=count==0?1:3) {
                panel.Present(Frame("A",PlayerMatchingUiState.Preparing,.45f,count));
                var team=panel.state_preparing.friendlyView; var slots=new[] {team.card1,team.card2,team.card3,team.card4}; int active=0;
                foreach (var card in slots) if (card.gameObject.activeSelf) active++;
                Check(active==count,"0/1/4 card refresh "+count);
            }
            var mutable=new List<PlayerMatchingCardDisplay> {new PlayerMatchingCardDisplay(null,null,null,"A",1,1)};
            var frozen=new PlayerMatchingTeamDisplay("A","A",null,mutable); mutable.Clear(); Check(frozen.Cards.Count==1,"display list copied");
            int actions=0; PlayerMatchingUiAction last=PlayerMatchingUiAction.Closed; string lastRequest=null;
            Action<PlayerMatchingUiAction,string> listener=(action,request)=>{ actions++; last=action; lastRequest=request; };
            panel.BindPresentation(Frame("A",PlayerMatchingUiState.Searching),listener);
            float angle=panel.state_search.searchView.arc42.eulerAngles.z; panel.TickPresentation(.5f);
            Check(!Mathf.Approximately(angle,panel.state_search.searchView.arc42.eulerAngles.z),"bitmap search rotation driven by view tick");
            panel.state_search.btnCancel.onClick.Invoke(); Check(actions==1 && last==PlayerMatchingUiAction.Cancel && lastRequest=="A","cancel intent only");
            panel.Present(Frame("A",PlayerMatchingUiState.Preparing)); panel.state_search.btnCancel.onClick.Invoke(); Check(actions==1,"search listeners removed after matched");
            panel.Present(Frame("A",PlayerMatchingUiState.Failed)); panel.state_failure.btnRetry.onClick.Invoke(); panel.state_failure.btnReturn.onClick.Invoke(); Check(actions==3 && last==PlayerMatchingUiAction.Return,"retry and return intents");
            panel.BindPresentation(Frame("B",PlayerMatchingUiState.Preparing),listener);
            Check(!panel.Present(Frame("A",PlayerMatchingUiState.Preparing,1)) && panel.state_preparing.txtPercent.text=="45%","stale request display rejected");
            panel.Present(Frame("B",PlayerMatchingUiState.Countdown)); panel.TickPresentation(5);
            Check(panel.state_countdown.txtStatus.text=="3","UI does not advance business countdown");
            foreach (PlayerMatchingUiState state in Enum.GetValues(typeof(PlayerMatchingUiState))) {
                int count=state==PlayerMatchingUiState.EmptyDeck?0:state==PlayerMatchingUiState.PartialDeck?1:state==PlayerMatchingUiState.ExcludedCards?3:4;
                panel.Present(Frame("B",state,.45f,count)); int active=0;
                foreach (Transform child in panel.transform.Find("boxCenteredContent")) if (child.gameObject.activeSelf) active++;
                Check(active==1,"one presentation state active "+state);
                string key=new[] {"search","preparing","countdown","failure","empty","excluded","entering","partial"}[(int)state];
                Capture(root,key,1280); Capture(root,key,1600);
            }
            int beforeClose=actions; panel.OnClose(); panel.OnClose(); Check(actions==beforeClose+1 && last==PlayerMatchingUiAction.Closed && lastRequest=="B","close intent once and cleanup idempotent");
            foreach (PlayerMatchingCardView card in root.GetComponentsInChildren<PlayerMatchingCardView>(true)) Check(card.imgArtwork.sprite==null && !card.imgArtwork.enabled,"dynamic sprite reference cleared "+card.name);
            foreach (PlayerMatchingAvatarView avatar in root.GetComponentsInChildren<PlayerMatchingAvatarView>(true)) Check(avatar.imgPortrait.sprite==null && !avatar.imgPortrait.enabled,"portrait reference cleared");
            Check(!panel.Present(Frame("B",PlayerMatchingUiState.Preparing)),"closed view rejects further updates");
            Check(panel.FullBleedChildren.Length==3 && panel.UsesFullScreenViewport,"viewport contract keeps background/tip separate");
            foreach (PlayerMatchingTeamView team in root.GetComponentsInChildren<PlayerMatchingTeamView>(true)) {
                var slots=new[] {team.card1,team.card2,team.card3,team.card4};
                foreach (var slot in slots) Check(slot!=null,"all team variants retain four runtime slots "+team.name);
                foreach (int count in new[] {0,1,2,3,4}) {
                    team.Render(Team(count)); int shown=0;
                    foreach (var slot in slots) if (slot.gameObject.activeSelf) shown++;
                    Check(shown==count,"team capacity independent from sample count "+team.name+"/"+count);
                }
            }
            ConsumerChecks(panel,root);
            return JsonUtility.ToJson(new Report {checks=Checks.ToArray(),binders=root.GetComponentsInChildren<UIBinder>(true).Length,members=members,captures=16},true);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
