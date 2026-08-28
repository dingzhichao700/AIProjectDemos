using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

/// <summary>
/// 战斗数据管理
/// </summary>
/// <remarks>
/// 作为单局战斗的统一入口，协调流程、场景元素、关卡、编队、奖励与碰撞子模型。
/// </remarks>
public sealed class BattleModel {

    /**场景元素、唯一 ID 与 Timer 分组模型*/
    private readonly BattleSceneModel sceneModel = new BattleSceneModel();

    /**战斗流程状态模型*/
    private readonly BattleFlowModel flowModel = new BattleFlowModel();

    /**关卡波次与特殊敌机推进模型*/
    private readonly BattleStageModel stageModel = new BattleStageModel();

    /**关卡奖励与自然补给模型*/
    private readonly BattleRewardModel rewardModel = new BattleRewardModel();

    /**跨阵营接触与伤害结算模型*/
    private readonly BattleCombatModel combatModel = new BattleCombatModel();

    /**玩家主机、僚机与生命状态模型*/
    private readonly BattleFormationModel formationModel = new BattleFormationModel();

    /**玩家子弹运行时列表*/
    internal readonly List<BulletVO> playerProjectiles = new List<BulletVO>();

    private AircraftVO playerUnit => formationModel.player;
    private AircraftCollisionVO playerCollision => formationModel.playerCollision;

    /**敌机运行时列表*/
    internal readonly List<AircraftVO> enemies = new List<AircraftVO>();

    /**敌方子弹运行时列表*/
    internal readonly List<BulletVO> enemyProjectiles = new List<BulletVO>();

    /**奖励道具运行时列表*/
    internal List<RewardVO> rewardDrops => rewardModel.rewards;

    /**本局累计分数*/
    private int battleScore;

    /**Boss 死亡表现结束后等待结算的剩余时间；负数表示尚未开始等待*/
    private float bossVictoryDelayRemaining = -1f;

    /**Boss 已被击毁，正在等待其死亡表现完成*/
    private bool waitingForBossDeathPresentation;

    /**本局流程状态与关卡进度*/
    public BattleFlowState flowState => flowModel.state;
    public float missionProgress => flowModel.missionProgress;

    /**场景 Timer 的逐帧更新时间*/
    public event Action<float> sceneTimeUpdated;

    /**玩家 Timer 的逐帧更新时间*/
    public event Action<float> playerTimeUpdated;

    /**通知表现层回收已移除的玩家子弹*/
    public event Action<long> playerProjectileRemoved;

    /**通知表现层为新建玩家子弹创建 View*/
    internal event Action<BulletVO> playerProjectileSpawned;

    /**玩家子弹与敌机发生有效接触*/
    internal event Action<BulletVO, AircraftVO, Vector2>
        playerProjectileHitEnemy;

    /**通知表现层回收敌机并播放对应离场表现*/
    internal event Action<AircraftVO, bool> enemyRemoved;

    /**通知表现层刷新未被击毁敌机的血量表现*/
    internal event Action<AircraftVO> enemyHealthChanged;

    /**通知表现层为新建奖励补充 View*/
    internal event Action<RewardVO> rewardSpawned;

    /**通知表现层回收奖励 View*/
    internal event Action<long> rewardRemoved;

    /**奖励被玩家拾取；整数参数为实际回血量*/
    internal event Action<RewardVO, int> rewardCollected;

    /**通知表现层刷新战斗分数*/
    internal event Action<int> scoreChanged;

    /**Boss 被击毁后请求进入胜利结算*/
    internal event Action victoryRequested;

    /**敌方子弹与玩家飞机发生有效接触*/
    internal event Action<BulletVO> enemyProjectileHitPlayer;

    /**通知表现层回收已移除的敌方子弹*/
    internal event Action<long> enemyProjectileRemoved;

    /**玩家生命或血量发生变化*/
    internal event Action playerStatusChanged;

    /**玩家进入死亡表现阶段*/
    internal event Action playerDefeatStarted;

    /**玩家开始复活飞入*/
    internal event Action playerRespawnStarted;

    /**玩家完成复活飞入*/
    internal event Action playerRespawnCompleted;

    /**复活前请求恢复默认临时等级*/
    internal event Action playerDefaultLevelRequested;

    /**生命耗尽后请求失败结算*/
    internal event Action defeatRequested;

