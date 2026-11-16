using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 培养页调用技能详情的表现入口。
/// </summary>
/// <remarks>只加载和开关窗口；关闭不会重建来源页或修改卡牌与存档。</remarks>
public static class CardSkillDetailControl {

    public const string PanelId = "default/mainView/CardSkillDetailPanel";

    private static int openVersion;

    /// <summary>
    /// 打开当前卡牌所选技能，skillId=0表示按入口查询正式关联。
    /// </summary>
    /// <remarks>来源页负责await并显示加载失败；Close可取消尚未完成的打开，过期加载不再弹窗。</remarks>
    public static async Task OpenAsync(int cardId, CardSkillSlot slot, int skillId = 0, Action onClosed = null) {
        if (PanelMgr.ins.IsPanelOpen(PanelId)) {
            return;
        }
        int version = ++openVersion;
        var state = CardCollectionControl.ins.model.GetState(cardId);
        CardSkillDetailQuery.Read(CfgManager.tables, cardId, state.level, slot, skillId);
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(PanelId), ResType.Prefab) });
        if (version != openVersion || PanelMgr.ins.IsPanelOpen(PanelId)) {
            return;
        }
        PanelMgr.ins.OpenPanel(PanelId, new object[] { new CardSkillDetailOpenVO { cardId = cardId, slot = slot, skillId = skillId, onClosed = onClosed } });
    }

    /**来源页离开时可调用；同时使尚未完成的异步打开失效。*/
    public static void Close() {
        openVersion++;
        PanelMgr.ins.ClosePanelByType(PanelId);
    }

}
