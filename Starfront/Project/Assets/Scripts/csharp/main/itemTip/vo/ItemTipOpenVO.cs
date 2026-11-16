using UnityEngine;

/// <summary>说明浮窗的只读数据与来源生命周期。</summary>
public sealed class ItemTipOpenVO {
    public int itemId;
    public GameObject source;
    public BasePanel owner;
    public TextAsset resourceMap;

    public bool IsValid {
        get {
            if (source == null || !source.activeInHierarchy || owner == null || owner.isClosing) {
                return false;
            }
            var material = source.GetComponent<CardCultivationMaterialItem>();
            return material == null || material.CurrentItemId == itemId;
        }
    }
}
