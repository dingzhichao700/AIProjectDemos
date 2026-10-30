using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证项目设计尺寸、局部弹窗和旧界面的安全内容布局。
/// </summary>
[InitializeOnLoad]
public static class UIDesignSizeValidation {

    static UIDesignSizeValidation() {
        EditorApplication.update += () => {
            if (File.Exists("Library/AIUI/validate-ui-design-size.flag") && !EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating) {
                File.Delete("Library/AIUI/validate-ui-design-size.flag");
                Run();
            }
        };
    }

    [MenuItem("Tools/Starfront/UI适配/验证设计尺寸")]
    public static void Run() {
        var previous = UIScreenRegions.EditorOverride;
        var canvasObject = new GameObject("DesignSizeValidation", typeof(RectTransform), typeof(Canvas));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var stage = new GameObject("stage", typeof(RectTransform)).GetComponent<RectTransform>();
        stage.SetParent(canvasObject.transform, false);
        stage.sizeDelta = new Vector2(720, 1280);
        try {
            var config = canvasObject.AddComponent<PortraitGameViewport>();
            var serialized = new SerializedObject(config);
            Require(config.DesignSize == new Vector2(720, 1280), "project portrait default");
            serialized.FindProperty("designSize").vector2Value = new Vector2(1280, 720);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(config.DesignSize == new Vector2(1280, 720), "landscape project configuration");
            string[] paths = {
                "opening/OpeningPanel", "title/loading/LoadingPanel", "title/option/OptionPanel",
                "title/setting/PerformanceMonitorView", "title/setting/SettingPanel", "title/warning/WarningPanel",
                "default/gm/GmPanel", "title/title/LoginPanel", "default/layereffect/LayerEffectTestPanel"
            };
            foreach (string path in paths) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/" + path + ".prefab");
                Require(prefab != null, path);
                var instance = UnityEngine.Object.Instantiate(prefab, stage, false);
                try {
                    var root = (RectTransform)instance.transform;
                    Vector2 size = new Vector2(720, 1280);
                    if (path.EndsWith("SettingPanel")) {
                        size = new Vector2(760, 640);
                    } else if (path.EndsWith("WarningPanel")) {
                        size = new Vector2(860, 340);
                    } else if (path.EndsWith("GmPanel")) {
                        size = root.rect.size;
                    }
                    string[] backgrounds = path.EndsWith("OpeningPanel") ? new[] { "bg" } : path.EndsWith("LoginPanel") ? new[] { "bg", "boxEff" } : path.EndsWith("OptionPanel") ? new[] { "bg", "imgMask", "imgPost" } : path.EndsWith("WarningPanel") ? new[] { "imgMask" } : Array.Empty<string>();
                    var fitter = instance.AddComponent<PanelSafeContent>();
                    foreach (float top in new[] { 1f, .88f }) {
                        UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(Screen.width * .04f, Screen.height * .03f, Screen.width * .92f, Screen.height * (top - .03f)) };
                        fitter.Initialize(backgrounds, size);
                        Canvas.ForceUpdateCanvases();
                        fitter.Apply();
                        Require(fitter.content.rect.size == size, path + " design size");
                        Inside(fitter.content, UIScreenRegions.LocalContentArea(stage), stage, path);
                        int children = fitter.content.childCount;
                        fitter.Initialize(backgrounds, size);
                        Require(fitter.content.childCount == children && instance.GetComponentsInChildren<PanelSafeContent>().Length == 1, path + " repeated initialization");
                    }
                    foreach (string key in new[] { "boxProcess", "listTab", "panelView", "imgBg", "grpStudioMark" }) {
                        var candidates = fitter.content.GetComponentsInChildren<RectTransform>(true);
                        foreach (var child in candidates) {
                            if (child.name == key) {
                                Inside(child, fitter.content.rect, fitter.content, path + "/" + key);
                            }
                        }
                    }
                } finally {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            // 旧横屏根节点也必须使用显式传入的项目基准，不能回读预制尺寸。
            var old = new GameObject("legacy", typeof(RectTransform));
            old.transform.SetParent(stage, false);
            ((RectTransform)old.transform).sizeDelta = new Vector2(1920, 1080);
            var test = old.AddComponent<PanelSafeContent>();
            foreach (var size in new[] { new Vector2(720, 1280), new Vector2(1280, 720) }) {
                test.Initialize(Array.Empty<string>(), size);
                Require(test.content.sizeDelta == size, "legacy root must not select orientation");
            }
            File.WriteAllText("Library/AIUI/ui-design-size-validation.txt", "passed: portrait/landscape configuration; 9 formal prefabs; safe-area bounds; content geometry; repeated initialization; legacy root ignored");
        } catch (Exception error) {
            File.WriteAllText("Library/AIUI/ui-design-size-validation.txt", "failed: " + error);
            Debug.LogException(error);
        } finally {
            UIScreenRegions.EditorOverride = previous;
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void Inside(RectTransform node, Rect area, RectTransform parent, string message) {
        var corners = new Vector3[4];
        node.GetWorldCorners(corners);
        foreach (var corner in corners) {
            var p = parent.InverseTransformPoint(corner);
            Require(p.x >= area.xMin - 1 && p.x <= area.xMax + 1 && p.y >= area.yMin - 1 && p.y <= area.yMax + 1, message + " outside " + area + ": " + p);
        }
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

}