    internal event Action<BulletVO, BulletConfigVO> enemyProjectileSpawned;

    /**敌方 Timer 的逐帧更新时间*/
    public event Action<float> enemyTimeUpdated;

    /**通知表现层为已创建的敌机补充 View*/
    internal event Action<AircraftVO> enemySpawned;

    /**当前全部战斗场景元素*/
    public IReadOnlyDictionary<long, SceneElementVO> elements => sceneModel.elements;

    /**当前权威战斗分数*/
    public int score => battleScore;

    /**玩家本局剩余生命*/
    public int playerLives => formationModel.playerLives;

    /**玩家当前是否允许操作和拾取*/
    public bool isPlayerAlive => formationModel.isPlayerAlive;

    public bool isPlaying => flowModel.isPlaying;

    /**分配一个本局唯一的场景元素 ID*/
    public long CreateElementId() {
        return sceneModel.CreateElementId();
    }

    /**初始化关卡波次运行状态*/
    public void InitializeStage(StageConfigVO config, int currentStageId = 0) {
        stageModel.Initialize(config);
        rewardModel.Initialize(currentStageId);
        battleScore = 0;
        bossVictoryDelayRemaining = -1f;
        waitingForBossDeathPresentation = false;
        formationModel.Initialize();
        flowModel.Initialize();
    }

    /**设置战斗模拟是否继续推进*/
    public void SetSimulationActive(bool value) {
        flowModel.SetSimulationActive(value);
    }

    /**开始本局战斗。*/
    public bool StartBattle() {
        if (!flowModel.Start()) {
            return false;
        }
        SetPlayerFiringEnabled(true);
        ResetPlayerLaunchers();
        return true;
    }

    /**暂停战斗逻辑。*/
    public bool PauseBattle() {
        return flowModel.Pause();
    }

    /**恢复暂停的战斗逻辑。*/
    public bool ResumeBattle() {
        return flowModel.Resume();
    }

    /**进入结算状态。*/
    public bool SettleBattle() {
        return flowModel.Settle();
    }

    public void CloseBattle() {
        flowModel.Close();
    }

    public void SetMissionProgress(float value) {
        flowModel.SetMissionProgress(value);
    }

    /**订阅三类场景计时器，开始推进战斗逻辑*/
    public void StartTimeFlow() {
        sceneModel.StartTimeFlow(OnSceneTimeUpdate, OnPlayerTimeUpdate,
            OnEnemyTimeUpdate);
    }

    /**取消计时器订阅，停止推进战斗逻辑*/
    public void StopTimeFlow() {
        sceneModel.StopTimeFlow();
    }

    /**登记战斗场景元素，并按其 Timer 类型归类*/
    public void AddElement(SceneElementVO element) {
        sceneModel.AddElement(element);
    }

    /**移除并销毁指定战斗场景元素*/
    public bool RemoveElement(long id) {
        return sceneModel.RemoveElement(id);
    }

    /**登记玩家主机，作为僚机编队的跟随目标*/
    internal void SetPlayerUnit(AircraftVO unit) {
        formationModel.SetPlayer(unit);
        playerUnit?.ConfigurePlayerFiring(CreatePlayerProjectile);
        playerUnit?.ConfigurePlayerLifecycle(OnPlayerDefeatPresentationCompleted,
            OnPlayerRespawnCompleted);
    }

    /**创建并登记玩家阵营飞机逻辑对象。*/
    internal AircraftVO CreatePlayerAircraft(string name, bool isPlayer,
        Vector2 position) {
        AircraftVO unit = new AircraftVO(CreateElementId(), name, isPlayer, position);
        AddElement(unit);
        if (isPlayer) {
            SetPlayerUnit(unit);
        }
        return unit;
    }

    /**结算一次僚机奖励：优先补齐槽位，否则升级较低等级僚机。*/
    internal AircraftVO ApplyWingmanReward(out bool created, out bool isLeft) {
        return formationModel.ApplyWingmanReward(CreatePlayerAircraft, out created, out isLeft);
    }

    /**配置本关特殊敌机和公共敌弹内容*/
    internal void ConfigureEnemyContent(EnemyConfigVO elite, BulletConfigVO defaultBullet) {
        stageModel.Configure(elite, defaultBullet);
    }

    /**更新玩家输入产生的权威逻辑坐标*/
    internal void SetPlayerPosition(Vector2 position) {
        playerUnit?.SetPosition(position);
    }

