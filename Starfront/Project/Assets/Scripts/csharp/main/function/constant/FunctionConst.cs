using System.IO;
using UnityEngine;

/// <summary>
/// 功能入口与 FunctionResource 的固定关联。
/// </summary>
/// <remarks>仅声明入口身份；开启条件和名称均从表读取。</remarks>
public static class FunctionConst {

    public const int Shop = 1;

    public const int Deck = 2;

    public const int Battle = 3;

    public const int Territory = 4;

    public const int Ranking = 5;

    public const string UnlockChanged = "Function.UnlockChanged";

    /**单机共用档；未来多角色存档需由角色存储边界提供路径。*/
    public static string SavePath => Path.Combine(Application.persistentDataPath, "functionUnlocks.json");

}
