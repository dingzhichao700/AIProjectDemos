using Luban;
using SimpleJSON;

namespace cfg
{
    /// <summary>
    /// Title baseline：仅加载设置选项表。
    /// </summary>
    public partial class Tables
    {
        public cfgObj.SettingOptionObj SettingOptionObj { get; }

        public Tables(System.Func<string, JSONNode> loader)
        {
            SettingOptionObj = new cfgObj.SettingOptionObj(loader("cfgobj_settingoptionobj"));
            SettingOptionObj.ResolveRef(this);
        }
    }
}
