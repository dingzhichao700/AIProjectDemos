using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// 独立源码检查仅替代 Unity 对象存活判断与日志，不替代 Timer 调度实现。
namespace UnityEngine {
    public class Object {
        public bool destroyed;
        public static bool operator ==(Object left, Object right) {
            bool a = ReferenceEquals(left, null) || left.destroyed;
            bool b = ReferenceEquals(right, null) || right.destroyed;
            return a || b ? a == b : ReferenceEquals(left, right);
        }
        public static bool operator !=(Object left, Object right) { return !(left == right); }
        public override bool Equals(object value) { return ReferenceEquals(this, value); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
    public static class Mathf {
        public static int FloorToInt(float value) { return (int)Math.Floor(value); }
    }
    public static class Debug {
        public static int exceptions;
        public static void LogException(Exception exception) { exceptions++; }
        public static void LogError(object value) { }
        public static void LogWarning(object value) { }
    }
}

public static class TimerRegression {

    private sealed class Target : UnityEngine.Object {
        public int calls;
        public void Run() { calls++; }
    }

    private static void Check(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }

    private static int Count(Timer timer, string name) {
        return ((ICollection)typeof(Timer).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(timer)).Count;
    }

    public static string Run() {
        var passed = new List<string>();
        Action<string, Action> test = (name, run) => { run(); passed.Add(name); };
        test("倍率、暂停及单次执行", () => {
            var t = new Timer(); var owner = new object(); int n = 0;
            t.Once(owner, 100, () => n++); t.scale = 0; t.SyncTime(1000);
            Check(n == 0 && t.curTime == 0, "paused"); t.scale = 0.5f; t.SyncTime(100);
            Check(n == 0, "scaled"); t.SyncTime(100); t.SyncTime(100);
            Check(n == 1 && Count(t, "handlerMap") == 0, "once cleanup");
        });
        test("零延迟保持立即执行", () => {
            var t = new Timer(); int n = 0; t.Once(t, 0, () => n++); Check(n == 1, "immediate");
        });
        test("覆盖更新参数，支持 null、0 及三参数", () => {
            var t = new Timer(); string value = "unset"; Action<string> call = x => value = x;
            t.Once(t, 10, call, "old", true); t.Once(t, 10, call, (string)null, true);
            int result = -1; t.Once<string, int, string>(t, 10, (a, b, c) => { Check(a == null && c == null, "null params"); result = b; }, null, 0, null);
            t.SyncTime(10); Check(value == null && result == 0, "typed args");
        });
        test("清理同名多个任务以及 CallLater", () => {
            var t = new Timer(); int n = 0; Action call = () => n++;
            t.Once(t, 10, call, false); t.Once(t, 10, call, false); t.CallLater(t, call); t.Clear(t, call); t.SyncTime(10);
            Check(n == 0 && Count(t, "handlerMap") == 0, "clear duplicates");
        });
        test("循环取消后重新注册不被旧任务覆盖", () => {
            var t = new Timer(); int n = 0; Action call = null;
            call = () => { n++; t.Clear(t, call); if (n == 1) t.Loop(t, 30, call); };
            t.Loop(t, 10, call); t.SyncTime(10); t.SyncTime(10); Check(n == 1, "reschedule gap");
            t.SyncTime(20); t.SyncTime(100); Check(n == 2, "self cancel");
        });
        test("回调取消其他任务、新增到期任务延至下一轮", () => {
            var t = new Timer(); int a = 0, b = 0; Action second = () => b++;
            t.Once(t, 1, () => { t.Clear(t, second); t.Once(t, -1, () => a++); });
            t.Once(t, 1, second); t.SyncTime(1); Check(a == 0 && b == 0, "defer and cancel");
            t.SyncTime(0); Check(a == 1, "next cycle");
        });
        test("CallLater不跳项、去重、自身新增延至下一轮", () => {
            var t = new Timer(); int n = 0; Action recursive = null;
            recursive = () => { n++; if (n == 1) t.CallLater(t, recursive); };
            t.CallLater(t, recursive); t.CallLater(t, recursive); int other = 0;
            t.CallLater(t, () => other++); t.CallLater(t, () => other++);
            t.SyncTime(1); Check(n == 1 && other == 2, "first batch");
            t.SyncTime(1); Check(n == 2 && Count(t, "handlerMap") == 0, "second batch");
        });
        test("逐帧监听新增、移除与下一帧任务", () => {
            var t = new Timer(); int a = 0, b = 0, later = 0; Action<float> second = d => b++;
            Action<float> first = null; first = d => { a++; t.RemoveUpdateListener(t, first); t.AddUpdateListener(t, second); t.CallLater(t, () => later++); };
            t.AddUpdateListener(t, first); t.SyncTime(16); Check(a == 1 && b == 0 && later == 0, "listener defer");
            t.SyncTime(16); Check(b == 1 && later == 1, "listener next");
        });
        test("循环卡帧不补跑、重注册更新间隔", () => {
            var t = new Timer(); int n = 0; Action call = () => n++;
            t.Loop(t, 10, call); t.SyncTime(1000); Check(n == 1, "no catchup");
            t.Loop(t, 50, call); t.SyncTime(49); Check(n == 1, "new gap"); t.SyncTime(1); Check(n == 2, "new due");
        });
        test("异常任务隔离且回收", () => {
            var t = new Timer(); int n = 0, errors = UnityEngine.Debug.exceptions;
            t.Once(t, 1, () => { throw new Exception("expected"); }); t.Once(t, 1, () => n++);
            t.CallLater(t, () => { throw new Exception("expected"); }); t.CallLater(t, () => n++);
            t.SyncTime(1); Check(n == 2 && UnityEngine.Debug.exceptions == errors + 2 && Count(t, "handlerMap") == 0, "exception isolation");
        });
        test("销毁调用者及委托目标不再执行", () => {
            var t = new Timer(); var owner = new Target(); var target = new Target();
            t.Once(owner, 100, target.Run); owner.destroyed = true; t.SyncTime(1);
            Check(Count(t, "handlerMap") == 0, "dead owner removed early");
            t.Once(t, 1, target.Run); target.destroyed = true; t.SyncTime(1); Check(target.calls == 0, "dead callback");
        });
        test("池化清空引用与普通Handler参数调用", () => {
            var owner = new object(); int n = 0;
            var h = TimeHandler.Create<string>(owner, x => { Check(x == null, "pooled null"); n++; }, null);
            h.Run(); TimeHandler.ReturnToPool(h); Check(h.caller == null && h.callback == null && h.param1 == null, "references cleared");
            var ordinary = Handler.Create<int, string>(owner, (a, b) => { Check(a == 0 && b == null, "ordinary args"); n++; }, 0, null);
            ordinary.Run(); Handler.ReturnToPool(ordinary); Check(n == 2 && ordinary.callback == null, "ordinary cleared");
        });
        test("大量同一调用者任务完成后索引和队列归零", () => {
            var t = new Timer(); int n = 0; Action call = () => n++;
            for (int i = 0; i < 10000; i++) t.Once(t, 1, call, false);
            t.SyncTime(1); Check(n == 10000 && Count(t, "handlerMap") == 0 && Count(t, "activeHandlers") == 0, "bulk cleanup");
        });
        test("重入被拒绝，后续任务仍可执行", () => {
            var t = new Timer(); int n = 0;
            t.Once(t, 1, () => t.SyncTime(1)); t.Once(t, 1, () => n++); t.SyncTime(1);
            Check(n == 1 && t.curTime == 1, "reentry");
        });
        return "Passed " + passed.Count + " checks: " + string.Join("; ", passed);
    }
}
