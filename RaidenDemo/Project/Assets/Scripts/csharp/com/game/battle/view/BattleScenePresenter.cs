using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗场景表现
/// </summary>
/// <remarks>
/// 统一管理战斗场景实体 View 的创建、同步与回收。
/// </remarks>
internal sealed class BattleScenePresenter {

    private readonly RectTransform entityLayer;
    private readonly RectTransform projectileLayer;
    private readonly RectTransform effectLayer;
    private readonly RectTransform bossHealthBar;
    private readonly Image bossHealthFill;
    private readonly BattleVisualPool visualPool;
    private readonly BattleEntityViewManager views;
    private readonly BattleEffectPresenter effects;
    private readonly BattleHudPresenter hud;
    private BattleModel model;

    public BattleScenePresenter(RectTransform entityLayer, RectTransform projectileLayer,
        RectTransform effectLayer, RectTransform bossHealthBar, Image bossHealthFill,
        BattleVisualPool visualPool, BattleEntityViewManager views,
        BattleEffectPresenter effects, BattleHudPresenter hud) {
        this.entityLayer = entityLayer;
        this.projectileLayer = projectileLayer;
        this.effectLayer = effectLayer;
        this.bossHealthBar = bossHealthBar;
        this.bossHealthFill = bossHealthFill;
        this.visualPool = visualPool;
        this.views = views;
        this.effects = effects;
        this.hud = hud;
    }

    /**订阅逻辑实体生命周期事件。*/
    public void Bind(BattleModel battleModel) {
        model = battleModel;
        model.playerProjectileSpawned += OnPlayerProjectileSpawned;
        model.playerProjectileRemoved += OnPlayerProjectileRemoved;
        model.enemySpawned += OnEnemySpawned;
        model.enemyRemoved += OnEnemyRemoved;
        model.enemyHealthChanged += OnEnemyHealthChanged;
        model.enemyProjectileSpawned += OnEnemyProjectileSpawned;
        model.enemyProjectileRemoved += OnEnemyProjectileRemoved;
        model.rewardSpawned += OnRewardSpawned;
        model.rewardRemoved += OnRewardRemoved;
    }

    /**取消逻辑实体生命周期事件。*/
    public void Unbind() {
        if (model == null) {
            return;
        }
        model.playerProjectileSpawned -= OnPlayerProjectileSpawned;
        model.playerProjectileRemoved -= OnPlayerProjectileRemoved;
        model.enemySpawned -= OnEnemySpawned;
        model.enemyRemoved -= OnEnemyRemoved;
        model.enemyHealthChanged -= OnEnemyHealthChanged;
        model.enemyProjectileSpawned -= OnEnemyProjectileSpawned;
        model.enemyProjectileRemoved -= OnEnemyProjectileRemoved;
        model.rewardSpawned -= OnRewardSpawned;
        model.rewardRemoved -= OnRewardRemoved;
        model = null;
    }

    /**同步场景 Timer 管理的奖励 View。*/
    public void SyncSceneViews() {
        for (int i = model.rewardDrops.Count - 1; i >= 0; i--) {
            RewardVO reward = model.rewardDrops[i];
            RectTransform view = views.GetReward(reward.id);
            if (view == null) {
                model.RemoveReward(reward);
                continue;
            }
            view.anchoredPosition = reward.position;
        }
    }

    /**同步玩家 Timer 管理的子弹 View。*/
    public void SyncPlayerViews() {
        for (int i = model.playerProjectiles.Count - 1; i >= 0; i--) {
            BulletVO projectile = model.playerProjectiles[i];
            RectTransform view = views.GetPlayerProjectile(projectile.id);
            if (view == null) {
                model.RemovePlayerProjectile(projectile);
                continue;
            }
            SyncProjectileRotation(projectile, view);
            view.anchoredPosition = projectile.position;
        }
    }

    /**同步敌方 Timer 管理的敌机和敌弹 View。*/
    public void SyncEnemyViews() {
        foreach (AircraftVO enemy in model.enemies) {
            RectTransform view = views.GetEnemy(enemy.id);
            if (view != null) {
                view.anchoredPosition = enemy.position;
            }
        }
        for (int i = model.enemyProjectiles.Count - 1; i >= 0; i--) {
            BulletVO projectile = model.enemyProjectiles[i];
            RectTransform view = views.GetEnemyProjectile(projectile.id);
            if (view == null) {
                model.RemoveEnemyProjectile(projectile);
                continue;
            }
            view.anchoredPosition = projectile.position;
        }
    }

