using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 已批准图层测试界面的定向组件设置与验证入口。
/// </summary>
/// <remarks>复用现有导出器和UIBinder生成器；不修改通用编辑器队列。</remarks>
[InitializeOnLoad]
public static class LayerEffectTestSetup {

    private const string PrefabPath = "Assets/Prefab/default/layereffect/LayerEffectTestPanel.prefab";

    private const string CodePath = "Assets/Scripts/csharp/main/layereffect/view/LayerEffectTestPanel.cs";

    [Serializable] private class Request {
        public string requestId;
        public int processId;
        public string action;
    }

    [Serializable] private class Member {
        public string id;
        public string role;
    }

    [Serializable] private class Policy {
        public Member[] members;
    }

    static LayerEffectTestSetup() {
        EditorApplication.update += Poll;
    }

    private static void Poll() {
        const string requestPath = "Library/layer-effect-request.json";
        if (!File.Exists(requestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        Request request = null;
        string status = "passed", error = "";
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            File.Delete(requestPath);
            if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("Invalid request identity or editor PID.");
            }
            if (request.action == "extendHoles") {
                ExtendHolePrototype();
            } else if (request.action == "import") {
                ImportAssets();
            } else if (request.action == "configure") {
                Configure();
            } else if (request.action == "prepareAtlas") {
                var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
                try {
                    prefab.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/atlasSource/default/layereffect/solidWhite.png");
                    PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                } finally {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            } else if (request.action == "finalize") {
                FinalizeAssets();
            } else if (request.action == "build") {
                AddressablesAutoBuilder.BuildPlayerContent();
            } else if (request.action == "validate") {
                File.WriteAllText("Library/layer-effect-validation-id.txt", request.requestId);
                LayerEffectTestValidation.Start();
            } else if (request.action == "removeMainEntry") {
                RemoveMainEntry();
            } else {
                throw new InvalidOperationException("Unknown action.");
            }
        } catch (Exception exception) {
            status = "failed";
            error = exception.ToString();
            Debug.LogException(exception);
        }
        File.WriteAllText("Library/layer-effect-result.json", "{\"requestId\":\"" + request?.requestId + "\",\"status\":\"" + status + "\",\"error\":\"" + error.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"}");
    }

    /**测试入口迁入GM，移除仅由本轮创建的主界面按钮与脚本。*/
    private static void RemoveMainEntry() {
        string mainPath = ResourceConst.GetUIPath(UIEnum.MAIN_PANEL);
        var root = PrefabUtility.LoadPrefabContents(mainPath);
        try {
            var button = root.transform.Find("btnLayerEffectTest");
            if (button != null) {
                UnityEngine.Object.DestroyImmediate(button.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, mainPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.DeleteAsset("Assets/Scripts/csharp/main/layereffect/view/LayerEffectTestEntry.cs");
        AssetDatabase.SaveAssets();
    }

    /**在现有弹窗的空白行复用标准按钮，并由既有生成器追加两个UI成员。*/
    private static void ExtendHolePrototype() {
        const string sourcePath = "Assets/Art/unpack/default/layereffect/terrainNeutral.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            var nodes = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(n => n.name);
            if (!nodes.ContainsKey("btnToggleGrid")) {
                var go = UnityEngine.Object.Instantiate(nodes["btnGridCancel"].gameObject, nodes["boxGridSettings"], false);
                go.name = "btnToggleGrid";
                var rect = (RectTransform)go.transform;
                rect.anchoredPosition = new Vector2(104, -748);
                go.GetComponentInChildren<Image>(true).name = "btnToggleGridBg";
                var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
                text.name = "txtGridToggleLabel";
                text.text = "格线：开启";
                var binder = root.GetComponent<UIBinder>();
                binder.uiList.Add(new UIBindComponentData { id = binder.uiList.Max(n => n.id) + 1, uiName = "btnToggleGrid", uiTypeName = "Button", go = go });
                binder.uiList.Add(new UIBindComponentData { id = binder.uiList.Max(n => n.id) + 1, uiName = "txtGridToggleLabel", uiTypeName = "TextMeshProUGUI", go = text.gameObject });
                var inspector = Editor.CreateEditor(binder);
                try {
                    inspector.GetType().GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, new object[] { CodePath });
                } finally {
                    UnityEngine.Object.DestroyImmediate(inspector);
                }
                binder.RefreshScriptBinding();
            }
            var serialized = new SerializedObject(root.GetComponent<LayerEffectTestRenderer>());
            serialized.FindProperty("source").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            nodes["btnClearPaint"].GetComponentInChildren<TextMeshProUGUI>(true).text = "清除编辑";
            nodes["txtHint"].GetComponent<TextMeshProUGUI>().text = "点击画笔切换染色/挖洞/还原；右侧预览支持拖动。";
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                foreach (char character in text.text + "格线开启关闭整格局部挖洞还原") {
                    if (!char.IsWhiteSpace(character) && !text.font.HasCharacter(character, true, true)) {
                        throw new InvalidOperationException("Missing prototype glyph: " + character);
                    }
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ImportAssets() {
        foreach (string file in Directory.GetFiles("Assets/Art/atlasSource/default/layereffect", "*.png")) {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var source = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/unpack/default/layereffect/terrainSource.png");
        source.textureType = TextureImporterType.Default;
        source.mipmapEnabled = false;
        source.isReadable = false;
        source.maxTextureSize = 2048;
        source.textureCompression = TextureImporterCompression.Uncompressed;
        source.SaveAndReimport();
    }

    /**只修正本轮控件所需组件，布局和独立文字框沿用批准结构。*/
    private static void Configure() {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            var nodes = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(n => n.name);
            var background = root.AddComponent<Image>();
            ColorUtility.TryParseHtmlString("#081722", out Color dark);
            background.color = dark;
            background.raycastTarget = true;
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                text.fontStyle = FontStyles.Normal;
                text.alignment = text.alignment == TextAlignmentOptions.MidlineLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline;
            }
            var policy = JsonUtility.FromJson<Policy>("{\"members\":" + File.ReadAllText("Library/layer-effect-policy.json") + "}");
            foreach (var member in policy.members) {
                GameObject go = nodes[member.id].gameObject;
                if (member.role == "Button") {
                    var button = go.AddComponent<Button>();
                    button.targetGraphic = nodes[member.id + "Bg"].GetComponent<Image>();
                    button.targetGraphic.raycastTarget = true;
                    button.transition = Selectable.Transition.None;
                } else if (member.role == "TMP_InputField") {
                    var field = go.AddComponent<TMP_InputField>();
                    var text = nodes[member.id + "Value"].GetComponent<TextMeshProUGUI>();
                    field.textViewport = nodes[member.id];
                    field.textComponent = text;
                    field.targetGraphic = nodes[member.id + "Bg"].GetComponent<Image>();
                    field.targetGraphic.raycastTarget = true;
                    field.text = text.text;
                    field.lineType = TMP_InputField.LineType.SingleLine;
                    field.characterLimit = member.id.Contains("Hex") ? 7 : 8;
                    field.contentType = TMP_InputField.ContentType.Standard;
                    field.transition = Selectable.Transition.None;
                } else if (member.role == "RawImage") {
                    var raw = go.AddComponent<RawImage>();
                    raw.raycastTarget = member.id == "rawResultPreview";
                    if (raw.raycastTarget) {
                        go.AddComponent<LayerEffectPaintInput>();
                    }
                } else if (member.role == "Slider") {
                    string suffix = member.id.Substring(6);
                    var slider = go.AddComponent<Slider>();
                    var area = new GameObject("box" + suffix + "Travel", typeof(RectTransform)).GetComponent<RectTransform>();
                    area.SetParent(go.transform, false);
                    area.anchorMin = area.anchorMax = area.pivot = new Vector2(0, 1);
                    area.anchoredPosition = new Vector2(12, -12);
                    area.sizeDelta = new Vector2(442, 24);
                    var handle = nodes["img" + suffix + "Handle"];
                    handle.SetParent(area, false);
                    handle.pivot = new Vector2(.5f, .5f);
                    handle.sizeDelta = new Vector2(24, 24);
                    handle.anchoredPosition = Vector2.zero;
                    slider.handleRect = handle;
                    slider.targetGraphic = handle.GetComponent<Image>();
                    slider.targetGraphic.raycastTarget = true;
                    var hit = go.AddComponent<Image>();
                    hit.color = Color.clear;
                    hit.raycastTarget = true;
                    slider.minValue = suffix == "Strength" ? 0 : .25f;
                    slider.maxValue = suffix == "Strength" ? 1 : 2.5f;
                    slider.SetValueWithoutNotify(suffix == "Strength" ? .85f : suffix == "Brightness" ? 1.25f : 1.15f);
                    slider.transition = Selectable.Transition.None;
                }
            }
            foreach (string name in new[] { "imgGrayChecker", "imgResultChecker" }) {
                if (nodes.TryGetValue(name, out RectTransform rect)) {
                    rect.GetComponent<Image>().type = Image.Type.Tiled;
                }
            }
            nodes["imgGridModalShade"].GetComponent<Image>().raycastTarget = true;
            var renderer = root.AddComponent<LayerEffectTestRenderer>();
            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("source").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/unpack/default/layereffect/terrainSource.png");
            serialized.FindProperty("shader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/shader/LayerEffectTest.shader");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var binder = root.AddComponent<UIBinder>();
            binder.csharpAsset = binder.csharpAssetPath = CodePath;
            binder.uiList = new List<UIBindComponentData>();
            foreach (var member in policy.members) {
                GameObject go = nodes[member.id].gameObject;
                if (go.GetComponent(member.role) == null) {
                    throw new InvalidOperationException("Missing component: " + member.id + " " + member.role);
                }
                binder.uiList.Add(new UIBindComponentData { id = binder.uiList.Count + 1, uiName = member.id, uiTypeName = member.role, go = go, isCustomClass = member.role == "TMP_InputField", customClassName = member.role == "TMP_InputField" ? "TMP_InputField" : "" });
            }
            binder.RefreshScriptBinding();
            var inspector = Editor.CreateEditor(binder);
            try {
                inspector.GetType().GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, new object[] { CodePath });
            } finally {
                UnityEngine.Object.DestroyImmediate(inspector);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /**图集打包后同步动态按钮贴图引用，入口走主界面和PanelMgr。*/
    private static void FinalizeAssets() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("Default Local Group");
        if (group == null) {
            throw new InvalidOperationException("Default Local Group missing.");
        }
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), group);
        entry.address = PrefabPath;
        EditorUtility.SetDirty(settings);
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/atlas/default/layereffect.png").OfType<Sprite>().ToDictionary(s => s.name);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            var renderer = root.GetComponent<LayerEffectTestRenderer>();
            renderer.neutralPlate = sprites["plateNeutral"];
            renderer.selectedPlate = sprites["plateSelected"];
            foreach (var slider in root.GetComponentsInChildren<Slider>(true)) {
                var handle = slider.handleRect;
                handle.anchorMin = handle.anchorMax = new Vector2(slider.normalizedValue, .5f);
                handle.anchoredPosition = Vector2.zero;
                // Slider会沿非移动轴拉伸；父容器已为24高，额外高度必须为0。
                handle.sizeDelta = new Vector2(24, 0);
            }
            foreach (var graphic in root.GetComponentsInChildren<Image>(true)) {
                if (graphic.name.StartsWith("img") && graphic.name.EndsWith("Checker")) {
                    graphic.type = Image.Type.Tiled;
                    graphic.pixelsPerUnitMultiplier = 1;
                }
            }
            if (root.GetComponent<UIBinder>().uiList.Count != 35 || root.GetComponent<LayerEffectTestPanel>() != null) {
                throw new InvalidOperationException("Binding count or root component mismatch.");
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

}
