using System;
using System.Collections.Generic;
using System.Reflection;
using cfg;
using UnityEngine;

/// <summary>
/// 发射器职责及运行状态回归检查
/// </summary>
/// <remarks>
/// 与当前业务源码一起编译为独立检查程序，不进入 Assets，不向运行时添加调试组件。
/// </remarks>
internal static class BulletLauncherRegression {

    /**累计通过的断言数*/
    private static int assertions;

    /**模拟配置查询，不依赖已启动的游戏入口*/
    private static BulletConfigVO Resolve(int type, int level, int additional) {
        return new BulletConfigVO(type, type, level + additional, null, 0, Vector2.one, 1, 100, 10 * (level + additional), 0, 0, BulletMotionType.TRACKING, 0, 1000, 90);
    }

    /**构造可明确验证发射角度和连发时序的测试配置*/
    private static BulletLauncherConfigVO Config(int count = 3, int intervalMs = 100) {
        return new BulletLauncherConfigVO(new Vector2(5f, -10f), 1, 1, count, 1f, intervalMs, -90f, BulletSpreadType.CENTERED, 30f);
    }

    /**失败时保留明确的规则名称*/
    private static void Check(bool value, string rule) {
        if (!value) {
            throw new Exception(rule);
        }
        assertions++;
    }

    /**浮点比较只用于测试计算误差*/
    private static bool Near(float a, float b) {
        return Math.Abs(a - b) < 0.001f;
    }

    /**入口依次验证发射器状态与真实场景登记链路*/
    public static int Main() {
        TestLauncher();
        TestRoundEndInterval();
        TestSceneIntegration();
        Console.WriteLine($"PASS: {assertions} launcher assertions");
        return 0;
    }

    /**验证冻结、连发恢复、升级快照、复位和相同配置的跨阵营一致性*/
    private static void TestLauncher() {
        AircraftVO owner = new AircraftVO(100, "testPlayer", true, new Vector2(100f, -200f));
        BulletLauncherVO launcher = new BulletLauncherVO(Config(), Resolve);
        List<BulletLaunchVO> shots = new List<BulletLaunchVO>();
        launcher.Update(0.01f, owner, shots.Add);
        Check(shots.Count == 1, "first shot");
        Check(Near(shots[0].position.x, 105f) && Near(shots[0].position.y, -210f), "configured spawn offset");
        Check(Near(shots[0].direction, -105f), "first spread direction");
        launcher.isActive = false;
        launcher.Update(10f, owner, shots.Add);
        Check(shots.Count == 1, "inactive launcher freezes pending shots");
        launcher.isActive = true;
        launcher.Update(0.05f, owner, shots.Add);
        Check(shots.Count == 1, "reactivation does not bypass cooldown");
        launcher.Update(0.05f, owner, shots.Add);
        Check(shots.Count == 2 && Near(shots[1].direction, -90f), "resume pending burst");
        launcher.Update(0.1f, owner, shots.Add);
        Check(shots.Count == 3 && Near(shots[2].direction, -75f), "last spread direction");
        launcher.SetAdditionalLevel(2);
        Check(launcher.effectiveBullet.level == 3 && shots[0].bullet.level == 1, "upgrade does not mutate old snapshots");
        launcher.Update(0f, owner, shots.Add);
        Check(shots.Count == 3, "zero timer delta does not emit");
        launcher.Update(0.8f, owner, shots.Add);
        Check(shots.Count == 3, "round interval starts after last projectile");
        launcher.Update(0.2f, owner, shots.Add);
        Check(shots.Count == 4 && shots[3].bullet.level == 3, "upgrade preserves post-round cooldown");
        launcher.isActive = false;
        launcher.Reset();
        launcher.Update(0.01f, owner, shots.Add);
        Check(launcher.isActive && shots.Count == 5 && Near(shots[4].direction, -105f), "reset starts fresh round");
        Check(shots[4].bullet.level == 3, "session reset preserves configured upgrade");
        BulletLauncherVO other = new BulletLauncherVO(Config(), Resolve);
        List<BulletLaunchVO> enemyShots = new List<BulletLaunchVO>();
        AircraftVO enemy = new AircraftVO(101, owner.position, EnemyClass.NORMAL, Vector2.one, null, 20);
        other.Update(0.01f, enemy, enemyShots.Add);
        Check(Near(enemyShots[0].direction, shots[0].direction) && enemyShots[0].bullet.damage == shots[0].bullet.damage, "same launcher config is independent of owner faction");
        Check(other.additionalLevel == 0, "upgrade state is independent per launcher");
        BulletVO bullet = new BulletVO(102, shots[4]);
        Check(bullet.weaponLevel == 3 && owner.effectiveLevel == 1, "bullet level comes from launcher not aircraft");
    }

