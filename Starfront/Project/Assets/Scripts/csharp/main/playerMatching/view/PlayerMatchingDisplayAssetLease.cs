using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>匹配页面按冻结卡组加载正式卡面与公共框体，不读取当前编组或重造资源加载器。</summary>
/// <remarks>适配器须在窗口清空Sprite引用后Dispose；在途加载完成后自行归还租用。</remarks>
public sealed class PlayerMatchingDisplayAssetLease : IDisposable {

    private readonly Dictionary<string, Task<Sprite>> images = new Dictionary<string, Task<Sprite>>();
    private readonly List<IDisposable> retained = new List<IDisposable>();
    private bool disposed;

    public async Task<PlayerMatchingTeamDisplay> BuildTeamAsync(BattleMatchPlayerVO player, string portraitAddress) {
        if (disposed) throw new ObjectDisposedException(nameof(PlayerMatchingDisplayAssetLease));
        if (player == null) throw new ArgumentNullException(nameof(player));
        var cards = new List<BattleMatchCardVO>(player.EffectiveCards);
        if (cards.Count > 4) throw new ArgumentException("本页显示容量为4张卡牌");
        await CfgManager.EnsureLoadedAsync();
        Sprite portrait = await LoadImageAsync(portraitAddress);
        var display = new List<PlayerMatchingCardDisplay>();
        foreach (var entry in cards) {
            var card = entry;
            if (card == null || card.Level <= 0) throw new ArgumentException("冻结卡牌显示数据无效");
            var art = CfgManager.tables.CardArtObj.Get(card.ArtId);
            if (art == null) throw new InvalidOperationException("缺少卡牌美术配置：" + card.ArtId);
            // 卡面是否已交付按既有美术配置显示空白；不借用其他兵种或旧图片补齐。
            Sprite artwork = null;
            if (!string.IsNullOrWhiteSpace(art.CardImage)) artwork = await LoadImageAsync(art.CardImage);
            else if (art.CardFaceReady) throw new InvalidOperationException("已就绪卡面缺少路径：" + card.ArtId);
            Sprite quality = await LoadImageAsync(BattleMatchResources.QualityAddress(card.Quality));
            Sprite profession = null;
            if (card.TagId > 0) {
                var tag = CfgManager.tables.CardTagObj.Get(card.TagId);
                if (tag == null) throw new InvalidOperationException("缺少卡牌标签配置：" + card.TagId);
                profession = await LoadImageAsync(tag.Icon);
            }
            display.Add(new PlayerMatchingCardDisplay(artwork, quality, profession, card.Name, card.ConstructionCost, card.Level));
        }
        if (disposed) throw new OperationCanceledException("匹配显示资源已关闭");
        return new PlayerMatchingTeamDisplay(player.Name, Identity(player), portrait, display);
    }

    public static string Identity(BattleMatchPlayerVO player) {
        string text=(player.IsComputer ? "电脑模拟 · " : "出战阵容 · ")+player.EffectiveCards.Count+"张卡牌";
        if (player.IgnoredCards.Count>0) text+=" · 已忽略"+player.IgnoredCards.Count+"张";
        return text;
    }

    public Sprite GetQualityFrame(int quality) {
        if (disposed) return null;
        // Flow已持有整个公共图集；卡面等待/失败不影响独立品质框显示。
        ImageResource image=ResourcePresentation.Policy.ResolveImage(BattleMatchResources.QualityAddress(quality));
        return ResourceManager.GetAtlasSprite(image.Address,image.SpriteName);
    }

    public Task<Sprite> LoadImageAsync(string address) {
        if (disposed) throw new ObjectDisposedException(nameof(PlayerMatchingDisplayAssetLease));
        if (string.IsNullOrWhiteSpace(address)) return Task.FromResult<Sprite>(null);
        images.TryGetValue(address, out Task<Sprite> task);
        if (task != null && (task.IsFaulted || task.IsCanceled)) { images.Remove(address); task=null; }
        if (task == null) {
            task = LoadCoreAsync(address);
            images.Add(address, task);
        }
        return task;
    }

    private async Task<Sprite> LoadCoreAsync(string address) {
        ImageResource resource = ResourcePresentation.Policy.ResolveImage(address);
        IDisposable lease = await CultivationImageLease.RetainImageAsync(address);
        bool accepted = false;
        try {
            if (disposed) throw new OperationCanceledException("匹配显示资源已关闭");
            Sprite sprite = resource.Type == ResType.Atlas
                ? ResourceManager.GetAtlasSprite(resource.Address, resource.SpriteName)
                : ResourceManager.GetUnpackImage(resource.Address);
            if (sprite == null) throw new InvalidOperationException("匹配显示资源加载后缺失：" + address);
            retained.Add(lease);
            accepted = true;
            return sprite;
        } finally {
            if (!accepted) lease.Dispose();
        }
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;
        foreach (IDisposable lease in retained) lease.Dispose();
        retained.Clear();
        images.Clear();
    }
}
