using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>陆战队员三发短点射：分层蒙皮、刚性枪械、公共展示时钟，不产生战斗命中。</summary>
public sealed class MarineBurstRig : MonoBehaviour {
    public SpriteSkin[] layers;
    public Transform pelvis;
    public Transform chest;
    public Transform head;
    public Transform weapon;
    public Transform nearArm;
    public Transform farArm;
    public Transform muzzle;
    public float durationSeconds = 1.5f;
    public float raiseDegrees = 18f;
    public float shotStartSeconds = .42f;
    public float lowerStartSeconds = .98f;
    public float shotInterval = .18f;
    public float recoilDistance = .075f;
    public float weaponDegrees = 3.5f;
    public MarineIdleRig idleSource;
    public bool paused;
    public float Elapsed { get; private set; }
    public float Flash { get; private set; }
    public float Aim { get; private set; }
    public int ShotIndex { get; private set; }
    public bool Finished => Elapsed >= durationSeconds;
    private Timer clock;
    private Vector3 weaponRest;
    private Vector3 chestRest;

    private void Awake() {
        weaponRest = weapon.localPosition;
        chestRest = chest.localPosition;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true)) {
            renderer.forceRenderingOff = true;
        }
    }

    private void OnEnable() {
        Elapsed = 0;
        paused = false;
        clock = RookieEngine.GetTimer(TimerType.COMMON);
        if (clock != null) { clock.AddUpdateListener(this, Advance); }
    }

    private void OnDisable() {
        if (clock != null) { clock.RemoveUpdateListener(this, Advance); clock = null; }
    }

    private void Advance(float delta) {
        if (!paused && delta > 0) { Elapsed = Mathf.Min(durationSeconds, Elapsed + delta); }
        ApplyPose();
    }

    public void ApplyPose() {
        if (idleSource != null) {
            pelvis.localPosition = idleSource.pelvis.localPosition;
            pelvis.localRotation = idleSource.pelvis.localRotation;
            chest.localPosition = idleSource.chest.localPosition;
            chest.localRotation = idleSource.chest.localRotation;
            head.localRotation = idleSource.head.localRotation;
        } else {
            chest.localPosition = chestRest;
        }
        // Raise visibly before firing; lower only after the final recoil settles.
        Aim = Elapsed < .32f ? Mathf.SmoothStep(0, 1, Elapsed / .32f) : 1;
        if (Elapsed >= lowerStartSeconds) { Aim = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lowerStartSeconds, durationSeconds, Elapsed)); }
        float recoil = 0;
        Flash = 0;
        ShotIndex = -1;
        if (Elapsed >= shotStartSeconds) {
            ShotIndex = Mathf.Min(2, Mathf.FloorToInt((Elapsed - shotStartSeconds) / shotInterval));
            float time = Elapsed - shotStartSeconds - ShotIndex * shotInterval;
            recoil = time < .035f ? Mathf.Sin(time / .035f * Mathf.PI * .5f) : Mathf.Exp(-(time - .035f) / .085f);
            if (time > .4f) { recoil = 0; }
            Flash = time < .065f ? 1 - time / .065f : 0;
        }
        chest.localRotation *= Quaternion.Euler(0, 0, -1.6f * Aim + recoil * 1.2f);
        head.localRotation *= Quaternion.Euler(0, 0, -Aim);
        Quaternion raise = Quaternion.Euler(0, 0, raiseDegrees * Aim);
        weapon.localPosition = weaponRest + new Vector3(.01f, .06f, 0) * Aim + raise * new Vector3(-.928f, .371f, 0) * (recoilDistance * recoil);
        weapon.localRotation = Quaternion.Euler(0, 0, raiseDegrees * Aim + weaponDegrees * recoil);
        AimArm(nearArm, weapon.TransformPoint(new Vector3(-.11f, 0, 0)), new Vector2(.59f, -.36f));
        AimArm(farArm, weapon.TransformPoint(new Vector3(.57f, -.32f, 0)), new Vector2(0, -.68f));
    }

    private void AimArm(Transform arm, Vector3 hand, Vector2 bindDirection) {
        Vector2 direction = (Vector2)chest.InverseTransformPoint(hand) - (Vector2)arm.localPosition;
        float delta = Mathf.Atan2(direction.y, direction.x) - Mathf.Atan2(bindDirection.y, bindDirection.x);
        arm.localRotation = Quaternion.Euler(0, 0, delta * Mathf.Rad2Deg);
    }
}
