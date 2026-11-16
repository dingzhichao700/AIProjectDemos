using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>匹配页的打开、返回和战斗页移交适配；实际准入与准备由业务请求裁决。</summary>
public sealed class PlayerMatchingFlow {
    // 最小资源准入方案B的目标；正式安装前与获批路径核对，不注册临时别名。
    public const string PanelId="default/playerMatching/PlayerMatchingPanel";
    public const string PortraitAddress="Assets/Art/unpack/default/playerMatching/avatar-portrait.png";
    private static PlayerMatchingFlow current;
    private static bool opening;
    private PlayerMatchingBusinessSource source;
    private PlayerMatchingDisplayAssetLease assets;
    private PlayerMatchingPanel panel;
    private Sprite portrait;
    private bool disposed, entering;

    /// <summary>首页提供过期检查；beforePageOpen在替换首页前解除首页旧准备清理职责。</summary>
    public static async Task<bool> OpenAsync(int sceneId, Func<bool> isEntryCurrent, Action beforePageOpen) {
        if (opening || current!=null) return false;
        if (isEntryCurrent==null || beforePageOpen==null) throw new ArgumentNullException("首页接入必须提供窗口有效性检查和打开前移交");
        opening=true;
        var flow=new PlayerMatchingFlow {assets=new PlayerMatchingDisplayAssetLease()};
        BattleMatchRequest request=null;
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> {new ResLoadInfo(ResourceConst.GetUIPath(PanelId),ResType.Prefab)});
            flow.portrait=await flow.assets.LoadImageAsync(PortraitAddress);
            // 共用图集是页面展示依赖，沿现有图片租用加载，绝不整组或全牌库预载。
            await flow.assets.LoadImageAsync(BattleMatchResources.QualityAddress(2));
            if (!isEntryCurrent()) { flow.Dispose(); return false; }
            request=BattleMatchControl.ins.BeginLocalSimulation(sceneId);
            flow.SetSource(request);
            current=flow;
            beforePageOpen();
            flow.OpenPage();
            return true;
        } catch {
            (flow.source?.Request ?? request)?.Close();
            flow.Dispose();
            throw;
        } finally { opening=false; }
    }

    private void SetSource(BattleMatchRequest request) {
        source=new PlayerMatchingBusinessSource(request,assets,portrait,PortraitAddress);
        source.Ended+=OnEnded;
        source.Entering+=OnEntering;
        source.ClosedFromView+=OnClosedFromView;
        source.Retrying+=OnRetrying;
        source.DisplayFailed+=OnDisplayFailed;
    }

    private void OpenPage() {
        PanelMgr.ins.OpenPanel(PanelId,new dynamic[] {source});
        panel=PanelMgr.ins.GetPanel(PanelId) as PlayerMatchingPanel;
        if (panel==null || !panel.isOpened) throw new InvalidOperationException("匹配界面未能通过正式窗口入口打开");
        source.Activate();
    }

    private bool Owns(PlayerMatchingBusinessSource input) { return !disposed && ReferenceEquals(source,input); }

    private void OnRetrying(PlayerMatchingBusinessSource previous, BattleMatchRequest request) {
        if (!Owns(previous)) { request.Close(); return; }
        try {
            SetSource(request);
            panel.BindSource(source);
            previous.Dispose();
            source.Activate();
        } catch (Exception exception) {
            request.Close(); Debug.LogException(exception);
            GlobalToast.Warn("匹配界面打开失败，请重试");
            ReturnHome();
        }
    }

    private void OnEnded(PlayerMatchingBusinessSource input) {
        if (!Owns(input)) return;
        ReturnHome();
    }

    private void OnClosedFromView(PlayerMatchingBusinessSource input) {
        if (!Owns(input)) return;
        // 已成功移交时业务Close保留战斗；素材租用在Panel清图后才归还。
        Dispose();
    }

    private void OnEntering(PlayerMatchingBusinessSource input) {
        if (!Owns(input) || entering || !input.Request.TryTakeBattle()) return;
        entering=true;
        BattleMatchRequest request=input.Request;
        try {
            PanelMgr.ins.OpenPanel(MainUIEnum.BATTLE_SCENE_PANEL);
            BasePanel battle=PanelMgr.ins.GetPanel(MainUIEnum.BATTLE_SCENE_PANEL);
            if (battle==null || !battle.isOpened) throw new InvalidOperationException("战斗界面未能完成打开");
        } catch (Exception exception) {
            request.ReportEntryFailure("战斗界面打开失败，请重试");
            Debug.LogException(exception);
            entering=false;
            if (!disposed && panel!=null && panel.isOpened) source.Refresh();
            else {
                // 旧匹配页已回收时重新创建独立显示宿主，仍绑定业务给出的失败请求。
                RecoverFailedEntry(request);
            }
        }
    }

    private static async void RecoverFailedEntry(BattleMatchRequest request) {
        var flow=new PlayerMatchingFlow {assets=new PlayerMatchingDisplayAssetLease()};
        try {
            flow.portrait=await flow.assets.LoadImageAsync(PortraitAddress);
            flow.SetSource(request); current=flow; flow.OpenPage();
        } catch (Exception exception) {
            request.Close(); flow.Dispose(); Debug.LogException(exception);
            GlobalToast.Warn("战斗界面打开失败，请从主页重试");
            PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
        }
    }

    private void OnDisplayFailed(PlayerMatchingBusinessSource input, Exception exception) {
        if (!Owns(input)) return;
        Debug.LogException(exception);
        input.Request.Close();
        GlobalToast.Warn("匹配界面资源未能显示，请重试");
        ReturnHome();
    }

    private void ReturnHome() {
        if (disposed) return;
        PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
        if (panel!=null && panel.isOpened) panel.Close();
    }

    private void Dispose() {
        if (disposed) return;
        disposed=true;
        source?.Dispose(); assets?.Dispose();
        if (ReferenceEquals(current,this)) current=null;
    }
}
