using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 混音器类型
/// </summary>
public enum AudioMixerType {
    TOTAL,      // 总声道
    BGM,        // 背景声道
    EFFECT,     // 效果声道
    HINT,       // 提示声道
}

public enum AudioBusType {
    BGM,        // 背景音效
    EFFECT,     // 效果音效
    DIALOGUE,   // 对话音效
    HINT,      // 提示音效（UI提示音、按钮音效等）
}

/// <summary>
/// 全局声道
/// </summary>
public class GlobalAudioSources : MonoBehaviour {

    [Header("全局混合器 AudioMixerGroup")]
    public AudioMixer mixer;

    [Header("全局声道 AudioSources")]
    public AudioSource sourceBgm;
    public AudioSource sourceDialogue;
    public AudioSource sourceHint;

    public static GlobalAudioSources Instance { get; private set; }

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /**根据类型获取公共播放源*/
    public AudioSource GetCommonAudioSource(AudioBusType type) {
        switch (type) {
            case AudioBusType.BGM: return sourceBgm;
            case AudioBusType.DIALOGUE: return sourceDialogue;
            case AudioBusType.HINT: return sourceHint;
            default: return null;
        }
    }

}
