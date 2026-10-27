using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 初始化模板的模块资源并验证序列化绑定。
/// </summary>
public static class FrameworkTemplateMigration {

    /// <summary>
    /// 在 Addressables 登记前准备模板资源和绑定。
    /// </summary>
    public static void PrepareAssets() {
        MoveLoginDependencies();
        PrepareOpeningFont();
        CreateMainPanel();
        var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts/csharp" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                foreach (UIBinder binder in root.GetComponentsInChildren<UIBinder>(true)) {
                    // 旧模板 hintItem 曾误绑设置 Tab，并带有两个失效字段。
                    if (path == "Assets/Prefab/title/keyHint/KeyHintView.prefab" && binder.name == "hintItem" && Path.GetFileName(binder.csharpAssetPath) == "SettingTabItem.cs") {
                        binder.csharpAssetPath = "Assets/Scripts/csharp/login/keyHint/KeyHintItem.cs";
                        binder.uiList.RemoveAll(item => item.uiName == "boxContent" || item.uiName == "imgHoverLight");
                    }
                    // 旧设置窗口中已删除的肩部容器没有运行时消费者。
                    if (Path.GetFileName(binder.csharpAssetPath) == "CommonPanelView.cs") {
                        binder.uiList.RemoveAll(item => item.uiName == "boxShoulder");
                    }
                    string fileName = Path.GetFileName(binder.csharpAssetPath);
                    string[] matches = scripts.Where(item => Path.GetFileName(item) == fileName).ToArray();
                    if (matches.Length != 1) {
                        throw new InvalidOperationException($"绑定迁移不唯一：{path} / {binder.name} / {fileName}");
                    }
                    binder.csharpAssetPath = matches[0];
                    binder.RefreshScriptBinding();
                    Type type = AssetDatabase.LoadAssetAtPath<MonoScript>(matches[0]).GetClass();
                    if (type == null) {
                        throw new InvalidOperationException("脚本类型无效：" + matches[0]);
                    }
                    foreach (UIBindComponentData member in binder.uiList) {
                        FieldInfo field = type.GetField(member.uiName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field == null || member.go == null || !typeof(Component).IsAssignableFrom(field.FieldType)) {
                            throw new InvalidOperationException($"成员绑定无效：{path} / {type.Name}.{member.uiName}");
                        }
                        if (member.go.GetComponent(field.FieldType) == null) {
                            var childBinder = member.go.GetComponent<UIBinder>();
                            if (childBinder == null || Path.GetFileNameWithoutExtension(childBinder.csharpAssetPath) != field.FieldType.Name) {
                                throw new InvalidOperationException($"绑定节点缺少组件：{path} / {type.Name}.{member.uiName}");
                            }
                        }
                    }
                    EditorUtility.SetDirty(binder);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        RegisterFonts();
        AssetDatabase.SaveAssets();
    }

    /**模板公共控件属于登录及之后的阶段，避免登录依赖正式内容组*/
    private static void MoveLoginDependencies() {
        // 图集按实际引用提取到 title/title，禁止把 default/common 整体迁入 title。
        string[] sources = { "Assets/Prefab/default/common/GameButton.prefab" };
        string[] targets = { "Assets/Prefab/title/common/GameButton.prefab" };
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        for (int i = 0; i < sources.Length; i++) {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sources[i]))) {
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targets[i]));
            AssetDatabase.Refresh();
            string error = AssetDatabase.MoveAsset(sources[i], targets[i]);
            if (!string.IsNullOrEmpty(error)) {
                throw new InvalidOperationException(error);
            }
            if (!AssetDatabase.IsValidFolder(targets[i])) {
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(targets[i]), settings.FindGroup("title")).address = targets[i];
            }
        }
    }

    /**开场仅引用静态 ASCII 字库，不带中文源字体或全局回退链*/
    private static void PrepareOpeningFont() {
        const string path = "Assets/Art/font/tmpFontAsset/common/fontOpening.asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null) {
            string guid = AssetDatabase.FindAssets("t:Font", new[] { "Assets/Art/font/source" }).First();
            Font source = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            font = TMP_FontAsset.CreateFontAsset(source, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512);
            string characters = new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray());
            if (!font.TryAddCharacters(characters, out string missing)) {
                throw new InvalidOperationException("开场字库缺少字符：" + missing);
            }
            font.name = "fontOpening";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            // 切换为 Static 会由 TMP 清空运行时 sourceFontFile 引用。
            font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            AssetDatabase.CreateAsset(font, path);
            foreach (Texture2D texture in font.atlasTextures) {
                AssetDatabase.AddObjectToAsset(texture, font);
            }
            AssetDatabase.AddObjectToAsset(font.material, font);
        }
        string prefabPath = ResourceConst.GetUIPath(UIEnum.OPENING_PANEL);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /**生成只用于展示模块切换的模板主界面*/
    private static void CreateMainPanel() {
        string path = ResourceConst.GetUIPath(UIEnum.MAIN_PANEL);
        if (File.Exists(path)) {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        GameObject root = new GameObject("MainPanel", typeof(RectTransform), typeof(Image), typeof(UIBinder));
        try {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 1280);
            root.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.14f);
            TextMeshProUGUI title = CreateText(root.transform, "Title", "Main module ready", new Vector2(0, 130));
            title.fontSize = 42;
            var button = new GameObject("btnBack", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(root.transform, false);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 90);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -60);
            button.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.55f);
            CreateText(button.transform, "Text", "Return to login", Vector2.zero);
            var gmButton = UnityEngine.Object.Instantiate(button, root.transform);
            gmButton.name = "btnGM";
            gmButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -170);
            gmButton.GetComponentInChildren<TextMeshProUGUI>().text = "GM";
            UIBinder binder = root.GetComponent<UIBinder>();
            binder.csharpAssetPath = "Assets/Scripts/csharp/main/MainPanel.cs";
            binder.uiList.Add(new UIBindComponentData { id = 1, uiName = "btnBack", uiTypeName = "Button", go = button });
            binder.uiList.Add(new UIBindComponentData { id = 2, uiName = "btnGM", uiTypeName = "Button", go = gmButton });
            binder.RefreshScriptBinding();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.Refresh();
    }

    /**创建无业务依赖的模板文本*/
    private static TextMeshProUGUI CreateText(Transform parent, string name, string content, Vector2 position) {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.sizeDelta = new Vector2(600, 80);
        text.rectTransform.anchoredPosition = position;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 30;
        text.alignment = TextAlignmentOptions.Center;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    /**公共字体显式登记，避免作为多个界面的隐式重复依赖*/
    private static void RegisterFonts() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("fonts") ?? settings.CreateGroup("fonts", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/Art/font" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) || Path.GetExtension(path) == ".md" || Path.GetExtension(path) == ".txt") {
                continue;
            }
            if (settings.FindAssetEntry(guid) == null) {
                settings.CreateOrMoveEntry(guid, group).address = path;
            }
        }
    }

}
