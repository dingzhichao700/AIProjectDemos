using cfg;
using SimpleJSON;
using System;

/// <summary>
/// 统一管理由 Addressables 预加载的 Luban JSON 配置。
/// </summary>
public static class CfgManager {
    static Tables _tables;

    public static Tables tables {
        get {
            if (_tables == null) {
                throw new InvalidOperationException("CfgManager 尚未初始化");
            }

            return _tables;
        }
    }

    /**使用已预加载的 JSON 初始化 Luban 配置表*/
    public static void Init() {
        _tables = new Tables(LoadJson);
    }

    public static void Clear() {
        _tables = null;
        foreach (string cfgName in ResourceConst.ALL_CONFIG_LIST) {
            ResourceManager.Release(ResourceConst.PATH_CONFIG + cfgName);
        }
    }

    static JSONNode LoadJson(string file) {
        string path = ResourceConst.PATH_CONFIG + file;
        JSONNode json = ResourceManager.GetJsonNode(path);
        if (json == null) {
            throw new InvalidOperationException($"Luban 配置尚未通过 Addressables 预加载：{path}.json");
        }

        return json;
    }
}
