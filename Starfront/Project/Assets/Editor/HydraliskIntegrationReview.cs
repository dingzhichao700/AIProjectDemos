using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static partial class MarineIdleIntegrationReview {
    [MenuItem("Tools/开发验收/刺蛇待机/未激活到激活对比")]
    public static void OpenHydraliskReview() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Pending)) { Debug.LogWarning("请在Edit空闲时打开刺蛇对比。"); return; }
        var request = new Request { requestId="hydralisk-user-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"), processId=System.Diagnostics.Process.GetCurrentProcess().Id, action="zerg-review", evidenceDirectory=Path.GetFullPath("Library/AIUI/HydraliskReview") };
        File.WriteAllText(Pending+".tmp",JsonUtility.ToJson(request));File.Move(Pending+".tmp",Pending);
    }
    private static async Task WaitAnimation(CardArtImage image) { await image.Loading; await image.AnimationLoading; await Frames(8); }
    private static void RefreshDeck(DeckPresentationView deck) { typeof(DeckPresentationView).GetMethod("RefreshCollection", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(deck,null); }
    private static bool DeployHydralisk(DeckPresentationView deck) {
        var model=TeamEditControl.ins.model;
        bool saved=model.TrySave(1,new[]{2202,2101,2103,2104},model.Read().revision,out string reason);
        Check(saved,"隔离编组激活刺蛇后可上阵："+reason); RefreshDeck(deck); return saved;
    }
    private static async Task RunZerg(string id, DeckPresentationView deck) {
        var growth=CardCollectionControl.ins.model.GetState(2202);
        Check(!growth.owned && growth.level==0,"激活权威为owned/level，不以碎片足够代替");
        Check(!TeamEditControl.ins.model.TrySave(1,new[]{2202,2101,2103,2104},TeamEditControl.ins.model.Read().revision,out string lockedReason),"未激活不绕过编组资格");
        deck.RequestCardInfo(2202);
        var panel=await CultivationOpened(2202);
        await WaitAnimation(panel.imgPortrait.GetComponent<CardArtImage>());
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig==null,"培养未激活静态无骨架");
        await Frames(10);Capture("cultivation-unactivated.png");
        if(SessionState.GetString(Key+".action","")=="zerg-review") {
            Action activated=null;
            activated=()=>{if(CardCollectionControl.ins.model.GetState(2202).owned){CardCollectionControl.ins.model.Changed-=activated;DeployHydralisk(deck);}};
            CardCollectionControl.ins.model.Changed+=activated;
            target.Release();UnityEngine.Object.Destroy(target);target=null;camera.targetTexture=null;
            var game=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(game).Show();EditorWindow.GetWindow(game).Focus();
            await Frames(15);ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Key+".out",""),"preview-unactivated.png"));
            WriteResult(id,"ready-for-user",null);return;
        }
        // 未激活不能真正上阵；直接以该权威VO检查阵容组件投影，不更改编组状态。
        var config=CfgManager.tables.CardObj.Get(2202);
        var probe=deck.slots[0];
        probe.Render(new DeckCardView.DisplayData{cardId=2202,artId=2202,title="刺蛇",owned=false,level=0,quality=(int)config.Quality,tagId=config.TagId},null);
        await probe.ArtLoading;await WaitAnimation(probe.artwork.GetComponent<CardArtImage>());
        Check(probe.artwork.GetComponent<CardArtIdleSkin>()?.Rig==null,"阵容组件未激活静态");
        var oldSprite=panel.imgPortrait.sprite;
        Check(CardCollectionControl.ins.TryUpgrade(2202,growth.level,growth.revision,out string reason),"隔离内存实际解锁事务："+reason);
        await Frames(15);await WaitAnimation(panel.imgPortrait.GetComponent<CardArtImage>());
        Check(CardCollectionControl.ins.model.GetState(2202).owned && panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig!=null,"激活事件刷新培养动画，无需重开");
        Check(panel.imgPortrait.sprite==oldSprite,"同图激活不替换立绘源");
        DeployHydralisk(deck);await Frames(15);
        var formation=deck.slots[0].artwork;await WaitAnimation(formation.GetComponent<CardArtImage>());
        Check(formation.GetComponent<CardArtIdleSkin>()?.Rig!=null,"激活后阵容刺蛇动画");
        Check(formation.GetComponent<CardArtIdleSkin>().Rig.skin.GetComponent<SpriteRenderer>().sprite.texture!=deck.slots[1].artwork.GetComponent<CardArtIdleSkin>().Rig.skin.GetComponent<SpriteRenderer>().sprite.texture,"两角色资源租约不串用");
        panel.btnReturn.onClick.Invoke(); await Frames(12);
        await CheckMotion(formation,"hydralisk-formation");
        deck.RequestCardInfo(2202); panel=await CultivationOpened(2202); await WaitAnimation(panel.imgPortrait.GetComponent<CardArtImage>());
        await CheckMotion(panel.imgPortrait,"hydralisk-cultivation");
        await CheckRoar(panel.imgPortrait);
        await CheckCultivationMotionPolicy(formation, panel.imgPortrait);
        var preserved=panel.imgPortrait.GetComponent<CardArtIdleSkin>().Rig;
        panel.imgPortrait.GetComponent<CardArtImage>().SetAnimationActivated(true);await Frames(3);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>().Rig==preserved,"同卡已激活刷新不重启动作");
        Check(!await panel.imgPortrait.GetComponent<CardArtIdleSkin>().PlayBurstAsync(),"刺蛇不误播陆战队员射击");
        var gate=panel.imgPortrait.GetComponent<CardArtImage>();gate.SetAnimationActivated(false);await Frames(3);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>().Rig==null && panel.imgPortrait.canvasRenderer.GetMesh().vertexCount==4,"门控关闭立即回原图四边形");
        Check(panel.imgPortrait.overrideSprite == panel.imgPortrait.sprite && !await panel.imgPortrait.GetComponent<CardArtIdleSkin>().PlaySpecialAsync(), "未激活恢复原静态纹理且不能咆哮");
        gate.SetAnimationActivated(true);gate.SetAnimationActivated(false);await gate.AnimationLoading;await Frames(3);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>().Rig==null,"激活动画加载中失效不回填");
        gate.SetAnimationActivated(true);await gate.AnimationLoading;await Frames(5);
        panel.btnNext.onClick.Invoke();panel=await CultivationOpened(2203);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig==null,"其他虫族保持静态不批量");
        panel.btnPrevious.onClick.Invoke();panel=await CultivationOpened(2202);await WaitAnimation(panel.imgPortrait.GetComponent<CardArtImage>());
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig!=null,"切回已激活刺蛇恢复动画");
        panel.btnReturn.onClick.Invoke();await Frames(12);
        Check(CardArtIdleSkin.ActiveLeases==2,"关闭培养保留阵容两角色各自租约");
        PanelMgr.ins.ClosePanelByType("default/mainView/MainPanel");await Frames(20);
        Check(CardArtIdleSkin.ActiveLeases==0 && UnityEngine.Object.FindObjectsOfType<MarineIdleRig>().Length==0,"全部关闭两角色骨架及租约释放");
        Check(writes==1,"只有一次隔离内存激活提交，未安装真实存档写入器");
        if(!string.IsNullOrEmpty(runtimeError)){throw new InvalidOperationException(runtimeError);}
        WriteResult(id,"passed",null);EditorApplication.isPlaying=false;
    }
}