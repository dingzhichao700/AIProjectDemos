using UnityEditor;
using UnityEngine;

/// <summary>
/// 在当前 Game 窗口模拟设备安全区和胶囊，测试结束可一键恢复。
/// </summary>
public static class UISafeAreaPreview {

    [MenuItem("Tools/Starfront/UI适配/模拟刘海与胶囊")]
    private static void Simulate() {
        float w = Screen.width;
        float h = Screen.height;
        UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(w * .04f, h * .03f, w * .92f, h * .92f), menu = new Rect(w * .72f, h * .88f, w * .25f, h * .06f) };
        EditorApplication.QueuePlayerLoopUpdate();
    }

    [MenuItem("Tools/Starfront/UI适配/恢复真实安全区")]
    private static void Reset() {
        UIScreenRegions.EditorOverride = null;
        EditorApplication.QueuePlayerLoopUpdate();
    }

}
