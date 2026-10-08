/// <summary>
/// 属性容器事件。
/// </summary>
public static class AttributeContainerEvent {

    /**容器中指定类型的属性汇总值发生变化*/
    public const string ATTR_CHANGED = "ATTRIBUTE_CONTAINER_ATTR_CHANGED";

    /**容器的子级结构发生变化，需要上级重新汇总整棵子树*/
    public const string CHILD_CHANGED = "ATTRIBUTE_CONTAINER_CHILD_CHANGED";

    /**容器完成一次全部属性汇总计算*/
    public const string RECALCULATED = "ATTRIBUTE_CONTAINER_RECALCULATED";

}
