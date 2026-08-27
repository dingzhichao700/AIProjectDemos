using System;
using System.Collections.Generic;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 战斗特效表现
/// </summary>
/// <remarks>
/// 统一播放并推进战斗中的命中特效与敌机死亡表现。
/// </remarks>
internal sealed class BattleEffectPresenter {

    private readonly RectTransform effectLayer;
    private readonly BattleVisualPool visualPool;
    private readonly List<AircraftDeathPresentationViewState> deathPresentations = new List<AircraftDeathPresentationViewState>();
    private readonly List<RectTransform> retainedAircraftRoots = new List<RectTransform>();

    public BattleEffectPresenter(RectTransform effectLayer, BattleVisualPool visualPool) {
        this.effectLayer = effectLayer;
        this.visualPool = visualPool;
    }

    /**播放配置指定的子弹命中特效*/
    public void PlayBulletHit(int effectId, Vector2 position, TimerType timerType) {
        if (effectId <= 0) {
            return;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null || effect.Type != EffectType.BULLET_HIT) {
            Debug.LogError($"子弹命中特效配置无效：{effectId}");
            return;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(effectLayer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        view.Play(path, false, null, true, 1f, 1, 1f, timerType);
    }

    /**在发射器局部坐标播放一次随载机移动的发射特效。*/
    public void PlayBulletLaunch(int effectId, RectTransform aircraftRoot, Vector2 launcherOffset, float rotation, TimerType timerType) {
        if (effectId <= 0 || aircraftRoot == null) {
            return;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null || effect.Type != EffectType.BULLET_LAUNCH) {
            Debug.LogError($"子弹发射特效配置无效：{effectId}");
            return;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(aircraftRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = launcherOffset;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = new Vector3(0f, 0f, rotation);
        view.Play(path, false, null, true, 1f, 1, 1f, timerType);
    }

    /**在玩家飞机原点播放升级特效。*/
    public FrameAnimationView PlayPlayerUpgrade(int effectId, RectTransform playerRoot, bool loop, Vector2 offset = default, float scale = 1f) {
        if (playerRoot == null) {
            return null;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null || effect.Type != EffectType.OTHER) {
            Debug.LogError($"玩家飞机升级特效配置无效：{effectId}");
            return null;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(playerRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        view.Play(path, loop, null, !loop, scale, 1, 1f, TimerType.PLAYER);
        return view;
    }

    /**按飞机配置启动死亡前爆炸表现。*/
    public void PlayAircraftDeath(RectTransform root, AircraftVO aircraft, bool preserveRootForReuse, Action completed = null) {
        if (root == null || aircraft == null) {
            completed?.Invoke();
            return;
        }
        if (!preserveRootForReuse) {
            root.SetParent(effectLayer, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = aircraft.position;
        }
        AircraftDeathPresentationViewState state = new AircraftDeathPresentationViewState(root, aircraft.position, aircraft.deathExplosions, aircraft.removeAfterDeathPresentation, preserveRootForReuse, aircraft.timerType, completed);
        deathPresentations.Add(state);
        UpdateDeathPresentation(state, 0f);
    }

    /**按指定 Timer 推进对应阵营的死亡表现。*/
    public void Update(float deltaTime, TimerType timerType) {
        for (int i = deathPresentations.Count - 1; i >= 0; i--) {
            AircraftDeathPresentationViewState state = deathPresentations[i];
            if (state.timerType == timerType) {
                UpdateDeathPresentation(state, deltaTime);
            }
        }
    }

    public void Clear() {
        foreach (AircraftDeathPresentationViewState state in deathPresentations) {
            if (!state.preserveRootForReuse && !state.aircraftVisualRemoved) {
                visualPool.Recycle(state.root);
            }
        }
        deathPresentations.Clear();
        foreach (RectTransform root in retainedAircraftRoots) {
            visualPool.Recycle(root);
        }
        retainedAircraftRoots.Clear();
    }

    private void UpdateDeathPresentation(AircraftDeathPresentationViewState state, float deltaTime) {
        state.elapsed += deltaTime;
        while (state.explosions != null && state.nextExplosionIndex < state.explosions.Count && state.elapsed * 1000f >= state.explosions[state.nextExplosionIndex].DelayMs) {
            PlayExplosion(state, state.explosions[state.nextExplosionIndex++]);
        }
        if ((state.explosions == null || state.nextExplosionIndex >= state.explosions.Count) && state.activeExplosionCount <= 0) {
            CompleteDeathPresentation(state);
        }
    }

    private void PlayExplosion(AircraftDeathPresentationViewState state, ExplosionEffect explosion) {
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(explosion.EffectId);
        if (effect == null || effect.Type != EffectType.AIRCRAFT_EXPLOSION) {
            Debug.LogError($"飞机爆炸特效配置无效：{explosion.EffectId}");
            return;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(effectLayer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = state.position + new Vector2(explosion.Position.X, explosion.Position.Y);
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        state.activeExplosionCount++;
        view.Play(path, false, Handler.Create(this, OnExplosionCompleted, state), true, 1f, 1, 1f, state.timerType);
        if (state.removeAfterCompletion && state.nextExplosionIndex >= state.explosions.Count) {
            RemoveAircraftVisual(state);
        }
    }

    private void OnExplosionCompleted(AircraftDeathPresentationViewState state) {
        state.activeExplosionCount = Mathf.Max(0, state.activeExplosionCount - 1);
        if (state.nextExplosionIndex >= state.explosions.Count && state.activeExplosionCount <= 0) {
            CompleteDeathPresentation(state);
        }
    }

    private void CompleteDeathPresentation(AircraftDeathPresentationViewState state) {
        if (!deathPresentations.Remove(state)) {
            return;
        }
        if (!state.removeAfterCompletion) {
            if (!state.preserveRootForReuse) {
                retainedAircraftRoots.Add(state.root);
            }
            state.completed?.Invoke();
            return;
        }
        RemoveAircraftVisual(state);
        state.completed?.Invoke();
    }

    /**隐藏或回收需要随死亡表现移除的飞机形象。*/
    private void RemoveAircraftVisual(AircraftDeathPresentationViewState state) {
        if (state.aircraftVisualRemoved) {
            return;
        }
        state.aircraftVisualRemoved = true;
        if (state.preserveRootForReuse) {
            RectTransform visual = state.root.Find("imgVisual") as RectTransform;
            if (visual != null) {
                visual.gameObject.SetActive(false);
            }
        } else {
            visualPool.Recycle(state.root);
        }
    }
}
