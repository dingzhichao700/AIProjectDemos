using System;
using System.Collections;
using System.Reflection;

/// <summary>
/// 序列化存储数据的基类
/// </summary>
[Serializable]
public class SerializableSaveData {

    /**打印成员变量的时候是否需要显示换行*/
    protected bool printNewLineAfterMember = false;

    public override string ToString() {
        string result = "";
        Type type = GetType(); //得到所有字段
        FieldInfo[] infos = type.GetFields();

        string content;
        for (int i = 0; i < infos.Length; i++) {
            FieldInfo info = infos[i];
            content = info.Name + ": ";
            if (IsBaseDataType(info.FieldType)) {
                //所有基元数据类型：变量名(变量类型名): 内容
                content += "(" + info.FieldType.Name + "): " + info.GetValue(this);
            } else if (typeof(IList).IsAssignableFrom(info.FieldType)) {
                //列表：列表名<元素类型>:[{元素1},{元素2},,,] 或 null

                IList list = info.GetValue(this) as IList;
                if(list != null) {
                    //先打印格式
                    Type[] genericArguments = info.FieldType.GetGenericArguments();//获取泛型类型
                    /**元素的类型*/
                    Type elementType = genericArguments[0];
                    /**元素是否是基元数据类型*/
                    bool isElementBaseData = IsBaseDataType(elementType);
                    content += "(List<" + elementType.Name + ">):";

                    //再打印内容
                    string strList = "";
                    for (int j = 0; j < list.Count; j++) {
                        if (isElementBaseData) {
                            strList += list[j];
                        } else {
                            strList += "{" + list[j] + "}";
                        }
                        strList += (j < list.Count - 1 ? "," : "");
                    }
                    content += "[" + strList + "]";
                } else {
                    content += "null";
                }

            } else if (typeof(IDictionary).IsAssignableFrom(info.FieldType)) {
                //字典：字典名<key类型，成员格式名>: {key1:{成员1},key2:{成员2},key3:{成员3}}  或 null

                IDictionary map = info.GetValue(this) as IDictionary;
                if (map != null) {
                    //先打印格式
                    Type[] genericArguments = info.FieldType.GetGenericArguments(); //获取泛型类型
                    /**元素的类型*/
                    Type elementType = genericArguments[1];
                    /**元素是否是基元数据类型*/
                    bool isElementBaseData = IsBaseDataType(elementType);
                    content += "(Dictionary<" + genericArguments[0].Name + "," + genericArguments[1].Name + ">): ";

                    //再打印内容
                    string strMap = "";
                    foreach (var key in map.Keys) {
                        if(strMap != "") {
                            strMap += ",";
                        }
                        strMap += key + ":";
                        if (isElementBaseData) {
                            strMap += map[key];
                        } else {
                            strMap += "{" + map[key] + "}";
                        }
                    }
                    if (strMap != "") {
                        content += "[" + strMap + "]";
                    }
                } else {
                    content += "null";
                }
            } else {
                //否则都认为是自定义类型，也直接打印内容
                content += "(" + info.FieldType.Name + "): " + info.GetValue(this);
            }
            result += content;
            result += (i < infos.Length - 1 ? "," : "");
            if (printNewLineAfterMember) {
                result += (i < infos.Length - 1 ? "\n" : "");
            }
        }
        return result;
    }

    /**某类型是否是基元数据类型*/
    public static bool IsBaseDataType(Type type) {
        return type == typeof(string) || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(bool);
    }
}
