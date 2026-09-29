using System;
using System.Collections.Generic;
using System.Reflection;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 验证飞机着火规则与生命周期。
/// </summary>
/// <remarks>
/// 从正式导表结果读取规则，独立验证血量边界、组合碰撞取点、计时器与资源依赖。
/// </remarks>
internal static class RaidenAircraftFireChecks {

    /**运行着火功能的模型与配置回归检查*/
    public static void Run(Tables tables, RaidenModel configs) {
        CheckRuleBoundaries(tables);
        CheckConfigurationChain(tables, configs);
        CheckCollisionSampling();
        CheckHealthTransitions(tables);
        CheckDeathAndTimers(tables, configs);
        CheckBattleDeathRegistration(tables, configs);
        Console.WriteLine("PASS: aircraft fire rules; exact health boundaries; collision union sampling; health transitions; death retention; owner timers; effect preload candidates.");
    }

    /**要求条件成立，否则报告对应业务行为*/
    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 检查左开右闭边界。
    /// </summary>
    /// <remarks>
    /// 保留万分比以下的生命比例，避免整数除法把区间外的生命值误判到边界内。
    /// </remarks>
    private static void CheckRuleBoundaries(Tables tables) {
        Require(tables.AircraftFireRuleObj.DataList.Count > 0, "Aircraft fire table is empty");
        foreach (AircraftFireRuleResource rule in tables.AircraftFireRuleObj.DataList) {
            Require(ReferenceEquals(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, rule.HealthRateMax, 10000), rule), $"Fire rule {rule.Id} must include upper boundary");
            Require(!ReferenceEquals(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, rule.HealthRateMin, 10000), rule), $"Fire rule {rule.Id} must exclude lower boundary");
            Require(!ReferenceEquals(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, rule.HealthRateMax * 10 + 1, 100000), rule), $"Fire rule {rule.Id} rounded a fractional ratio down at upper boundary");
            Require(ReferenceEquals(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, rule.HealthRateMin * 10 + 1, 100000), rule), $"Fire rule {rule.Id} lost a positive fractional ratio above lower boundary");
            Require(ReferenceEquals(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, rule.HealthRateMax * 200000, 2000000000), rule), $"Fire rule {rule.Id} overflowed with large health values");
            Require(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, 0, 10000) == null, "Zero health should not select a new fire rule");
            Require(BattleAircraftFireConst.FindRule(rule.AircraftSizeType, 10000, 10000) == null, "Full health should not select a fire rule");
        }
        Require(BattleAircraftFireConst.FindRule(AircraftSizeType.SMALL, 1, 10000) == null, "Small aircraft should not have fire rules");
    }

    /// <summary>
    /// 检查体型转换与特效预加载。
    /// </summary>
    /// <remarks>
    /// 只调用资源清单收集入口，不启动 Unity 资源加载或改写正式配置。
    /// </remarks>
    private static void CheckConfigurationChain(Tables tables, RaidenModel configs) {
        Require(ResourceConst.ALL_CONFIG_LIST.Contains("cfgobj_aircraftfireruleobj"), "Aircraft fire table is absent from config preload list");
        foreach (PlayerAircraftLevelResource source in tables.PlayerAircraftLevelObj.DataList) {
            Require(configs.GetPlayerAircraftBattleLevel(source.AircraftId, source.Level).aircraftSizeType == source.Unit.AircraftSizeType, $"Player level {source.Id} lost aircraft size");
        }
        foreach (EnemyResource source in tables.EnemyObj.DataList) {
            Require(configs.GetEnemyConfig(source.Id).aircraftSizeType == source.Unit.AircraftSizeType, $"Enemy {source.Id} lost aircraft size");
        }
        foreach (WingmanResource source in tables.WingmanObj.DataList) {
            Require(configs.GetWingmanConfig(source.Id).aircraftSizeType == source.Unit.AircraftSizeType, $"Wingman {source.Id} lost aircraft size");
        }

        List<ResLoadInfo> preload = new List<ResLoadInfo>();
        HashSet<string> keys = new HashSet<string>();
        MethodInfo collect = typeof(BattlePreloadCollector).GetMethod("AddFixedStageResources", BindingFlags.Static | BindingFlags.NonPublic);
        Require(collect != null, "Missing fixed stage resource collection entry");
        collect.Invoke(null, new object[] { preload, keys });
        int initialCount = preload.Count;
        collect.Invoke(null, new object[] { preload, keys });
        Require(preload.Count == initialCount, "Repeated fire dependency collection was not deduplicated");
        for (int id = 7001; id <= 7006; id++) {
            IReadOnlyList<int> effectIds = ConfigValueHelper.GetList<int>(id);
            Require(effectIds.Count > 0, $"Fire effect candidates {id} are empty");
            foreach (int effectId in effectIds) {
                EffectResource effect = tables.EffectObj.GetOrDefault(effectId);
                Require(effect != null && effect.Type == EffectType.OTHER, $"Fire candidate {effectId} does not reference an ordinary effect");
                string path = BattlePreloadCollector.GetEffectResourcePath(effect);
                Require(preload.Exists(resource => resource.resType == ResType.FrameAnim && resource.path == path), $"Fire effect candidate {effectId} is missing from preload list");
            }
        }
        Require(ConfigValueHelper.GetInt(7007) > 0, "Smoke interval must advance in positive milliseconds");
    }

    /// <summary>
    /// 检查实际组合形状内的随机取点。
    /// </summary>
    /// <remarks>
    /// 使用断开的矩形、圆形和重叠矩形，独立计算命中条件，防止仅在总包围盒内取点。
    /// </remarks>
    private static void CheckCollisionSampling() {
        AircraftCollisionVO disconnected = AircraftCollisionVO.Create(new Shape[] { CreateRectangle(2f, 2f, 4f, 0.5f), CreateRectangle(2f, 2f, -3f, 0.5f) });
        Require(!disconnected.ContainsPoint(Vector2.zero), "Disconnected collision union incorrectly includes its bounding-box center");
        System.Random random = new System.Random(34821);
        int left = 0;
        int right = 0;
        for (int index = 0; index < 2000; index++) {
            Vector2 point = disconnected.GetRandomPoint(random);
            bool inLeft = point.x >= -8f && point.x <= -6f && Math.Abs(point.y) <= 1f;
            bool inRight = point.x >= 6f && point.x <= 8f && Math.Abs(point.y) <= 1f;
            Require(inLeft || inRight, "Random fire point fell into the gap between disconnected shapes");
            left += inLeft ? 1 : 0;
            right += inRight ? 1 : 0;
        }
        Require(left > 100 && right > 100, "Random fire points ignored one disconnected shape");

        AircraftCollisionVO mixed = AircraftCollisionVO.Create(new Shape[] { new CircleShape(SimpleJSON.JSON.Parse("{\"radius\":2}")), CreateRectangle(2f, 2f, -2f, 0.5f) });
        Require(mixed.ContainsPoint(new Vector2(0f, 2f)) && !mixed.ContainsPoint(new Vector2(1.9f, 1.9f)), "Circle containment used its square bounds");
        for (int index = 0; index < 2000; index++) {
            Vector2 point = mixed.GetRandomPoint(random);
            bool inCircle = point.sqrMagnitude <= 4.0001f;
            bool inRectangle = point.x >= 4f && point.x <= 6f && Math.Abs(point.y) <= 1f;
            Require(inCircle || inRectangle, "Random point is outside the mixed collision union");
        }

        AircraftCollisionVO overlap = AircraftCollisionVO.Create(new Shape[] { CreateRectangle(2f, 2f, 0.5f, 0.5f), CreateRectangle(2f, 2f, 0f, 0.5f) });
        int[] regionCounts = new int[3];
        for (int index = 0; index < 9000; index++) {
            Vector2 point = overlap.GetRandomPoint(random);
            Require(point.x >= -1f && point.x <= 2f && Math.Abs(point.y) <= 1f, "Random point is outside overlapping rectangles");
            regionCounts[point.x < 0f ? 0 : point.x <= 1f ? 1 : 2]++;
        }
        foreach (int count in regionCounts) {
            Require(count > 2500 && count < 3500, "Overlapping collision area was sampled more often than equal non-overlapping area");
        }
    }

    /**创建独立测试用的矩形配置*/
    private static RectangleShape CreateRectangle(float width, float height, float pivotX, float pivotY) {
        SimpleJSON.JSONObject node = new SimpleJSON.JSONObject();
        SimpleJSON.JSONObject rect = new SimpleJSON.JSONObject();
        SimpleJSON.JSONObject pivot = new SimpleJSON.JSONObject();
        rect["x"] = width;
        rect["y"] = height;
        pivot["x"] = pivotX;
        pivot["y"] = pivotY;
        node["rect"] = rect;
        node["pivot"] = pivot;
        return new RectangleShape(node);
    }

    /**创建具有完整生命值的测试飞机*/
    private static PlayerAircraftUnitVO CreatePlayer(long id, AircraftSizeType size, AircraftCollisionVO collision) {
        PlayerAircraftUnitVO player = new PlayerAircraftUnitVO(id, "fire-check", new Vector2(70f, -120f));
        player.ApplyBaseAttributes(new Dictionary<AttributeType, int> { { AttributeType.MAX_LIFE, 10000 } });
        player.ConfigureFirePoints(size, collision);
        return player;
    }

    /// <summary>
    /// 检查血量变化与着火点稳定性。
    /// </summary>
    /// <remarks>
    /// 检查点对象身份，确保同一血量区间内坐标和随机特效不会重新抽取。
    /// </remarks>
    private static void CheckHealthTransitions(Tables tables) {
        AircraftCollisionVO collision = AircraftCollisionVO.Create(new Shape[] { CreateRectangle(12f, 4f, 0.25f, 0.5f) });
        foreach (AircraftFireRuleResource rule in tables.AircraftFireRuleObj.DataList) {
            PlayerAircraftUnitVO unit = CreatePlayer(40 + rule.Id, rule.AircraftSizeType, collision);
            unit.TakeDamage(10000 - rule.HealthRateMax);
            CheckPointComposition(unit, rule);
            foreach (AircraftFirePointVO point in unit.firePoints) {
                Require(collision.ContainsPoint(point.localPosition), "Fire point is outside configured collision shapes");
                Vector2 before = unit.GetFirePointWorldPosition(point);
                Vector2 movement = new Vector2(37f, -29f);
                unit.SetPosition(unit.position + movement);
                Require((unit.GetFirePointWorldPosition(point) - before - movement).sqrMagnitude < 0.0001f, "Fire point world position did not follow its owning unit");
            }
            unit.Destroy();
        }

        PlayerAircraftUnitVO player = CreatePlayer(100, AircraftSizeType.MEDIUM, collision);
        int changes = 0;
        player.firePointsChanged += unit => changes++;
        Require(player.firePoints.Count == 0, "Healthy player started burning");
        player.TakeDamage(3000);
        Require(player.firePoints.Count > 0 && changes == 1, "70 percent health did not create fire points");
        AircraftFirePointVO first = player.firePoints[0];
        player.TakeDamage(1000);
        player.Heal(500);
        player.OnTimeUpdate(0f);
        Require(changes == 1 && ReferenceEquals(first, player.firePoints[0]), "Damage or healing within a bracket rebuilt fire points");
        int upperBracketCount = player.firePoints.Count;
        player.TakeDamage(3500);
        Require(changes == 2 && ReferenceEquals(first, player.firePoints[0]) && player.firePoints.Count > upperBracketCount, "Crossing to 30 percent did not retain existing points and add the difference");
        player.Heal(1);
        Require(changes == 3 && ReferenceEquals(first, player.firePoints[0]) && player.firePoints.Count == upperBracketCount, "Healing above 30 percent did not retain existing points and remove the difference");
        player.RestoreFullHealth();
        Require(changes == 4 && player.firePoints.Count == 0, "Restoring full health did not clear fire points");
        player.TakeDamage(6500);
        first = player.firePoints[0];
        AttributeContainer maxHealthChange = new AttributeContainer();
        maxHealthChange.SetAttr(AttributeType.MAX_LIFE, -1000);
        player.battleAttributeContainer.AddChild(maxHealthChange);
        Require(ReferenceEquals(first, player.firePoints[0]), "Maximum health changes did not retain an existing point while adding the difference");
        player.battleAttributeContainer.RemoveChild(maxHealthChange);
        Require(player.maxHealth == 10000 && player.health == 3500, "Maximum health source removal did not restore state limits");
        player.Destroy();
        int afterDestroy = changes;
        player.Destroy();
        player.TakeDamage(1);
        Require(player.firePoints.Count == 0 && changes == afterDestroy, "Destroyed aircraft recreated fire points or repeated destruction notifications");

    }

    /**核对着火点规模、数量和特效所属队列*/
    private static void CheckPointComposition(PlayerAircraftUnitVO unit, AircraftFireRuleResource rule) {
        Dictionary<AircraftFireSize, int> expected = new Dictionary<AircraftFireSize, int>();
        foreach (AircraftFirePointCount group in rule.FirePointList) {
            expected[group.FireSize] = group.Count;
        }
        foreach (AircraftFirePointVO point in unit.firePoints) {
            Require(expected.ContainsKey(point.fireSize), $"Fire rule {rule.Id} created an unconfigured size");
            expected[point.fireSize]--;
            Require(Contains(ConfigValueHelper.GetList<int>(7000 + (int)point.fireSize), point.fireEffectId), "Fire effect was chosen from the wrong size queue");
            Require(Contains(ConfigValueHelper.GetList<int>(7003 + (int)point.fireSize), point.smokeEffectId), "Smoke effect was chosen from the wrong size queue");
        }
        foreach (int remaining in expected.Values) {
            Require(remaining == 0, $"Fire rule {rule.Id} point count differs from the configured composition");
        }
    }

    /**判断只读配置队列是否包含指定 ID*/
    private static bool Contains(IReadOnlyList<int> values, int id) {
        foreach (int value in values) {
            if (value == id) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查死亡保留与所属计时器。
    /// </summary>
    /// <remarks>
    /// 场景移除才结束着火点生命周期；死亡过程中仍用原单位的 Timer 生成烟雾。
    /// </remarks>
    private static void CheckDeathAndTimers(Tables tables, RaidenModel configs) {
        StageConfigVO stage = configs.GetStageConfig(tables.StageObj.DataList[0].Id);
        EnemyWaveVO wave = stage.bossWave;
        EnemyConfigVO source = wave.enemy;
        EnemyAircraftVO enemy = new EnemyAircraftVO(201, wave.spawnCenter, source.enemyClass, source.displaySize, source.collision, source.baseAttributes, specialMotion: wave);
        enemy.ApplyBaseAttributes(new Dictionary<AttributeType, int> { { AttributeType.MAX_LIFE, 10000 }, { AttributeType.SPEED, 10 } });
        enemy.ConfigureFirePoints(AircraftSizeType.GIANT, source.collision);
        enemy.TakeDamage(8000);
        AircraftFirePointVO first = enemy.firePoints[0];
        int enemyPointCount = enemy.firePoints.Count;
        enemy.TakeDamage(10000);
        enemy.BeginDeathPresentation(AircraftDeathType.DISINTEGRATE);
        enemy.OnLastDeathExplosionStarted();
        Require(enemy.health == 0 && !enemy.destroyed && enemy.firePoints.Count == enemyPointCount && ReferenceEquals(first, enemy.firePoints[0]), "Enemy death cleared or rebuilt fire points before destruction");

        PlayerAircraftUnitVO player = CreatePlayer(202, AircraftSizeType.MEDIUM, source.collision);
        player.TakeDamage(8000);
        int playerPointCount = player.firePoints.Count;
        player.TryTakeDamage(10000);
        Require(player.lifecycleState == PlayerLifecycleState.Dying && player.firePoints.Count == playerPointCount, "Player death removed its existing fire points");

        BattleSceneModel scene = new BattleSceneModel();
        scene.AddElement(enemy);
        scene.AddElement(player);
        int enemySmoke = 0;
        int playerSmoke = 0;
        enemy.smokeRequested += (owner, point) => enemySmoke++;
        player.smokeRequested += (owner, point) => playerSmoke++;
        float interval = ConfigValueHelper.GetInt(7007) / 1000f;
        scene.UpdateElements(TimerType.SCENE, interval * 10f);
        Require(enemySmoke == 0 && playerSmoke == 0, "Scene timer advanced aircraft smoke");
        scene.UpdateElements(TimerType.PLAYER, interval * 0.5f);
        Require(playerSmoke == 0 && enemySmoke == 0, "Smoke was generated before its configured interval");
        scene.UpdateElements(TimerType.PLAYER, interval * 0.501f);
        Require(playerSmoke == playerPointCount && enemySmoke == 0, "Player smoke ignored player timer ownership");
        scene.UpdateElements(TimerType.ENEMY, 0f);
        Require(enemySmoke == 0, "Paused enemy timer generated smoke");
        scene.UpdateElements(TimerType.ENEMY, interval * 1.001f);
        Require(enemySmoke == enemyPointCount, "Dying enemy did not generate smoke on enemy timer");
        scene.RemoveElement(enemy.id);
        scene.UpdateElements(TimerType.ENEMY, interval * 10f);
        Require(enemy.destroyed && enemy.firePoints.Count == 0 && enemySmoke == enemyPointCount, "Removed enemy kept fire points or continued generating smoke");
        player.BeginRespawn();
        Require(player.health == player.maxHealth && player.firePoints.Count == 0, "Player respawn retained previous-life fire points");
        scene.Clear();

        PlayerAircraftUnitVO oneShot = CreatePlayer(203, AircraftSizeType.MEDIUM, source.collision);
        oneShot.TryTakeDamage(20000);
        Require(oneShot.firePoints.Count == 0, "A full-health instant kill created a new zero-health fire pattern");
        oneShot.Destroy();
    }

    /// <summary>
    /// 检查战斗入口中的死亡元素登记。
    /// </summary>
    /// <remarks>
    /// 敌机退出可战斗列表后仍应保留在场景计时器中，直到死亡表现要求真正移除。
    /// </remarks>
    private static void CheckBattleDeathRegistration(Tables tables, RaidenModel configs) {
        StageConfigVO stage = configs.GetStageConfig(tables.StageObj.DataList[0].Id);
        EnemyWaveVO wave = stage.bossWave;
        EnemyConfigVO source = wave.enemy;
        foreach (EnemyClass enemyClass in new[] { EnemyClass.ELITE, EnemyClass.BOSS }) {
            BattleModel battle = new BattleModel();
            battle.InitializeStage(stage);
            EnemyAircraftVO enemy = new EnemyAircraftVO(battle.CreateElementId(), wave.spawnCenter, enemyClass, source.displaySize, source.collision, source.baseAttributes, specialMotion: wave);
            enemy.ApplyBaseAttributes(new Dictionary<AttributeType, int> { { AttributeType.MAX_LIFE, 10000 }, { AttributeType.SPEED, 10 } });
            enemy.ConfigureFirePoints(AircraftSizeType.GIANT, source.collision);
            enemy.TakeDamage(8000);
            int count = enemy.firePoints.Count;
            battle.AddElement(enemy);
            battle.enemies.Add(enemy);
            enemy.TakeDamage(10000);
            Require(battle.ResolveEnemy(enemy, true), "Battle did not resolve defeated enemy");
            Require(battle.enemies.Count == 0 && battle.elements.ContainsKey(enemy.id) && !enemy.destroyed, "Battle removed dying enemy from scene timer prematurely");
            int smokeCount = 0;
            enemy.smokeRequested += (owner, point) => smokeCount++;
            battle.StartBattle();
            MethodInfo advance = typeof(BattleModel).GetMethod("OnEnemyTimeUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            float interval = ConfigValueHelper.GetInt(7007) / 1000f;
            advance.Invoke(battle, new object[] { interval * 1.001f });
            Require(smokeCount == count, "Battle enemy timer omitted dying enemy smoke");
            battle.NotifyEnemyLastExplosionStarted(enemy);
            bool retained = enemyClass == EnemyClass.BOSS;
            Require(enemy.destroyed != retained, "Battle ignored aircraft death retention rule");
            advance.Invoke(battle, new object[] { interval * 1.001f });
            Require(smokeCount == count * (retained ? 2 : 1), "Death presentation removal and retained smoke lifetime disagree");
            battle.Clear();
            Require(enemy.destroyed && enemy.firePoints.Count == 0, "Battle exit did not clear retained enemy fire points");
        }
    }

}
