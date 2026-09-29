using System;
using System.Collections.Generic;
using System.Reflection;
using cfg;
using UnityEngine;

/// <summary>
/// 验证飞机死亡判定、坠落和残骸移动。
/// </summary>
internal static class RaidenAircraftDeathChecks {

    /**运行飞机死亡流程的模型检查*/
    public static void Run(Tables tables, RaidenModel configs) {
        EnemyConfigVO source = configs.GetEnemyConfig(tables.EnemyObj.DataList[0].Id);
        CheckExplosionSequences(source.collision);
        CheckDeathTypeBoundary(source);
        CheckFallingMovement(source);
        CheckBattleLanding(configs.GetStageConfig(tables.StageObj.DataList[0].Id));
        CheckWreckageMovement();
        Console.WriteLine("PASS: aircraft death explosions; death threshold; falling movement and rotation; landing state; wreckage ground scrolling.");
    }

    /**验证各体型和死亡方式生成的爆炸规模、数量、延迟和位置*/
    private static void CheckExplosionSequences(AircraftCollisionVO collision) {
        CheckExplosionSequence(AircraftSizeType.SMALL, AircraftDeathType.DISINTEGRATE, collision, AircraftExplosionSize.SMALL);
        CheckExplosionSequence(AircraftSizeType.MEDIUM, AircraftDeathType.DISINTEGRATE, collision, AircraftExplosionSize.SMALL, AircraftExplosionSize.MEDIUM);
        CheckExplosionSequence(AircraftSizeType.MEDIUM, AircraftDeathType.FALLING, collision, AircraftExplosionSize.SMALL, AircraftExplosionSize.SMALL);
        CheckExplosionSequence(AircraftSizeType.LARGE, AircraftDeathType.DISINTEGRATE, collision, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.LARGE);
        CheckExplosionSequence(AircraftSizeType.LARGE, AircraftDeathType.FALLING, collision, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM);
        CheckExplosionSequence(AircraftSizeType.GIANT, AircraftDeathType.DISINTEGRATE, collision, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.LARGE, AircraftExplosionSize.LARGE, AircraftExplosionSize.GIANT);
        CheckExplosionSequence(AircraftSizeType.GIANT, AircraftDeathType.FALLING, collision, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.MEDIUM, AircraftExplosionSize.LARGE, AircraftExplosionSize.LARGE);
    }

    /**验证单组动态爆炸序列*/
    private static void CheckExplosionSequence(AircraftSizeType aircraftSize, AircraftDeathType deathType, AircraftCollisionVO collision, params AircraftExplosionSize[] expectedSizes) {
        IReadOnlyList<AircraftExplosionVO> sequence = AircraftExplosionSequenceBuilder.Build(aircraftSize, deathType, collision, new System.Random(1));
        Require(sequence.Count == expectedSizes.Length, $"{aircraftSize} {deathType} explosion count differs");
        int expectedDelayMs = 0;
        for (int index = 0; index < sequence.Count; index++) {
            AircraftExplosionVO explosion = sequence[index];
            Require(explosion.delayMs == expectedDelayMs, $"{aircraftSize} {deathType} explosion delay differs at {index}");
            Require(collision.ContainsPoint(explosion.localPosition), $"{aircraftSize} {deathType} explosion is outside collision area");
            Require(Contains(BattleAircraftDeathConst.GetExplosionEffectIds(expectedSizes[index]), explosion.effectId), $"{aircraftSize} {deathType} explosion used the wrong size list");
            if (index + 1 < sequence.Count) {
                bool nextIsDelayedLastExplosion = aircraftSize >= AircraftSizeType.LARGE && index + 1 == sequence.Count - 1;
                expectedDelayMs += nextIsDelayedLastExplosion ? BattleAircraftDeathConst.LastExplosionIntervalMs : BattleAircraftDeathConst.NormalExplosionIntervalMs;
            }
        }
    }

