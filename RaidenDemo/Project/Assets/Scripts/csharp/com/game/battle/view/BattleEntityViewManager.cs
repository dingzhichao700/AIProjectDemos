using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗实体表现管理
/// </summary>
/// <remarks>
/// 维护战斗逻辑实体 ID 与场景表现节点之间的映射。
/// </remarks>
internal sealed class BattleEntityViewManager {

    private readonly Dictionary<long, RectTransform> units = new Dictionary<long, RectTransform>();
    private readonly Dictionary<long, RectTransform> playerProjectiles = new Dictionary<long, RectTransform>();
    private readonly Dictionary<long, RectTransform> enemies = new Dictionary<long, RectTransform>();
    private readonly Dictionary<long, EliteEnemyHealthBarView> eliteHealthBars = new Dictionary<long, EliteEnemyHealthBarView>();
    private readonly Dictionary<long, RectTransform> enemyProjectiles = new Dictionary<long, RectTransform>();
    private readonly Dictionary<long, RectTransform> rewards = new Dictionary<long, RectTransform>();
    private readonly Dictionary<long, FrameAnimationView> rewardEffects = new Dictionary<long, FrameAnimationView>();
    private readonly Dictionary<long, FrameAnimationView> projectileEffects = new Dictionary<long, FrameAnimationView>();

    public void BindUnit(long id, RectTransform view) => units.Add(id, view);
    public void BindPlayerProjectile(long id, RectTransform view) => playerProjectiles.Add(id, view);
    public void BindEnemy(long id, RectTransform view) => enemies.Add(id, view);
    public void BindEliteHealthBar(long id, EliteEnemyHealthBarView view) => eliteHealthBars.Add(id, view);
    public void BindEnemyProjectile(long id, RectTransform view) => enemyProjectiles.Add(id, view);
    public void BindReward(long id, RectTransform view) => rewards.Add(id, view);
    public void BindRewardEffect(long id, FrameAnimationView view) {
        if (view != null) {
            rewardEffects.Add(id, view);
        }
    }
    public void BindProjectileEffect(long id, FrameAnimationView view) {
        if (view != null) projectileEffects.Add(id, view);
    }

    public RectTransform GetUnit(long id) => Get(units, id);
    public RectTransform GetPlayerProjectile(long id) => Get(playerProjectiles, id);
    public RectTransform GetEnemy(long id) => Get(enemies, id);
    public EliteEnemyHealthBarView GetEliteHealthBar(long id) => Get(eliteHealthBars, id);
    public RectTransform GetEnemyProjectile(long id) => Get(enemyProjectiles, id);
    public RectTransform GetReward(long id) => Get(rewards, id);

    public RectTransform RemovePlayerProjectile(long id) => Remove(playerProjectiles, id);
    public RectTransform RemoveEnemy(long id) => Remove(enemies, id);
    public EliteEnemyHealthBarView RemoveEliteHealthBar(long id) => Remove(eliteHealthBars, id);
    public RectTransform RemoveEnemyProjectile(long id) => Remove(enemyProjectiles, id);
    public RectTransform RemoveReward(long id) => Remove(rewards, id);
    public FrameAnimationView RemoveRewardEffect(long id) => Remove(rewardEffects, id);
    public FrameAnimationView RemoveProjectileEffect(long id) => Remove(projectileEffects, id);
    public RectTransform RemoveUnit(long id) => Remove(units, id);

    public void Clear() {
        units.Clear();
        playerProjectiles.Clear();
        enemies.Clear();
        foreach (EliteEnemyHealthBarView healthBar in eliteHealthBars.Values) healthBar.Dispose();
        eliteHealthBars.Clear();
        enemyProjectiles.Clear();
        foreach (FrameAnimationView effect in rewardEffects.Values) {
            effect?.Destroy();
        }
        rewardEffects.Clear();
        foreach (FrameAnimationView effect in projectileEffects.Values) effect?.Destroy();
        projectileEffects.Clear();
        rewards.Clear();
    }

    private static RectTransform Get(Dictionary<long, RectTransform> map, long id) {
        map.TryGetValue(id, out RectTransform view);
        return view;
    }

    private static RectTransform Remove(Dictionary<long, RectTransform> map, long id) {
        map.Remove(id, out RectTransform view);
        return view;
    }

    private static T Get<T>(Dictionary<long, T> map, long id) where T : class {
        map.TryGetValue(id, out T view);
        return view;
    }

    private static T Remove<T>(Dictionary<long, T> map, long id) where T : class {
        map.Remove(id, out T view);
        return view;
    }
}
