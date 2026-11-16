using TMPro;
using UnityEngine;

/// <summary>
/// 可复用的单行滚动提示条目。
/// </summary>
/// <remarks>独立TMP文字与渐变背景；回池清理消息、时序和透明度。</remarks>
public sealed class GlobalToastItem : BaseView {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtMessage;
    /******************* UIComponent Define finish ************************/

    private CanvasGroup opacity;

    public string Message { get; private set; }

    public bool Warning { get; private set; }

    public float Age { get; private set; }

    private bool laneInitialized;

    private float laneFrom;

    private float laneTarget;

    private float laneElapsed;

    private float lifeOffset;

    public override void OnInit() {
        opacity = GetComponent<CanvasGroup>();
        opacity.blocksRaycasts = false;
        opacity.interactable = false;
        txtMessage.raycastTarget = false;
    }

    /**每次取出完全恢复基准；测量26号字后确定底板宽度，超宽交给TMP缩字及省略。*/
    public void Present(string message, bool warning) {
        Message = message;
        Warning = warning;
        Age = 0;
        laneInitialized = false;
        laneFrom = laneTarget = laneElapsed = 0;
        lifeOffset = -20;
        Trans.anchoredPosition = Vector2.zero;
        Trans.localScale = Vector3.one;
        txtMessage.text = message;
        txtMessage.color = warning ? new Color(1, 220f / 255, 140f / 255) : Color.white;
        float preferred = txtMessage.GetPreferredValues(message, Mathf.Infinity, 44).x;
        Trans.sizeDelta = new Vector2(Mathf.Clamp(Mathf.Ceil(preferred) + 104, 300, 640), 44);
        opacity.alpha = 0;
        txtMessage.ForceMeshUpdate();
    }

    /// <summary>
    /// 槽位挤开与自身入退场分别推进，连续改目标从当前显示位置接续。
    /// </summary>
    /// <remarks>最大Y来自上方条目实际位置，让新条先在下方入场，不穿过尚未让开的旧条。</remarks>
    public void Advance(float seconds, float laneY, float maximumY = float.PositiveInfinity) {
        if (!laneInitialized) {
            laneInitialized = true;
            laneFrom = laneTarget = laneY;
            laneElapsed = GlobalToast.ReflowSeconds;
        } else if (!Mathf.Approximately(laneTarget, laneY)) {
            // 当前坐标已包含上一帧的间距约束，扣除自己的生命周期偏移后作为新起点。
            laneFrom = Trans.anchoredPosition.y - lifeOffset;
            laneTarget = laneY;
            laneElapsed = 0;
        }
        float delta = Mathf.Max(0, seconds);
        Age += delta;
        laneElapsed = Mathf.Min(GlobalToast.ReflowSeconds, laneElapsed + delta);
        float slide = laneElapsed / GlobalToast.ReflowSeconds;
        slide = 1 - (1 - slide) * (1 - slide);
        float lane = Mathf.Lerp(laneFrom, laneTarget, slide);
        if (Age < GlobalToast.EnterSeconds) {
            float progress = 1 - Mathf.Pow(1 - Age / GlobalToast.EnterSeconds, 2);
            lifeOffset = -20 * (1 - progress);
            opacity.alpha = progress;
        } else if (Age < GlobalToast.EnterSeconds + GlobalToast.HoldSeconds) {
            lifeOffset = 0;
            opacity.alpha = 1;
        } else {
            float progress = Mathf.Clamp01((Age - GlobalToast.EnterSeconds - GlobalToast.HoldSeconds) / GlobalToast.ExitSeconds);
            progress *= progress;
            lifeOffset = 28 * progress;
            opacity.alpha = 1 - progress;
        }
        Trans.anchoredPosition = new Vector2(0, Mathf.Min(lane + lifeOffset, maximumY));
    }

    public void ResetForPool() {
        Message = null;
        Warning = false;
        Age = 0;
        laneInitialized = false;
        laneFrom = laneTarget = laneElapsed = lifeOffset = 0;
        txtMessage.text = "";
        txtMessage.color = Color.white;
        opacity.alpha = 0;
        Trans.anchoredPosition = Vector2.zero;
        gameObject.SetActive(false);
    }

}
