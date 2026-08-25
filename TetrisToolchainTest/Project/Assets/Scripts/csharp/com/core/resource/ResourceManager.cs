using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 资源加载入口类，只负责：
/// 1.发起资源加载请求，实际上不负责加载；
/// 2.加载完成回调的绑定；
/// 3.管理已加载的资源及卸载资源；
/// 
/// 资源加载走ResourceLoader
/// </summary>
public class ResourceManager {

    /**已加载的所有预制map<预制路径，对应的go>*/
    private static readonly Dictionary<string, GameObject> loadedPrefabMap = new();
    /**已加载的所有图集map<图集名，图集map<图片名，对应的Sprite>>*/
    private static readonly Dictionary<string, Dictionary<string, Sprite>> loadedAtlasMap = new();
    /**已加载的所有散图map<散图路径，对应的Sprite>*/
    private static readonly Dictionary<string, Sprite> loadedUnpackImgMap = new();
    /**已加载的材质map<材质路径，材质>*/
    private static readonly Dictionary<string, Material> loadedMaterialMap = new();
    /**已加载的音效map<路径，音效>*/
    private static readonly Dictionary<string, AudioClip> loadedAudioMap = new();
    /**已加载的所有Json对象map<路径，对应的JSON对象>*/
    private static readonly Dictionary<string, JSONNode> loadedJsonMap = new();

    private static readonly Dictionary<string, Task<GameObject>> loadingPrefabTasks = new();
    private static readonly Dictionary<string, Task<Sprite[]>> loadingAtlasTasks = new();
    private static readonly Dictionary<string, Task<Sprite>> loadingUnpackImgTasks = new();
    private static readonly Dictionary<string, Task<Material>> loadingMaterialTasks = new();
    private static readonly Dictionary<string, Task<AudioClip>> loadingAudioTasks = new();
    private static readonly Dictionary<string, Task<TextAsset>> loadingJsonTasks = new();

    /**已加载资源的句柄map<路径，对应的handle>*/
    private static readonly Dictionary<string, AsyncOperationHandle> loadedHandleMap = new();

    /**异步资源加载*/
    public static async Task LoadAsync(ResLoadInfo info) {
        switch (info.resType) {
            case ResType.Prefab:
                // 若已加载则跳过
                if (loadedPrefabMap.ContainsKey(info.path))
                    break;

                // 若正在加载，则等待 loadingTask
                if (loadingPrefabTasks.TryGetValue(info.path, out var loadingPrefab)) {
                    await loadingPrefab;
                    break;
                }

                // 开始加载
                var handlePrefab = Addressables.LoadAssetAsync<GameObject>(info.path);
                var taskPrefab = handlePrefab.Task;
                loadingPrefabTasks[info.path] = taskPrefab;

                try {
                    var prefab = await taskPrefab;

                    // 加载失败
                    if (handlePrefab.Status == AsyncOperationStatus.Failed) {
                        Debug.LogError($"加载失败: {info.path}");
                        break;
                    }

                    // 加入句柄与资源缓存
                    loadedHandleMap[info.path] = handlePrefab;
                    loadedPrefabMap[info.path] = prefab;
                } finally {
                    // 无论成功失败都要移除 loading 状态
                    loadingPrefabTasks.Remove(info.path);
                }
                break;
            case ResType.Atlas:
                // 若已加载则跳过
                if (loadedAtlasMap.ContainsKey(info.path))
                    break;

                // 若正在加载，则等待
                if (loadingAtlasTasks.TryGetValue(info.path, out var loadingAtlas)) {
                    await loadingAtlas;
                    break;
                }

                // 开始加载图集（png 文件）
                var handleAtlas = Addressables.LoadAssetAsync<Sprite[]>(info.path + ".png");
                var taskAtlas = handleAtlas.Task;
                loadingAtlasTasks[info.path] = taskAtlas;

                try {
                    var sprites = await taskAtlas;

                    // 加载失败
                    if (handleAtlas.Status == AsyncOperationStatus.Failed) {
                        Debug.LogError($"加载失败: {info.path}");
                        break;
                    }

                    // 生成图集 map
                    var spriteMap = new Dictionary<string, Sprite>();
                    foreach (var sp in sprites)
                        spriteMap[sp.name] = sp;

                    loadedHandleMap[info.path] = handleAtlas;
                    loadedAtlasMap[info.path] = spriteMap;
                } finally {
                    loadingAtlasTasks.Remove(info.path);
                }
                break;
            case ResType.UnpackImage:
                // 若已加载则跳过
                if (loadedUnpackImgMap.ContainsKey(info.path))
                    break;

                // 若正在加载，则等待
                if (loadingUnpackImgTasks.TryGetValue(info.path, out var loadingImg)) {
                    await loadingImg;
                    break;
                }

                // 开始加载散图
                var handleImg = Addressables.LoadAssetAsync<Sprite>(info.path);
                var taskImg = handleImg.Task;
                loadingUnpackImgTasks[info.path] = taskImg;

                try {
                    var sp = await taskImg;

                    // 加载失败
                    if (handleImg.Status == AsyncOperationStatus.Failed) {
                        Debug.LogError($"加载失败: {info.path}");
                        break;
                    }

                    loadedHandleMap[info.path] = handleImg;
                    loadedUnpackImgMap[info.path] = sp;
                } finally {
                    loadingUnpackImgTasks.Remove(info.path);
                }
                break;
            case ResType.Material:
                // 若已加载则跳过
                if (loadedMaterialMap.ContainsKey(info.path))
                    break;

                // 若正在加载，则等待
                if (loadingMaterialTasks.TryGetValue(info.path, out var loadingMat)) {
                    await loadingMat;
                    break;
                }

                // 加载材质
                var handleMat = Addressables.LoadAssetAsync<Material>(info.path + ".mat");
                var taskMat = handleMat.Task;
                loadingMaterialTasks[info.path] = taskMat;

                try {
                    var mat = await taskMat;

                    // 加载失败
                    if (handleMat.Status == AsyncOperationStatus.Failed) {
                        Debug.LogError($"加载失败: {info.path}");
                        break;
                    }

                    loadedHandleMap[info.path] = handleMat;
                    loadedMaterialMap[info.path] = mat;
                } finally {
                    loadingMaterialTasks.Remove(info.path);
                }
                break;
            case ResType.Audio:
                // 若已加载则跳过
                if (loadedAudioMap.ContainsKey(info.path))
                    break;

                // 若正在加载，则等待
                if (loadingAudioTasks.TryGetValue(info.path, out var loadingAudio)) {
                    await loadingAudio;
                    break;
                }

                // 加载音效
                var handleAudio = Addressables.LoadAssetAsync<AudioClip>(info.path);
                var taskAudio = handleAudio.Task;
                loadingAudioTasks[info.path] = taskAudio;

                try {
                    var clip = await taskAudio;

                    // 加载失败
                    if (handleAudio.Status == AsyncOperationStatus.Failed) {
                        Debug.LogError($"加载失败: {info.path}");
                        break;
                    }

                    loadedHandleMap[info.path] = handleAudio;
                    loadedAudioMap[info.path] = clip;
                } finally {
                    loadingAudioTasks.Remove(info.path);
                }
                break;
            case ResType.Json:
                // 若已加载则跳过
                if (loadedJsonMap.ContainsKey(info.path))
                    break;

                // 若正在加载，则等待
                if (loadingJsonTasks.TryGetValue(info.path, out var loadingJson)) {
                    await loadingJson;
                    break;
                }

                // 加载 json 文本
                var handleJson = Addressables.LoadAssetAsync<TextAsset>(info.path + ".json");
                var taskJson = handleJson.Task;
                loadingJsonTasks[info.path] = taskJson;

                try {
                    var txt = await taskJson;

                    // 加载失败
                    if (handleJson.Status == AsyncOperationStatus.Failed) {
                        Debug.LogError($"加载失败: {info.path}");
                        break;
                    }

                    loadedHandleMap[info.path] = handleJson;
                    loadedJsonMap[info.path] = JSON.Parse(txt.text);
                } finally {
                    loadingJsonTasks.Remove(info.path);
                }
                break;
            default:
                Debug.LogError($"未支持的 ResType: {info.resType}");
                break;
        }
    }