    /**更新当前玩家飞机用于逻辑碰撞的组合形状*/
    internal void SetPlayerCollision(AircraftCollisionVO collision) {
        formationModel.SetCollision(collision);
    }

    /**按当前飞机等级重建全部玩家发射器*/
    internal void ConfigurePlayerLaunchers(IReadOnlyList<PlayerBulletLauncherVO> configs) {
        playerUnit?.bulletLaunchers.Clear();
        if (configs == null) {
            return;
        }
        foreach (PlayerBulletLauncherVO config in configs) {
            playerUnit.bulletLaunchers.Add(new BulletLauncherVO(config));
        }
    }

    /**重置玩家发射器的整轮与轮内发射状态*/
    internal void ResetPlayerLaunchers() {
        playerUnit?.ResetLaunchers();
    }

    /**控制玩家发射器是否参与 playerTimer 更新*/
    internal void SetPlayerFiringEnabled(bool enabled) {
        playerUnit?.SetFiringEnabled(enabled);
    }

    /**应用玩家与僚机的局外养成等级。*/
    internal void ApplyPersistentFormationLevels(int playerLevel, int wingmanLevel) {
        formationModel.ApplyPersistentLevels(playerLevel, wingmanLevel);
        playerStatusChanged?.Invoke();
    }

    /**应用玩家与现有僚机的本关临时等级。*/
    internal void ApplyStageFormationBonus(int playerBonusLevel,
        int wingmanBonusLevel) {
        formationModel.ApplyStageBonus(playerBonusLevel, wingmanBonusLevel);
        playerStatusChanged?.Invoke();
    }

    /**移除玩家子弹并通知表现层回收 View*/
    internal bool RemovePlayerProjectile(BulletVO projectile) {
        if (projectile == null || !playerProjectiles.Remove(projectile)) {
            return false;
        }
        RemoveElement(projectile.id);
        playerProjectileRemoved?.Invoke(projectile.id);
        return true;
    }

    /**由逻辑层统一移除敌机并完成分数、波次、掉落与 Boss 推进结算*/
    internal bool ResolveEnemy(AircraftVO enemy, bool defeated) {
        if (enemy == null || !enemies.Remove(enemy)) {
            return false;
        }
        Vector2 position = enemy.position;
        RemoveElement(enemy.id);
        if (defeated && enemy.isBoss) {
            waitingForBossDeathPresentation = true;
        }
        enemyRemoved?.Invoke(enemy, defeated);
        if (enemy.showsSharedHealth) {
            ResolveSpecialEnemy(enemy, defeated);
            return true;
        }
        int completedWaveIndex = stageModel.RecordNormalEnemyResolved(defeated, enemies.Count);
        if (defeated) {
            AddScore(enemy.scoreValue);
        }
        if (completedWaveIndex >= 0) {
            SpawnReward(position, BattleRewardModel.GetWaveRewardType(completedWaveIndex));
        }
        return true;
    }

    /**收到 Boss 死亡表现完成通知后，开始结算前延迟。*/
    internal void NotifyBossDeathPresentationCompleted() {
        if (!waitingForBossDeathPresentation || bossVictoryDelayRemaining >= 0f) {
            return;
        }
        waitingForBossDeathPresentation = false;
        bossVictoryDelayRemaining = BattleConst.BossVictoryDelayAfterDeathPresentation;
    }

    /**登记一个由关卡逻辑生成的奖励道具*/
    internal RewardVO SpawnReward(Vector2 position, StageItemType type, bool isNaturalSupply = false) {
        return rewardModel.Spawn(position, type, isNaturalSupply, CreateElementId, AddElement, reward => rewardSpawned?.Invoke(reward));
    }

    /**按关卡时间自然生成补给，同屏最多保留一个自然补给*/
    private void UpdateNaturalSupply(float deltaTime) {
        rewardModel.UpdateNaturalSupply(deltaTime, stageModel.bossSpawned, SpawnNaturalReward);
    }

    private void SpawnNaturalReward(Vector2 position, StageItemType type, bool isNaturalSupply) {
        SpawnReward(position, type, isNaturalSupply);
    }

    /**同步奖励目标、清理越界奖励并结算拾取*/
    private void UpdateRewards() {
        rewardModel.Update(playerUnit, isPlayerAlive, playerCollision, RemoveReward, AddPlayerLifeFromReward, () => playerStatusChanged?.Invoke(), (reward, healed) => rewardCollected?.Invoke(reward, healed));
    }

