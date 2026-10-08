using cfg;
using cfg.resource;
using System.Collections.Generic;

public class SettingOptionCfgMgr {

    /// <summary>
    /// 获取指定类型的设置配置列表
    /// </summary>
    /// <param name="type">设置类型</param>
    /// <returns></returns>
    public static List<SettingOptionResource> GetCfgByType(SettingType type) {
        List<SettingOptionResource> cfgs = new List<SettingOptionResource>();
        foreach (KeyValuePair<int, SettingOptionResource> pair in CfgManager.tables.SettingOptionObj.DataMap) {
            SettingOptionResource cfg = pair.Value;
            if (cfg.Type == type) {
                cfgs.Add(cfg);
            }
        }
        return cfgs;
    }
}