    /**加载散图*/
    public static async void LoadUnpackImage(string path, Action action) {
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path, ResType.UnpackImage) }, action);
    }

    /**加载预制*/
    public static async void LoadPrefab(string prefabPath, Action action) {
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(prefabPath, ResType.Prefab) }, action);
    }

    /**加载音效*/
    public static async void LoadAudio(string path, Action action) {
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path, ResType.Audio) }, action);
    }

    /**********************************************************获取接口 开始**********************************************************/
    /**获取已加载的预制*/
    public static GameObject GetLoadedPrefab(string path) {
        loadedPrefabMap.TryGetValue(path, out GameObject go);
        return go;
    }

    /**获取图集中的图片*/
    public static Sprite GetAtlasSprite(string atlasName, string spriteName) {
        Dictionary<string, Sprite> atlasMap;
        loadedAtlasMap.TryGetValue(atlasName, out atlasMap);
        if (atlasMap != null) {
            Sprite spriteItem;
            atlasMap.TryGetValue(spriteName, out spriteItem);
            if (spriteItem != null) {
                return spriteItem;
            }
            Debug.LogWarning(string.Format("加载错误：已加载的图集{0}中，不存在图片{1}，请检查！", atlasName, spriteName));
        } else {
            Debug.LogWarning(string.Format("加载错误：图片{0}所属的图集{1}不存在，请检查！", spriteName, atlasName));
        }
        return null;
    }

    /**获取散图*/
    public static Sprite GetUnpackImage(string unpackImgName) {
        loadedUnpackImgMap.TryGetValue(unpackImgName, out Sprite tex);
        return tex;
    }

    /**获取json*/
    public static JSONNode GetJsonNode(string path) {
        loadedJsonMap.TryGetValue(path, out JSONNode result);
        return result;
    }

    /**获取材质*/
    public static Material GetMaterial(string matPath) {
        loadedMaterialMap.TryGetValue(matPath, out Material mat);
        return mat;
    }

    /**获取音效*/
    public static AudioClip GetAudioClip(string path) {
        loadedAudioMap.TryGetValue(path, out AudioClip clip);
        return clip;
    }
    /**********************************************************获取接口 结束**********************************************************/

    /**是否已加载某图集*/
    public static bool HasLoadedAtlas(string atlasName) {
        return loadedAtlasMap.ContainsKey(atlasName);
    }

    /**是否已加载某散图*/
    public static bool HasLoadedUnpackImage(string unpackImgName) {
        return loadedUnpackImgMap.ContainsKey(unpackImgName);
    }

    /**********************************************************释放接口 开始**********************************************************/

    /**释放资源*/
    public static void Release(string path) {
        if (loadedHandleMap.TryGetValue(path, out var handle)) {
            Addressables.Release(handle);
            loadedHandleMap.Remove(path);
        }

        // 同时把已加载资源从 Map 移除
        loadedPrefabMap.Remove(path);
        loadedAtlasMap.Remove(path);
        loadedUnpackImgMap.Remove(path);
        loadedMaterialMap.Remove(path);
        loadedAudioMap.Remove(path);
        loadedJsonMap.Remove(path);
        //Resources.UnloadUnusedAssets();
    }

    /**********************************************************释放接口 结束**********************************************************/

}
