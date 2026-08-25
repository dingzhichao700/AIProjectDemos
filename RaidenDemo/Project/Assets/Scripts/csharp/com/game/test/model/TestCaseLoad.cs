using Newtonsoft.Json.Linq;
using SimpleJSON;
using UnityEngine;

/// <summary>
/// 测试加载
/// </summary>
public class TestCaseLoad {

    public TestCaseLoad() {
    }

    private void OnLoadJson(string path, JToken json) {
        JObject frame1 = json["frames"][0] as JObject;
        string picName = frame1.Value<string>("filename");
        JObject frameInfo = frame1.Value<JObject>("frame");
    }

    private void OnLoadJsonNode(JSONNode json) {
        Debug.Log("JsonNode Load Complete:" + json);
    }

    private void OnLoadJsonNodeList() {
        Debug.Log("JsonNodeList Load Complete!");
    }

    //测试存档读写
    /*private void TestSave() {
        #region Windows
        //PlayerPrefers存储的数据在Windows平台存储在注册表里
        //HKCU\Software\[公司名称]\[产品名称] 项下的注册表中
        //其中 公司和产品名称 是在“Project Settings”中设置得名称

        //运行regedit
        //HKEY_CURRENT_USER
        //\Software
        //\Unity
        //\UnityEditor
        //\公司名称
        //产品名称
        #endregion

        #region 知识点一 反射知识的回顾
        //反射三剑客 -- 1T 和两A
        //Type --用于获取类的所有信息 字段 属性 方法 等等
        //Assembly --用于获取程序集的信息 通过程序集获取Type
        //Activator --用于快速实例化对象
        #endregion

        #region 知识点二 判断一个类型的对象 是否可以让另一个类型为自己分配空间
        //父类装子类
        //是否可以从某一个类型的对象 为自己 分配空间
        //Type fatherType = typeof(Father);
        //Type sonType = typeof(Son);

        ////调用者 通过该方法进行判断 判断是否可以通过传入类型为自己 分配空间
        //if (fatherType.IsAssignableFrom(sonType))
        //{
        //    print("可以装");
        //    Father f = Activator.CreateInstance(sonType) as Father;
        //    print(f);
        //}
        //else { Debug.Log("装不了一点"); }
        #endregion

        #region 知识点三 通过反射获取泛型类型

        //获取列表内容的泛型类型
        *//*List<string> list = new List<string>();
        Type listType = list.GetType();
        //获取泛型类型
        Type[] genericArguments = listType.GetGenericArguments();
        foreach (Type type in genericArguments)
        {
            Debug.Log(type);
        }*//*

        //获取字典key和value的泛型类型
        *//*Dictionary<int, string> dic = new Dictionary<int, string>();
        Type dicType = dic.GetType();
        Type[] genericArguments = dicType.GetGenericArguments();
        foreach (Type type in genericArguments)
        {
            Debug.Log(type);
        }*//*
        #endregion

        //读取
        Save save = BinarySave.LoadInfo("saveTest1");
        if (save != null) {
            Debug.Log(save);
        } else {
            save = new Save();
        }

        //游戏逻辑中 会去修改这个玩家数据
        save.name = "张三";
        save.age = 18;
        //save.sex = true;
        //save.height = 1.88f;
        //save.width = 2.18f;

        save.myItem = new ItemInfo(123, "自定义道具");

        save.listInt = new List<int> { 5, 10, 15, 20, 25, 30 };
        save.listString = new List<string> { "str1", "str2", "str3", "str4" };

        save.itemInfoList = new List<ItemInfo> { new ItemInfo(1001, "道具1"), new ItemInfo(1002, "道具2"), new ItemInfo(1003, "道具3") };
        save.itemInfoList = new List<ItemInfo>();
        save.itemInfoList.Add(new ItemInfo(1, "123"));
        save.itemInfoList.Add(new ItemInfo(2, "456"));

        save.baseMap = new Dictionary<int, string>() { { 1, "123" }, { 2, "456" } };

        save.itemMap = new Dictionary<string, ItemInfo>();
        save.itemMap.Add("aa", new ItemInfo(20, "aaAA"));
        save.itemMap.Add("bb", new ItemInfo(21, "bbBB"));
        save.itemMap.Add("cc", new ItemInfo(22, "ccCC"));
        save.itemMap.Add("dd", new ItemInfo(23, "zzZZ"));

        //保存数据
        BinarySave.SaveInfo(save, "saveTest1");
    }*/

}
