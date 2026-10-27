using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;

public class FileTreeView<T> : TreeView
{

    private FileTreeItem<T> m_rootItem = null;

    /// <summary>
    /// 单击选择文件回调
    /// </summary>
    private Action<T> singleClickHandler;

    /// <summary>
    /// 双击选择文件回调
    /// </summary>
    private Action<T> doubleClickHandler;

    /// <summary>
    /// 当前搜索关键字
    /// </summary>
    private string m_CurSearchStr = string.Empty;

    /// <summary>
    /// 存储所有的节点，包括目录
    /// </summary>
    public Dictionary<int, FileTreeItem<T>> m_DicAllItems = new Dictionary<int, FileTreeItem<T>>();

    /// <summary>
    /// 存储最顶端节点
    /// </summary>
    public List<TreeViewItem> m_RootShowItemList = new List<TreeViewItem>();

    /// <summary>
    /// 如果搜索，保存显示搜索的节点
    /// </summary>
    public List<TreeViewItem> m_FilterSearchList = new List<TreeViewItem>();

    /// <summary>
    /// 最终需要显示的节点列表
    /// </summary>
    public List<TreeViewItem> m_ShowItemLists = new List<TreeViewItem>();


    private int m_ItemId = -1;

    public FileTreeView(TreeViewState state) : base(state)
    {
        m_ShowItemLists = m_RootShowItemList;
        Reload();
    }

    public int V_ItemID
    {
        get
        {
            return m_ItemId++;
        }
    }

    protected override TreeViewItem BuildRoot()
    {
        m_rootItem = new FileTreeItem<T>(V_ItemID, -1, "文件搜索根节点");
        SetupParentsAndChildrenFromDepths(m_rootItem, m_ShowItemLists);
        return m_rootItem;
    }

    /// <summary>
    /// 监听单击节点
    /// </summary>
    /// <param name="action"></param>
    public void SetSingleClickListener(Action<T> action)
    {
        singleClickHandler = action;
    }

    protected override void SingleClickedItem(int id)
    {
        base.SingleClickedItem(id);
        FileTreeItem<T> clickItem = m_DicAllItems[id];
        if (singleClickHandler != null)
        {
            singleClickHandler(clickItem.V_Data);
        }
    }

    /// <summary>
    /// 监听双击节点
    /// </summary>
    /// <param name="action"></param>
    public void SetDoubleClickListener(Action<T> action)
    {
        doubleClickHandler = action;
    }

    protected override void DoubleClickedItem(int id) {
        base.DoubleClickedItem(id);
        FileTreeItem<T> clickItem = m_DicAllItems[id];
        if (doubleClickHandler != null) {
            doubleClickHandler(clickItem.V_Data);
        }
    }

    /// <summary>
    /// 增加一个节点到某节点
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="data"></param>
    /// <param name="displayName"></param>
    /// <param name="isParentNode"></param>
    /// <param name="isTopFile"></param>
    /// <returns></returns>
    public FileTreeItem<T> AddChild(FileTreeItem<T> parent, T data, string displayName, bool isParentNode = false, bool isTopFile = false)
    {
        if (parent == null)
        {
            parent = m_rootItem;
        }
        FileTreeItem<T> childItem = parent.F_AddChild(V_ItemID, displayName, data);
        childItem.V_IsParentNode = isParentNode;
        if (isTopFile)
        {
            m_RootShowItemList.Add(childItem);
        }
        m_DicAllItems.Add(childItem.id, childItem);
        return childItem;
    }

    /// <summary>
    /// 设置搜索关键字
    /// </summary>
    /// <param name="searchString"></param>
    public void SetSearch(string searchString)
    {
        if (string.IsNullOrEmpty(searchString) && !string.IsNullOrEmpty(m_CurSearchStr))
        {
            m_CurSearchStr = searchString;
            foreach (var item in m_FilterSearchList)
            {
                (item as FileTreeItem<T>).RevertDepth();
            }
            m_FilterSearchList.Clear();
            m_ShowItemLists = m_RootShowItemList;
            Reload();
            return;
        }

        if (m_CurSearchStr == searchString.ToLower())
        {
            return;
        }
        m_CurSearchStr = searchString.ToLower();
        m_FilterSearchList.Clear();
        foreach (var item in m_DicAllItems.Values)
        {
            if (item.V_IsParentNode)
                continue;
            if (item.displayName.ToLower().Contains(m_CurSearchStr))
            {
                item.SetDepthToTop();
                m_FilterSearchList.Add(item);
            }
        }
        m_ShowItemLists = m_FilterSearchList;
        Reload();
    }

}
