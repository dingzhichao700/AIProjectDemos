using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>培养动态背景和图标的资源租用及过期回调保护。</summary>
public sealed class CultivationImageLease : MonoBehaviour {

    private sealed class Lease { public int count; public bool owned; public Task loading; }
    private static readonly Dictionary<string, Lease> leases = new Dictionary<string, Lease>();
    private string path;
    private string heldKey;
    private int version;
    private string pendingAddress;
    private Task pendingBind;

    public Task BindAsync(string address) {
        if (pendingAddress == address && pendingBind != null && !pendingBind.IsCompleted) {
            return pendingBind;
        }
        var task = BindCoreAsync(address);
        pendingAddress = address;
        pendingBind = task;
        return task;
    }

    private async Task BindCoreAsync(string address) {
        // 数据刷新仍指向同一已持有资源时，保留图像与租约，避免先清空再重载。
        var currentImage = GetComponent<Image>();
        if (!string.IsNullOrWhiteSpace(address) && path == address && currentImage.sprite != null) {
            currentImage.color = Color.white;
            currentImage.enabled = true;
            return;
        }
        Clear();
        if (string.IsNullOrWhiteSpace(address)) {
            return;
        }
        int token = version;
        // 同一图集下不同图标共享一个真实资源句柄，关闭单个图标不释放其他图标。
        bool atlas = address.StartsWith(ResourceConst.PATH_ATLAS, StringComparison.Ordinal);
        ImageResource resource = atlas ? ResourcePresentation.Policy.ResolveImage(address) : new ImageResource(address, ResType.UnpackImage);
        string key = resource.Address;
        Lease lease = Retain(resource);
        bool assigned = false;
        try {
            await lease.loading;
            if (this == null || !isActiveAndEnabled || token != version) {
                return;
            }
            var image = GetComponent<Image>();
            image.sprite = atlas ? ResourceManager.GetAtlasSprite(key, resource.SpriteName) : ResourceManager.GetUnpackImage(key);
            if (image.sprite == null) {
                throw new InvalidOperationException("培养资源加载后缺失：" + address);
            }
            image.color = Color.white;
            image.enabled = true;
            path = address;
            heldKey = key;
            assigned = true;
        } finally {
            if (!assigned) {
                Release(key);
            }
        }
    }

    public void Clear() {
        version++;
        pendingAddress = null;
        pendingBind = null;
        var image = GetComponent<Image>();
        image.sprite = null;
        image.enabled = false;
        if (path != null) {
            Release(heldKey);
            heldKey = null;
            path = null;
        }
    }

    private static Lease Retain(ImageResource resource)
    {
        string key = resource.Address;
        if (!leases.TryGetValue(key, out Lease lease))
        {
            lease = new Lease
            {
                owned = resource.Type == ResType.Atlas ? !ResourceManager.HasLoadedAtlas(key) : !ResourceManager.HasLoadedUnpackImage(key),
                loading = ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(key, resource.Type) })
            };
            leases.Add(key, lease);
        }
        lease.count++;
        return lease;
    }

    private sealed class RetainedImage : IDisposable
    {
        private string key;
        public RetainedImage(string key)
        {
            this.key = key;
        }
        public void Dispose()
        {
            if (key != null)
            {
                Release(key);
                key = null;
            }
        }
    }

    /// <summary>显式保留跨页面共用图片；场景不创建或持有表现节点。</summary>
    public static async Task<IDisposable> RetainImageAsync(string address)
    {
        ImageResource resource = address.StartsWith(ResourceConst.PATH_ATLAS, StringComparison.Ordinal)
            ? ResourcePresentation.Policy.ResolveImage(address) : new ImageResource(address, ResType.UnpackImage);
        Lease lease = Retain(resource);
        try
        {
            await lease.loading;
            return new RetainedImage(resource.Address);
        }
        catch
        {
            Release(resource.Address);
            throw;
        }
    }

    private static void Release(string address) {
        var lease = leases[address];
        lease.count--;
        if (lease.count == 0) {
            leases.Remove(address);
            if (lease.owned) {
                ResourceManager.Release(address);
            }
        }
    }

    private void OnDisable() {
        Clear();
    }

}
