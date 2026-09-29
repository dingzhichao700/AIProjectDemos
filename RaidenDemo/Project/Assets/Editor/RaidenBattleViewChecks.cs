using System;
using System.Collections;
using System.Collections.Generic;
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
            CheckFireBinding(effectService, pool, entities);
            object presenter = Activator.CreateInstance(Game.GetType("BattleAircraftDeathPresenter"), new object[] { effectService, pool });
            // Empty explosion sequences exercise completion/recycle without loading art.
            foreach (bool retain in new[] { false, true }) {
                RectTransform dying = Create(pool, entities);
                object aircraft = Enemy(retain);
                int last = 0, completed = 0;
                Call(presenter, "PlayAircraftDeath", dying, aircraft, false, (Action)(() => last++), (Action)(() => completed++));
                Require(dying.parent == entities && last == 1 && completed == 1, "Death changed layer or duplicate notification");
                Require(dying.gameObject.activeSelf == retain, "Death body visibility policy");
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
            Debug.Log("PASS: Raiden battle view lifecycle — fixed layers, isolated pools, death completion, retained body cleanup, fire binding destruction and idempotent clear.");
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static object Enemy(bool retain) {
        Type type = Game.GetType("EnemyAircraftVO");
        var constructor = type.GetConstructors().Single(c => c.GetParameters().Any(p => p.Name == "enemyClass"));
        object[] args = constructor.GetParameters().Select(p => p.Name == "enemyClass" ? (object)(retain ? cfg.EnemyClass.BOSS : cfg.EnemyClass.NORMAL) : p.Name == "baseAttributes" ? new Dictionary<cfg.AttributeType, int> { { cfg.AttributeType.MAX_LIFE, 100 } } : p.HasDefaultValue ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        return constructor.Invoke(args);
    }

    /// <summary>
    /// 验证着火表现绑定在单位销毁时释放。
    /// </summary>
    /// <remarks>
    /// 使用满血单位，不创建美术动画，检查重复绑定、销毁解绑和实体层级。
    /// </remarks>
    private static void CheckFireBinding(object effectService, object pool, RectTransform entities) {
        object presenter = Activator.CreateInstance(Game.GetType("BattleAircraftFirePresenter"), new[] { effectService });
        object aircraft = Enemy(false);
        RectTransform body = Create(pool, entities);
        FieldInfo viewsField = presenter.GetType().GetField("aircraftViews", BindingFlags.Instance | BindingFlags.NonPublic);
        IDictionary views = (IDictionary)viewsField.GetValue(presenter);
        Call(presenter, "Bind", aircraft, body);
        Call(presenter, "Bind", aircraft, body);
        Require(views.Count == 1 && body.Find("aircraftFire") == null, "Healthy aircraft duplicate binding or unexpected fire visuals");
        Call(aircraft, "Destroy");
        Require(views.Count == 0 && body.parent == entities && body.gameObject.activeSelf, "Aircraft destruction did not release fire binding independently of its body");
        Call(presenter, "Clear");
        Call(presenter, "Clear");
        Call(pool, "Recycle", body);
        Require(Create(pool, entities) == body, "Fire binding cleanup prevented body reuse");
        Call(pool, "Recycle", body);
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
