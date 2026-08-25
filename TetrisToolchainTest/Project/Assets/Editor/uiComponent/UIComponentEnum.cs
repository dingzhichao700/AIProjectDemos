using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI组件类型枚举
/// </summary>
public class UIComponentEnum
{

    /// <summary>
    /// 定义组件id和内容的字典<组件id,[组件类，需要的导入]>
    /// </summary>
    private static Dictionary<int, string[]> componentNameMap;

    /// <summary>
    /// 注册组件
    /// </summary>
    private static void RegistComponent()
    {
        if (componentNameMap == null)
        {
            componentNameMap = new Dictionary<int, string[]>
            {
                { 1, new string[] { "RectTransform", "UnityEngine" } },   //容器
                { 2, new string[] { "Text", "UnityEngine.UI" } },         //文本
                { 3, new string[] { "Image", "UnityEngine.UI" } },        //图片
                { 4, new string[] { "Button", "UnityEngine.UI" } },       //按钮
                { 5, new string[] { "Dropdown", "UnityEngine.UI" } },     //下拉列表
                { 6, new string[] { "Toggle", "UnityEngine.UI" } },       //勾选按钮
                { 7, new string[] { "TextMeshProUGUI", "TMPro" } },       //TMP文本
                { 8, new string[] { "TMP_InputField", "TMPro" } },        //TMP输入文本框
                { 9, new string[] { "Slider", "UnityEngine.UI" } },       //拖动条
                { 10, new string[] { "CanvasGroup", "UnityEngine.UI" } }, //透明度组件
                { 11, new string[] { "ScrollRect", "UnityEngine.UI" } },  //滑动区域

                //从100开始是自定义组件
                { 100, new string[] { "UIComponnet" } },  //自定义组件基类
                { 101, new string[] { "ScrollList" } },
                { 102, new string[] { "GameButton" } }
            };
        }
    }

    /// <summary>
    /// 获取是否有效组件
    /// </summary>
    /// <param name="compName"></param>
    /// <returns></returns>
    public static bool IsValidComponent(string compName)
    {
        RegistComponent();
        foreach (var item in componentNameMap)
        {
            string[] strs = item.Value;
            if (strs[0] == compName)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取组件类型需要的导入类名
    /// </summary>
    /// <param name="type">组件类型</param>
    /// <returns>导入类名</returns>
    public static string GetComponentImport(int type)
    {
        RegistComponent();
        if (componentNameMap.ContainsKey(type))
        {
            string[] strs = componentNameMap[type];
            return strs[1];
        }
        Debug.LogError("获取组件失败，类型：" + type);
        return "";
    }

}
