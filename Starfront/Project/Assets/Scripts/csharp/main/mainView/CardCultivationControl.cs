using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>从上阵位或牌库打开同一培养全屏页。</summary>
public static class CardCultivationControl {

    public const string PanelId = "default/mainView/CardCultivationPanel";
    private static int version;
    private static CardCultivationOpenVO active;
    private static bool opening;

    public static async Task OpenAsync(CardCultivationOpenVO args) {
        if (args == null || args.owner == null || args.orderedCardIds == null || args.orderedCardIds.Length == 0 || args.orderedCardIds.Any(id => id <= 0) || args.orderedCardIds.Distinct().Count() != args.orderedCardIds.Length || !args.orderedCardIds.Contains(args.cardId)) {
            throw new ArgumentException("培养页需要包含当前卡牌的有效来源序列");
        }
        if (opening || PanelMgr.ins.IsPanelOpen(PanelId)) {
            return;
        }
        var request = new CardCultivationOpenVO { cardId = args.cardId, orderedCardIds = args.orderedCardIds.ToArray(), source = args.source, owner = args.owner, onReturn = args.onReturn };
        int token = ++version;
        active = request;
        opening = true;
        try {
            await CfgManager.EnsureLoadedAsync();
            if (token != version || request.owner == null || request.owner is UnityEngine.Behaviour source && !source.isActiveAndEnabled) {
                return;
            }
            foreach (int id in request.orderedCardIds) {
                CardCollectionControl.ins.model.GetState(id);
            }
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(PanelId), ResType.Prefab) });
            if (token != version || request.owner == null) {
                return;
            }
            PanelMgr.ins.OpenPanel(PanelId, new object[] { request });
        } finally {
            if (token == version) {
                opening = false;
                if (!PanelMgr.ins.IsPanelOpen(PanelId)) {
                    active = null;
                }
            }
        }
    }

    /**来源销毁/离开时同时取消在途加载与已打开培养页，避免迟到弹出。*/
    public static void CloseForSource(UnityEngine.Object owner) {
        if (active == null || active.owner != owner) {
            return;
        }
        version++;
        opening = false;
        PanelMgr.ins.ClosePanelByType(PanelId);
        active = null;
    }

    public static void OnClosed(CardCultivationOpenVO request) {
        if (ReferenceEquals(active, request)) {
            active = null;
        }
    }

}
