
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
        Type classType = Type.GetType(className);
        if (classType == null) {
            Debug.LogErrorFormat("UIBinder生成对象{0}类实例失败：{1}，路径：{2}", this.name, className, csharpAssetPath);
            return;
        }

        //挂绑定的脚本
        targetComp = gameObject.GetComponent(Type.GetType(className));
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
        SetMemberVariable(className, gameObject.GetComponent(Type.GetType(className)));
    }

    /// <summary>
    /// 设置成员变量
    /// </summary>
    /// <param name="className">要设置的类名</param>
    /// <param name="targetComp">要设置的目标组件</param>
    private void SetMemberVariable(string className, Component targetComp) {
        for (int i = 0; i < uiList.Count; i++) {
            UIBindComponentData item = uiList[i];
            Component uiComponent;
            if (item.go != null) {
                if (item.isCustomClass) {
                    //自定义类
                    uiComponent = item.go.GetComponent(item.customClassName);
                } else {
                    //UI组件类
                    uiComponent = item.go.GetComponent(item.uiTypeName);
                }
                SetParameters(className, targetComp, item.uiName, uiComponent);
            }
        }
    }

    /// <summary>
    /// 设置字段
    /// </summary>
    /// <param name="className">要设置的类名</param>
    /// <param name="tarComponent">要设置的目标组件</param>
    /// <param name="FieldName">要设置的字段名</param>
    /// <param name="value">要设置的值</param>
    private void SetParameters(string className, Component tarComponent, string FieldName, object value) {
        //通过类名获取类型
        Type type = Type.GetType(className);
        //通过字段名找到字段 找到会返回，没找到会返回null
        FieldInfo tarField = type.GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (tarField != null) {
            //设置字段参数
            tarField.SetValue(tarComponent, value);
        } else {
            Debug.LogError("设置类" + className + "实例的字段" + FieldName + "找不到字段目标");
        }
    }

}
