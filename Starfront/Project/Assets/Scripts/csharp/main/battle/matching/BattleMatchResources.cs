using System;
using System.Collections.Generic;
using System.Linq;
using cfg;

/// <summary>只收集本次有效卡的匹配显示依赖；加载及物理引用仍由现有资源租约管理。</summary>
public static class BattleMatchResources
{
    public static string QualityAddress(int quality)
    {
        string name;
        switch (Math.Max(2, Math.Min(5, quality)))
        {
            case 2: name = "green"; break;
            case 3: name = "blue"; break;
            case 4: name = "purple"; break;
            case 5: name = "gold"; break;
            default: throw new ArgumentOutOfRangeException(nameof(quality));
        }
        return ResourceConst.PATH_ATLAS + "default/mainView/quality-frame-" + name;
    }

    public static IReadOnlyList<string> CollectDisplayImages(BattleMatchPairVO pair)
    {
        var images = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in pair.Friendly.EffectiveCards.Concat(pair.Enemy.EffectiveCards))
        {
            var art = CfgManager.tables.CardArtObj.Get(card.ArtId);
            if (art == null)
            {
                throw new InvalidOperationException("缺少卡牌美术配置：" + card.ArtId);
            }
            if (!string.IsNullOrWhiteSpace(art.CardImage))
            {
                images.Add(art.CardImage);
            }
            else if (art.CardFaceReady)
            {
                throw new InvalidOperationException("已交付卡面缺少路径：" + card.ArtId);
            }
            if (card.TagId > 0)
            {
                var tag = CfgManager.tables.CardTagObj.Get(card.TagId);
                if (tag == null)
                {
                    throw new InvalidOperationException("缺少卡牌标签配置：" + card.TagId);
                }
                if (!string.IsNullOrWhiteSpace(tag.Icon))
                {
                    images.Add(tag.Icon);
                }
            }
            images.Add(QualityAddress(card.Quality));
        }
        return Array.AsReadOnly(images.OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }
}