    /**验证长连发不会积累跨轮冷却，以及帧内补偿和轮后冻结*/
    private static void TestRoundEndInterval() {
        AircraftVO owner = new AircraftVO(200, "intervalTest", true, Vector2.zero);
        BulletLauncherVO launcher = new BulletLauncherVO(Config(3, 800), Resolve);
        List<double> times = new List<double>();
        double elapsed = 0.0;
        Action<float> advance = dt => {
            elapsed += dt;
            launcher.Update(dt, owner, shot => times.Add(elapsed - shot.inFrameElapsed));
        };
        advance(1.7f);
        Check(times.Count == 3, "long burst does not trigger overdue round");
        Check(Near((float)times[0], 0f) && Near((float)times[1], 0.8f) && Near((float)times[2], 1.6f), "burst interval remains unchanged");
        advance(0.8f);
        Check(times.Count == 3, "full interval is required after last shot");
        advance(0.11f);
        Check(times.Count == 4 && Near((float)times[3], 2.6f), "next burst starts after post-round wait");
        launcher = new BulletLauncherVO(Config(3, 0), Resolve);
        times.Clear();
        elapsed = 0.0;
        advance(0.1f);
        Check(times.Count == 3 && Near((float)times[2], 0f), "zero intra-round interval emits simultaneous volley");
        launcher.isActive = false;
        launcher.Update(10f, owner, shot => times.Add(-1));
        launcher.isActive = true;
        advance(0.8f);
        Check(times.Count == 3, "post-round wait freezes while disabled");
        advance(0.11f);
        Check(times.Count == 6 && Near((float)times[3], 1f), "post-round wait resumes without debt");
    }

    /**验证飞机生命周期许可、生成请求登记以及双方追踪能力的配置入口*/
    private static void TestSceneIntegration() {
        BattleModel model = new BattleModel();
        AircraftVO player = model.CreatePlayerAircraft("testPlayer", true, new Vector2(360f, -900f));
        BulletLauncherVO launcher = new BulletLauncherVO(Config(1, 0), Resolve);
        player.bulletLaunchers.Add(launcher);
        player.OnTimeUpdate(0.01f);
        Check(model.playerProjectiles.Count == 0, "aircraft lifecycle gate remains effective");
        player.SetFiringEnabled(true);
        launcher.isActive = false;
        player.OnTimeUpdate(0.01f);
        Check(model.playerProjectiles.Count == 0, "per launcher gate remains effective");
        launcher.isActive = true;
        player.OnTimeUpdate(0.01f);
        Check(model.playerProjectiles.Count == 1, "aircraft delegates to launcher and scene registers result");
        model.SetBulletAdditionalLevel(2);
        Check(launcher.effectiveBullet.level == 3, "upgrade entry applies launcher modifier");
        AircraftVO enemy = new AircraftVO(model.CreateElementId(), new Vector2(360f, -200f), EnemyClass.NORMAL, new Vector2(50f, 60f), null, 100);
        model.enemies.Add(enemy);
        MethodInfo create = typeof(BattleModel).GetMethod("CreateProjectile", BindingFlags.NonPublic | BindingFlags.Instance);
        Action<BulletLaunchVO> emit = (Action<BulletLaunchVO>)Delegate.CreateDelegate(typeof(Action<BulletLaunchVO>), model, create);
        BulletLauncherVO enemyLauncher = new BulletLauncherVO(Config(1, 0), Resolve);
        enemyLauncher.Update(0.01f, enemy, emit);
        Check(model.enemyProjectiles.Count == 1, "enemy request is registered");
        BulletVO enemyBullet = model.enemyProjectiles[0];
        Check(enemyBullet.weaponLevel == 1, "player modifier does not leak into enemy creation");
        // 独立 Mono 无 Unity 原生转向函数，直接验证真实注入的目标查询与失效判断。
        Func<Vector2, AircraftVO> finder = (Func<Vector2, AircraftVO>)typeof(BulletVO).GetField("targetFinder", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemyBullet);
        Predicate<AircraftVO> validator = (Predicate<AircraftVO>)typeof(BulletVO).GetField("targetValidator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemyBullet);
        Check(finder(enemyBullet.position) == player && validator(player), "enemy tracking configuration has target provider");
        player.TryTakePlayerDamage(player.health);
        Check(finder(enemyBullet.position) == null && !validator(player), "dead player is not a tracking target");
        int count = model.playerProjectiles.Count;
        player.OnTimeUpdate(0.1f);
        Check(model.playerProjectiles.Count == count, "death blocks active launcher");
    }

}
