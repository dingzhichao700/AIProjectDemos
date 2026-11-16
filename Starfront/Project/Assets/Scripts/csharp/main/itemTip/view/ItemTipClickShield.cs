using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>消耗外部点击；仅将明确的材料来源转交说明入口。</summary>
public sealed class ItemTipClickShield : MonoBehaviour, IPointerClickHandler {
    public ItemTipPanel panel;

    public void OnPointerClick(PointerEventData data) {
        if (panel == null || !panel.isOpened) {
            return;
        }
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        foreach (var hit in hits) {
            if (hit.gameObject.transform.IsChildOf(panel.transform)) {
                continue;
            }
            var material = hit.gameObject.GetComponentInParent<CardCultivationMaterialItem>();
            if (material != null && material.CurrentItemId > 0) {
                ItemTipControl.Open(material.CurrentItemId, material.gameObject, material.itemResourceMap);
            } else {
                ItemTipControl.Close();
            }
            data.Use();
            return;
        }
        ItemTipControl.Close();
        data.Use();
    }
}
