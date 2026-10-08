
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// UI变量绑定类
/// </summary>
public class UIBinder : MonoBehaviour {

    /// <summary>
    /// 类名称
    /// </summary>
    private string className;

    /// <summary>
    /// 类名对应的脚本实例
    /// </summary>
    private Component targetComp;

    /// <summary>
    /// 绑定的C#资源路径
    /// </summary>
    public string csharpAssetPath;

    /// <summary>
    /// 绑定的C#资源
    /// </summary>
    public string csharpAsset;

    /// <summary>
    /// 绑定的C#资源路径
    /// </summary>
    public List<UIBindComponentData> uiList;

    /// <summary>
    /// UI定义开始
    /// </summary>
    public static string UI_DEFINE_STR_BEGIN = "UIComponent Define begin";

    /// <summary>
    /// UI定义结束
    /// </summary>
    public static string UI_DEFINE_STR_FINISH = "UIComponent Define finish";

    void Awake() {
        AppendScriptClass();
        BaseView baseView = GetComponent<BaseView>();
        if (baseView != null) {
            baseView.isInit = true;
            baseView.OnInit();
        }
    }

    /**添加对应的脚本*/
    private void AppendScriptClass() {
        //先递归一遍，让自身和下级元素的UIBinder都挂上对应脚本
        string[] strList = csharpAssetPath.Split('/');
        string classStr = strList[strList.Length - 1];
        className = classStr.Split('.')[0];
        Type classType = FindScriptType(className);
        if (classType == null) {
            Debug.LogErrorFormat("UIBinder未找到唯一可用的脚本类型：对象{0}，类{1}，路径{2}。请确认所属程序集已加载。", this.name, className, csharpAssetPath);
            return;
        }

        //挂绑定的脚本
        targetComp = gameObject.GetComponent(classType);
        if (targetComp == null) {
            targetComp = gameObject.AddComponent(classType);
        }

        for (int i = 0; i < uiList.Count; i++) {
            UIBindComponentData data = uiList[i];
            if (data.go != null && data.go != gameObject) {//在成员中如何还能找到挂了UIBinder的，继续递归（这个成员要排除自身，否则会不停给自己append脚本引发死循环）
                UIBinder binder = data.go.GetComponent<UIBinder>();
                if (binder != null) {
                    binder.AppendScriptClass();
                }
            }
        }
        SetMemberVariable(className, targetComp);
    }

    /// <summary>
    /// 在已加载程序集中查找唯一的组件类型。
    /// </summary>
    /// <param name="typeName">脚本类型名</param>
    /// <remarks>不主动加载业务程序集；热更新 DLL 必须在实例化界面前加载完成。</remarks>
    private static Type FindScriptType(string typeName) {
        Type result = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            Type candidate = assembly.GetType(typeName, false);
            if (candidate == null || !typeof(MonoBehaviour).IsAssignableFrom(candidate) || candidate.IsAbstract || candidate.ContainsGenericParameters) {
                continue;
            }
            if (result != null) {
                Debug.LogErrorFormat("UIBinder脚本类型重名：{0}，程序集：{1}、{2}", typeName, result.Assembly.GetName().Name, assembly.GetName().Name);
                return null;
            }
            result = candidate;
        }
        return result;
    }

    /// <summary>
    /// 按目标字段的实际类型绑定界面组件。
    /// </summary>
    /// <param name="className">目标类名</param>
    /// <param name="targetComp">目标组件</param>
    /// <remarks>使用 Type 获取组件，兼容热更新程序集，避免字符串查找返回空引用。</remarks>
    private void SetMemberVariable(string className, Component targetComp) {
        Type targetType = targetComp.GetType();
        for (int i = 0; i < uiList.Count; i++) {
            UIBindComponentData item = uiList[i];
            FieldInfo field = targetType.GetField(item.uiName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !typeof(Component).IsAssignableFrom(field.FieldType)) {
                throw new InvalidOperationException($"UIBinder绑定失败：{className}.{item.uiName} 不是有效的组件字段，对象：{name}。");
            }
            if (item.go == null) {
                throw new InvalidOperationException($"UIBinder绑定失败：{className}.{item.uiName} 未指定节点，对象：{name}。");
            }
            Component component = item.go.GetComponent(field.FieldType);
            if (component == null) {
                throw new InvalidOperationException($"UIBinder绑定失败：{className}.{item.uiName}，节点 {item.go.name} 缺少组件 {field.FieldType.FullName}。");
            }
            field.SetValue(targetComp, component);
        }
    }

}