    private void AddPlayerLifeFromReward(int value) {
        AddPlayerLife(value);
    }

    /**覆盖本局分数，供既有调试入口使用*/
    internal void SetScore(int value) {
        battleScore = Mathf.Max(0, value);
        scoreChanged?.Invoke(battleScore);
    }

    /**增加关卡内生命并返回最新数量*/
    internal int AddPlayerLife(int value) {
        int playerLifeCount = formationModel.AddLife(value);
        playerStatusChanged?.Invoke();
        return playerLifeCount;
    }

    /**升级演出期间暂停继续拾取升级奖励*/
    internal void SetPlayerUpgradeBlocked(bool value) {
        rewardModel.SetPlayerUpgradeBlocked(value);
    }

    /**移除奖励并通知表现层回收 View*/
    internal bool RemoveReward(RewardVO reward) {
        if (reward == null || !rewardDrops.Remove(reward)) {
            return false;
        }
        RemoveElement(reward.id);
        rewardRemoved?.Invoke(reward.id);
        return true;
    }

    /**移除敌方子弹并通知表现层回收 View*/
    internal bool RemoveEnemyProjectile(BulletVO projectile) {
        if (projectile == null || !enemyProjectiles.Remove(projectile)) {
            return false;
        }
        RemoveElement(projectile.id);
        enemyProjectileRemoved?.Invoke(projectile.id);
        return true;
    }

    /**销毁并清空本局全部逻辑数据*/
    public void Clear() {
        sceneModel.Clear();
        playerProjectiles.Clear();
        enemies.Clear();
        enemyProjectiles.Clear();
        rewardModel.Clear();
        formationModel.Clear();
        stageModel.Clear();
        InitializeStage(null);
    }

    /**推进场景元素和关卡波次*/
    private void OnSceneTimeUpdate(float deltaTime) {
        if (!flowModel.simulationActive || deltaTime <= 0f) {
            return;
        }
        sceneModel.UpdateElements(TimerType.SCENE, deltaTime);
        stageModel.Update(deltaTime, enemies.Count, SpawnNormalEnemy, SpawnSpecialEnemy);
        UpdateNaturalSupply(deltaTime);
        UpdateRewards();
        UpdateBossVictoryDelay(deltaTime);
        sceneTimeUpdated?.Invoke(deltaTime);
    }

    /**推进玩家阵营元素*/
    private void OnPlayerTimeUpdate(float deltaTime) {
        if (!flowModel.simulationActive || deltaTime <= 0f) {
            return;
        }
        sceneModel.UpdateElements(TimerType.PLAYER, deltaTime);
        RemoveOutOfBoundsPlayerProjectiles();
        playerTimeUpdated?.Invoke(deltaTime);
    }

    /**推进敌方阵营元素*/
    private void OnEnemyTimeUpdate(float deltaTime) {
        if (!flowModel.simulationActive || deltaTime <= 0f) {
            return;
        }
        sceneModel.UpdateElements(TimerType.ENEMY, deltaTime);
        RemoveOutOfBoundsEnemies();
        RemoveOutOfBoundsEnemyProjectiles();
        ResolveCombatContacts();
        enemyTimeUpdated?.Invoke(deltaTime);
    }

    /**根据普通波次请求创建一架敌机。*/
    private void SpawnNormalEnemy(EnemyWaveVO wave, int formationIndex) {
        Vector2 spawnPosition = GetEnemySpawnPosition(wave, formationIndex);
        CreateEnemy(wave.enemy, spawnPosition, wave.motionType, wave.enemy.moveSpeed, wave.enemy.fireInterval, wave.enemy.fireType, wave.enemy.score, formationIndex, wave.count, wave.direction);
    }

    /**根据特殊敌机请求创建精英或 Boss。*/
    private void SpawnSpecialEnemy(EnemyConfigVO config, Vector2 position) {
        CreateEnemy(config, position);
    }

