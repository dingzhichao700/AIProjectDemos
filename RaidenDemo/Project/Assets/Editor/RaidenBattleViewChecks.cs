using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>在编辑器内验证真实 Transform 的固定层级及死亡回收。</summary>
public static class RaidenBattleViewChecks {
    private static readonly Assembly Game = typeof(BattleConst).Assembly;

    [MenuItem("Tools/Raiden/Check battle view lifecycle %#F9")]
    public static void Run() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            throw new InvalidOperationException("Stop Play mode before running isolated view checks.");
        }
        var root = new GameObject("RaidenBattleViewChecks", typeof(RectTransform));
        root.hideFlags = HideFlags.HideAndDontSave;
        try {
            var entities = Layer("entityLayer", root.transform);
            var effects = Layer("effectLayer", root.transform);
            var projectiles = Layer("projectileLayer", root.transform);
            object pool = Activator.CreateInstance(Game.GetType("BattleVisualPool"), true);
            RectTransform body = Create(pool, entities);
            Call(pool, "Recycle", body);
            Require(body.parent == entities, "Recycle changed entity layer");
            RectTransform again = Create(pool, entities);
            Require(again == body && again.gameObject.activeSelf, "Same-layer reuse failed");
            Call(pool, "Recycle", again);
            RectTransform bullet = Create(pool, projectiles);
            Require(bullet != body && bullet.parent == projectiles && body.parent == entities, "Cross-layer pool reuse");
            object effectService = Activator.CreateInstance(Game.GetType("BattleEffectPresenter"), new object[] { effects });
            object presenter = Activator.CreateInstance(Game.GetType("BattleAircraftDeathPresenter"), new object[] { effectService, pool });
            // Empty explosion sequences exercise completion/recycle without loading art.
            foreach (bool remove in new[] { true, false }) {
                RectTransform dying = Create(pool, entities);
                object aircraft = Enemy(remove);
                int last = 0, completed = 0;
                Call(presenter, "PlayAircraftDeath", dying, aircraft, false, (Action)(() => last++), (Action)(() => completed++));
                Require(dying.parent == entities && last == 1 && completed == 1, "Death changed layer or duplicate notification");
                Require(dying.gameObject.activeSelf != remove, "Death body visibility policy");
                Call(presenter, "Update", .1f, TimerType.ENEMY);
                Call(presenter, "Clear");
                Call(presenter, "Clear");
                Require(!dying.gameObject.activeSelf && dying.parent == entities, "Death cleanup moved/leaked body");
                RectTransform first = Create(pool, entities);
                RectTransform second = Create(pool, entities);
                Require(first != second, "Death cleanup recycled body twice");
                Call(pool, "Recycle", first);
                Call(pool, "Recycle", second);
            }
            Debug.Log("PASS: Raiden battle view lifecycle — fixed layers, isolated pools, death completion, retained body cleanup and idempotent clear.");
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static object Enemy(bool remove) {
        Type type = Game.GetType("AircraftVO");
        var constructor = type.GetConstructors().Single(c => c.GetParameters().Any(p => p.Name == "enemyClass"));
        object[] args = constructor.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue :
            p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        object enemy = constructor.Invoke(args);
        Call(enemy, "ConfigureDeathPresentation", null, remove);
        return enemy;
    }

    private static RectTransform Layer(string name, Transform parent) {
        var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        return layer;
    }

    private static RectTransform Create(object pool, RectTransform layer) {
        return (RectTransform)Call(pool, "CreateEmpty", "fixture", layer, Vector2.one, Vector2.zero, "sameResource");
    }

    private static object Call(object target, string method, params object[] args) {
        return target.GetType().GetMethod(method).Invoke(target, args);
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
