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
    private readonly List<BattleDefeatEffectRuntime> defeatEffects =
        new List<BattleDefeatEffectRuntime>();

    public BattleEffectPresenter(RectTransform effectLayer, BattleVisualPool visualPool) {
        this.effectLayer = effectLayer;
        this.visualPool = visualPool;
    }

    /**播放配置指定的子弹命中特效*/
    public void PlayBulletHit(int effectId, Vector2 position) {
        if (effectId <= 0) {
            return;
        }
        EffectResource effect = CfgManager.tables.EffectObj.GetOrDefault(effectId);
        if (effect == null || effect.Type != EffectType.BULLET_HIT) {
            Debug.LogError($"子弹命中特效配置无效：{effectId}");
            return;
        }
        FrameAnimationView view = FrameAnimationView.GetInstance();
        RectTransform rect = view.trans;
        rect.SetParent(effectLayer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
        view.Play(BattlePreloadCollector.GetEffectResourcePath(effect), false, null, true);
    }

    /**接管敌机原 View 并开始缩小淡出的死亡表现*/
    public void PlayEnemyDefeat(RectTransform root, RectTransform visual, Vector2 position) {
        root.SetParent(effectLayer, false);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = position;
        defeatEffects.Add(new BattleDefeatEffectRuntime(root, visual));
    }

    /**推进全部敌机死亡表现并回收完成项*/
    public void Update(float deltaTime) {
        for (int i = defeatEffects.Count - 1; i >= 0; i--) {
            BattleDefeatEffectRuntime effect = defeatEffects[i];
            if (effect.visual == null) {
                defeatEffects.RemoveAt(i);
                continue;
            }
            effect.remaining = Mathf.Max(0f, effect.remaining - deltaTime);
            float normalized = effect.remaining / BattleConst.EnemyDefeatDuration;
            effect.visual.localScale = Vector3.one * Mathf.Max(0.15f, normalized);
            if (effect.image != null) {
                Color color = effect.image.color;
                color.a = normalized;
                effect.image.color = color;
            }
            if (effect.remaining <= 0f) {
                visualPool.Recycle(effect.root);
                defeatEffects.RemoveAt(i);
            }
        }
    }

    public void Clear() {
        defeatEffects.Clear();
    }
}
