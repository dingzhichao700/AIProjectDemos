using System.Collections;
using UnityEngine;

public class AudioManager {

    private static AudioManager _ins;
    public static AudioManager ins {
        get {
            if (_ins == null) _ins = new AudioManager();
            return _ins;
        }
    }

    /**当前 BGM 淡入淡出协程*/
    private Coroutine _bgmFadeCoroutine = null;

    /**BGM 目标音量（始终由 PlaySound 传入，不再依赖 AudioSource.volume）*/
    private float _bgmTargetVolume = 1f;

    /**取消 BGM 淡入淡出*/
    private void CancelBgmFade() {
        var bus = GlobalAudioSources.Instance;
        if (bus == null) return;

        if (_bgmFadeCoroutine != null) {
            bus.StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = null;
        }
    }

    /**启动 BGM 淡入淡出*/
    private void StartBgmFade(IEnumerator routine) {
        var bus = GlobalAudioSources.Instance;
        if (bus == null || routine == null) return;

        CancelBgmFade();
        _bgmFadeCoroutine = bus.StartCoroutine(routine);
    }

    /**淡入*/
    private IEnumerator FadeIn(AudioSource src, float targetVolume, float duration) {
        float t = 0f;
        src.volume = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            if (src == null)
                yield break;
            src.volume = Mathf.Lerp(0f, targetVolume, t / duration);
            yield return null;
        }
        if (src != null)
            src.volume = targetVolume;
    }

    /**淡出并停止*/
    private IEnumerator FadeOutAndStop(AudioSource src, float startVolume, float duration) {
        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            if (src == null) yield break;
            src.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }
        if (src != null) {
            src.volume = 0f;
            src.Stop();
            src.clip = null;
            src.loop = false;
        }
    }

    /**淡出并暂停*/
    private IEnumerator FadeOutAndPause(AudioSource src, float startVolume, float duration) {
        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            if (src == null) yield break;
            src.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }
        if (src != null) {
            src.volume = 0f;
            src.Pause();
        }
    }

    /**淡入恢复*/
    private IEnumerator FadeInAfterResume(AudioSource src, float targetVolume, float duration) {
        float t = 0f;
        float start = src.volume;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            if (src == null) yield break;
            src.volume = Mathf.Lerp(start, targetVolume, t / duration);
            yield return null;
        }
        if (src != null)
            src.volume = targetVolume;
    }

    /**基于音效id播放音效*/
    public void PlaySoundById(AudioBusType type, int audioId, float volume = 1f, bool loop = false, bool fadeIn = false, float fadeDuration = 0.2f) {
        PlaySound(type, ResourceConst.GetAudioPathById(audioId), volume, loop, fadeIn, fadeDuration);
    }

    /**播放音效*/
    public void PlaySound(AudioBusType type, string audioPath, float volume = 1f, bool loop = false, bool fadeIn = false, float fadeDuration = 0.2f) {
        AudioClip clip = ResourceManager.GetAudioClip(audioPath);
        var bus = GlobalAudioSources.Instance;
        if (bus == null || clip == null)
            return;

        AudioSource src = bus.GetCommonAudioSource(type);
        if (src == null)
            return;

        if (type == AudioBusType.BGM) {
            CancelBgmFade();
            _bgmTargetVolume = volume;
            src.loop = loop;
            src.clip = clip;

            if (fadeIn && fadeDuration > 0f) {
                src.volume = 0f;
                src.Play();
                StartBgmFade(FadeIn(src, volume, fadeDuration));
            } else {
                src.volume = volume;
                src.Play();
            }
            return;
        }

        // 非 BGM：不支持淡入
        src.loop = loop;
        if (loop) {
            src.clip = clip;
            src.volume = volume;
            src.Play();
        } else {
            src.clip = null;
            src.volume = volume;
            src.PlayOneShot(clip);
        }
    }

    /**停止音效（只有 BGM 支持淡出）*/
    public void StopSound(AudioBusType type, bool fadeOut = false, float fadeDuration = 0.3f) {
        var bus = GlobalAudioSources.Instance;
        if (bus == null) return;

        AudioSource src = bus.GetCommonAudioSource(type);
        if (src == null) return;

        if (type != AudioBusType.BGM || !fadeOut || fadeDuration <= 0f) {
            CancelBgmFade();
            src.Stop();
            src.clip = null;
            src.loop = false;
            return;
        }

        float startVolume = src.volume;
        StartBgmFade(FadeOutAndStop(src, startVolume, fadeDuration));
    }

    /**暂停音效（只有 BGM 支持淡出）*/
    public void PauseSound(AudioBusType type, bool fadeOut = false, float fadeDuration = 0.3f) {
        var bus = GlobalAudioSources.Instance;
        if (bus == null) return;

        AudioSource src = bus.GetCommonAudioSource(type);
        if (src == null || !src.isPlaying) return;

        if (type != AudioBusType.BGM || !fadeOut || fadeDuration <= 0f) {
            CancelBgmFade();
            src.Pause();
            return;
        }

        float startVolume = src.volume;
        StartBgmFade(FadeOutAndPause(src, startVolume, fadeDuration));
    }

    /**恢复音效（只有 BGM 支持淡入）*/
    public void ResumeSound(AudioBusType type, bool fadeIn = false, float fadeDuration = 0.3f) {
        var bus = GlobalAudioSources.Instance;
        if (bus == null) return;

        AudioSource src = bus.GetCommonAudioSource(type);
        if (src == null) return;

        if (src.isPlaying) return;

        if (type != AudioBusType.BGM || !fadeIn || fadeDuration <= 0f) {
            CancelBgmFade();
            src.volume = _bgmTargetVolume;
            src.UnPause();
            return;
        }

        src.UnPause();
        StartBgmFade(FadeInAfterResume(src, _bgmTargetVolume, fadeDuration));
    }

    /// <summary>
    /// 设置音量
    /// </summary>
    /// <param name="type">混合器类型</param>
    /// <param name="volume">音量值</param>
    /// <param name="fadeDuration">淡出时长</param>
    public void SetVolume(AudioMixerType type, int volume, float fadeDuration = 0) {
        var bus = GlobalAudioSources.Instance;
        if (bus == null)
            return;

        string mixerName = "";
        switch (type) {
            case AudioMixerType.TOTAL:
                mixerName = "total";
                break;
            case AudioMixerType.BGM:
                mixerName = "bgm";
                break;
            case AudioMixerType.EFFECT:
                mixerName = "effect";
                break;
            case AudioMixerType.HINT:
                mixerName = "hint";
                break;
        }

        float targetVolume = (float)volume / 100;
        //Debug.Log("设置音量值：" + targetVolume);
        if (fadeDuration > 0) {
            FadeOutVolume(mixerName, targetVolume, fadeDuration);
        } else {
            float db = Mathf.Log10(Mathf.Clamp(targetVolume, 0.0001f, 1f)) * 20f;
            //Debug.Log("设置音量分贝：" + db);
            GlobalAudioSources.Instance.mixer.SetFloat(mixerName, db);
        }
    }

    /// <summary>
    /// 淡出某个混音器音量
    /// </summary>
    /// <param name="mixerName">目标声道类型</param>
    /// <param name="targetVolume">目标音量</param>
    /// <param name="duration">补间时长</param>
    /// <returns></returns>
    private IEnumerator FadeOutVolume(string mixerName, float targetVolume, float duration) {
        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            // linear: 0 ~ 1
            float volume = Mathf.Lerp(0f, targetVolume, t / duration);
            float db = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
            GlobalAudioSources.Instance.mixer.SetFloat(mixerName, db);
            yield return null;
        }
    }

    // ====================== BGM API ======================

    private string _currentBgmPath = "";
    public string CurrentBgmPath => _currentBgmPath;

    /**通过音效id播放 BGM*/
    public void PlayBgmById(int audioId, float volume = 1f, bool restartIfSame = false, bool fadeIn = true, float fadeDuration = 1f) {
        PlayBgm(ResourceConst.GetAudioPathById(audioId), volume, restartIfSame, fadeIn, fadeDuration);
    }

    /**播放 BGM*/
    public void PlayBgm(string audioPath, float volume = 1f, bool restartIfSame = false, bool fadeIn = true, float fadeDuration = 1f) {
        if (!restartIfSame && _currentBgmPath == audioPath) return;
        _currentBgmPath = audioPath;
        PlaySound(AudioBusType.BGM, audioPath, volume, true, fadeIn, fadeDuration);
    }

    /**停止 BGM*/
    public void StopBgm(bool fadeOut = true, float fadeDuration = 1f) {
        StopSound(AudioBusType.BGM, fadeOut, fadeDuration);
        _currentBgmPath = "";
    }

    /**暂停 BGM*/
    public void PauseBgm(bool fadeOut = true, float fadeDuration = 0.3f) {
        PauseSound(AudioBusType.BGM, fadeOut, fadeDuration);
    }

    /**恢复 BGM*/
    public void ResumeBgm(bool fadeIn = true, float fadeDuration = 0.3f) {
        ResumeSound(AudioBusType.BGM, fadeIn, fadeDuration);
    }

    /**当前 BGM 是否在播放*/
    public bool IsBgmPlaying() {
        var bus = GlobalAudioSources.Instance;
        if (bus == null) return false;
        var src = bus.GetCommonAudioSource(AudioBusType.BGM);
        return src != null && src.isPlaying;
    }
}
