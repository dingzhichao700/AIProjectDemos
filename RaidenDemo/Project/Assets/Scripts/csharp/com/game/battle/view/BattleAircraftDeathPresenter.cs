using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>飞机死亡阶段的机身表现与爆炸序列协调。</summary>
/// <remarks>机身保持实体层；只读取移动状态，阶段结束通知 Model；独立爆炸交给特效服务。</remarks>
internal sealed class BattleAircraftDeathPresenter {
    private readonly BattleEffectPresenter effects;
    private readonly BattleVisualPool pool;
    private readonly List<AircraftDeathPresentationViewState> active = new List<AircraftDeathPresentationViewState>();
    private readonly List<AircraftDeathPresentationViewState> retained = new List<AircraftDeathPresentationViewState>();

    public BattleAircraftDeathPresenter(BattleEffectPresenter effects, BattleVisualPool pool) {
        this.effects = effects;
        this.pool = pool;
    }

    public void PlayAircraftDeath(RectTransform root, AircraftVO aircraft, bool preserveRootForReuse,
        Action lastExplosionStarted = null, Action completed = null) {
        if (root == null || aircraft == null) {
            lastExplosionStarted?.Invoke();
            completed?.Invoke();
            return;
        }
        var state = new AircraftDeathPresentationViewState(root, aircraft, aircraft.deathExplosions,
            aircraft.removeAfterDeathPresentation, preserveRootForReuse, aircraft.timerType, lastExplosionStarted, completed);
        active.Add(state);
        UpdateState(state, 0f);
    }

    public void Update(float deltaTime, TimerType timerType) {
        foreach (var state in retained) {
            if (state.timerType == timerType) {
                SyncBody(state);
            }
        }
        for (int i = active.Count - 1; i >= 0; i--) {
            var state = active[i];
            if (state.timerType == timerType) {
                UpdateState(state, deltaTime);
            }
        }
    }

    private static void SyncBody(AircraftDeathPresentationViewState state) {
        if (!state.aircraftVisualRemoved && state.root != null) {
            state.root.anchoredPosition = state.aircraft.position;
        }
    }

    private void UpdateState(AircraftDeathPresentationViewState state, float deltaTime) {
        state.elapsed += deltaTime;
        SyncBody(state);
        while (!state.cancelled && state.explosions != null && state.nextExplosionIndex < state.explosions.Count &&
               state.elapsed * 1000f >= state.explosions[state.nextExplosionIndex].DelayMs) {
            var explosion = state.explosions[state.nextExplosionIndex++];
            state.activeExplosionCount++;
            bool completedSynchronously = false;
            FrameAnimationView handle = null;
            handle = effects.PlayAircraftExplosion(explosion, state.aircraft.position, state.timerType, () => {
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
            if (!completedSynchronously) {
                state.activeEffects.Add(handle);
            }
        }
        if (!state.lastExplosionNotified && (state.explosions == null || state.nextExplosionIndex >= state.explosions.Count)) {
            state.lastExplosionNotified = true;
            state.lastExplosionStarted?.Invoke();
            if (state.removeAfterCompletion) {
                RemoveBody(state);
            }
        }
        TryComplete(state);
    }

    private void TryComplete(AircraftDeathPresentationViewState state) {
        if (state.cancelled || !state.lastExplosionNotified || state.activeExplosionCount > 0 || !active.Remove(state)) {
            return;
        }
        if (!state.removeAfterCompletion && !state.preserveRootForReuse) {
            retained.Add(state);
        }
        state.completed?.Invoke();
    }

    private void RemoveBody(AircraftDeathPresentationViewState state) {
        if (state.aircraftVisualRemoved) {
            return;
        }
        state.aircraftVisualRemoved = true;
        if (state.preserveRootForReuse) {
            Transform visual = state.root.Find("imgVisual");
            if (visual != null) {
                visual.gameObject.SetActive(false);
            }
        } else {
            pool.Recycle(state.root);
        }
    }

    public void Clear() {
        foreach (var state in active) {
            state.cancelled = true;
            foreach (FrameAnimationView handle in state.activeEffects) {
                handle.Destroy();
            }
            state.activeEffects.Clear();
            if (!state.preserveRootForReuse) {
                RemoveBody(state);
            }
        }
        active.Clear();
        foreach (var state in retained) {
            state.cancelled = true;
            RemoveBody(state);
        }
        retained.Clear();
    }
}
