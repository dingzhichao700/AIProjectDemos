
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

    /**由有效的编辑器绑定生成，运行时不依赖 MonoScript*/
    [SerializeField] private string scriptTypeName;

#if UNITY_EDITOR
    /**同步 Inspector 所选脚本与运行时类型标识*/
    public void RefreshScriptBinding() {
        var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(csharpAssetPath ?? string.Empty);
        Type type = script != null ? script.GetClass() : null;
        scriptTypeName = type != null && typeof(MonoBehaviour).IsAssignableFrom(type) ? type.FullName + ", " + type.Assembly.GetName().Name : string.Empty;
    }

    private void OnValidate() {
        RefreshScriptBinding();
    }
#endif

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
    public List<UIBindComponentData> uiList = new List<UIBindComponentData>();

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
#if UNITY_EDITOR
        RefreshScriptBinding();
#endif
        if (string.IsNullOrWhiteSpace(csharpAssetPath) || string.IsNullOrWhiteSpace(scriptTypeName)) {
            throw new InvalidOperationException($"UIBinder绑定为空或脚本无效：{name}，路径：{csharpAssetPath}。");
        }
        Type classType = Type.GetType(scriptTypeName, false);
        if (classType == null || !typeof(MonoBehaviour).IsAssignableFrom(classType) || classType.IsAbstract) {
            throw new InvalidOperationException($"UIBinder类型不可用：{scriptTypeName}。请先加载所属程序集。");
        }
        className = classType.FullName;

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