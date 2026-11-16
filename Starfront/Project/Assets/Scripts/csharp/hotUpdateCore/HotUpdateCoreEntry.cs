using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

/// <summary>在任何登录界面创建前安装共享UI实现。</summary>
[Preserve]
public sealed class HotUpdateCoreEntry : IGameModuleEntry {
    [Preserve]
    public Task InitializeAsync() {
        UiRuntimeHost.Install(new SharedUiRuntime());
        return Task.CompletedTask;
    }
}

internal sealed class SharedUiRuntime : IUiRuntime {
    private readonly IdleFrameDiagnostics diagnostics = new IdleFrameDiagnostics();

    public void Initialize(UiRootContext roots) {
        PanelMgr.ins.SetUIRoot(roots.ViewportRoot, roots.SafeAreaRoot, roots.PoolRoot, roots.DesignSize);
        PanelMgr.ins.Init();
    }

    public void Tick() {
        bool measuring = diagnostics.Begin(FrameworkDevelopmentSettings.GMEnabled);
        PanelMgr.ins.Update();
        if (measuring) {
            diagnostics.End();
        }
    }

    public void OnApplicationFocus(bool focused) {
        diagnostics.Focus(focused);
        PanelMgr.ins.Dispatch(PanelEvent.APPLICATION_FOCUS_UPDATE);
    }
}

/// <summary>只记录帧间隔与共用UI段，不将GC共现误判为卡顿原因；不逐帧分配或打印。</summary>
internal sealed class IdleFrameDiagnostics {
    private bool enabled;
    private bool focused = true;
    private bool pendingReport;
    private bool previousReported;
    private double resumeAt;
    private double previousStart;
    private double windowStart;
    private double tickStart;
    private double previousUiMs;
    private double worstGapMs;
    private double worstUiMs;
    private double uiAtWorstGapMs;
    private double afterReportGapMs;
    private int frames;
    private int over50;
    private int over100;
    private int over250;
    private int gc0;
    private int gc1;
    private int gc2;
    private long managedBytes;

    private static double Now() {
        return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
    }

    public void Focus(bool value) {
        focused = value;
        Reset(Now(), 2d);
    }

    public bool Begin(bool allowed) {
        double now = Now();
        if (!allowed) {
            enabled = false;
            return false;
        }
        if (!enabled) {
            enabled = true;
            Reset(now, 5d);
            Debug.Log("[PERF] enabled; interval=10s; longFrame=50/100/250ms; startupGrace=5s; focusGrace=2s; gcCounts=runtime-reported; managedMB excludes GPU/native/browser memory");
        }
        if (!focused || now < resumeAt) {
            previousStart = 0d;
            return false;
        }
        tickStart = now;
        if (previousStart == 0d) {
            windowStart = now;
            CaptureMemory();
        } else {
            double gap = (now - previousStart) * 1000d;
            frames++;
            if (gap >= 50d) { over50++; }
            if (gap >= 100d) { over100++; }
            if (gap >= 250d) { over250++; }
            if (gap > worstGapMs) {
                worstGapMs = gap;
                uiAtWorstGapMs = previousUiMs;
            }
            if (previousReported && gap > afterReportGapMs) {
                afterReportGapMs = gap;
            }
        }
        previousReported = false;
        previousStart = now;
        pendingReport = now - windowStart >= 10d;
        tickStart = Now();
        return true;
    }

    public void End() {
        double now = Now();
        previousUiMs = (now - tickStart) * 1000d;
        if (previousUiMs > worstUiMs) { worstUiMs = previousUiMs; }
        if (!pendingReport) { return; }
        double seconds = now - windowStart;
        int next0 = GC.CollectionCount(0);
        int next1 = GC.CollectionCount(1);
        int next2 = GC.CollectionCount(2);
        long nextBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "[PERF] frame={0} window={1:F1}s fps={2:F1} worstGapMs={3:F1} over50/100/250={4}/{5}/{6} uiMaxMs={7:F2} uiBeforeWorstGapMs={8:F2} gcDelta0/1/2={9}/{10}/{11} managedMB={12:F1} managedDeltaMB={13:F2} afterReportGapMs={14:F1}",
            Time.frameCount, seconds, frames / seconds, worstGapMs, over50, over100, over250,
            worstUiMs, uiAtWorstGapMs, next0 - gc0, next1 - gc1, next2 - gc2,
            nextBytes / 1048576d, (nextBytes - managedBytes) / 1048576d, afterReportGapMs));
        gc0 = next0; gc1 = next1; gc2 = next2; managedBytes = nextBytes;
        ClearWindow(now);
        previousReported = true;
    }

    private void Reset(double now, double grace) {
        resumeAt = now + grace;
        previousStart = 0d;
        previousUiMs = 0d;
        previousReported = false;
        ClearWindow(now);
    }

    private void ClearWindow(double now) {
        windowStart = now;
        frames = over50 = over100 = over250 = 0;
        worstGapMs = worstUiMs = uiAtWorstGapMs = afterReportGapMs = 0d;
        pendingReport = false;
    }

    private void CaptureMemory() {
        gc0 = GC.CollectionCount(0);
        gc1 = GC.CollectionCount(1);
        gc2 = GC.CollectionCount(2);
        managedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
    }
}

