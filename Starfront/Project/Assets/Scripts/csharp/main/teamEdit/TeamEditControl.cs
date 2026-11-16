using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cfg;
using UnityEngine;

/// <summary>
/// 主阵容调用队伍编辑的入口及正式资格查询。
/// </summary>
/// <remarks>主界面会话负责按钮接线；本模块不修改MainPanel或战斗开局。</remarks>
public sealed class TeamEditControl {

    public const string PanelId = "default/mainView/TeamEditPanel";

    private static TeamEditControl instance;

    private static int openVersion;

    public static TeamEditControl ins {
        get {
            if (instance == null) {
                var store = new TeamSchemeStore(Path.Combine(Application.persistentDataPath, "teamSchemes.json"));
                instance = new TeamEditControl(CfgManager.tables, CardCollectionControl.ins.model, store.Load(), store.Save);
            }
            return instance;
        }
    }

    public TeamSchemeModel model { get; }

    public TeamEditQuery Query { get; }

    public TeamEditControl(Tables tables, CardCollectionModel cards, TeamSchemeSaveVO saved, Action<TeamSchemeSaveVO> persist, Func<int, int?> profession = null) {
        if (saved != null && saved.schemes != null && saved.schemes.SelectMany(row => row ?? new int[0]).Any(id => id != 0 && tables.CardObj.GetOrDefault(id) == null)) {
            throw new InvalidOperationException("队伍存档引用不存在的卡牌，保留原文件");
        }
        Query = new TeamEditQuery(tables, cards, profession);
        model = new TeamSchemeModel(saved, Query.UnavailableReason, persist);
    }

    /**调用方订阅成功事件后读取该副本；返回连续靠左的四位副本，0仅作为尾部空槽保留。*/
    public int[] ReadSelected(out int number) {
        var saved = model.Read();
        number = saved.selected;
        return saved.schemes[number - 1];
    }

    /// <summary>
    /// 主界面await打开；onSaved仅在成功提交时通知刷新。
    /// </summary>
    /// <remarks>加载失败向调用方抛出；取消在途打开使用CancelPendingOpen，关闭已开窗口必须走草稿提示。</remarks>
    public static async Task OpenAsync(Action onSaved = null) {
        if (PanelMgr.ins.IsPanelOpen(PanelId)) {
            return;
        }
        var owner = ins;
        int version = ++openVersion;
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(PanelId), ResType.Prefab) });
        if (version == openVersion && !PanelMgr.ins.IsPanelOpen(PanelId)) {
            PanelMgr.ins.OpenPanel(PanelId, new object[] { owner, onSaved });
        }
    }


    /**关闭入口保留未保存提示；不让主界面直接调用PanelMgr强制丢弃草稿。*/
    public static void RequestClose() {
        CancelPendingOpen();
        var panel = PanelMgr.ins.GetPanel(PanelId) as TeamEditPanel;
        panel?.RequestClose();
    }

    public static void CancelPendingOpen() {
        openVersion++;
    }

#if UNITY_EDITOR
    /**验证整体替换并finally恢复，不触碰真实玩家的方案文件。*/
    public static TeamEditControl InstallValidationSession(TeamEditControl value) {
        var previous = instance;
        instance = value;
        return previous;
    }
#endif

}
