using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class UIBindComponentData
{
    /// <summary>
    /// UI组件序号
    /// </summary>
    public int id;

    /// <summary>
    /// UI变量名
    /// </summary>
    public string uiName;

    /// <summary>
    /// 组件类名
    /// </summary>
    public string uiTypeName;

    /// <summary>
    /// 是否自定义类
    /// </summary>
    public bool isCustomClass;

    /// <summary>
    /// 自定义类名
    /// </summary>
    public string customClassName;

    /// <summary>
    /// UI组件元素对象
    /// </summary>
    public GameObject go;

}
