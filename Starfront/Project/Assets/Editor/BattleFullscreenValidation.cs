using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 全屏尺寸、安全区换算与镜头坐标回归。
/// </summary>
public static class BattleFullscreenValidation {

    public static void Validate(BattleScenePanel panel, BattleInfoPanel hud) {
        Vector2 design = new Vector2(720, 1280);
        foreach (Vector2 screen in new[] { design, new Vector2(1320, 2848), new Vector2(1080, 2400), new Vector2(1536, 2048) }) {
            var size = PortraitGameViewport.FullScreenDesignSize(screen, design);
            var fit = PortraitGameViewport.FitRect(screen, size);
            Require(fit.position.sqrMagnitude < .01f && Vector2.Distance(fit.size, screen) < .01f, "full screen without letterbox");
            var camera = new BattleSceneCamera(new Vector2(2000, 2000), new Vector2(1000, 1000));
            camera.Resize(size);
            var point = new Vector2(87, -123);
            Require(Vector2.Distance(camera.SceneToViewport(camera.ViewportToScene(point)), point) < .01f, "click coordinate round trip");
            camera.Pan(new Vector2(9999, 9999));
            Require(camera.visibleRect.xMin >= -.01f && camera.visibleRect.yMax <= 2000.01f, "drag limits use expanded viewport");
        }
        var safe = BattleHudSafeArea.ConvertSafeArea(new Vector2(440, 949), new Vector2(1320, 2847), new Rect(0, 90, 440, 825));
        Require(Mathf.Abs(safe.yMin - 102) < .01f && Mathf.Abs(safe.yMax - 2577) < .01f, "top-left safe/capsule pixels converted once");
        var view = panel.GetComponent<BattleSceneView>();
        Require(panel.UsesFullScreenViewport && hud.UsesFullScreenViewport, "only explicit opt-in panels");
        var corners = new Vector3[4];
        view.viewport.GetWorldCorners(corners);
        var canvas = view.viewport.GetComponentInParent<Canvas>();
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Require(min.magnitude < 2 && Vector2.Distance(max, new Vector2(Screen.width, Screen.height)) < 2, "live battle viewport covers screen: min=" + min + ", max=" + max + ", screen=" + Screen.width + "x" + Screen.height + ", rect=" + view.viewport.rect + ", root=" + panel.trans.rect);
        Require(hud.transform.Find("hudSafeArea/barTerritory") != null && hud.transform.Find("hudSafeArea/barCurrency") != null, "HUD anchors inside separate safe root");
        UISafeAreaValidation.ValidateHud(panel, hud);
        File.WriteAllText("Library/battle-fullscreen-validation.txt", "passed: 720x1280/1320x2848/1080x2400/1536x2048; full fit; camera coordinate roundtrip and limits; safe coordinates; live viewport corners and HUD hierarchy");
    }

    private static void Require(bool value, string message) {
        if (!value) {
            throw new InvalidOperationException("Full screen: " + message);
        }
    }
}
