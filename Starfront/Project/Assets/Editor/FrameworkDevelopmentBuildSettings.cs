using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将工程构建开关固化到入口场景，覆盖普通、测试及批处理构建。
/// </summary>
/// <remarks>只处理构建场景副本，不保存源场景，也不自动发起构建。</remarks>
public sealed class FrameworkDevelopmentBuildSettings : IProcessSceneWithReport {

    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report) {
        if (report == null) {
            return;
        }
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (RookieEngine engine in root.GetComponentsInChildren<RookieEngine>(true)) {
                var settings = engine.GetComponent<FrameworkDevelopmentSettings>();
                if (settings == null) {
                    settings = engine.gameObject.AddComponent<FrameworkDevelopmentSettings>();
                }
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("enableGM").boolValue = FrameworkPlatformConfig.Read().enableGM;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

}