    /**按编队中心和成员间距计算普通敌机出生点*/
    private static Vector2 GetEnemySpawnPosition(EnemyWaveVO wave, int enemyIndex) {
        float centeredIndex = enemyIndex - (wave.count - 1) * 0.5f;
        Vector2 position = wave.spawnCenter + new Vector2(centeredIndex * wave.spacing, 0f);
        if (wave.formationType == EnemyFormationType.DIAGONAL) {
            position.x = wave.direction > 0f
                ? 80f + enemyIndex * wave.spacing
                : 640f - enemyIndex * wave.spacing;
            position.y -= enemyIndex * 55f;
        }
        return position;
    }

    /**创建、登记并配置一架敌机逻辑对象*/
    private AircraftVO CreateEnemy(EnemyConfigVO config, Vector2 position,
        EnemyMotionType motionType = EnemyMotionType.STRAIGHT, float moveSpeed = 0f,
        float fireInterval = 0f, EnemyFireType fireType = EnemyFireType.SINGLE,
        int scoreValue = 0, int formationIndex = 0, int formationCount = 1,
        float motionDirection = 1f) {
        AircraftVO enemy = new AircraftVO(CreateElementId(), position, config.enemyClass,
            config.displaySize, config.collision, config.baseHealth, motionType,
            moveSpeed, fireInterval, fireType, scoreValue, formationIndex,
            formationCount, motionDirection, config.bullet, config.appearancePath);
        enemy.ConfigureDeathPresentation(config.deathExplosions, config.removeAfterDeathPresentation);
        enemy.ConfigureEnemyBehavior(
            () => playerUnit != null ? playerUnit.position : Vector2.zero,
            CreateEnemyProjectile, stageModel.defaultEnemyBullet);
        enemies.Add(enemy);
        AddElement(enemy);
        enemySpawned?.Invoke(enemy);
        return enemy;
    }

    /**创建玩家子弹逻辑对象并通知表现层补充 View*/
    private void CreatePlayerProjectile(AircraftVO owner, PlayerBulletLauncherVO launcher,
        int projectileIndex) {
        Vector2 spawnPosition = owner.position + launcher.offset;
        float direction = GetProjectileDirection(launcher, projectileIndex);
        BulletVO projectile = new BulletVO(
            CreateElementId(), spawnPosition, owner, launcher, direction);
        projectile.ConfigureTracking(FindNearestVisibleEnemy,
            target => enemies.Contains(target) && IsEnemyInsideBattleViewport(target));
        playerProjectiles.Add(projectile);
        AddElement(projectile);
        playerProjectileSpawned?.Invoke(projectile);
    }

    private void CreateEnemyProjectile(AircraftVO owner, Vector2 position, Vector2 velocity, BulletConfigVO config) {
        BulletVO projectile = new BulletVO(CreateElementId(), position, owner, velocity, config.hitSize, config.damage, config.hitEffectId, config.launchEffectId);
        enemyProjectiles.Add(projectile);
        AddElement(projectile);
        enemyProjectileSpawned?.Invoke(projectile, config);
    }

    /**根据发射器散布策略计算当前子弹的初始飞行角度*/
    private static float GetProjectileDirection(PlayerBulletLauncherVO launcher, int index) {
        int count = launcher.bulletCount;
        if (count <= 1 || launcher.spreadAngle <= 0f) {
            return launcher.direction;
        }
        float ratio = index / (float)(count - 1);
        switch (launcher.spreadType) {
            case BulletSpreadType.LEFT:
                return launcher.direction + launcher.spreadAngle * ratio;
            case BulletSpreadType.RIGHT:
                return launcher.direction - launcher.spreadAngle * ratio;
            default:
                return launcher.direction - launcher.spreadAngle * 0.5f +
                    launcher.spreadAngle * ratio;
        }
    }

    /**移除已经飞出战斗区域的玩家子弹*/
    private void RemoveOutOfBoundsPlayerProjectiles() {
        for (int i = playerProjectiles.Count - 1; i >= 0; i--) {
            BulletVO projectile = playerProjectiles[i];
            Vector2 position = projectile.position;
            if (position.y > 80f || position.x < -40f || position.x > 760f) {
                RemovePlayerProjectile(projectile);
            }
        }
    }

    /**移除已经飞出战斗区域的敌方子弹*/
    private void RemoveOutOfBoundsEnemyProjectiles() {
        for (int i = enemyProjectiles.Count - 1; i >= 0; i--) {
            BulletVO projectile = enemyProjectiles[i];
            Vector2 position = projectile.position;
            if (position.y < -1320f || position.x < -40f || position.x > 760f) {
                RemoveEnemyProjectile(projectile);
            }
        }
    }

