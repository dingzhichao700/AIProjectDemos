using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 管理共享帧动画资源与播放实例池。
/// </summary>
public class FrameAnimationManager : MonoBehaviour {

    /**加载完成的动画数据字典<路径,帧动画信息>*/
    public static Dictionary<string, FrameAnimationRes> loadedMap = new Dictionary<string, FrameAnimationRes>();

    /**合并同一动画的加载与裁帧请求，完成或失败后移除任务*/
    private static Dictionary<string, Task<FrameAnimationRes>> loadingMap = new Dictionary<string, Task<FrameAnimationRes>>();

    /**帧动画对象池*/
    private static List<FrameAnimationView> pool;

    private static int createIndex = 0;

    /**所有裁帧任务共用的每帧软预算，单次纹理 API 无法中断*/
    private const double GENERATION_BUDGET_MS = 2;
    private static readonly System.Diagnostics.Stopwatch generationWatch = new System.Diagnostics.Stopwatch();
    private static int generationFrame = -1;

    /**池中实例上限*/
    private const int POOL_MAX = 300;

    /**某资源是否已加载*/
    public static Boolean HasLoad(string path) {
        return loadedMap != null && loadedMap.ContainsKey(path);
    }

    /**获取某资源*/
    public static FrameAnimationRes GetRes(string path) {
        if (HasLoad(path)) {
            return loadedMap[path];
        }
        return null;
    }

    /**获取实例*/
    public static FrameAnimationView GetInstance() {
        FrameAnimationView item = null;
        while (pool != null && pool.Count > 0 && item == null) {
            int index = pool.Count - 1;
            item = pool[index];
            pool.RemoveAt(index);
        }
        if (item == null) {
            GameObject go = new GameObject("FrameAnimation" + createIndex, typeof(RectTransform));
            createIndex++;
            item = go.AddComponent<FrameAnimationView>();
        }
        item.MarkRented();
        ResetTransform(item.trans);
        return item;
    }

    /**归还实例到池*/
    public static void RecoverItem(FrameAnimationView item) {
        if (item == null || item.isInPool) {
            return;
        }
        if (pool == null) {
            pool = new List<FrameAnimationView>();
        }
        if (pool.Count >= POOL_MAX) {
            item.Destroy();
            return;
        }
        item.MarkRecovered();
        ResetTransform(item.trans);
        if (PanelMgr.ins != null && PanelMgr.ins.uiPool != null) {
            item.trans.SetParent(PanelMgr.ins.uiPool, false);
        }
        pool.Add(item);
    }

    /**移除已经进入对象池的实例*/
    internal static void RemoveItem(FrameAnimationView item) {
        if (pool != null) {
            pool.Remove(item);
        }
    }

    /**清除上一次使用遗留的节点变换*/
    private static void ResetTransform(RectTransform trans) {
        trans.anchorMin = trans.anchorMax = new Vector2(0.5f, 0.5f);
        trans.pivot = new Vector2(0.5f, 0.5f);
        trans.anchoredPosition = Vector2.zero;
        trans.sizeDelta = Vector2.zero;
        trans.localScale = Vector3.one;
        trans.localEulerAngles = Vector3.zero;
    }

