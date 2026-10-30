using System;
using System.Collections.ObjectModel;

/// <summary>Login 热更新模块拥有的配置预加载清单，与 Luban Tables 和数据同版本发布。</summary>
public static class ConfigManifest
{
    /// <summary>无扩展名配置名；加载和释放共用，调用方不可修改。</summary>
    public static ReadOnlyCollection<string> Names { get; } = Array.AsReadOnly(new string[]
    {
        "cfgobj_gmobj",
        "cfgobj_settingoptionobj",
        "cfgobj_configvalueobj",
        "cfgobj_itemobj",
        "cfgobj_functionobj",
        "cfgobj_battlesceneobj",
        "cfgobj_battlescenecellobj",
        "cfgobj_cardobj",
        "cfgobj_buildingobj",
        "cfgobj_unitobj",
        "cfgobj_unitvisualobj",
        "cfgobj_skillobj",
        "cfgobj_projectileobj",
        "cfgobj_unitproductionobj",
        "cfgobj_cardlevelobj",
        "cfgobj_battlestartobj",
        "cfgobj_battleaiprofileobj",
        "cfgobj_battledeckobj",
        "cfgobj_currencyproductionobj",
        "cfgobj_cellunlockobj",
    });
}
