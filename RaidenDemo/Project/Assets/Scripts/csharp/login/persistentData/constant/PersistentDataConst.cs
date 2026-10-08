using UnityEngine;

/// <summary>
/// 持久化数据常量
/// </summary>
public class PersistentDataConst {

    /**持久化路径*/
    public static string PERSISTENT_PATH = Application.persistentDataPath;

    /**用户设置*/
    public static string USER_SETTING = PERSISTENT_PATH + "/userSetting";

    /**本地设置*/
    public static string LOCAL_DATA = PERSISTENT_PATH + "/localData";

    /**存档根目录*/
    public static string SAVE_PATH = PERSISTENT_PATH + "/save";
    /**存档名称前缀*/
    public static string SAVE_NAME_PREFIX = "save";
    /**存档上限数量*/
    public static int SAVE_MAX = 3;

}