    /// <summary>
    /// 加载一个帧动画资源
    /// </summary>
    /// <param name="path">动画路径</param>
    /// <param name="action">加载完成回调</param>
    public static async void LoadFrameAnimationRes(string path, Action<FrameAnimationRes> action) {
        try {
            FrameAnimationRes data = await LoadFrameAnimationResAsync(path);
            action?.Invoke(data);
        } catch (Exception exception) {
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// 获取共享动画资源，合并预加载与播放发起的并发请求。
    /// </summary>
    /// <param name="path">动画路径</param>
    public static async Task<FrameAnimationRes> LoadFrameAnimationResAsync(string path) {
        if (loadedMap.TryGetValue(path, out FrameAnimationRes data)) {
            return data;
        }
        if (loadingMap.TryGetValue(path, out Task<FrameAnimationRes> pending)) {
            return await pending;
        }
        Task<FrameAnimationRes> task = LoadResourceAsync(path);
        loadingMap.Add(path, task);
        try {
            data = await task;
            loadedMap.Add(path, data);
            return data;
        } finally {
            loadingMap.Remove(path);
        }
    }

    /// <summary>
    /// 加载源图集并还原动画帧，结束后释放源资源。
    /// </summary>
    /// <param name="path">动画路径</param>
    /// <remarks>还原后的纹理由共享缓存持有，播放实例回收时不销毁它们。</remarks>
    private static async Task<FrameAnimationRes> LoadResourceAsync(string path) {
        try {
            Task textureTask = ResourceManager.LoadAsync(new ResLoadInfo(path + ".png", ResType.UnpackImage));
            Task jsonTask = ResourceManager.LoadAsync(new ResLoadInfo(path, ResType.Json));
            await Task.WhenAll(textureTask, jsonTask);
            JSONNode jsonNode = ResourceManager.GetJsonNode(path);
            Sprite sprite = ResourceManager.GetUnpackImage(path + ".png");
            if (jsonNode == null || sprite == null || sprite.texture == null) {
                throw new InvalidOperationException("帧动画资源缺失：" + path);
            }
            return await GenerateAsync(jsonNode, sprite, path.Replace(ResourceConst.PATH_FRAME_ANIMATION, ""));
        } finally {
            ResourceManager.Release(path);
            ResourceManager.Release(path + ".png");
        }
    }

    /// <summary>
    /// 生成完整的动画帧与播放时长。
    /// </summary>
    /// <param name="json">帧动画配置。</param>
    /// <param name="sprite">可读取像素的源图集。</param>
    /// <param name="animationName">用于定位异常的动画名称。</param>
    /// <remarks>等待主线程实际完成全部裁帧，预加载才可结束并释放源图集。</remarks>
    public static async Task<FrameAnimationRes> GenerateAsync(JSONNode json, Sprite sprite, string animationName) {
        FrameAnimationRes data = new FrameAnimationRes();
        JSONArray frames = json["frames"] as JSONArray;
        if (frames == null || frames.Count == 0) {
            throw new InvalidOperationException($"帧动画资源缺少有效帧：{animationName}");
        }
        data.sprites = new Sprite[frames.Count];
        data.durations = new int[frames.Count];
        data.totalDuration = 0;
        JSONNode pivot = json["pivot"];
        if (pivot == null || pivot["x"] == null || pivot["y"] == null) {
            throw new InvalidOperationException($"帧动画资源缺少 pivot 配置：{animationName}");
        }

        if (!float.TryParse(pivot["x"], out float pivotX) || !float.TryParse(pivot["y"], out float pivotY)) {
            throw new InvalidOperationException($"帧动画资源 pivot 格式错误：{animationName}");
        }

        data.pivot = new Vector2(pivotX, pivotY);

        for (int i = 0; i < frames.Count; i++) {
            JSONNode duration = frames[i]["duration"];
            if (duration == null || !int.TryParse(duration, out int frameDuration) || frameDuration <= 0) {
                throw new InvalidOperationException($"帧动画资源 duration 缺失或格式错误：{animationName}，frame={i}");
            }
            data.durations[i] = frameDuration;
            data.totalDuration = checked(data.totalDuration + frameDuration);
        }

        TaskCompletionSource<FrameAnimationRes> completion = new TaskCompletionSource<FrameAnimationRes>();
        UnityMainThreadDispatcher.Instance.Enqueue(async () => {
            List<Texture2D> generatedTextures = new List<Texture2D>();
            try {
                // 图集只读取一次；Color32 避免为每帧分配浮点颜色数组。
                await WaitForGenerationBudgetAsync();
                Color32[] sourcePixels = sprite.texture.GetPixels32();
                int totalWidth = sprite.texture.width;
                int totalHeight = sprite.texture.height;
                for (int index = 0; index < frames.Count; index++) {
                    await WaitForGenerationBudgetAsync();
                    JSONNode singleFrame = frames[index];
                    JSONNode frame = singleFrame["frame"];
                    JSONNode spriteSourceSize = singleFrame["spriteSourceSize"];

                    int originTexWidth = singleFrame["sourceSize"]["w"];
                    int originTexHeight = singleFrame["sourceSize"]["h"];

                    int frameW = frame["w"];
                    int frameH = frame["h"];
                    int posX = frame["x"];
                    int posY = totalHeight - frame["y"] - frameH;

                    Texture2D targetTex = new Texture2D(originTexWidth, originTexHeight, TextureFormat.RGBA32, false);
                    generatedTextures.Add(targetTex);
                    Color32[] targetPixels = new Color32[originTexWidth * originTexHeight];

                    // 还原导出前的透明边界，使各帧共用配置锚点。
                    int transOriginX = spriteSourceSize["x"];
                    int transOriginY = originTexHeight - (spriteSourceSize["y"] + frameH);
                    if (frameW <= 0 || frameH <= 0 || posX < 0 || posY < 0 || posX + frameW > totalWidth || posY + frameH > totalHeight || transOriginX < 0 || transOriginY < 0 || transOriginX + frameW > originTexWidth || transOriginY + frameH > originTexHeight) {
                        throw new InvalidOperationException($"帧动画裁剪范围无效：{animationName}，frame={index}");
                    }
                    for (int row = 0; row < frameH; row++) {
                        Array.Copy(sourcePixels, (posY + row) * totalWidth + posX, targetPixels, (transOriginY + row) * originTexWidth + transOriginX, frameW);
                    }
                    targetTex.SetPixels32(targetPixels);

                    Sprite sp = Sprite.Create(targetTex, new Rect(0, 0, targetTex.width, targetTex.height), data.pivot);
                    sp.name = animationName + "_" + index;
                    data.sprites[index] = sp;
                    // Sprite 创建完成后释放 CPU 副本，保留 GPU 纹理供所有播放实例共享。
                    targetTex.Apply(false, true);
                }
                completion.SetResult(data);
            } catch (Exception exception) {
                foreach (Sprite generatedSprite in data.sprites) {
                    if (generatedSprite != null) {
                        Destroy(generatedSprite);
                    }
                }
                foreach (Texture2D generatedTexture in generatedTextures) {
                    Destroy(generatedTexture);
                }
                completion.SetException(exception);
            }
        });
        return await completion.Task;
    }

    /// <summary>
    /// 裁帧超出本帧预算时等待下一渲染帧，保留主线程执行 Unity API。
    /// </summary>
    private static async Task WaitForGenerationBudgetAsync() {
        while (true) {
            if (generationFrame != Time.frameCount) {
                generationFrame = Time.frameCount;
                generationWatch.Restart();
            }
            if (generationWatch.Elapsed.TotalMilliseconds < GENERATION_BUDGET_MS) {
                return;
            }
            var nextFrame = new TaskCompletionSource<bool>();
            UnityMainThreadDispatcher.Instance.StartCoroutine(CompleteOnNextFrame(nextFrame));
            await nextFrame.Task;
        }
    }

    /**通过协程确保让出当前帧，避免队列在同一 Update 内重新执行*/
    private static IEnumerator CompleteOnNextFrame(TaskCompletionSource<bool> completion) {
        yield return null;
        completion.SetResult(true);
    }

}
