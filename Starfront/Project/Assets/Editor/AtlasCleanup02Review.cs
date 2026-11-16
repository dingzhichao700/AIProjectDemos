#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// This historical queue is intentionally retired. Keep its class and .meta identity
// so old pending requests receive an explicit rejection instead of recreating assets.
[InitializeOnLoad]
internal static class AtlasCleanup02Review {
    const string Retirement = "RETIRED-ATLAS-CLEANUP-02";
    const string Pending = "Library/AIUI/pending-atlas-cleanup-02.json";
    const string ResultPath = "Library/AIUI/atlas-cleanup-02-result.json";
    [Serializable] class Request { public string requestId, action; public int processId; }
    [Serializable] class Result {
        public string requestId, status, error;
        public bool retired = true;
        public int checks, moved, deleted, replaced;
        public string[] details;
    }
    static AtlasCleanup02Review() { EditorApplication.update += Poll; }
    static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var result = new Result { status = "failed", error = Retirement + ": Historical atlas-cleanup queue retired by resource governance. Use the current controlled resource transaction; archived review evidence remains read-only.", details = new[] { "No packing, migration, prefab editing, capture or asset deletion was executed." } };
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
            if (request != null) {
                result.requestId = request.requestId;
                // Another Editor's request is never consumed by this process.
                if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) return;
            }
            if (request == null || string.IsNullOrWhiteSpace(result.requestId)) result.error = "Retired queue: missing request identity; no resource operation executed.";
            File.Move(Pending, Pending + ".retired-" + DateTime.UtcNow.Ticks);
        } catch (IOException) {
            return;
        } catch (Exception error) {
            result.error = "Retired queue rejected malformed request without resource changes: " + error.Message;
            if (File.Exists(Pending)) File.Move(Pending, Pending + ".retired-invalid-" + DateTime.UtcNow.Ticks);
        }
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
    }
}
#endif
