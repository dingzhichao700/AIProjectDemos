#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// Editor-only diagnostics. No Player/AOT changes and no forced collection/unloading.
namespace Starfront.Diagnostics {
    internal sealed class RenderSampleWindow {
        internal int Samples, PeakDrawCalls, PeakBatches, PeakSetPass, LastFrame = -1;
        internal long DrawCalls, Batches, SetPass;
        internal double MaxObservedGapMs;
        private double previousTime = -1;

        internal void Add(int frame, double time, int draws, int batches, int passes) {
            if (frame == LastFrame) return;
            LastFrame = frame;
            if (previousTime >= 0) MaxObservedGapMs = Math.Max(MaxObservedGapMs, (time - previousTime) * 1000);
            previousTime = time;
            Samples++;
            DrawCalls += draws; Batches += batches; SetPass += passes;
            PeakDrawCalls = Math.Max(PeakDrawCalls, draws);
            PeakBatches = Math.Max(PeakBatches, batches);
            PeakSetPass = Math.Max(PeakSetPass, passes);
        }

        internal void Reset() {
            Samples = PeakDrawCalls = PeakBatches = PeakSetPass = 0;
            DrawCalls = Batches = SetPass = 0;
            MaxObservedGapMs = 0;
            previousTime = -1;
            LastFrame = -1;
        }
    }

    [InitializeOnLoad]
    public static class EditorMemoryRenderMonitor {
        private const string ArmedKey = "Starfront.PerfMemoryRender.R1.Armed";
        private const double Interval = 10;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly RenderSampleWindow Window = new RenderSampleWindow();
        private static StreamWriter writer;
        private static string capturePath;
        private static double started, nextReport;
        private static long baselineManaged, baselineAllocated;
        private static int previousGc;
        private static bool paused;
        private static bool exitingPlay;

