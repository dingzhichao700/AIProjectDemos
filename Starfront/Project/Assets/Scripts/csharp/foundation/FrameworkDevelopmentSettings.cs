using System;
using UnityEngine;

/// <summary>
/// 构建固化的客户端开发入口设置。
/// </summary>
/// <remarks>Editor读取同一工程构建配置；Player只读取构建场景中的序列化值，不读取玩家存档。</remarks>
public sealed class FrameworkDevelopmentSettings : MonoBehaviour {

    [SerializeField] private bool enableGM;

    private static FrameworkDevelopmentSettings current;

    public static bool GMEnabled { get; private set; }

    public static event Action Changed;

    [Serializable]
    private sealed class Config {

        public bool enableGM;

    }

    private void Awake() {
        current = this;
        Reload();
    }

    /**没有配置、非法配置和普通包均保持关闭，不以Editor或DevelopmentBuild绕过。*/
    public static void Reload() {
        bool enabled = current != null && current.enableGM;
#if UNITY_EDITOR
        enabled = false;
        const string path = "ProjectSettings/FrameworkPlatform.json";
        try {
            if (System.IO.File.Exists(path)) {
                enabled = JsonUtility.FromJson<Config>(System.IO.File.ReadAllText(path)).enableGM;
            }
        } catch (Exception exception) {
            Debug.LogWarning("开发入口配置无效，GM保持关闭：" + exception.Message);
        }
#endif
        if (GMEnabled != enabled) {
            GMEnabled = enabled;
            Changed?.Invoke();
        }
    }

    private void OnDestroy() {
        if (current != this) {
            return;
        }
        current = null;
        GMEnabled = false;
        Changed?.Invoke();
    }

}
