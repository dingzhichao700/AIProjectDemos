using System;
using System.Collections.Generic;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 战斗特效表现
/// </summary>
/// <remarks>
/// 播放战斗独立特效及附着特效，不决定实体生命周期。
/// </remarks>
internal sealed class BattleEffectPresenter {

    private readonly RectTransform effectLayer;
    public BattleEffectPresenter(RectTransform effectLayer) {
        this.effectLayer = effectLayer;
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

    /**在子弹节点上循环播放作为弹体外观的特效。*/
    public FrameAnimationView PlayBulletBody(int effectId, RectTransform projectileRoot, TimerType timerType) {
        if (effectId <= 0 || projectileRoot == null) return null;
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null || effect.Type != EffectType.BULLET) {
            Debug.LogError($"子弹弹体特效配置无效：{effectId}");
            return null;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(projectileRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        view.Play(path, true, null, false, 1f, 1, 1f, timerType);
        return view;
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

    /**在奖励道具原点循环播放配置指定的叠加特效。*/
    public FrameAnimationView PlayRewardLoop(int effectId, RectTransform rewardRoot) {
        if (effectId <= 0 || rewardRoot == null) {
            return null;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null || effect.Type != EffectType.OTHER) {
            Debug.LogError($"关卡奖励循环特效配置无效：{effectId}");
            return null;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(rewardRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        view.Play(path, true, null, false, BattleConst.RewardLoopEffectScale, 1, 1f, TimerType.SCENE);
        return view;
    }

    /**在道具拾取位置播放一次统一拾取特效。*/
    public void PlayRewardPickup(RectTransform rewardRoot, Action completed) {
        if (rewardRoot == null) {
            completed?.Invoke();
            return;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(BattleConst.RewardPickupEffectId);
        if (effect == null) {
            Debug.LogError($"关卡奖励拾取特效配置无效：{BattleConst.RewardPickupEffectId}");
            completed?.Invoke();
            return;
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(rewardRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        Handler handler = completed != null ? Handler.Create(this, completed) : null;
        view.Play(path, false, handler, true, 1f, 1, 1f, TimerType.SCENE);
    }

    /**在特效层播放独立爆炸，返回播放句柄供拥有者取消。*/
    public FrameAnimationView PlayAircraftExplosion(ExplosionEffect explosion, Vector2 position, TimerType timerType, Action completed) {
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(explosion.EffectId);
        if (effect == null || effect.Type != EffectType.AIRCRAFT_EXPLOSION) {
            throw new InvalidOperationException($"飞机爆炸特效配置无效：{explosion.EffectId}");
        }
        string path = BattlePreloadCollector.GetEffectResourcePath(effect);
        BattlePreloadCollector.RequireFrameAnimationPreloaded(path);
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(effectLayer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = position + new Vector2(explosion.Position.X, explosion.Position.Y);
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        view.Play(path, false, Handler.Create(this, completed), true, 1f, 1, 1f, timerType);
        return view;
    }
}
