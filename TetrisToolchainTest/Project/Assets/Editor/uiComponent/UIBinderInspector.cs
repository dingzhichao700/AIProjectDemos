using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIBinder))]
public class UIBinderInspector : Editor
{

    private UIBinder _target;

    private SerializedObject binding;

    /// <summary>
    /// 缓存的c#资源对象属性
    /// </summary>
    private SerializedProperty cachedCSharpPathProp;

    /// <summary>
    /// 缓存的c#对象（根据缓存路径获取到）
    /// </summary>
    private UnityEngine.Object cachedCsharpAsset;

    /// <summary>
    /// 临时c#对象
    /// </summary>
    private UnityEngine.Object tempCSharpAsset;

    /// <summary>
    /// 查找C#文件用的窗口
    /// </summary>
    private FindObjectWindow _findCSharpWindow;

    /// <summary>
    /// C#文件路径
    /// </summary>
    private static string ROOT_PATH_CSHARP = "Assets/Scripts/csharp";

    /// <summary>
    /// C#文件后缀
    /// </summary>
    private static string STR_CSHARP_EXTENSION = ".cs";

    /// <summary>
    /// UI定义用的缩进字符
    /// </summary>
    private static string UI_DEFINE_STR_INDENT = "    ";

    private int popIndex;

    private void OnEnable()
    {
        _target = target as UIBinder;
        binding = new SerializedObject(target);
        cachedCSharpPathProp = binding.FindProperty("csharpAssetPath");
    }