        static EditorMemoryRenderMonitor() {
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += CloseForReload;
            EditorApplication.quitting += Disable;
            if (SessionState.GetBool(ArmedKey, false)) {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Tools/Performance/Start Memory and DrawCall Capture")]
        public static void Enable() {
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[PERF-EDITOR] Armed. Capture starts in Play. Memory scope=whole Editor; rendering=observed UnityStats frames. No automatic leak verdict.");
        }

        [MenuItem("Tools/Performance/Stop Memory and DrawCall Capture")]
        public static void Disable() {
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.update -= Tick;
            Close();
        }

        [MenuItem("Tools/Performance/Open Capture Folder")]
        public static void OpenFolder() {
            string folder = CaptureFolder();
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        private static string CaptureFolder() {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PerfDiagnostics"));
        }

        private static void Begin() {
            string folder = CaptureFolder();
            Directory.CreateDirectory(folder);
            capturePath = Path.Combine(folder, "perf-editor-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", Invariant) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".csv");
            writer = new StreamWriter(new FileStream(capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            writer.AutoFlush = true;
            writer.WriteLine("# PERF-EDITOR R1; memory=whole Editor, not just game; UnityStats=observed frames, not isolated camera; zero is a reported value; trends are not proof of a leak; MiB=1048576 bytes");
            writer.WriteLine("utc,elapsedSeconds,scene,focused,paused,samples,managedMiB,managedDeltaFromStartMiB,managedHeapMiB,unityAllocatedMiB,allocatedDeltaFromStartMiB,unityReservedMiB,gc0Delta,drawCallsAvg,drawCallsPeak,batchesAvg,batchesPeak,setPassAvg,setPassPeak,maxObservedGapMs,rowKind");
            baselineManaged = Profiler.GetMonoUsedSizeLong();
            baselineAllocated = Profiler.GetTotalAllocatedMemoryLong();
            previousGc = GC.CollectionCount(0);
            started = EditorApplication.timeSinceStartup;
            nextReport = started + Interval;
            paused = false;
            Window.Reset();
            Debug.Log("[PERF-EDITOR] Capture: " + capturePath);
        }

        private static void Tick() {
            if (exitingPlay || !SessionState.GetBool(ArmedKey, false) || !EditorApplication.isPlaying) return;
            try {
                if (writer == null) Begin();
                if (EditorApplication.isPaused) {
                    if (!paused) { WriteReport("pause-boundary"); paused = true; Window.Reset(); }
                    return;
                }
                if (paused) {
                    paused = false;
                    Window.Reset();
                    previousGc = GC.CollectionCount(0);
                    nextReport = EditorApplication.timeSinceStartup + Interval;
                }
                double now = EditorApplication.timeSinceStartup;
                Window.Add(Time.frameCount, now, UnityStats.drawCalls, UnityStats.batches, UnityStats.setPassCalls);
                if (now >= nextReport) {
                    WriteReport("periodic");
                    Window.Reset();
                    nextReport = EditorApplication.timeSinceStartup + Interval;
                }
            } catch (Exception error) { Fail(error); }
        }

        private static string F(double value) { return value.ToString("F3", Invariant); }
        private static double MiB(long bytes) { return bytes / 1048576.0; }
        private static string Csv(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }

        private static void WriteReport(string kind) {
            if (writer == null) return;
            long managed = Profiler.GetMonoUsedSizeLong();
            long heap = Profiler.GetMonoHeapSizeLong();
            long allocated = Profiler.GetTotalAllocatedMemoryLong();
            long reserved = Profiler.GetTotalReservedMemoryLong();
            int gc = GC.CollectionCount(0), deltaGc = gc - previousGc;
            double divisor = Math.Max(1, Window.Samples);
            string row = string.Join(",", new [] {
                DateTime.UtcNow.ToString("O", Invariant), F(EditorApplication.timeSinceStartup - started), Csv(SceneManager.GetActiveScene().name),
                Application.isFocused ? "1" : "0", EditorApplication.isPaused ? "1" : "0", Window.Samples.ToString(Invariant),
                F(MiB(managed)), F(MiB(managed - baselineManaged)), F(MiB(heap)), F(MiB(allocated)), F(MiB(allocated - baselineAllocated)), F(MiB(reserved)), deltaGc.ToString(Invariant),
                F(Window.DrawCalls / divisor), Window.PeakDrawCalls.ToString(Invariant), F(Window.Batches / divisor), Window.PeakBatches.ToString(Invariant),
                F(Window.SetPass / divisor), Window.PeakSetPass.ToString(Invariant), F(Window.MaxObservedGapMs), kind
            });
            writer.WriteLine(row);
            previousGc = gc;
            Debug.Log("[PERF-EDITOR] managedMiB=" + F(MiB(managed)) + " delta=" + F(MiB(managed - baselineManaged)) +
                " unityAllocatedMiB=" + F(MiB(allocated)) + " gc0=" + deltaGc + " draws(avg/peak)=" + F(Window.DrawCalls / divisor) + "/" + Window.PeakDrawCalls +
                " batchesPeak=" + Window.PeakBatches + " setPassPeak=" + Window.PeakSetPass + " observedGapMs=" + F(Window.MaxObservedGapMs) + " kind=" + kind);
        }

        // Explicit, potentially expensive object inventory; never run in the periodic sampler.
        // Compare same-page settled snapshots. Editor assets/caches are included, not just live game objects.
        [MenuItem("Tools/Performance/Take Loaded Object Inventory")]
        public static void TakeInventory() {
            if (writer == null) { Debug.LogWarning("[PERF-EDITOR] Start a Play capture first."); return; }
            try {
                WriteReport("before-manual-inventory");
                string path = Path.ChangeExtension(capturePath, null) + "-objects-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", Invariant) + ".csv";
                using (var output = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))) {
                    output.WriteLine("# Loaded Editor objects including assets/caches; runtimeBytes estimates are not additive total process memory; snapshot creates measurement overhead");
                    output.WriteLine("type,instanceId,name,runtimeBytes");
                    Inventory<Texture>(output);
                    Inventory<Sprite>(output);
                    Inventory<Material>(output);
                    Inventory<AudioClip>(output);
                }
                Debug.Log("[PERF-EDITOR] Object inventory: " + path);
                // Do not attribute an explicitly requested scan or its immediate next frame to idle stutter.
                Window.Reset();
                previousGc = GC.CollectionCount(0);
                nextReport = EditorApplication.timeSinceStartup + Interval;
            } catch (Exception error) { Fail(error); }
        }

        private static void Inventory<T>(StreamWriter output) where T : UnityEngine.Object {
            T[] items = Resources.FindObjectsOfTypeAll<T>();
            foreach (T item in items) {
                if (item == null) continue;
                output.WriteLine(Csv(typeof(T).Name) + "," + item.GetInstanceID().ToString(Invariant) + "," + Csv(item.name) + "," + Profiler.GetRuntimeMemorySizeLong(item).ToString(Invariant));
            }
        }

        private static void OnPlayState(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) exitingPlay = false;
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) { exitingPlay = true; Close(); }
        }

        private static void CloseForReload() {
            EditorApplication.update -= Tick;
            Close();
        }

        private static void Close() {
            if (writer == null) return;
            try { WriteReport("capture-end"); }
            catch (Exception error) { Debug.LogWarning("[PERF-EDITOR] Final report failed: " + error.Message); }
            finally {
                StreamWriter closing = writer;
                writer = null;
                Window.Reset();
                try { closing.Dispose(); }
                catch (Exception error) { Debug.LogWarning("[PERF-EDITOR] Closing file failed: " + error.Message); }
            }
        }

        private static void Fail(Exception error) {
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.update -= Tick;
            Close();
            Debug.LogError("[PERF-EDITOR] Capture stopped after an error: " + error);
        }
    }
}
#endif
