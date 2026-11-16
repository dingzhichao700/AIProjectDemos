using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>刺蛇独立下颌：微张待机、蓄力抬头咆哮、收回；只产生展示姿态。</summary>
[DefaultExecutionOrder(-1000)]
public sealed class HydraliskMouthRig : MonoBehaviour {
    public MarineIdleRig idle;
    public SpriteSkin jawSkin;
    public Transform mandible;
    public float idleJawDegrees = -36f;
    public float idleJawAmplitudeDegrees = 2.5f;
    public float idleJawPeriodSeconds = 1.6f;
    private float jawElapsed;
    public float durationSeconds = 1.65f;
    public bool paused;
    public bool IsRoaring { get; private set; }
    public float RoarElapsed { get; private set; }
    public float OpenAmount { get; private set; }
    public float Preparation { get; private set; }
    private Timer clock;
    public bool Ready => idle != null && jawSkin != null && idle.skin.HasCurrentDeformedVertices() && jawSkin.HasCurrentDeformedVertices();

    private void OnEnable() {
        IsRoaring = false; RoarElapsed = 0; paused = false; jawElapsed = 0;
        clock = RookieEngine.GetTimer(TimerType.COMMON);
        if (clock != null) { clock.AddUpdateListener(this, Advance); }
    }
    private void OnDisable() {
        if (clock != null) { clock.RemoveUpdateListener(this, Advance); clock = null; }
    }
    public bool PlayRoar() {
        var owner = GetComponentInParent<CardArtIdleSkin>();
        if (owner == null || !owner.CanPlaySpecial || !isActiveAndEnabled || IsRoaring) { return false; }
        IsRoaring = true; RoarElapsed = 0; return true;
    }
    public void StopRoar() { IsRoaring = false; RoarElapsed = 0; OpenAmount = Preparation = 0; }
    private void Advance(float delta) {
        if (!paused && delta > 0 && idle != null && !idle.paused) { jawElapsed = Mathf.Repeat(jawElapsed + delta, idleJawPeriodSeconds); }
        if (IsRoaring && !paused && delta > 0) {
            RoarElapsed = Mathf.Min(durationSeconds, RoarElapsed + delta);
            if (RoarElapsed >= durationSeconds) { IsRoaring = false; }
        }
    }
    private void LateUpdate() {
        if (idle == null || mandible == null) { return; }
        // 从基础待机重算，暂停和低帧率都不累加旋转；先于SpriteSkin LateUpdate提交。
        idle.ApplyPose();
        float t = RoarElapsed;
        Preparation = OpenAmount = 0;
        if (IsRoaring) {
            if (t < .25f) { Preparation = Mathf.SmoothStep(0, 1, t / .25f); }
            else if (t < .48f) { float u = Mathf.SmoothStep(0, 1, (t - .25f) / .23f); Preparation = 1 - u; OpenAmount = u; }
            else if (t < .90f) { OpenAmount = 1; }
            else { OpenAmount = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.90f, durationSeconds, t)); }
        }
        idle.chest.localPosition += Vector3.up * (-.08f * Preparation + .08f * OpenAmount);
        idle.chest.localRotation *= Quaternion.Euler(0, 0, 4f * Preparation - 6f * OpenAmount);
        float tremor = IsRoaring && t >= .48f && t < .90f ? Mathf.Sin(t * Mathf.PI * 36) * .45f : 0;
        idle.head.localRotation *= Quaternion.Euler(0, 0, 6f * Preparation - 10f * OpenAmount + tremor);
        float breath = Mathf.Sin(jawElapsed * Mathf.PI * 2 / idleJawPeriodSeconds) * idleJawAmplitudeDegrees;
        mandible.localRotation = Quaternion.Euler(0, 0, idleJawDegrees - 3 * Preparation + (2 - idleJawDegrees) * OpenAmount + breath * (1 - OpenAmount));
    }
}