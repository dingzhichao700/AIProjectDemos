using cfg;
using SimpleJSON;
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Title baseline：仅加载设置选项配置。
/// </summary>
public static class CfgManager
{
    static Tables _tables;

    public static Tables tables
    {
        get
        {
            if (_tables == null)
            {
                _tables = new Tables(LoadJson);
            }

            return _tables;
        }
    }

    public static void Clear()
    {
        _tables = null;
        foreach (string cfgName in ResourceConst.ALL_CONFIG_LIST)
        {
            ResourceManager.Release(ResourceConst.PATH_CONFIG + cfgName);
        }
    }

    static JSONNode LoadJson(string file)
    {
        string path = $"{ResourceConst.PATH_CONFIG}{file}.json";
        return JSON.Parse(File.ReadAllText(path));
    }
}
