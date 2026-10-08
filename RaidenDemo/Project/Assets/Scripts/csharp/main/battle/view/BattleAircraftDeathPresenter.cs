using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 飞机死亡表现
/// </summary>
/// <remarks>
/// 同步爆炸解体与坠落阶段的机身，并在坠地后播放爆炸、烟尘和完成通知。
/// </remarks>
internal sealed class BattleAircraftDeathPresenter {

    private readonly BattleEffectPresenter effects;
    private readonly BattleVisualPool pool;
    private readonly List<AircraftDeathPresentationViewState> active = new List<AircraftDeathPresentationViewState>();
    private readonly List<AircraftDeathPresentationViewState> retained = new List<AircraftDeathPresentationViewState>();

    public BattleAircraftDeathPresenter(BattleEffectPresenter effects, BattleVisualPool pool) {
        this.effects = effects;
        this.pool = pool;
    }

    /// <summary>
    /// 播放一架飞机的死亡表现。
    /// </summary>
    /// <param name="root">机身根节点</param>
    /// <param name="aircraft">死亡飞机</param>
    /// <param name="preserveRootForReuse">是否保留根节点供复活复用</param>
    /// <param name="lastExplosionStarted">最后一段爆炸开始回调</param>
    /// <param name="completed">完整死亡表现结束回调</param>
    public void PlayAircraftDeath(RectTransform root, AircraftVO aircraft, bool preserveRootForReuse, Action lastExplosionStarted = null, Action completed = null) {
        if (aircraft == null) {
            lastExplosionStarted?.Invoke();
            completed?.Invoke();
            return;
        }
        AircraftDeathPresentationViewState state = new AircraftDeathPresentationViewState(root, aircraft, aircraft.deathExplosions, preserveRootForReuse, aircraft.timerType, lastExplosionStarted, completed);
        active.Add(state);
        if (aircraft.deathType == AircraftDeathType.FALLING) {
            ApplyDamagedAppearance(state);
            state.fallingSmokeEffect = effects.PlayAircraftFallingSmoke(aircraft.fallingSmokeEffectId, aircraft.GetFallingSmokeWorldPosition(), aircraft.timerType);
        }
        UpdateState(state, 0f);
    }

    /**按所属计时器推进死亡表现*/
    public void Update(float deltaTime, TimerType timerType) {
        foreach (AircraftDeathPresentationViewState state in retained) {
            if (state.timerType == timerType) {
                SyncBody(state);
            }
        }
        for (int index = active.Count - 1; index >= 0; index--) {
            AircraftDeathPresentationViewState state = active[index];
            if (state.timerType == timerType) {
                UpdateState(state, deltaTime);
            }
        }
    }

    /**清理全部死亡表现*/
    public void Clear() {
        foreach (AircraftDeathPresentationViewState state in active) {
            state.cancelled = true;
            foreach (FrameAnimationView handle in state.activeEffects) {
                handle?.Recover();
            }
            state.activeEffects.Clear();
            StopFallingSmoke(state);
            if (!state.preserveRootForReuse) {
                RemoveBody(state);
            }
        }
        active.Clear();
        foreach (AircraftDeathPresentationViewState state in retained) {
            state.cancelled = true;
            RemoveBody(state);
        }
        retained.Clear();
    }

    /**同步死亡机身的位置和高度比例*/
    private static void SyncBody(AircraftDeathPresentationViewState state) {
        if (state.aircraftVisualRemoved || state.root == null) {
            return;
        }
        state.root.anchoredPosition = state.aircraft.position;
        state.root.localScale = Vector3.one * state.aircraft.displayScale;
        state.root.localEulerAngles = new Vector3(0f, 0f, state.aircraft.deathRotationOffset);
        RectTransform visual = state.root.Find("imgVisual") as RectTransform;
        if (visual != null) {
            visual.localEulerAngles = new Vector3(0f, 0f, state.aircraft.deathStartRotation);
        }
        SyncFallingSmoke(state);
    }

    /**根据死亡类型推进对应表现*/
    private void UpdateState(AircraftDeathPresentationViewState state, float deltaTime) {
        SyncBody(state);
        state.elapsed += deltaTime;
        PlayScheduledExplosions(state);
        if (state.aircraft.deathType == AircraftDeathType.FALLING) {
            UpdateFalling(state);
        } else {
            UpdateDisintegration(state);
        }
    }

    /**播放已经达到延迟时间的死亡开始爆炸*/
    private void PlayScheduledExplosions(AircraftDeathPresentationViewState state) {
        while (!state.cancelled && state.explosions != null && state.nextExplosionIndex < state.explosions.Count && state.elapsed * 1000f >= state.explosions[state.nextExplosionIndex].delayMs) {
            AircraftExplosionVO explosion = state.explosions[state.nextExplosionIndex++];
            PlayEffect(state, completed => effects.PlayAircraftExplosion(explosion.effectId, state.aircraft.GetDeathEffectWorldPosition(explosion.localPosition), state.timerType, completed));
        }
    }

