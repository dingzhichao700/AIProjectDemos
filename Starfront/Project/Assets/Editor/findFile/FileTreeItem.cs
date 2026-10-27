using UnityEditor.IMGUI.Controls;

public class FileTreeItem<T> : TreeViewItem
{
    public T V_Data;

    /// <summary>
    /// 是否是父节点
    /// </summary>
    public bool m_isParentNode;

    /// <summary>
    /// 保存原有的深度值
    /// </summary>
    private int _depth = 0;

    public FileTreeItem() : base()
    {
    }

    public FileTreeItem(int id, int depth, string displayName, T data = default(T), bool isParentNode = true) : base(id, depth, displayName)
    {
        V_Data = data;
        m_isParentNode = isParentNode;
        _depth = depth;
    }

    public void SetDepthToTop()
    {
        depth = 1;
    }

    /// <summary>
    /// 恢复深度值
    /// </summary>
    public void RevertDepth()
    {
        depth = _depth;
    }

    public FileTreeItem<T> F_AddChild(int id, string displayName, T data = default, bool isParent = false)
    {
        FileTreeItem<T> item = new FileTreeItem<T>(id, depth + 1, displayName, data, isParent);
        AddChild(item);
        return item;
    }

    /// <summary>
    /// 是否父节点
    /// </summary>
    public bool V_IsParentNode
    {
        get
        {
            return m_isParentNode;
        }
        set
        {
            m_isParentNode = value;
        }
    }

}
