using System;
using System.Diagnostics;
using System.Threading.Tasks;

/// <summary>
/// 记录加载阶段的实际耗时与发布版本。
/// </summary>
public static class LoadTiming {

    /**编辑器不执行热更新时使用明确的版本占位*/
    public static string version = "editor";

    /**输出阶段耗时，计时不受游戏 Timer 倍率影响*/
    public static void Report(string stage, Stopwatch watch, string detail = "") {
        UnityEngine.Debug.Log($"[LoadTiming] {stage} elapsedMs={watch.ElapsedMilliseconds} version={version} {detail}");
    }

    /// <summary>
    /// 计量异步加载任务，失败时记录耗时并继续抛出异常。
    /// </summary>
    /// <param name="stage">阶段名称</param>
    /// <param name="load">实际加载任务</param>
    public static async Task MeasureAsync(string stage, Func<Task> load) {
        Stopwatch watch = Stopwatch.StartNew();
        try {
            await load();
            Report(stage, watch, "result=success");
        } catch {
            Report(stage, watch, "result=failed");
            throw;
        }
    }

}
