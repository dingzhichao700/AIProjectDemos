using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 通过真实窗口验证安全区、胶囊避让及恢复，模拟不写入玩家数据。
/// </summary>
public static class UISafeAreaValidation {

    public static void ValidateMain(MainPanel panel) {
        var previous = UIScreenRegions.EditorOverride;
        var layout = panel.GetComponent<MainPanelLayout>();
        var background = layout.background.rect.size;
        try {
            ValidateGeometry();
            foreach (var safe in Samples()) {
                UIScreenRegions.EditorOverride = safe;
                layout.ApplyLayout();
                Canvas.ForceUpdateCanvases();
                Inside(layout.contentViewport, UIScreenRegions.ContentArea, "main content");
                Inside(layout.navigation, UIScreenRegions.ContentArea, "main navigation");
                Require(layout.background.rect.size == background, "background must not shrink with safe area");
                Require(Mathf.Abs(layout.deckContent.rect.width - 720) < .1f && Mathf.Abs(layout.deckContent.rect.height - 1130) < .1f, "deck design remains 720x1280 including navigation");
            }
            // 普通窗口使用真实正式预制体，覆盖绑定、动态内容及重复打开。
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/default/gm/GmPanel.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab, panel.transform.parent, false);
            try {
                var fitter = instance.AddComponent<PanelSafeContent>();
                fitter.Initialize(Array.Empty<string>(), ((RectTransform)instance.transform).rect.size);
                Inside(fitter.content, UIScreenRegions.ContentArea, "standard panel content");
                int childCount = fitter.content.childCount;
                instance.SetActive(false);
                instance.SetActive(true);
                fitter.Initialize(Array.Empty<string>(), ((RectTransform)instance.transform).rect.size);
                Require(instance.GetComponents<PanelSafeContent>().Length == 1 && fitter.content.childCount == childCount, "reuse must not duplicate wrapper");
            } finally {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        } finally {
            UIScreenRegions.EditorOverride = previous;
            layout.ApplyLayout();
            Canvas.ForceUpdateCanvases();
        }
        File.WriteAllText("Library/ui-safe-area-main-validation.txt", "passed: safe/capsule coordinate matrix, invalid fallback, 720x1280 deck fit, background unchanged, formal GM prefab safe bounds and repeated initialization");
    }

    public static void ValidateHud(BattleScenePanel scene, BattleInfoPanel hud) {
        var previous = UIScreenRegions.EditorOverride;
        var fitter = hud.GetComponentInChildren<SafeAreaFitter>();
        var viewport = scene.GetComponent<BattleSceneView>().viewport;
        var size = viewport.rect.size;
        try {
            foreach (var safe in Samples()) {
                UIScreenRegions.EditorOverride = safe;
                fitter.Apply();
                Canvas.ForceUpdateCanvases();
                Inside((RectTransform)fitter.transform, UIScreenRegions.ContentArea, "HUD safe root");
                foreach (RectTransform child in fitter.transform) {
                    Inside(child, UIScreenRegions.ContentArea, "HUD " + child.name);
                }
                Require(viewport.rect.size == size, "battle viewport never shrinks");
            }
        } finally {
            UIScreenRegions.EditorOverride = previous;
            fitter.Apply();
            Canvas.ForceUpdateCanvases();
        }
        File.WriteAllText("Library/ui-safe-area-hud-validation.txt", "passed: device only/capsule only/combined/side inset; actual HUD widget corners inside safe area; battle viewport unchanged; override restored");
    }

    private static UIScreenRegions.Regions[] Samples() {
        float w = Screen.width;
        float h = Screen.height;
        return new[] {
            new UIScreenRegions.Regions { safeArea = new Rect(0, 0, w, h) },
            new UIScreenRegions.Regions { safeArea = new Rect(0, h * .03f, w, h * .92f) },
            new UIScreenRegions.Regions { safeArea = new Rect(0, 0, w, h), menu = new Rect(w * .72f, h * .88f, w * .25f, h * .06f) },
            new UIScreenRegions.Regions { safeArea = new Rect(w * .04f, h * .03f, w * .92f, h * .92f), menu = new Rect(w * .72f, h * .88f, w * .25f, h * .06f) }
        };
    }

    private static void ValidateGeometry() {
        foreach (Vector2 size in new[] { new Vector2(720, 1280), new Vector2(1320, 2848), new Vector2(1080, 2400), new Vector2(1536, 2048) }) {
            var screen = new Rect(Vector2.zero, size);
            var area = UIScreenRegions.FromTopLeft(size / 3, size, new Rect(0, 20, size.x / 3, size.y / 3 - 30));
            Require(Mathf.Abs(area.yMin - 30) < .01f && Mathf.Abs(area.yMax - (size.y - 60)) < .01f, "pixel ratio and Y direction");
            var menu = new Rect(size.x - 200, size.y - 140, 180, 70);
            var safe = UIScreenRegions.Resolve(screen, area, menu);
            Require(Mathf.Abs(safe.yMax - menu.yMin) < .01f, "device and menu use max inset, not sum");
            Require(UIScreenRegions.Resolve(screen, Rect.zero, Rect.zero) == screen, "missing platform rectangles fall back");
        }
    }

    private static void Inside(RectTransform rect, Rect safe, string message) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        foreach (var corner in corners) {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(camera, corner);
            Require(p.x >= safe.xMin - 1 && p.x <= safe.xMax + 1 && p.y >= safe.yMin - 1 && p.y <= safe.yMax + 1, message + " outside safe area: " + p + " / " + safe);
        }
    }

    private static void Require(bool value, string message) {
        if (!value) {
            throw new InvalidOperationException("UI safe area: " + message);
        }
    }

}
