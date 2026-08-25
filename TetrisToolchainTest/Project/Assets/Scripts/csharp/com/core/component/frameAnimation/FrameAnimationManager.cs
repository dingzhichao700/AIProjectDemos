
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
        FrameAnimationView item;
        if (pool != null && pool.Count > 0) {
            item = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        } else {
            GameObject go = new GameObject("FrameAnimation" + createIndex, typeof(RectTransform));
            createIndex++;
            item = go.AddComponent<FrameAnimationView>();
        }
        return item;
    }

    /**归还实例到池*/
    public static void RecoverItem(FrameAnimationView item) {
        if (pool == null) {
            pool = new List<FrameAnimationView>();
        }
        item.Clear();
        pool.Add(item);
        item.transform.SetParent(PanelMgr.ins.uiPool);
        if (pool.Count > POOL_MAX) {
            Debug.LogWarning("警告！帧动画池的实例数超过上限数量" + POOL_MAX);
        }
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
    /// 生成动画数据
    /// </summary>
    /// <param name="json">json数据</param>
    /// <param name="sprite">精灵图集</param>
    /// <param name="animationName">动画名称（为了方便定位异常，）</param>
    /// <returns></returns>
    public static async Task<FrameAnimationRes> GenerateAsync(JSONNode json, Sprite sprite, string animationName) {
        int totalHeight = sprite.texture.height;
        FrameAnimationRes data = new FrameAnimationRes();
        JSONArray frames = json["frames"] as JSONArray;
        data.sprites = new Sprite[frames.Count];
        data.durations = new int[frames.Count];
        data.totalDuration = 0;
        JSONNode pivot = json["pivot"];
        data.pivot = new Vector2(float.Parse(pivot["x"]), float.Parse(pivot["y"]));

        List<Task> tasks = new List<Task>();

        for (int i = 0; i < frames.Count; i++) {
            int index = i;

            // 异步处理每一帧，非图形部分可以在子线程执行
            tasks.Add(Task.Run(() => {
                JSONNode singleFrame = frames[index];
                JSONNode frame = singleFrame["frame"];
                JSONNode spriteSourceSize = singleFrame["spriteSourceSize"];
                data.durations[index] = singleFrame["duration"];
                data.totalDuration += data.durations[index];

                int originTexWidht = singleFrame["sourceSize"]["w"];
                int originTexHeight = singleFrame["sourceSize"]["h"];

                int frameW = frame["w"];
                int frameH = frame["h"];
                int posX = frame["x"];
                int posY = totalHeight - frame["y"] - frameH;

                // 非图形操作：计算裁剪区域
                // 此部分可以在子线程中进行
                // （这里是计算而非直接操作图形数据）
            }));

            // 将图形操作（GetPixels, Sprite.Create）回调到主线程
            tasks.Add(Task.Run(() => {
                // 图形操作回到主线程执行
                UnityMainThreadDispatcher.Instance.Enqueue(() => {
                    JSONNode singleFrame = frames[index];
                    JSONNode frame = singleFrame["frame"];
                    JSONNode spriteSourceSize = singleFrame["spriteSourceSize"];

                    int originTexWidht = singleFrame["sourceSize"]["w"];
                    int originTexHeight = singleFrame["sourceSize"]["h"];

                    int frameW = frame["w"];
                    int frameH = frame["h"];
                    int posX = frame["x"];
                    int posY = totalHeight - frame["y"] - frameH;

                    // 获取裁剪区域的颜色数据
                    Color[] colors = sprite.texture.GetPixels(posX, posY, frameW, frameH);

                    // 新建纹理对象作为目标纹理，宽高和原图保持一致
                    Texture2D targetTex = new Texture2D(originTexWidht, originTexHeight);
                    Color[] transPixels = new Color[targetTex.width * targetTex.height];
                    for (int j = 0; j < transPixels.Length; j++) { transPixels[j] = Color.clear; }
                    targetTex.SetPixels(transPixels);

                    // 计算转换后的Y值
                    int transOriginY = targetTex.height - (spriteSourceSize["y"] + frameH);
                    targetTex.SetPixels(spriteSourceSize["x"], transOriginY, frameW, frameH, colors);
                    targetTex.Apply();

                    // 创建Sprite
                    Sprite sp = Sprite.Create(targetTex, new Rect(0, 0, targetTex.width, targetTex.height), data.pivot);
                    sp.name = animationName + "_" + index;
                    data.sprites[index] = sp;
                });
            }));
        }

        // 等待所有任务完成
        await Task.WhenAll(tasks);
        return data;
    }

}