    /**验证近期伤害阈值等号归入爆炸解体*/
    private static void CheckDeathTypeBoundary(EnemyConfigVO source) {
        int maxHealth = 10000;
        int thresholdDamage = maxHealth * BattleAircraftDeathConst.DisintegrateDamageRate / 10000;
        EnemyAircraftVO equal = CreateEnemy(301, source, maxHealth);
        equal.TakeDamage(maxHealth - thresholdDamage);
        ExpireRecentDamage(equal);
        equal.TakeDamage(thresholdDamage);
        Require(AircraftDeathResolver.Resolve(equal) == AircraftDeathType.DISINTEGRATE, "Exact recent-damage threshold did not disintegrate");

        EnemyAircraftVO below = CreateEnemy(302, source, maxHealth);
        below.TakeDamage(maxHealth - thresholdDamage + 1);
        ExpireRecentDamage(below);
        below.TakeDamage(thresholdDamage - 1);
        Require(AircraftDeathResolver.Resolve(below) == AircraftDeathType.FALLING, "Damage below threshold did not fall");

        PlayerAircraftUnitVO player = new PlayerAircraftUnitVO(303, "death-player", Vector2.zero);
        player.ApplyBaseAttributes(new Dictionary<AttributeType, int> { { AttributeType.MAX_LIFE, maxHealth } });
        player.TakeDamage(maxHealth);
        Require(AircraftDeathResolver.Resolve(player) == AircraftDeathType.DISINTEGRATE, "Player aircraft did not always disintegrate");
    }

    /**验证两种坠落初速度、高度和销毁后的坠地标记*/
    private static void CheckFallingMovement(EnemyConfigVO source) {
        EnemyAircraftVO moving = CreateEnemy(304, source, 10000);
        SetVelocity(moving, new Vector2(40f, 0f));
        Vector2 movingStart = moving.position;
        moving.BeginDeathPresentation(AircraftDeathType.FALLING);
        moving.OnTimeUpdate(1f);
        Require(Mathf.Approximately(moving.position.x - movingStart.x, 20f), "Moving aircraft did not keep half velocity while falling");
        float expectedHeight = BattleAircraftDeathConst.NormalHeight - BattleAircraftDeathConst.FallingHeightAcceleration * 0.5f;
        float expectedScale = Mathf.Clamp(expectedHeight / BattleAircraftDeathConst.NormalHeight, BattleAircraftDeathConst.GroundAircraftDisplayScale, 1f);
        Require(Mathf.Approximately(moving.height, expectedHeight) && Mathf.Approximately(moving.displayScale, expectedScale), "Falling acceleration did not drive height and display scale");

        EnemyAircraftVO stationary = CreateEnemy(305, source, 10000);
        Vector2 stationaryStart = stationary.position;
        float rotationStart = stationary.rotation;
        stationary.BeginDeathPresentation(AircraftDeathType.FALLING);
        Require(source.collision.ContainsPoint(stationary.fallingSmokeLocalPosition), "Falling smoke point is outside collision area");
        Require(Contains(BattleAircraftDeathConst.FallingSmokeEffectIds, stationary.fallingSmokeEffectId), "Falling smoke used an unconfigured effect");
        Vector2 initialSmokePosition = stationary.position + stationary.fallingSmokeLocalPosition * stationary.displayScale;
        Require(Vector2.Distance(stationary.GetFallingSmokeWorldPosition(), initialSmokePosition) < 0.001f, "Starting falling applied the decorative aircraft rotation to attachment points");
        stationary.OnTimeUpdate(1f);
        Require(Mathf.Abs(Vector2.Distance(stationary.position, stationaryStart) - BattleAircraftDeathConst.StationaryFallingSpeed) < 0.01f, "Stationary aircraft did not receive fixed falling speed");
        IReadOnlyList<int> angularAccelerationRange = BattleAircraftDeathConst.FallingAngularAccelerationRange;
        float rotationDelta = Mathf.DeltaAngle(rotationStart, stationary.rotation);
        float angularAcceleration = rotationDelta * 2f;
        Require(angularAcceleration >= angularAccelerationRange[0] && angularAcceleration <= angularAccelerationRange[1], "Falling angular acceleration is outside configured range");
        Vector2 scaledSmokePosition = stationary.fallingSmokeLocalPosition * stationary.displayScale;
        float smokeRadians = stationary.deathRotationOffset * Mathf.Deg2Rad;
        Vector2 expectedSmokePosition = stationary.position + new Vector2(scaledSmokePosition.x * Mathf.Cos(smokeRadians) - scaledSmokePosition.y * Mathf.Sin(smokeRadians), scaledSmokePosition.x * Mathf.Sin(smokeRadians) + scaledSmokePosition.y * Mathf.Cos(smokeRadians));
        Require(Vector2.Distance(stationary.GetFallingSmokeWorldPosition(), expectedSmokePosition) < 0.001f, "Falling smoke did not follow scaled and rotated attachment position");
        stationary.OnTimeUpdate(9f);
        Require(stationary.hasLanded && Mathf.Approximately(stationary.height, BattleAircraftDeathConst.GroundHeight), "Aircraft did not land at minimum height");
        Require(Mathf.Approximately(stationary.displayScale, BattleAircraftDeathConst.GroundAircraftDisplayScale), "Landed aircraft did not use configured display scale");
        stationary.Destroy();
        Require(stationary.hasLanded, "Destroying landed aircraft erased the presentation landing state");
    }

