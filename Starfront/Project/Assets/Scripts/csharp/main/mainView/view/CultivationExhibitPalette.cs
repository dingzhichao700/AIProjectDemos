using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 培养展台的独立品质光效与立绘激活表现；不持有成长状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class CultivationExhibitPalette : MonoBehaviour {

    public Sprite normalGlow;
    public Sprite rareGlow;
    public Sprite epicGlow;
    public Sprite legendGlow;
    public Material inactivePortraitMaterial;

    [Header("蓝色局部脉冲试装；关闭后恢复原静态光效")]
    public bool previewRarePulse;
    public string rarePulseAnimation;

    [Header("其他品质局部脉冲；暂准版本，可独立回退静态")]
    public bool previewOtherQualityPulses;
    public string normalPulseAnimation;
    public string epicPulseAnimation;
    public string legendPulseAnimation;

    private string PulsePath(cfg.Quality quality) {
        if (quality == cfg.Quality.RARE) {
            return previewRarePulse ? rarePulseAnimation : null;
        }
        if (!previewOtherQualityPulses) { return null; }
        switch (quality) {
            case cfg.Quality.NORMAL:
            case cfg.Quality.GREAT: return normalPulseAnimation;
            case cfg.Quality.EPIC: return epicPulseAnimation;
            case cfg.Quality.LEGEND:
            case cfg.Quality.LEGEND_PLUS: return legendPulseAnimation;
            default: return null;
        }
    }

    private FrameAnimationView pulse;
    private string requestedPulse;
    private int pulseVersion;

    public Sprite GetGlow(cfg.Quality quality) {
        switch (quality) {
            case cfg.Quality.NORMAL:
            case cfg.Quality.GREAT:
                return normalGlow;
            case cfg.Quality.RARE:
                return rareGlow;
            case cfg.Quality.EPIC:
                return epicGlow;
            case cfg.Quality.LEGEND:
            case cfg.Quality.LEGEND_PLUS:
                return legendGlow;
            default:
                throw new ArgumentOutOfRangeException(nameof(quality));
        }
    }

    /// <summary>固定座台不参与切换；各品质光效复用现有帧动画链。</summary>
    public void Apply(Image glow, Image portrait, cfg.Quality quality, bool owned) {
        glow.sprite = owned ? GetGlow(quality) : null;
        glow.enabled = owned && glow.sprite != null;
        glow.color = Color.white;
        glow.material = null;
        portrait.color = Color.white;
        portrait.material = owned ? null : inactivePortraitMaterial;
        string animationPath = owned ? PulsePath(quality) : null;
        if (!string.IsNullOrWhiteSpace(animationPath)) {
            StartPulse(glow, animationPath);
        } else {
            StopPulse();
        }
    }

    /// <summary>加载时保留静态回退；切卡/关闭使旧回调失效，同次刷新不重复启动。</summary>
    private async void StartPulse(Image glow, string animationPath) {
        if (pulse != null && pulse.path == animationPath) {
            glow.enabled = false;
            return;
        }
        if (requestedPulse == animationPath) {
            return;
        }
        StopPulse();
        int token = ++pulseVersion;
        string path = requestedPulse = animationPath;
        try {
            var data = await FrameAnimationManager.LoadFrameAnimationResAsync(path);
            if (this == null || !isActiveAndEnabled || token != pulseVersion || requestedPulse != path) {
                return;
            }
            pulse = FrameAnimationView.GetInstance();
            pulse.trans.SetParent(glow.rectTransform, false);
            pulse.trans.anchorMin = pulse.trans.anchorMax = new Vector2(.5f, .5f);
            pulse.trans.anchoredPosition = Vector2.zero;
            float scale = glow.rectTransform.rect.width / data.sourceSizes[0].x;
            pulse.Play(path, loop: true, recoverAfterPlay: false, scale: scale, timerType: TimerType.COMMON);
            glow.enabled = false;
        } catch (Exception error) {
            if (this != null && token == pulseVersion) {
                requestedPulse = null;
                Debug.LogWarning("培养品质脉冲加载失败，保留静态光效：" + error.GetType().Name);
            }
        }
    }

    private void StopPulse() {
        pulseVersion++;
        requestedPulse = null;
        if (pulse != null) {
            pulse.Recover();
            pulse = null;
        }
    }

    public void ResetState(Image glow, Image portrait) {
        StopPulse();
        glow.enabled = false;
        glow.sprite = null;
        portrait.material = null;
        portrait.color = Color.white;
    }

    private void OnDisable() {
        StopPulse();
    }
}
