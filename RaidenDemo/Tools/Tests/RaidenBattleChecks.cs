using System;
using System.IO;
using System.Reflection;
using cfg;
using UnityEngine;

/// <summary>从真实导表结果验证路径、调度和死亡生命周期，不复制关卡数据。</summary>
internal static class RaidenBattleChecks {
    private static void Require(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }

    public static int Main(string[] args) {
        var tablesField = typeof(CfgManager).GetField("_tables", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = tablesField.GetValue(null);
        try {
            var tables = new Tables(name => SimpleJSON.JSON.Parse(File.ReadAllText(Path.Combine(args[0], name + ".json"))));
            tablesField.SetValue(null, tables);
            var configs = new RaidenModel();
            int paths = 0;
            foreach (var source in tables.StageObj.DataList) {
                var stage = configs.GetStageConfig(source.Id);
                foreach (var wave in stage.enemyWaves) {
                    if (wave.enemyClass != EnemyClass.NORMAL) continue;
                    CheckPath(wave);
                    paths++;
                }
                CheckStage(stage);
                CheckDeath(stage);
            }
            CheckLauncherBounds(tables, configs);
            Require(BattleConst.ClampPlayerPosition(new Vector2(-1, 1)).y == BattleConst.PlayerAreaTop, "Player upper boundary");
            Require(BattleConst.ClampPlayerPosition(new Vector2(9999, -9999)).y == -BattleConst.BattleViewportHeight, "Player lower boundary");
            Console.WriteLine($"PASS: {tables.StageObj.DataList.Count} stages; {paths} configured paths; independent timers; rewards; Boss supply; death movement and configured victory delay.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        } finally {
            tablesField.SetValue(null, previous);
        }
    }

    private static void CheckLauncherBounds(Tables tables, RaidenModel configs) {
        var bullet = tables.BulletObj.DataList[0];
        var owner = new AircraftVO(1, "test", true, Vector2.zero);
        int emitted = 0;
        var maximum = new BulletLauncherVO(new BulletLauncherConfigVO(Vector2.zero, bullet.Type, bullet.Level,
            999, 0f, -1, 0f, default(cfg.BulletSpreadType), 0f), configs.GetBulletConfig);
        maximum.Update(.001f, owner, launch => emitted++);
        Require(emitted == BattleConst.shotCountMax, "Launcher maximum count or zero interval boundary");
        maximum.Update(.008f, owner, launch => emitted++);
        Require(emitted == BattleConst.shotCountMax, "Launcher fired before minimum cooldown");
        maximum.Update(.002f, owner, launch => emitted++);
        Require(emitted > BattleConst.shotCountMax, "Launcher did not fire after minimum cooldown");

        emitted = 0;
        var minimum = new BulletLauncherVO(new BulletLauncherConfigVO(Vector2.zero, bullet.Type, bullet.Level,
            0, 1f, 0, 0f, default(cfg.BulletSpreadType), 0f), configs.GetBulletConfig);
        minimum.Update(.001f, owner, launch => emitted++);
        Require(emitted == BattleConst.shotCountMin, "Launcher minimum count boundary");
    }

    private static void CheckPath(EnemyWaveVO wave) {
        var path = new EnemyFormationPathVO(wave);
        float halfHeight = (wave.enemy.displaySize.y + wave.enemy.displaySize.x * Mathf.Sin(BattleConst.EnemyFormationBankAngle * Mathf.Deg2Rad)) * .5f;
        bool fired = false;
        // Bound derived from the configured route and speed, with room for entry and exit.
        float limit = 4f * (Mathf.Abs(wave.spawnCenter.y) + BattleConst.BattleViewportHeight + BattleConst.BattleViewportWidth) /
            (wave.enemy.moveSpeed * Mathf.Min(1f, wave.entrySpeedMultiplier)) + wave.attackDuration;
        float step = .02f;
        for (float t = 0; t < limit && !path.isLeaving; t += step) {
            path.Update(step);
            for (int i = 0; i < wave.count; i++) {
                Vector2 p = path.GetMemberPosition(i);
                Require(p.y - halfHeight >= BattleConst.EnemyActivityBottom - .01f, $"Wave {wave.id} crossed lower boundary");
                if (i > 0) {
                    Vector2 actual = p - path.GetMemberPosition(0);
                    Vector2 expected = wave.layout.GetOffset(i) - wave.layout.GetOffset(0);
                    Require((actual - expected).sqrMagnitude < .001f, $"Wave {wave.id} formation drift");
                }
            }
            fired |= path.canFire;
        }
        Require(fired && path.isLeaving && !path.canFire, $"Wave {wave.id} missing attack/exit phase");
    }

    private static void CheckStage(StageConfigVO config) {
        var stage = new BattleStageModel();
        stage.Initialize(config);
        int active = 0, bossCount = 0;
        EnemyFormationPathVO current = null;
        Action<EnemyWaveVO, int, EnemyFormationPathVO> normal = (w, i, p) => { active++; current = p; };
        Action<EnemyWaveVO> special = w => { if (w.enemyClass == EnemyClass.BOSS) bossCount++; else active++; };
        if (config.enemyWaves.Length > 0) {
            stage.TryRequestBoss(0, special);
            Require(bossCount == 0, "Boss spawned before ordinary waves");
        }
        foreach (var wave in config.enemyWaves) {
            stage.Update(config.waveInterval + .01f, active, normal, special);
            Require(active == wave.count, "Wave count mismatch");
            if (current != null) {
                Vector2 before = current.GetMemberPosition(0);
                stage.Update(.1f, active, normal, special);
                Require(current.GetMemberPosition(0) == before, "Scene timer advanced enemy path");
                stage.UpdateEnemyMovement(.1f);
                Require(current.GetMemberPosition(0) != before, "Enemy timer did not advance path");
                current = null;
            }
            int rewardIndex = -1;
            while (active > 0) rewardIndex = stage.RecordNormalEnemyResolved(true, --active);
            if (wave.enemyClass == EnemyClass.NORMAL) {
                Require(rewardIndex >= 0 && stage.GetWaveRewardItemId(rewardIndex) == wave.rewardItemId, "Configured wave reward mismatch");
            }
        }
        stage.Update(config.waveInterval + .01f, 0, normal, special);
        Require(stage.bossSpawned && bossCount == 1, "Boss progression");
        stage.Update(config.waveInterval + .01f, 0, normal, special);
        Require(bossCount == 1, "Duplicate Boss");
        var rewards = new BattleRewardModel((min, max) => (min + max) * .5f);
        rewards.Initialize(config);
        int supplied = 0;
        Action<Vector2, cfg.resource.StageItemResource> supply = (p, item) => {
            Require(item.Id == config.supplyItems[supplied % config.supplyItems.Count].Id, "Supply order");
            supplied++;
        };
        rewards.UpdateNaturalSupply(0f, supply);
        Require(supplied == 0, "Paused supply advanced");
        rewards.UpdateNaturalSupply(config.supplyFirstDelay + .001f, supply);
        for (int i = 0; i < config.supplyItems.Count; i++) rewards.UpdateNaturalSupply(config.supplyInterval, supply);
        Require(config.supplyItems.Count == 0 || supplied == config.supplyItems.Count + 1, "Boss supply stopped");
        stage.Initialize(config);
        active = 0;
        stage.Update(.01f, active, normal, special);
        while (active > 0) Require(stage.RecordNormalEnemyResolved(false, --active) == -1, "Escaped enemy granted full-kill reward");
    }

    private static void CheckDeath(StageConfigVO config) {
        var wave = config.bossWave;
        var enemy = wave.enemy;
        var boss = new AircraftVO(1, wave.spawnCenter, enemy.enemyClass, enemy.displaySize, enemy.collision,
            enemy.baseHealth, moveSpeed: enemy.moveSpeed, scoreValue: enemy.score, specialMotion: wave);
        boss.ConfigureDeathPresentation(enemy.deathExplosions, enemy.removeAfterDeathPresentation);
        boss.BeginEnemyDeathPresentation();
        boss.OnLastDeathExplosionStarted();
        Vector2 before = boss.position;
        boss.OnTimeUpdate(.1f);
        Require(boss.deathMovementActive == !enemy.removeAfterDeathPresentation, "Boss death movement policy");
        Require(enemy.removeAfterDeathPresentation || boss.position != before, "Retained Boss stopped moving");
        var battle = new BattleModel();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((BattleStageModel)typeof(BattleModel).GetField("stageModel", flags).GetValue(battle)).Initialize(config);
        typeof(BattleModel).GetField("waitingForBossDeathPresentation", flags).SetValue(battle, true);
        int victories = 0;
        battle.victoryRequested += () => victories++;
        var advance = typeof(BattleModel).GetMethod("UpdateBossVictoryDelay", flags);
        advance.Invoke(battle, new object[] { config.victoryDelay + 1f });
        Require(victories == 0, "Victory before explosions completed");
        battle.NotifyBossDeathPresentationCompleted();
        advance.Invoke(battle, new object[] { config.victoryDelay * .5f });
        Require(config.victoryDelay == 0 || victories == 0, "Early victory");
        advance.Invoke(battle, new object[] { config.victoryDelay * .5f + .001f });
        advance.Invoke(battle, new object[] { 1f });
        Require(victories == 1, "Victory delay/completion must fire once");
    }
}