    private void OnPlayerProjectileSpawned(BulletVO projectile) {
        RectTransform view = visualPool.Create("playerLaser", projectileLayer,
            projectile.displaySize, projectile.position, projectile.resPath, 0f, true);
        views.BindPlayerProjectile(projectile.id, view);
        SyncProjectileRotation(projectile, view);
        PlayBulletLaunch(projectile);
    }

    private void OnPlayerProjectileRemoved(long id) {
        visualPool.Recycle(views.RemovePlayerProjectile(id));
    }

    private void OnEnemySpawned(AircraftVO enemy) {
        RectTransform view = visualPool.Create(enemy.semanticName, entityLayer, enemy.size, enemy.position, enemy.appearancePath, BattleConst.EnemyAircraftVisualRotation);
        views.BindEnemy(enemy.id, view);
        if (enemy.isBoss) {
            bossHealthBar.gameObject.SetActive(true);
            bossHealthFill.rectTransform.sizeDelta = BattleConst.BossHealthFillSize;
            hud.RefreshBoss(model.enemies);
        }
    }

    private void OnEnemyRemoved(AircraftVO enemy, bool defeated) {
        RectTransform root = views.GetEnemy(enemy.id);
        views.RemoveEnemy(enemy.id);
        if (defeated && root != null) {
            effects.PlayAircraftDeath(root, enemy, false, enemy.isBoss ? model.NotifyBossDeathPresentationCompleted : null);
        } else {
            visualPool.Recycle(root);
            if (defeated && enemy.isBoss) {
                model.NotifyBossDeathPresentationCompleted();
            }
        }
        if (enemy.isBoss) {
            bossHealthBar.gameObject.SetActive(false);
        }
    }

    private void OnEnemyHealthChanged(AircraftVO enemy) {
        if (enemy.isBoss) {
            hud.RefreshBoss(model.enemies);
        }
    }

    private void OnEnemyProjectileSpawned(BulletVO projectile,
        BulletConfigVO config) {
        RectTransform view = visualPool.Create("enemyBullet", projectileLayer,
            config.displaySize, projectile.position, config.appearancePath, 0f, true);
        views.BindEnemyProjectile(projectile.id, view);
        PlayBulletLaunch(projectile);
    }

    private void OnEnemyProjectileRemoved(long id) {
        visualPool.Recycle(views.RemoveEnemyProjectile(id));
    }

    /**将发射特效绑定到子弹所属飞机的发射点。*/
    private void PlayBulletLaunch(BulletVO projectile) {
        if (projectile == null || projectile.owner == null) {
            return;
        }
        RectTransform aircraftRoot = projectile.owner.faction == SceneElementFaction.PLAYER ? views.GetUnit(projectile.owner.id) : views.GetEnemy(projectile.owner.id);
        Vector2 launcherOffset = projectile.position - projectile.owner.position;
        effects.PlayBulletLaunch(projectile.launchEffectId, aircraftRoot, launcherOffset, projectile.launchRotation, projectile.timerType);
    }

    private void OnRewardSpawned(RewardVO reward) {
        RectTransform view = visualPool.Create("rewardDrop", effectLayer,
            BattleConst.UpgradeDropSize, reward.position, GetRewardPath(reward.type));
        views.BindReward(reward.id, view);
    }

    private void OnRewardRemoved(long id) {
        visualPool.Recycle(views.RemoveReward(id));
    }

    private static void SyncProjectileRotation(BulletVO projectile,
        RectTransform projectileView) {
        RectTransform visual = projectileView != null
            ? projectileView.Find("imgVisual") as RectTransform
            : null;
        if (visual != null) {
            visual.localEulerAngles = new Vector3(0f, 0f, projectile.rotation);
        }
    }

    private static string GetRewardPath(BattleRewardType type) {
        switch (type) {
            case BattleRewardType.Health:
                return BattleConst.HealthDropPath;
            case BattleRewardType.PlayerUpgrade:
                return BattleConst.UpgradeDropPath;
            case BattleRewardType.WingmanUpgrade:
                return BattleConst.WingmanUpgradeDropPath;
            default:
                return BattleConst.LifeDropPath;
        }
    }
}