    /**结算已经飞出关卡下边界的普通敌机*/
    private void RemoveOutOfBoundsEnemies() {
        for (int i = enemies.Count - 1; i >= 0; i--) {
            AircraftVO enemy = enemies[i];
            if (enemy.enemyClass == EnemyClass.NORMAL && enemy.position.y < -1380f) {
                ResolveEnemy(enemy, false);
            }
        }
    }

    /**查找当前战斗视窗内距离最近的敌机*/
    private AircraftVO FindNearestVisibleEnemy(Vector2 position) {
        AircraftVO nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (AircraftVO enemy in enemies) {
            if (!IsEnemyInsideBattleViewport(enemy)) {
                continue;
            }
            float distance = (enemy.position - position).sqrMagnitude;
            if (distance < nearestDistance) {
                nearest = enemy;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    /**判断敌机形象范围是否仍与战斗视窗重叠*/
    private static bool IsEnemyInsideBattleViewport(AircraftVO enemy) {
        if (enemy == null || enemy.destroyed) {
            return false;
        }
        Vector2 halfSize = enemy.size * 0.5f;
        Vector2 position = enemy.position;
        Rect enemyBounds = Rect.MinMaxRect(position.x - halfSize.x, position.y - halfSize.y,
            position.x + halfSize.x, position.y + halfSize.y);
        return Rect.MinMaxRect(0f, -1280f, 720f, 0f).Overlaps(enemyBounds);
    }

    /**集中检测三类战斗接触，并将命中结果通知表现协调层*/
    private void ResolveCombatContacts() {
        combatModel.Resolve(playerUnit, playerCollision, playerProjectiles, enemies, enemyProjectiles, RemovePlayerProjectile, ResolveEnemy, enemy => enemyHealthChanged?.Invoke(enemy), (projectile, enemy, point) => playerProjectileHitEnemy?.Invoke(projectile, enemy, point), RemoveEnemyProjectile, projectile => enemyProjectileHitPlayer?.Invoke(projectile), () => playerStatusChanged?.Invoke(), () => playerDefeatStarted?.Invoke());
    }

    /**结算精英或 Boss 的击毁结果*/
    private void ResolveSpecialEnemy(AircraftVO enemy, bool defeated) {
        if (!defeated) {
            return;
        }
        AddScore(enemy.isBoss ? BattleConst.BossScore : BattleConst.EliteScore);
        foreach (AircraftVO remainingEnemy in enemies) {
            if (remainingEnemy.showsSharedHealth) {
                return;
            }
        }
        if (enemy.isBoss) {
            return;
        }
        stageModel.TryRequestBoss(enemies.Count, SpawnSpecialEnemy);
    }

    /**使用 sceneTimer 推进 Boss 死亡表现结束后的结算延迟。*/
    private void UpdateBossVictoryDelay(float deltaTime) {
        if (bossVictoryDelayRemaining < 0f) {
            return;
        }
        bossVictoryDelayRemaining -= deltaTime;
        if (bossVictoryDelayRemaining > 0f) {
            return;
        }
        bossVictoryDelayRemaining = -1f;
        victoryRequested?.Invoke();
    }

    /**累加并广播权威战斗分数*/
    private void AddScore(int value) {
        battleScore = Mathf.Max(0, battleScore + value);
        scoreChanged?.Invoke(battleScore);
    }

    /**死亡表现完成后消费生命并决定失败或复活*/
    private void OnPlayerDefeatPresentationCompleted() {
        int playerLifeCount = formationModel.ConsumeLife();
        playerStatusChanged?.Invoke();
        if (playerLifeCount <= 0) {
            defeatRequested?.Invoke();
            return;
        }
        playerDefaultLevelRequested?.Invoke();
        playerUnit.BeginRespawn();
        ClearEnemyProjectiles();
        playerStatusChanged?.Invoke();
        playerRespawnStarted?.Invoke();
    }

    /**完成复活后恢复玩家编队的正常表现*/
    private void OnPlayerRespawnCompleted() {
        playerRespawnCompleted?.Invoke();
    }

    /**复活时清空全部敌方子弹*/
    private void ClearEnemyProjectiles() {
        for (int i = enemyProjectiles.Count - 1; i >= 0; i--) {
            RemoveEnemyProjectile(enemyProjectiles[i]);
        }
    }

}
