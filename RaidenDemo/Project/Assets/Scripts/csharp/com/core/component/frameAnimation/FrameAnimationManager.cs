using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class FrameAnimationManager : MonoBehaviour {

    /**加载完成的动画数据字典<路径,帧动画信息>*/
    public static Dictionary<string, FrameAnimationRes> loadedMap = new Dictionary<string, FrameAnimationRes>();
    /**加载中的动画数据字典<路径,加载完成回调>*/
    private static Dictionary<string, List<Action<FrameAnimationRes>>> loadingMap = new Dictionary<string, List<Action<FrameAnimationRes>>>();
    /**帧动画对象池*/
    private static List<FrameAnimationView> pool;

    private static int createIndex = 0;

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
        if (loadedMap.ContainsKey(path)) {
            //已加载完成
            loadedMap.TryGetValue(path, out FrameAnimationRes data);
            action(data);
        } else {
            //未加载完成
            loadingMap.TryGetValue(path, out List<Action<FrameAnimationRes>> handlers);
            bool isLoading = false;
            if (handlers == null) {
                handlers = new List<Action<FrameAnimationRes>>();
                loadingMap.Add(path, handlers);
            } else {
                isLoading = true;
            }
            handlers.Add(action);
            if (!isLoading) {
                //ResourceManager.LoadJson(path + ".json", OnLoadJsonComplete);
                await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path, ResType.FrameAnim) }, async () => {
                    await OnLoadAnimResComplete(path);
                    Debug.Log("加载动画完成：" + path);
                });
                //RookieEngine.PrintLog("加载动画：" + path, EngineLogType.LOAD_INFO);
                Debug.Log("加载动画：" + path);
            }
        }
    }

    /**加载动画资源完成*/
    public static async Task OnLoadAnimResComplete(string animationName) {
        JSONNode jsonNode = ResourceManager.GetJsonNode(animationName);
        Sprite sprite = ResourceManager.GetUnpackImage(animationName + ".png");
        if (jsonNode == null || sprite == null || sprite.texture == null)
        {
            Debug.LogError($"帧动画资源缺失: {animationName} (json={jsonNode != null}, sprite={sprite != null})");
            loadingMap.Remove(animationName);
            return;
        }

        if (!loadedMap.ContainsKey(animationName)) {
            // 生成动画数据的异步操作
            FrameAnimationRes data = await GenerateAsync(jsonNode, sprite, animationName.Replace(ResourceConst.PATH_FRAME_ANIMATION, ""));

            Debug.Log("动画资源处理完成：" + animationName);

            // 在卸载资源之前，确保数据已经准备好
            ResourceManager.Release(animationName); // 卸载json
            ResourceManager.Release(animationName + ".png"); // 卸载图片

            // 将处理完成的数据添加到 loadedMap 中
            loadedMap.Add(animationName, data);

            // 获取并执行所有与该动画相关的回调
            List<Action<FrameAnimationRes>> complateHandlers;
            if (loadingMap.TryGetValue(animationName, out complateHandlers) && complateHandlers != null) {
                // 确保回调在主线程中执行，避免在异步线程中操作 Unity 的图形资源
                foreach (var handler in complateHandlers) {
                    // 调用回调方法，并确保它们在主线程执行
                    UnityMainThreadDispatcher.Instance.Enqueue(() => handler(data));
                }
            }
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
        UnityMainThreadDispatcher.Instance.Enqueue(() => {
            List<Texture2D> generatedTextures = new List<Texture2D>();
            try {
                int totalHeight = sprite.texture.height;
                for (int index = 0; index < frames.Count; index++) {
                    JSONNode singleFrame = frames[index];
                    JSONNode frame = singleFrame["frame"];
                    JSONNode spriteSourceSize = singleFrame["spriteSourceSize"];

                    int originTexWidth = singleFrame["sourceSize"]["w"];
                    int originTexHeight = singleFrame["sourceSize"]["h"];

                    int frameW = frame["w"];
                    int frameH = frame["h"];
                    int posX = frame["x"];
                    int posY = totalHeight - frame["y"] - frameH;

                    Color[] colors = sprite.texture.GetPixels(posX, posY, frameW, frameH);
                    Texture2D targetTex = new Texture2D(originTexWidth, originTexHeight);
                    generatedTextures.Add(targetTex);
                    Color[] transPixels = new Color[targetTex.width * targetTex.height];
                    for (int j = 0; j < transPixels.Length; j++) {
                        transPixels[j] = Color.clear;
                    }
                    targetTex.SetPixels(transPixels);

                    // 还原导出前的透明边界，使各帧共用配置锚点。
                    int transOriginY = targetTex.height - (spriteSourceSize["y"] + frameH);
                    targetTex.SetPixels(spriteSourceSize["x"], transOriginY, frameW, frameH, colors);
                    targetTex.Apply();

                    Sprite sp = Sprite.Create(targetTex, new Rect(0, 0, targetTex.width, targetTex.height), data.pivot);
                    sp.name = animationName + "_" + index;
                    data.sprites[index] = sp;
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

}