    /**在爆炸解体序列全部开始后执行机身收尾*/
    private void UpdateDisintegration(AircraftDeathPresentationViewState state) {
        if (!state.lastExplosionNotified && (state.explosions == null || state.nextExplosionIndex >= state.explosions.Count)) {
            state.lastExplosionNotified = true;
            state.lastExplosionStarted?.Invoke();
            if (!state.aircraft.retainBodyAfterDeathPresentation) {
                RemoveBody(state);
            }
        }
        TryComplete(state);
    }

    /**等待逻辑坠地，再播放两项一次性特效*/
    private void UpdateFalling(AircraftDeathPresentationViewState state) {
        if (!state.aircraft.hasLanded || state.landingEffectsStarted) {
            return;
        }
        state.landingEffectsStarted = true;
        state.lastExplosionNotified = true;
        state.lastExplosionStarted?.Invoke();
        StopFallingSmoke(state);
        RemoveBody(state);
        PlayEffect(state, completed => effects.PlayAircraftCrashEffect(state.aircraft.crashExplosionEffectId, EffectType.AIRCRAFT_EXPLOSION, state.aircraft.position, state.timerType, completed));
        PlayEffect(state, completed => effects.PlayAircraftCrashEffect(state.aircraft.crashSmokeEffectId, EffectType.OTHER, state.aircraft.position, state.timerType, completed));
        TryComplete(state);
    }

    /**登记一个死亡阶段特效播放句柄*/
    private void PlayEffect(AircraftDeathPresentationViewState state, Func<Action, FrameAnimationView> play) {
        state.activeExplosionCount++;
        FrameAnimationView handle = null;
        bool completedSynchronously = false;
        handle = play(() => {
            completedSynchronously = true;
            if (state.cancelled) {
                return;
            }
            if (handle != null) {
                state.activeEffects.Remove(handle);
            }
            state.activeExplosionCount--;
            TryComplete(state);
        });
        if (handle != null && !completedSynchronously) {
            state.activeEffects.Add(handle);
        }
    }

    /**同步坠落冒烟点的位置、缩放和固定角度*/
    private static void SyncFallingSmoke(AircraftDeathPresentationViewState state) {
        if (state.fallingSmokeEffect == null) {
            return;
        }
        RectTransform rect = state.fallingSmokeEffect.trans;
        rect.anchoredPosition = state.aircraft.GetFallingSmokeWorldPosition();
        rect.localScale = Vector3.one * state.aircraft.displayScale;
        rect.localEulerAngles = Vector3.zero;
    }

    /**回收坠落期间持续播放的冒烟特效*/
    private static void StopFallingSmoke(AircraftDeathPresentationViewState state) {
        state.fallingSmokeEffect?.Recover();
        state.fallingSmokeEffect = null;
    }

    /**在全部死亡特效结束后完成表现*/
    private void TryComplete(AircraftDeathPresentationViewState state) {
        if (state.cancelled || !state.lastExplosionNotified || state.nextExplosionIndex < state.explosions.Count || state.activeExplosionCount > 0 || !active.Remove(state)) {
            return;
        }
        if (state.aircraft.retainBodyAfterDeathPresentation && !state.preserveRootForReuse) {
            retained.Add(state);
        }
        state.completed?.Invoke();
    }

    /**切换为配置的战损飞机图片*/
    private static void ApplyDamagedAppearance(AircraftDeathPresentationViewState state) {
        if (state.root == null || string.IsNullOrWhiteSpace(state.aircraft.damagedAppearancePath)) {
            return;
        }
        RectTransform visual = state.root.Find("imgVisual") as RectTransform;
        Image image = visual != null ? visual.GetComponent<Image>() : null;
        if (image == null) {
            return;
        }
        BattlePreloadCollector.RequireUnpackImagePreloaded(state.aircraft.damagedAppearancePath);
        UITools.SetImage(image, state.aircraft.damagedAppearancePath, true);
    }

    /**回收或隐藏死亡飞机机身*/
    private void RemoveBody(AircraftDeathPresentationViewState state) {
        if (state.aircraftVisualRemoved) {
            return;
        }
        state.aircraftVisualRemoved = true;
        if (state.root == null) {
            return;
        }
        state.root.localScale = Vector3.one;
        state.root.localEulerAngles = Vector3.zero;
        if (state.preserveRootForReuse) {
            Transform visual = state.root.Find("imgVisual");
            if (visual != null) {
                visual.gameObject.SetActive(false);
            }
        } else {
            pool.Recycle(state.root);
        }
    }

}
