using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 资源加载loader
/// </summary>
public static class ResourceLoader {

    /// <summary>
    /// 资源批量加载信息
    /// </summary>
    class BatchInfo {
        //
        public int total;
        public int loaded;
        public Action onComplete;
        public Action<float> onProgress;

        public BatchInfo(int total, int loaded, Action onComplete, Action<float> onProgress = null) {
            this.total = total;
            this.loaded = loaded;
            this.onComplete = onComplete;
            this.onProgress = onProgress;
        }
    }

    // 正在进行的批次
    private static Dictionary<int, BatchInfo> _activeBatchMap = new();
    private static int _batchIdCounter = 0;

    /// <summary>
    /// 加载一批资源（不重复回调 / 带进度）
    /// </summary>
    public static async Task LoadListAsync(List<ResLoadInfo> list, Action onComplete = null, Action<float> onProgress = null) {
        if (list == null || list.Count == 0) {
            onProgress?.Invoke(1f);
            onComplete?.Invoke();
            return;
        }

        int batchId = ++_batchIdCounter;

        BatchInfo batch = new BatchInfo(list.Count, 0, onComplete, onProgress);

        _activeBatchMap.Add(batchId, batch);
        try {
            // 顺序加载可让界面准确反映离散资源完成比例。
            foreach (var item in list) {
                if (item.resType == ResType.FrameAnim) {
                    if (!FrameAnimationManager.HasLoad(item.path)) {
                        var texTask = ResourceManager.LoadAsync(new ResLoadInfo(item.path + ".png", ResType.UnpackImage));
                        var jsonTask = ResourceManager.LoadAsync(new ResLoadInfo(item.path, ResType.Json));
                        await Task.WhenAll(texTask, jsonTask);
                        await FrameAnimationManager.OnLoadAnimResComplete(item.path);
                    }
                } else {
                    await ResourceManager.LoadAsync(item);
                }
                batch.loaded++;
                batch.onProgress?.Invoke((float)batch.loaded / batch.total);
            }
            batch.onProgress?.Invoke(1f);
            batch.onComplete?.Invoke();
        } finally {
            _activeBatchMap.Remove(batchId);
        }
    }

}