    /**验证残骸跟随地面层滚动*/
    private static void CheckWreckageMovement() {
        AircraftWreckageVO wreckage = new AircraftWreckageVO(306, new Vector2(100f, -200f), "hole", "damaged", Vector2.one, 0f, 50f);
        wreckage.OnTimeUpdate(2f);
        Require(wreckage.position == new Vector2(100f, -300f), "Wreckage did not follow ground scroll speed");
    }

    /**验证战斗模型在坠地时生成残骸并销毁飞机实体*/
    private static void CheckBattleLanding(StageConfigVO stage) {
        EnemyWaveVO wave = stage.bossWave;
        EnemyConfigVO source = wave.enemy;
        EnemyAircraftVO enemy = new EnemyAircraftVO(307, wave.spawnCenter, source.enemyClass, source.displaySize, source.collision, new Dictionary<AttributeType, int> { { AttributeType.MAX_LIFE, 10000 } }, appearancePath: source.appearancePath, damagedAppearancePath: source.damagedAppearancePath, specialMotion: wave);
        enemy.ConfigureFirePoints(source.aircraftSizeType, source.collision);
        int thresholdDamage = BattleAircraftDeathConst.DisintegrateDamageRate;
        enemy.TakeDamage(10000 - thresholdDamage + 1);
        ExpireRecentDamage(enemy);
        enemy.TakeDamage(thresholdDamage - 1);

        BattleModel battle = new BattleModel();
        battle.InitializeStage(stage);
        battle.ConfigureGroundScroll(50f);
        battle.enemies.Add(enemy);
        battle.AddElement(enemy);
        int spawnedWreckages = 0;
        battle.aircraftWreckageSpawned += wreckage => spawnedWreckages++;
        Require(battle.ResolveEnemy(enemy, true) && enemy.deathType == AircraftDeathType.FALLING, "Battle model did not start falling death");
        enemy.OnTimeUpdate(10f);
        Require(spawnedWreckages == 1 && battle.aircraftWreckages.Count == 1, "Landing did not create exactly one wreckage");
        Require(enemy.destroyed && enemy.hasLanded && !battle.elements.ContainsKey(enemy.id), "Landing did not end the aircraft logic lifecycle");
        Require(battle.elements.ContainsKey(battle.aircraftWreckages[0].id), "Landing wreckage was not registered as a scene element");
        Require(Mathf.Approximately(battle.aircraftWreckages[0].aircraftRotation, enemy.rotation), "Wreckage did not inherit landing rotation");
        battle.Clear();
    }

    /**创建具备中型体型和固定生命的测试敌机*/
    private static EnemyAircraftVO CreateEnemy(long id, EnemyConfigVO source, int maxHealth) {
        EnemyAircraftVO enemy = new EnemyAircraftVO(id, Vector2.zero, EnemyClass.NORMAL, source.displaySize, source.collision, new Dictionary<AttributeType, int> { { AttributeType.MAX_LIFE, maxHealth } }, damagedAppearancePath: source.damagedAppearancePath);
        enemy.ConfigureFirePoints(AircraftSizeType.MEDIUM, source.collision);
        return enemy;
    }

    /**令已有近期伤害越过配置统计窗口*/
    private static void ExpireRecentDamage(AircraftVO aircraft) {
        FieldInfo field = typeof(AircraftVO).GetField("recentDamageTracker", BindingFlags.Instance | BindingFlags.NonPublic);
        RecentDamageTracker tracker = (RecentDamageTracker)field.GetValue(aircraft);
        tracker.Update(BattleAircraftDeathConst.RecentDamageDurationMs / 1000f + 0.001f);
    }

    /**设置死亡前一帧记录的实际速度*/
    private static void SetVelocity(FlyingUnitVO unit, Vector2 velocity) {
        PropertyInfo property = typeof(FlyingUnitVO).GetProperty("velocity", BindingFlags.Instance | BindingFlags.Public);
        property.SetValue(unit, velocity);
    }

    /**判断候选列表是否包含指定特效ID*/
    private static bool Contains(IReadOnlyList<int> values, int value) {
        foreach (int candidate in values) {
            if (candidate == value) {
                return true;
            }
        }
        return false;
    }

    /**要求条件成立，否则报告对应业务行为*/
    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

}
