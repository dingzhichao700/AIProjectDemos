using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>医疗运输机展示动作；只驱动原生骨架，不决定卡牌资格或页面布局。</summary>
public sealed class MedivacDisplayRig : MonoBehaviour {
    public SpriteSkin[] skins;
    public Transform body;
    public Transform leftPlume;
    public Transform rightPlume;
    public float pixelsPerUnit = 100;
    public float idleDuration = 3;
    public float thrustDuration = 2.4f;
    public float idleLiftPixels = 2;
    public float idlePitchDegrees = .5f;
    public float thrustLiftPixels = 16;
    public float thrustPitchDegrees = -4;
    public bool paused;
    public float speed = 1;
    private Timer clock;
    private Vector3 bodyRest;
    private Quaternion bodyRotation;
    private Vector3 leftRestScale;
    private Vector3 rightRestScale;
    private float idleElapsed;
    private float thrustElapsed;
    private bool thrustActive;
    private bool bindInitialized;
    public bool IsThrustActive => thrustActive;

    private void Awake() {
        InitializeBind();
    }
    public void InitializeBind() {
        if (bindInitialized) return;
        bodyRest = body.localPosition;
        bodyRotation = body.localRotation;
        leftRestScale = leftPlume.localScale;
        rightRestScale = rightPlume.localScale;
        bindInitialized = true;
    }
    private void OnEnable() {
        ResetPose();
        clock = Application.isPlaying ? RookieEngine.GetTimer(TimerType.COMMON) : null;
        if (clock != null) clock.AddUpdateListener(this, Advance);
    }
    private void OnDisable() {
        if (clock != null) clock.RemoveUpdateListener(this, Advance);
        clock = null;
        ResetPose();
    }
    public void ResetPose() {
        if (body == null || leftPlume == null || rightPlume == null) return;
        InitializeBind();
        idleElapsed = 0;
        thrustElapsed = 0;
        thrustActive = false;
        paused = false;
        speed = 1;
        ApplyPose();
    }
    public void PlayThrust() {
        thrustElapsed = 0;
        thrustActive = true;
    }
    public void CancelThrust() {
        thrustElapsed = 0;
        thrustActive = false;
        ApplyPose();
    }
    private void Advance(float delta) {
        if (paused || delta <= 0) return;
        float dt = delta * Mathf.Max(0, speed);
        idleElapsed = Mathf.Repeat(idleElapsed + dt, Mathf.Max(.01f, idleDuration));
        if (thrustActive) {
            thrustElapsed += dt;
            if (thrustElapsed >= thrustDuration) thrustActive = false;
        }
        ApplyPose();
    }
    public void ApplyPose() {
        // Recalculate from bind state; no cumulative position, rotation or scale drift.
        float wave = Mathf.Sin(idleElapsed * 2 * Mathf.PI / Mathf.Max(.01f, idleDuration));
        float actionTime = thrustElapsed / Mathf.Max(.01f, thrustDuration) * 2.4f;
        float impulse = thrustActive ? ThrustEnvelope(actionTime, .12f, .56f, 1.02f, 2.30f) : 0;
        float plumeImpulse = thrustActive ? ThrustEnvelope(actionTime, 0, .18f, .93f, 2.12f) : 0;
        body.localPosition = bodyRest + Vector3.up * ((idleLiftPixels * wave + thrustLiftPixels * impulse) / Mathf.Max(.01f, pixelsPerUnit));
        body.localRotation = bodyRotation * Quaternion.Euler(0, 0, idlePitchDegrees * wave + thrustPitchDegrees * impulse);
        float plumeLength = 1 + .06f * Mathf.Sin(idleElapsed * 2 * Mathf.PI / .6f) + .65f * plumeImpulse;
        leftPlume.localScale = Vector3.Scale(leftRestScale, new Vector3(1, plumeLength, 1));
        rightPlume.localScale = Vector3.Scale(rightRestScale, new Vector3(1, plumeLength, 1));
    }
    private static float ThrustEnvelope(float time, float riseStart, float riseEnd, float holdEnd, float returnEnd) {
        if (time < riseStart) return 0;
        if (time < riseEnd) return Mathf.SmoothStep(0, 1, (time - riseStart) / (riseEnd - riseStart));
        if (time < holdEnd) return 1;
        if (time < returnEnd) return 1 - Mathf.SmoothStep(0, 1, (time - holdEnd) / (returnEnd - holdEnd));
        return 0;
    }
    public void Sample(float time, bool thrust) {
        InitializeBind();
        idleElapsed = Mathf.Repeat(time, Mathf.Max(.01f, idleDuration));
        thrustElapsed = time;
        thrustActive = thrust && time < thrustDuration;
        ApplyPose();
    }
}
