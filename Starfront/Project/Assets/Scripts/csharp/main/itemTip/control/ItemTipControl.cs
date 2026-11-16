using UnityEngine;
using System.Collections.Generic;

/// <summary>只读道具说明入口；同源切换关闭，异源原位换内容。</summary>
public static class ItemTipControl {
    public const string PanelId = "default/itemTip/ItemTipPanel";
    public static ItemTipOpenVO Pending { get; private set; }

    public static async void Open(int itemId, GameObject sourceGo, TextAsset resourceMap = null) {
        if (sourceGo == null || !sourceGo.activeInHierarchy || !(sourceGo.transform is RectTransform) || CfgManager.tables == null) {
            return;
        }
        if (Pending != null && Pending.source == sourceGo) {
            Close();
            return;
        }
        var material = sourceGo.GetComponent<CardCultivationMaterialItem>();
        if (resourceMap == null && material != null) {
            resourceMap = material.itemResourceMap;
        }
        var value = new ItemTipOpenVO { itemId = itemId, source = sourceGo, owner = sourceGo.GetComponentInParent<BasePanel>(), resourceMap = resourceMap };
        if (!value.IsValid) {
            return;
        }
        Pending = value;
        int windowRevision = PanelMgr.ins.InteractiveWindowRevision;
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(PanelId), ResType.Prefab) });
        } catch (System.Exception error) {
            if (ReferenceEquals(Pending, value)) {
                Pending = null;
                Debug.LogException(error);
            }
            return;
        }
        if (!ReferenceEquals(Pending, value) || !value.IsValid || windowRevision != PanelMgr.ins.InteractiveWindowRevision) {
            Released(value);
            return;
        }
        var panel = PanelMgr.ins.GetPanel(PanelId) as ItemTipPanel;
        if (panel != null && !panel.isClosing) {
            panel.Present(value);
        } else {
            PanelMgr.ins.OpenTip(PanelId, sourceGo, new dynamic[] { value });
        }
    }

    public static void Close() {
        Pending = null;
        PanelMgr.ins.ClosePanelByType(PanelId);
    }

    public static void CancelSource(GameObject source) {
        if (Pending != null && Pending.source == source) {
            Close();
        }
    }

    public static void Released(ItemTipOpenVO value) {
        if (ReferenceEquals(Pending, value)) {
            Pending = null;
        }
    }
}
