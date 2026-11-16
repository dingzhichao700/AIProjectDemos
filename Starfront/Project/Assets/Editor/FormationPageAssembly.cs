using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



[InitializeOnLoad]
internal static class FormationPageGameCheck {
    const string Q="Library/AIUI/";
    const string Key="StarfrontFormationGameCheck";
    [Serializable] class Request {public int processId;public string requestId;}
    static FormationPageGameCheck(){EditorApplication.update+=Tick;}
    static double readyAt;
    static void Tick(){
        var f=Q+"pending-formation-gamecheck.json";
        if(File.Exists(f)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){
            var request=JsonUtility.FromJson<Request>(File.ReadAllText(f));
            if(request.processId!=System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId))return;
            File.Delete(f);SessionState.SetString(Key,request.requestId);SessionState.SetFloat(Key+"start",(float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key+"bg",Application.runInBackground); if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
        }
        var id=SessionState.GetString(Key,"");if(id==""||!EditorApplication.isPlaying)return;
        try{
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"start",0)>95)throw new TimeoutException("未进入主界面或截图未完成");
            Application.runInBackground=true; EditorApplication.QueuePlayerLoopUpdate(); if(EditorApplication.isPaused)EditorApplication.isPaused=false;
            if((int)EditorApplication.timeSinceStartup!=SessionState.GetInt(Key+"tick",-1)){
                SessionState.SetInt(Key+"tick",(int)EditorApplication.timeSinceStartup);
                File.WriteAllText(Q+"formation-step.txt",string.Join(" ",Resources.FindObjectsOfTypeAll<BasePanel>().Where(x=>x.gameObject.scene.IsValid()).Select(x=>x.name))+" frames="+Time.frameCount+" focus="+Application.isFocused);
            }
            var main=Resources.FindObjectsOfTypeAll<MainPanel>().FirstOrDefault(p=>p.gameObject.scene.IsValid()&&p.isActiveAndEnabled&&p.btnDeck);
            if(!main){
                var login=UnityEngine.Object.FindObjectOfType<LoginPanel>();if(login&&login.txt&&login.txt.gameObject.activeInHierarchy)login.SendMessage("EnterMain",SendMessageOptions.DontRequireReceiver);
                return;
            }
            if(!main.viewDeck.gameObject.activeSelf){main.btnDeck.onClick.Invoke();readyAt=EditorApplication.timeSinceStartup+2;return;}
            var view=main.viewDeck.GetComponentInChildren<DeckPresentationView>();
            if(!view||!view.ownedCount||!view.editTeam)throw new InvalidDataException("加载到旧版 MainPanel");
            if(EditorApplication.timeSinceStartup<readyAt||view.previewCards==null||view.previewCards.Length==0)return;
            var owner=view.transform;if((owner.Find("boxSchemes") && owner.Find("boxSchemes").gameObject.activeInHierarchy) || !owner.Find("boxPowerHeader").gameObject.activeInHierarchy || view.filters.Length!=5 || !view.editTeam.isActiveAndEnabled)throw new InvalidDataException("运行层级错误");
            var screen=Path.GetFullPath(Q+"formation-page-game.png");
            var requested=SessionState.GetString(Key+"capture","");
            if(requested!=id){SessionState.SetString(Key+"capture",id);ScreenCapture.CaptureScreenshot(screen);return;}
            if(!File.Exists(screen))return;
            Write(id,"passed",view.ownedCount.text);
        }catch(Exception e){Write(id,"failed",e.ToString());}
    }
    static void Write(string id,string status,string detail){
        SessionState.EraseString(Key);Application.runInBackground=SessionState.GetBool(Key+"bg",false);
        File.WriteAllText(Q+"formation-game-result.json",JsonUtility.ToJson(new GameResult{requestId=id,status=status,detail=detail},true));
    }
    [Serializable] class GameResult {public string requestId,status,detail;}
}