    public override void OnInspectorGUI()
    {
        //base.OnInspectorGUI();

        GUILayout.BeginHorizontal();
        GUILayout.Label("绑定C#:", GUILayout.MaxWidth(50));
        //先把缓存对象设置为缓存对象路径获取到的资源对象
        cachedCsharpAsset = AssetDatabase.LoadAssetAtPath(cachedCSharpPathProp.stringValue, typeof(Object));
        //再尝试把缓存文件对象放到对象选框中
        tempCSharpAsset = EditorGUILayout.ObjectField(cachedCsharpAsset, typeof(Object), false);
        if (tempCSharpAsset)
        {
            string path = AssetDatabase.GetAssetPath(tempCSharpAsset);
            if (path.Contains(ROOT_PATH_CSHARP))
            {
                if (cachedCSharpPathProp.stringValue != path && path.EndsWith(STR_CSHARP_EXTENSION))
                {
                    //如果缓存对象路径和临时资源对象的路径不一致，记录并同步
                    cachedCSharpPathProp.stringValue = path;
                    binding.ApplyModifiedProperties();
                }
            }
            else
            {
                Debug.LogWarning("只能绑定以下路径的c#：" + ROOT_PATH_CSHARP);
            }
        }

        if (GUILayout.Button("选择", new GUIStyle(EditorStyles.toolbarButton), GUILayout.MaxWidth(100)))
        {
            if (_findCSharpWindow == null)
            {
                _findCSharpWindow = FindObjectWindow.OpenWindow("csharp", ".cs");
                _findCSharpWindow.SetSelectHandler(OnClickFindCSharpScript);
            }
        }
        GUILayout.EndHorizontal();

        //绘制需要导出的ui变量列表
        int maxId = 0;
        List<UIBindComponentData> uiList = _target.uiList;
        if (uiList != null)
        {
            GUILayout.BeginVertical();
            for (int i = 0; i < uiList.Count; i++)
            {
                UIBindComponentData item = uiList[i];
                GUILayout.BeginHorizontal();

                //变量的id
                item.id = int.Parse(GUILayout.TextField(item.id.ToString(), GUILayout.Width(30)));
                GameObject cacheGo = item.go;

                GUILayout.BeginVertical(GUILayout.Height(40));
                GUILayout.BeginHorizontal();
                //变量的go
                item.go = EditorGUILayout.ObjectField(cacheGo, typeof(GameObject), true, GUILayout.Width(150)) as GameObject;
                if (item.go != cacheGo)
                {
                    EditorUtility.SetDirty(_target);
                }

                maxId = Mathf.Max(maxId, item.id);

                //变量名
                string variableName = item.uiName;
                if ((item.uiName == null || item.uiName == "") && item.go)
                {
                    variableName = item.go.name;
                }
                item.uiName = GUILayout.TextField(variableName, GUILayout.MinWidth(150));
                if (item.uiName != variableName)
                {
                    EditorUtility.SetDirty(_target);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();

                bool isCustomClass = GUILayout.Toggle(item.isCustomClass, "映射类名", GUILayout.Width(150));
                if (isCustomClass != item.isCustomClass)
                {
                    item.isCustomClass = isCustomClass;
                    EditorUtility.SetDirty(_target);
                }

                if (isCustomClass)
                {
                    //自定义映射类
                    string customClassName = GUILayout.TextField(item.customClassName, GUILayout.MinWidth(150));
                    if (item.customClassName != customClassName)
                    {
                        item.customClassName = customClassName;
                        EditorUtility.SetDirty(_target);
                    }
                }
                else
                {
                    //变量上挂的组件，显示下拉列表
                    List<string> componentNameList = GetNodeComponetTypeNames(item.go as GameObject);

                    string cacheUITypeName = item.uiTypeName;
                    int selectIndex = Mathf.Max(componentNameList.IndexOf(item.uiTypeName), 0);
                    selectIndex = EditorGUILayout.Popup(selectIndex, componentNameList.ToArray(), GUILayout.MinWidth(150));
                    //Debug.Log("selectIndex:" + selectIndex);
                    if (componentNameList.Count > 0)
                    {
                        item.uiTypeName = componentNameList[selectIndex];
                    }
                    if (cacheUITypeName != item.uiTypeName)
                    {
                        EditorUtility.SetDirty(_target);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                //删除按钮
                if (GUILayout.Button("-", GUILayout.MinWidth(25), GUILayout.Width(50)))
                {
                    uiList.RemoveAt(i);
                    EditorUtility.SetDirty(_target);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        //添加新ui按钮
        if (GUILayout.Button("+", GUILayout.Height(25)))
        {
            if (uiList == null)
            {
                uiList = new List<UIBindComponentData>();
            }
            UIBindComponentData item = new UIBindComponentData();
            item.id = maxId + 1;
            uiList.Add(item);
            EditorUtility.SetDirty(_target);
        }

        if (GUILayout.Button("一键生成绑定", new GUIStyle(EditorStyles.toolbarButton)))
        {
            GenerateUiBind(cachedCSharpPathProp.stringValue);
        }
    }

    /// <summary>
    /// 选择文件回调
    /// </summary>
    /// <param name="file"></param>
    private void OnClickFindCSharpScript(FileItem file)
    {
        if (file.isDirectory)
        {
            ShowMsgWindow("该文件是目录：" + file.resName);
        }
        else
        {
            Debug.Log("选择文件：" + file.resName);
            string assetPath = file.assetPath;
            if (assetPath.Contains(ROOT_PATH_CSHARP))
            {
                cachedCSharpPathProp.stringValue = assetPath;
                binding.ApplyModifiedProperties();
            }
            _findCSharpWindow.Close();
            _findCSharpWindow = null;
        }
    }
    /// 生成UI绑定到对应的C#文件
    /// </summary>
    /// <param name="path">C#文件路径</param>
    private void GenerateUiBind(string path)
    {
        //使用ReadAllLines读取，字符串数组接收
        int beginLine = 0;
        int finishLine = 0;
        /**左花括弧首次出现的行*/
        int firstLeftColumnLine = 0;
        List<string> allLines = new List<string>(File.ReadAllLines(path));
        EnsureComponentNamespaces(allLines);
        for (int i = 0; i < allLines.Count; i++)
        {
            string line = allLines[i];
            if (line.IndexOf(UIBinder.UI_DEFINE_STR_BEGIN) >= 0)
            {
                beginLine = i;
            }
            if (line.IndexOf(UIBinder.UI_DEFINE_STR_FINISH) >= 0)
            {
                finishLine = i + 1;
            }
            if (firstLeftColumnLine == 0 && line.IndexOf("{") >= 0)
            {
                firstLeftColumnLine = i + 1;
            }
        }
        if (beginLine == 0)
        {
            //找不到已声明变量的起始和结束行，说明还未生成过UI变量声明，直接在首个{后面加
            beginLine = finishLine = firstLeftColumnLine;
        }

        List<string> insertVariable = GenerateUIVariableContent();

        List<string> fontList = allLines.GetRange(0, beginLine);
        List<string> backList = allLines.GetRange(finishLine, (allLines.Count - finishLine));
        List<string> result = new List<string>();
        result.AddRange(fontList);
        result.AddRange(insertVariable);
        result.AddRange(backList);

        //File.WriteAllLines(path, result.ToArray());
        File.WriteAllLines(path, result.ToArray(), Encoding.UTF8);
        SyncSiblingCloneBinders();
        //Debug.Log(File.ReadAllText(path));
    }

    /// <summary>
    /// ScrollList 编辑期预览项可能早于模板绑定生成；生成成员时同步其同级克隆，
    /// 避免运行时对象池优先复用旧克隆而得到空成员。
    /// </summary>
    private void SyncSiblingCloneBinders()
    {
        Transform template = _target.transform;
        Transform parent = template.parent;
        if (parent == null || template.name.EndsWith("(Clone)"))
        {
            return;
        }

        string cloneName = template.name + "(Clone)";
        int syncedCount = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform clone = parent.GetChild(i);
            if (clone == template || clone.name != cloneName)
            {
                continue;
            }

            UIBinder cloneBinder = clone.GetComponent<UIBinder>();
            if (cloneBinder == null)
            {
                Debug.LogWarning("UIBinder同步跳过：克隆缺少UIBinder，节点=" + clone.name, clone);
                continue;
            }

            List<UIBindComponentData> cloneUiList = new List<UIBindComponentData>();
            foreach (UIBindComponentData source in _target.uiList)
            {
                if (source.go == null)
                {
                    continue;
                }

                string relativePath = AnimationUtility.CalculateTransformPath(source.go.transform, template);
                Transform cloneTarget = string.IsNullOrEmpty(relativePath) ? clone : clone.Find(relativePath);
                if (cloneTarget == null)
                {
                    Debug.LogWarning("UIBinder同步跳过成员：克隆中找不到路径=" + relativePath, clone);
                    continue;
                }

                cloneUiList.Add(new UIBindComponentData
                {
                    id = source.id,
                    uiName = source.uiName,
                    uiTypeName = source.uiTypeName,
                    isCustomClass = source.isCustomClass,
                    customClassName = source.customClassName,
                    go = cloneTarget.gameObject,
                });
            }

            Undo.RecordObject(cloneBinder, "Sync UIBinder Clone Members");
            cloneBinder.csharpAssetPath = _target.csharpAssetPath;
            cloneBinder.csharpAsset = _target.csharpAsset;
            cloneBinder.uiList = cloneUiList;
            EditorUtility.SetDirty(cloneBinder);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cloneBinder);
            syncedCount++;
        }

        if (syncedCount > 0)
        {
            Debug.Log("UIBinder已同步同级克隆：" + syncedCount + "，模板=" + template.name, template);
        }
    }

    /// <summary>
    /// 根据当前导出成员的真实组件类型，补齐目标脚本需要的 using。
    /// </summary>
    private void EnsureComponentNamespaces(List<string> allLines)
    {
        HashSet<string> requiredNamespaces = new HashSet<string>();
        foreach (UIBindComponentData data in _target.uiList)
        {
            if (data.go == null)
            {
                continue;
            }

            string componentTypeName = data.isCustomClass ? data.customClassName : data.uiTypeName;
            if (string.IsNullOrEmpty(componentTypeName))
            {
                continue;
            }

            Component component = data.go.GetComponent(componentTypeName);
            string componentNamespace = component == null ? null : component.GetType().Namespace;
            if (!string.IsNullOrEmpty(componentNamespace))
            {
                requiredNamespaces.Add(componentNamespace);
            }
        }

        int lastUsingLine = -1;
        for (int i = 0; i < allLines.Count; i++)
        {
            string trimmedLine = allLines[i].Trim();
            if (trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"))
            {
                lastUsingLine = i;
                string existingNamespace = trimmedLine.Substring(6, trimmedLine.Length - 7).Trim();
                requiredNamespaces.Remove(existingNamespace);
            }
        }

        if (requiredNamespaces.Count == 0)
        {
            return;
        }

        List<string> sortedNamespaces = new List<string>(requiredNamespaces);
        sortedNamespaces.Sort();
        int insertLine = lastUsingLine + 1;
        foreach (string componentNamespace in sortedNamespaces)
        {
            allLines.Insert(insertLine++, "using " + componentNamespace + ";");
        }

        if (insertLine < allLines.Count && !string.IsNullOrEmpty(allLines[insertLine]))
        {
            allLines.Insert(insertLine, string.Empty);
        }
    }

    /// <summary>
    /// 获取一个节点所挂载的所有UI组件类型名称（用于展示下拉列表）
    /// </summary>
    /// <returns></returns>
    private List<string> GetNodeComponetTypeNames(GameObject go)
    {
        List<string> result = new List<string>();
        if (go != null)
        {
            Component[] comps = go.GetComponents<Component>();
            foreach (Component component in comps)
            {
                string compTypeName = component.GetType().Name;
                if (UIComponentEnum.IsValidComponent(compTypeName))
                {
                    result.Add(compTypeName);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 生成所有组件对应的代码
    /// </summary>
    /// <param name="msg"></param>
    private List<string> GenerateUIVariableContent()
    {
        List<string> result = new List<string>();
        result.Add(UI_DEFINE_STR_INDENT + "/******************* " + UIBinder.UI_DEFINE_STR_BEGIN + " ************************/");
        //result.Add(UI_DEFINE_STR_INDENT + "public Text txt1;");
        //result.Add(UI_DEFINE_STR_INDENT + "public Text txt2;");
        //result.Add(UI_DEFINE_STR_INDENT + "public Text txt3;");
        List<UIBindComponentData> uiList = _target.uiList;
        for (int i = 0; i < uiList.Count; i++)
        {
            UIBindComponentData comp = uiList[i];
            if (comp.isCustomClass)
            {
                //自定义类映射
                result.Add(UI_DEFINE_STR_INDENT + "public " + comp.customClassName + " " + comp.uiName + ";");
            }
            else if (comp.uiTypeName != "")
            {
                //ui组件类
                result.Add(UI_DEFINE_STR_INDENT + "public " + comp.uiTypeName + " " + comp.uiName + ";");
            }

        }
        result.Add(UI_DEFINE_STR_INDENT + "/******************* " + UIBinder.UI_DEFINE_STR_FINISH + " ************************/");
        return result;
    }

    /// 
    /// <summary>
    /// 显示提示窗口
    /// </summary>
    /// <param name="msg"></param>
    private void ShowMsgWindow(string msg)
    {
        EditorUtility.DisplayDialog("消息提示", msg, "确认");
    }

}
