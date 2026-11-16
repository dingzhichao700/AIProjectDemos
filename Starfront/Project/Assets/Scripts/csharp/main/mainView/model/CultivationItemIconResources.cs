using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>已批准道具图标的表现层映射；不决定成本、道具品质或卡牌是否启用。</summary>
public sealed class CultivationItemIconResources {

    private static readonly Dictionary<TextAsset, CultivationItemIconResources> catalogs = new Dictionary<TextAsset, CultivationItemIconResources>();
    private readonly Dictionary<int, string> addresses = new Dictionary<int, string>();
    private readonly Dictionary<int, string> backgrounds = new Dictionary<int, string>();

    private CultivationItemIconResources(TextAsset source) {
        var document = SimpleJSON.JSON.Parse(source.text);
        foreach (SimpleJSON.JSONNode entry in document["entries"].AsArray) {
            int itemId = entry["itemId"].AsInt;
            string address = entry["content"].Value;
            if (itemId <= 0 || string.IsNullOrWhiteSpace(address) || addresses.ContainsKey(itemId)) {
                throw new InvalidOperationException("道具图标资源映射无效：" + source.name + "/" + itemId);
            }
            addresses.Add(itemId, address);
        }
        if (document["qualityBackgrounds"].IsArray) {
            foreach (SimpleJSON.JSONNode entry in document["qualityBackgrounds"].AsArray) {
                int quality = entry["quality"].AsInt;
                string address = entry["address"].Value;
                if (quality <= 0 || string.IsNullOrWhiteSpace(address) || backgrounds.ContainsKey(quality)) {
                    throw new InvalidOperationException("道具品质背景映射无效：" + source.name);
                }
                backgrounds.Add(quality, address);
            }
        }
    }

    public static bool TryGetBackgroundAddress(TextAsset source, int itemId, int quality, out string address) {
        return TryGetQualityBackgroundAddress(source, itemId, quality, out address);
    }

    /**材料格和道具说明共用品质背景解析；品质由道具配置决定。*/
    public static bool TryGetQualityBackgroundAddress(TextAsset source, int itemId, int quality, out string address) {
        address = null;
        if (!TryGetAddress(source, itemId, out string ignored)) {
            return false;
        }
        return catalogs[source].backgrounds.TryGetValue(quality, out address);
    }

    public static bool TryGetAddress(TextAsset source, int itemId, out string address) {
        address = null;
        if (source == null) {
            return false;
        }
        if (!catalogs.TryGetValue(source, out CultivationItemIconResources catalog)) {
            catalog = new CultivationItemIconResources(source);
            catalogs.Add(source, catalog);
        }
        return catalog.addresses.TryGetValue(itemId, out address);
    }

}
