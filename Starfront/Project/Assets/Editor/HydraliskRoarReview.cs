using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class MarineIdleIntegrationReview {
    [MenuItem("Tools/开发验收/刺蛇双动作/空闲与咆哮预览")]
    public static void OpenRoarPreview() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Pending)) { Debug.LogWarning("请在Edit空闲时打开刺蛇双动作预览。"); return; }
        var request = new Request { requestId="hydralisk-roar-user-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"),processId=System.Diagnostics.Process.GetCurrentProcess().Id,action="zerg-roar-review",evidenceDirectory=Path.GetFullPath("Library/AIUI/HydraliskRoarReview") };
        File.WriteAllText(Pending+".tmp",JsonUtility.ToJson(request));File.Move(Pending+".tmp",Pending);
    }
    [MenuItem("Tools/开发验收/刺蛇双动作/在当前Play启用咆哮对比")]
    public static void EnableRoarCurrentPlay() { if (EditorApplication.isPlaying) { HydraliskRoarPreviewControls.Install(); } }

    private static async Task CheckRoar(Image image) {
        var skin=image.GetComponent<CardArtIdleSkin>();var idle=skin.Rig;var mouth=idle.GetComponent<HydraliskMouthRig>();
        bool previousAutomatic=skin.AutomaticSpecialEnabled;skin.AutomaticSpecialEnabled=false;skin.StopSpecial();await Frames(2);
        Check(mouth!=null && mouth.Ready,"刺蛇独立下颌与身体均有原生缓冲");
        Check(Mathf.Abs(Mathf.DeltaAngle(mouth.mandible.localEulerAngles.z,mouth.idleJawDegrees))<=mouth.idleJawAmplitudeDegrees+.1f,"空闲下颌微张且呼吸幅度小");
        Check(image.sprite!=image.overrideSprite && image.mainTexture==idle.skin.GetComponent<SpriteRenderer>().sprite.texture,"原静态引用不改且动态双层使用正确图集");
        Check(await skin.PlaySpecialAsync(),"特定攻击表现触发咆哮");
        Check(!await skin.PlaySpecialAsync(),"咆哮播放中不重入");
        bool preparation=false,open=false,recover=false;float first=idle.Elapsed;double deadline=EditorApplication.timeSinceStartup+6;
        while(mouth.IsRoaring && EditorApplication.timeSinceStartup<deadline) {
            preparation|=mouth.Preparation>.3f && mouth.OpenAmount<.01f;
            open|=mouth.OpenAmount>.95f;
            recover|=mouth.RoarElapsed>1.2f && mouth.OpenAmount<.6f;
            if(mouth.OpenAmount>.95f && !File.Exists(Path.Combine(SessionState.GetString(Key+".out",""),"roar-peak.png"))){Capture("roar-peak.png");}
            await Frames(1);
        }
        await Frames(3);
        Check(!mouth.IsRoaring && preparation && open && recover,"蓄力收颈抬头张嘴咆哮回空闲完整覆盖");
        Check(skin.Rig==idle && mouth.OpenAmount==0 && Mathf.Abs(Mathf.DeltaAngle(mouth.mandible.localEulerAngles.z,mouth.idleJawDegrees))<=mouth.idleJawAmplitudeDegrees+.1f,"咆哮后回微张空闲同一实例");
        Check(idle.Elapsed!=first,"咆哮期间基础待机相位继续");
        Capture("idle-after-roar.png");
        skin.AutomaticSpecialEnabled=previousAutomatic;
    }
    private static async Task RunRoarPreview(string id,DeckPresentationView deck) {
        DeployHydralisk(deck);await Frames(12);deck.RequestCardInfo(2202);
        var panel=await CultivationOpened(2202);await WaitAnimation(panel.imgPortrait.GetComponent<CardArtImage>());await Frames(12);
        Capture("idle-closed.png");
        var skin=panel.imgPortrait.GetComponent<CardArtIdleSkin>();var mouth=skin.Rig.GetComponent<HydraliskMouthRig>();
        Check(mouth!=null,"刺蛇双动作预览组件可用");
        target.Release();UnityEngine.Object.Destroy(target);target=null;camera.targetTexture=null;
        var control=HydraliskRoarPreviewControls.Install();
        var game=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(game).Show();EditorWindow.GetWindow(game).Focus();
        control.FireNow();
        for(int i=0;i<200 && mouth.OpenAmount<.95f;i++){await Frames(1);}
        Check(mouth.OpenAmount>.95f && control.TriggerCount>0,"预览咆哮实际张口抬头");
        mouth.paused=true;skin.Rig.paused=true;ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Key+".out",""),"preview-roar.png"));await Frames(8);mouth.paused=false;skin.Rig.paused=false;
        if(!string.IsNullOrEmpty(runtimeError)){throw new InvalidOperationException(runtimeError);}
        WriteResult(id,"ready-for-user",null);
    }
}