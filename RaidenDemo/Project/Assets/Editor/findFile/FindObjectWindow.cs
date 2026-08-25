using System;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class FindObjectWindow : EditorWindow
{

    /// <summary>
    /// 文件树
    /// </summary>
    private FileTreeView<FileItem> fileTreeView;

    /// <summary>
    /// 树状态
    /// </summary>
    private TreeViewState treeState;

    /// <summary>
    /// 文件类型后缀，如".cs"、".lua"、".png"
    /// </summary>
    private string extendName;

    /// <summary>
    /// 当前选中的文件对象
    /// </summary>
    private FileItem curSelectItem;

    /// <summary>
    /// 当前文件搜索关键字
    /// </summary>
    private string fileSearchKeyword = "";

    private Vector2 scrollVec = Vector2.zero;

    private Rect treeRect = new Rect(0, 0, WINDOW_W, TREE_HEIGHT - 5);

    /// <summary>
    /// 选中按钮
    /// </summary>
    private Rect btnRect = new Rect(WINDOW_W * 0.5f - 35, TREE_HEIGHT + SEARCH_HEIGHT, 70, BTN_HEIGHT * 0.5f);

    /// <summary>
    /// 选择完成回调
    /// </summary>
    private Action<FileItem> onSureClickListen;

    /// <summary>
    /// 窗口宽度
    /// </summary>
    private static float WINDOW_W = 600;

    /// <summary>
    /// 窗口高度
    /// </summary>
    private static float WINDOW_H = 400;

    /// <summary>
    /// 搜索区域高度
    /// </summary>
    private static float SEARCH_HEIGHT = WINDOW_H * 0.1f;

    /// <summary>
    /// 树节点高度
    /// </summary>
    private static float TREE_HEIGHT = WINDOW_H * 0.8f;

    /// <summary>
    /// 选中按钮高度
    /// </summary>
    private static float BTN_HEIGHT = WINDOW_H * 0.15f;

    /// <summary>
    /// 搜索栏标题矩形
    /// </summary>
    private static Rect SEARCH_TITLE_RECT = new Rect(10, SEARCH_HEIGHT * 0.1f, 40, 20);

    /// <summary>
    /// 搜索栏矩形
    /// </summary>
    private static Rect SEARCH_RECT = new Rect(SEARCH_TITLE_RECT.x + SEARCH_TITLE_RECT.width + 5, SEARCH_HEIGHT * 0.1f, 200, 20);

    /// <summary>
    /// 文件树区域矩形
    /// </summary>
    private Rect treeAreaRect = new Rect(0, SEARCH_HEIGHT, WINDOW_W, WINDOW_H);

    /// <summary>
    /// 打开文件查找窗口
    /// </summary>
    /// <param name="findType">查找文件类型</param>
    /// <param name="extendName">文件后缀名</param>
    /// <returns></returns>
    public static FindObjectWindow OpenWindow(string findType, string extendName = null)
    {
        FindObjectWindow window = (FindObjectWindow)GetWindow(typeof(FindObjectWindow), true, "查找文件类型：" + findType, true);
        //window.position = new Rect((Screen.currentResolution.width) * 0.3f, Screen.currentResolution.height * 0.3f, WINDOW_W, WINDOW_H);
        window.Show();
        window.SetSearchPath(findType, extendName);
        window.minSize = new Vector2(WINDOW_W, WINDOW_H);
        return window;
    }

    /// <summary>
    /// 设置搜索类型和文件类型名
    /// </summary>
    /// <param name="findType"></param>
    /// <param name="extendName"></param>
    public void SetSearchPath(string findType, string extendName)
    {
        findType = findType.ToLower();
        string rootPath = "";
        switch (findType)
        {
            case "lua":
                rootPath = "Assets/Scripts/lua";
                break;
            case "csharp":
                rootPath = "Assets/Scripts/csharp/com/game";
                break;
            default:
                Debug.LogErrorFormat("查找窗口找不到对应的路径类型：{0}", findType);
                break;
        }
        this.extendName = extendName;
        GetDirs(rootPath);
        fileTreeView.Reload();
    }

    private void OnEnable()
    {
        treeState = new TreeViewState();
        fileTreeView = new FileTreeView<FileItem>(treeState);
        fileTreeView.SetSingleClickListener(OnClickItem);
        fileTreeView.SetDoubleClickListener(OnDoubleClickItem);
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索：", GUILayout.Width(50));
        fileSearchKeyword = GUILayout.TextField(fileSearchKeyword);
        GUILayout.EndHorizontal();
        fileTreeView.SetSearch(fileSearchKeyword);
        GUILayout.BeginArea(treeAreaRect, EditorStyles.helpBox);
        EditorGUILayout.BeginScrollView(scrollVec);
        fileTreeView.OnGUI(treeRect);
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
        if (GUI.Button(btnRect, "选中"))
        {
            onSureClickListen(curSelectItem);
        }
    }

    /// <summary>
    /// 设置选择完成回调
    /// </summary>
    /// <param name="func"></param>
    public void SetSelectHandler(Action<FileItem> func)
    {
        onSureClickListen = func;
    }

    /**单击*/
    void OnClickItem(FileItem fileItem)
    {
        curSelectItem = fileItem;
        Debug.Log((fileItem.isDirectory ? "选中文件夹：" : "选中文件：") + fileItem.resName);
    }

    /**双击*/
    void OnDoubleClickItem(FileItem fileItem)
    {
        if (!fileItem.isDirectory) {
            if (onSureClickListen != null) {
                onSureClickListen(fileItem);
            }
        }
        //curSelectItem = fileItem;
        //Debug.Log((fileItem.isDirectory ? "选中文件夹：" : "选中文件：") + fileItem.resName);
    }

    /// <summary>
    /// 递归式获取某目录下的文件信息
    /// </summary>
    /// <param name="dirPath">路径</param>
    /// <param name="parentItem"></param>
    private void GetDirs(string dirPath, FileTreeItem<FileItem> parentItem = null)
    {
        if (!Directory.Exists(dirPath))
        {
            return;
        }

        DirectoryInfo directoryInfo = new DirectoryInfo(dirPath);

        //文件夹
        DirectoryInfo[] directoryInfos = directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
        if (directoryInfos != null)
        {
            foreach (DirectoryInfo item in directoryInfos)
            {
                string totalPath = item.FullName.Replace("\\", "/");
                string name;

                int index = totalPath.LastIndexOf("/");
                if (index < 0)
                {
                    continue;
                }
                name = totalPath.Substring(index + 1);
                FileItem fileItem = new FileItem() { resName = name, hardTotalPath = totalPath, isDirectory = true, assetPath = GetAssetResPath(totalPath) };
                FileTreeItem<FileItem> directItem = fileTreeView.AddChild(parentItem, fileItem, name, true, parentItem == null);
                GetDirs(totalPath, directItem);
            }
        }

        //文件
        FileInfo[] fileInfos = directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
        if (fileInfos != null)
        {
            foreach (FileInfo file in fileInfos)
            {
                string totalPath = file.FullName.Replace("\\", "/");
                string name;

                int index = totalPath.LastIndexOf("/");
                if (index < 0)
                {
                    continue;
                }
                name = totalPath.Substring(index + 1);
                string fileExtendName = name.Substring(name.LastIndexOf("."));
                if (fileExtendName == extendName)
                {
                    FileItem fileItem = new FileItem() { resName = name, hardTotalPath = totalPath, isDirectory = false, assetPath = GetAssetResPath(totalPath) };
                    fileTreeView.AddChild(parentItem, fileItem, name, false, parentItem == null);
                }
            }
        }
    }

    /// <summary>
    /// 获取一个Asset目录下的完整硬盘路径
    /// </summary>
    /// <param name="hardTotalPath"></param>
    /// <returns></returns> 
    string GetAssetResPath(string hardTotalPath)
    {
        string assetPath = hardTotalPath;
        if (hardTotalPath.StartsWith(Application.dataPath))
        {
            assetPath = hardTotalPath.Substring(Application.dataPath.Length - 6);
        }
        return assetPath;
    }

}
