using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>原生二维展示骨架的轻微原地待机，不决定业务状态。</summary>
/// <remarks>角色参数由Prefab保存；使用公共Timer，暂停保留本实例相位，关闭注销监听。</remarks>
public sealed class MarineIdleRig : MonoBehaviour {

    public SpriteSkin skin;

    public Transform pelvis;

    public Transform chest;

    public Transform head;

    public float durationSeconds = 4f;

    public float pelvisDegrees = 0.8f;

    public float chestDegrees = -0.4f;

    public float headDegrees = 0.45f;

    public float chestLift = 0.012f;

    public bool paused;

    public float speed = 1f;

    private Vector3 chestRest;

    private Timer clock;

    private float elapsed;

    /**本实例累计时间，用于界面生命周期和循环边界复核。*/
    public float Elapsed => elapsed;

    private void Awake() {
        chestRest = chest.localPosition;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true)) {
            // SpriteSkin负责原生变形，像素只交现有uGUI Image绘制，不增加场景精灵覆盖层。
            renderer.forceRenderingOff = true;
        }
    }

    private void OnEnable() {
        elapsed = 0;
        paused = false;
        speed = 1;
        clock = RookieEngine.GetTimer(TimerType.COMMON);
        if (clock != null) {
            clock.AddUpdateListener(this, Advance);
        }
        ApplyPose();
    }

    private void OnDisable() {
        if (clock != null) {
            clock.RemoveUpdateListener(this, Advance);
            clock = null;
        }
    }

    private void Advance(float deltaSeconds) {
        if (!paused && deltaSeconds > 0) {
            elapsed = Mathf.Repeat(elapsed + deltaSeconds * Mathf.Max(0, speed), durationSeconds);
            ApplyPose();
        }
    }

    /**只改变视觉子骨骼，外部锚点与脚部根不参与待机位移。*/
    public void ApplyPose() {
        float phase = elapsed * Mathf.PI * 2f / durationSeconds;
        pelvis.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * pelvisDegrees);
        chest.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase + .4f) * chestDegrees);
        chest.localPosition = chestRest + Vector3.up * ((1f - Mathf.Cos(phase)) * chestLift * .5f);
        head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase + .8f) * headDegrees);
    }

}