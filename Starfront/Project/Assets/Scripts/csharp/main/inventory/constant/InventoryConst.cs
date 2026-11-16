/// <summary>
/// 背包业务的配置常量入口。
/// </summary>
/// <remarks>容量通过正式常量表调整，不在GM中维护另一套上限。</remarks>
public static class InventoryConst {

    /**背包总格数，1200为背包业务号段首项。*/
    public static int SlotCapacity => ConfigValueHelper.GetInt(1200);

}
