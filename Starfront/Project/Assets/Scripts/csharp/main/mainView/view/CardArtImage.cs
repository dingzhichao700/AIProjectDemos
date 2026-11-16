using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>按美术条目与用途加载透明主体；美术ID不等于卡牌ID或持有状态。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class CardArtImage : MonoBehaviour
{
    public enum Usage { Portrait, Formation, Card, Reveal, Tag }
    public bool autoLoadOnEnable = true;
    public int artId;
    public Usage usage;
    private bool animationActivated;
    public bool AnimationActivated => animationActivated;
    public Task AnimationLoading { get; private set; } = Task.CompletedTask;

    /**只消费页面的权威owned状态；不决定解锁资格或写玩家数据。*/
    public void SetAnimationActivated(bool activated) {
        if (animationActivated == activated) { return; }
        animationActivated = activated;
        if (!activated) { var skin = GetComponent<CardArtIdleSkin>(); if (skin != null) { skin.Clear(); } }
        else { AnimationLoading = SyncAnimationAsync(); Observe(AnimationLoading); }
    }

    private async Task SyncAnimationAsync() {
        int request = version;
        var image = GetComponent<Image>();
        Sprite sprite = image.sprite;
        await Task.Yield();
        if (this == null || !isActiveAndEnabled || request != version || !animationActivated || sprite == null || image.sprite != sprite || !CardArtIdleSkin.Supports(this, artId, usage)) { return; }
        var idle = GetComponent<CardArtIdleSkin>() ?? gameObject.AddComponent<CardArtIdleSkin>();
        await idle.BindAsync(sprite);
    }

    private int version;
    private string displayedPath;
    public Task Loading { get; private set; } = Task.CompletedTask;

    private sealed class Lease
    {
        public int users;
        public Task task;
        public IDisposable retained;
    }
    private static readonly Dictionary<string, Lease> leases = new Dictionary<string, Lease>();

    private void OnEnable()
    {
        if (Application.isPlaying && autoLoadOnEnable && artId > 0)
        {
            Loading = RefreshAsync();
            Observe(Loading);
        }
    }

    private async void Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    public Task BindAsync(int id, Usage targetUsage)
    {
        artId = id;
        usage = targetUsage;
        Loading = RefreshAsync();
        return Loading;
    }

    public static string PathFor(cfg.resource.CardArtResource item, Usage targetUsage)
    {
        switch (targetUsage)
        {
            case Usage.Portrait: return item.Portrait;
            case Usage.Formation: return item.Formation;
            case Usage.Card: return item.CardImage;
            case Usage.Reveal: return item.RevealImage;
            default: throw new ArgumentOutOfRangeException(nameof(targetUsage));
        }
    }

    /// <summary>关闭或列表复用时立即清空，未完成请求自行释放，不回填旧图。</summary>
    public void Clear()
    {
        var idle = GetComponent<CardArtIdleSkin>();
        if (idle != null) {
            idle.Clear();
        }
        version++;
        var image = GetComponent<Image>();
        image.sprite = null;
        image.enabled = false;
        if (displayedPath != null)
        {
            Release(displayedPath);
            displayedPath = null;
        }
    }

    private async Task RefreshAsync()
    {
        Clear();
        int request = version;
        int id = artId;
        Usage selectedUsage = usage;
        await CfgManager.EnsureLoadedAsync();
        if (this == null || !isActiveAndEnabled || request != version)
        {
            return;
        }
        string path;
        if (selectedUsage == Usage.Tag) {
            var tag = CfgManager.tables.CardTagObj.Get(id);
            if (tag == null) {
                throw new InvalidOperationException("卡牌标签不存在：" + id);
            }
            path = tag.Icon;
        } else {
            var item = CfgManager.tables.CardArtObj.Get(id);
            if (item == null) {
                throw new InvalidOperationException("未配置卡牌美术条目：" + id);
            }
            path = PathFor(item, selectedUsage);
            // 尚未交付的新版美术保留空白；不能加载旧卡形象作为替代。
            if (string.IsNullOrWhiteSpace(path) && !item.CardFaceReady) {
                return;
            }
        }
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/Art/unpack/", StringComparison.Ordinal) || path.Contains(".."))
        {
            throw new InvalidOperationException("无效卡牌美术路径：" + path);
        }
        Lease lease;
        if (!leases.TryGetValue(path, out lease))
        {
            lease = new Lease();
            lease.task = RetainSharedAsync(path, lease);
            leases.Add(path, lease);
        }
        lease.users++;
        bool assigned = false;
        try
        {
            await lease.task;
            if (this == null || !isActiveAndEnabled || request != version)
            {
                return;
            }
            var sprite = ResourceManager.GetUnpackImage(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("卡牌美术加载后缺少Sprite：" + path);
            }
            var image = GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = true;
            displayedPath = path;
            assigned = true;
            AnimationLoading = SyncAnimationAsync();
            await AnimationLoading;
        }
        finally
        {
            if (!assigned)
            {
                Release(path);
            }
        }
    }

    private static async Task RetainSharedAsync(string path, Lease lease)
    {
        lease.retained = await CultivationImageLease.RetainImageAsync(path);
    }

    private static void Release(string path)
    {
        if (!leases.TryGetValue(path, out var lease))
        {
            return;
        }
        lease.users--;
        if (lease.users == 0)
        {
            leases.Remove(path);
            lease.retained?.Dispose();
        }
    }
}
