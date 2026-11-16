#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>当前正常Play可直接开启开发点射，独立于隔离验收场景；不替换玩家数据。</summary>
[InitializeOnLoad]
public static class MarineBurstLiveReview {
    private const string Pending = "Library/AIUI/pending-marine-fire-live.json";
    private const string Enabled = "Library/AIUI/marine-fire-preview-enabled.json";
    private const string Key = "MarineFire.NormalEntry";
    private static bool running;
    [Serializable] private sealed class Request { public string requestId; public int processId; public string action; public string evidenceDirectory; }
    [Serializable] private sealed class CameraInfo { public string name; public string scene; public bool active; public bool enabled; public int display; public string texture; }
    [Serializable] private sealed class Result { public string requestId; public string status; public string error; public string scene; public int activeCameras; public CameraInfo[] cameras; public bool controlPresent; public int automaticTriggers; public int activeRigCount; public bool leftPlaying; }
    static MarineBurstLiveReview() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }

    [MenuItem("Tools/开发验收/陆战队员短点射/在当前Play启用点射")]
    public static void EnableCurrent() {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("请先进入正常Play，再启用点射；也可使用打开培养预览。"); return; }
        File.WriteAllText(Enabled, "{\"enabled\":true,\"scope\":\"Editor preview only\"}");
        MarineBurstPreviewControls.ToolbarVisible = true;
        MarineBurstPreviewControls.Install().EnableAutomatic();
    }
    [MenuItem("Tools/开发验收/陆战队员短点射/关闭开发点射预览")]
    public static void DisableCurrent() {
        if (File.Exists(Enabled)) { File.Delete(Enabled); }
        foreach (var view in UnityEngine.Object.FindObjectsOfType<MarineBurstPreviewControls>()) { UnityEngine.Object.Destroy(view.gameObject); }
        foreach (var skin in UnityEngine.Object.FindObjectsOfType<CardArtIdleSkin>()) { if (skin.Burst != null) { skin.Burst.Cancel(); } }
    }
    private static void Poll() {
        if (EditorApplication.isPlaying && (File.Exists(Enabled) || running)) { EditorApplication.QueuePlayerLoopUpdate(); }
        if (running || !File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
        Request request = null;
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) { throw new InvalidOperationException("进程不匹配"); }
            File.Delete(Pending);
            Directory.CreateDirectory(request.evidenceDirectory);
            if (request.action == "inspect") { Write(request, "inspected", null); return; }
            if (request.action == "enable") { EnableCurrent(); Write(request, "enabled", null); return; }
            if (request.action == "normal-start" || request.action == "normal-start-with-camera") {
                if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("不覆盖当前Play，请先明确结束现场"); }
                var scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/Scenes/GameEntrance.unity") {
                    if (scene.isDirty) { throw new InvalidOperationException("当前场景有未保存修改，未自动丢弃"); }
                    EditorSceneManager.OpenScene("Assets/Scenes/GameEntrance.unity");
                }
                Write(request, "before-normal-play", null);
                if (request.action == "normal-start-with-camera") {
                    var mainCamera = SceneManager.GetActiveScene().GetRootGameObjects().SingleOrDefault(x => x.name == "Main Camera");
                    if (mainCamera == null || mainCamera.GetComponent<Camera>() == null) { throw new InvalidOperationException("未找到正式场景已有Main Camera，未创建替代相机"); }
                    bool before = mainCamera.activeSelf;
                    mainCamera.SetActive(true);
                    File.WriteAllText(Path.Combine(request.evidenceDirectory, "camera-restoration.json"), "{\"object\":\"Main Camera\",\"beforeActive\":" + before.ToString().ToLowerInvariant() + ",\"afterActive\":true,\"savedSceneAssetChanged\":false}");
                }
                File.WriteAllText(Enabled, "{\"enabled\":true,\"scope\":\"Editor preview only\"}");
                SessionState.SetString(Key, JsonUtility.ToJson(request));
                EditorApplication.isPlaying = true;
                return;
            }
            throw new InvalidOperationException("未知当前Play预览请求");
        } catch (Exception error) { if (request != null) { Write(request, "failed", error.ToString()); } Debug.LogException(error); }
    }
    private static void State(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredEditMode) { running = false; return; }
        if (state != PlayModeStateChange.EnteredPlayMode) { return; }
        if (File.Exists(Enabled) && string.IsNullOrEmpty(SessionState.GetString("MarineIdle.R1", ""))) { MarineBurstPreviewControls.Install().EnableAutomatic(); }
        string pending = SessionState.GetString(Key, "");
        if (!string.IsNullOrEmpty(pending)) { SessionState.EraseString(Key); RunNormal(JsonUtility.FromJson<Request>(pending)); }
    }
    private static async void RunNormal(Request request) {
        running = true;
        try {
            Application.runInBackground = true;
            MainPanel main = null;
            for (int i = 0; i < 750; i++) {
                main = UnityEngine.Object.FindObjectOfType<MainPanel>();
                if (main != null && main.isOpened) { break; }
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始") {
                    typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
                }
                await Frame();
            }
            if (main == null) { throw new TimeoutException("正常启动未打开MainPanel"); }
            main.btnDeck.onClick.Invoke();
            await Frames(20);
            var deck = main.GetComponentInChildren<DeckPresentationView>();
            if (deck == null) { throw new InvalidOperationException("正常阵容入口未就绪"); }
            deck.RequestCardInfo(2101);
            CardCultivationPanel panel = null;
            for (int i = 0; i < 500; i++) {
                panel = UnityEngine.Object.FindObjectOfType<CardCultivationPanel>();
                if (panel != null && panel.isOpened && panel.CurrentCardId == 2101 && panel.imgPortrait.sprite != null) { await panel.imgPortrait.GetComponent<CardArtImage>().Loading; break; }
                await Frame();
            }
            if (panel == null || panel.CurrentCardId != 2101) { throw new InvalidOperationException("正常培养2101未就绪"); }
            await Frames(25);
            EnableCurrent();
            var game = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(game).Show(); EditorWindow.GetWindow(game).Focus();
            var effect = panel.imgPortrait.GetComponent<CardArtIdleSkin>();
            MarineBurstGraphic burst = null;
            for (int i = 0; i < 400; i++) {
                burst = effect != null ? effect.Burst : null;
                if (burst != null && burst.IsReady && burst.Rig.Flash > .2f) { break; }
                await Frame();
            }
            if (burst == null || !burst.IsReady || burst.Rig.Flash <= .2f) { throw new InvalidOperationException("正常培养未实际触发开火"); }
            burst.Rig.paused = true; effect.Rig.paused = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, "normal-cultivation-fire.png"));
            await Frames(8);
            if (burst != null && burst.Rig != null) { burst.Rig.paused = false; }
            if (effect != null && effect.Rig != null) { effect.Rig.paused = false; }
            // Read actual cameras; never disable the GameView warning or create a substitute camera.
            Write(request, Camera.allCameras.Any(x => x.isActiveAndEnabled && x.targetTexture == null && x.targetDisplay == 0) ? "ready-for-user" : "camera-investigation-required", null);
        } catch (Exception error) { Write(request, "failed", error.ToString()); Debug.LogException(error); }
        finally { running = false; }
    }
    private static async Task Frame() { EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(20); }
    private static async Task Frames(int count) { for (int i = 0; i < count; i++) { await Frame(); } }
    private static void Write(Request request, string status, string error) {
        var control = UnityEngine.Object.FindObjectOfType<MarineBurstPreviewControls>();
        var result = new Result { requestId = request.requestId, status = status, error = error, scene = SceneManager.GetActiveScene().path, activeCameras = Camera.allCamerasCount, cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(x => x.gameObject.scene.IsValid()).Select(x => new CameraInfo { name=x.name, scene=x.gameObject.scene.name, active=x.gameObject.activeInHierarchy, enabled=x.enabled, display=x.targetDisplay, texture=x.targetTexture == null ? "" : x.targetTexture.name }).ToArray(), controlPresent=control != null, automaticTriggers=control == null ? 0 : control.AutomaticBurstsStarted, activeRigCount=UnityEngine.Object.FindObjectsOfType<MarineBurstRig>().Length, leftPlaying=EditorApplication.isPlaying };
        string text=JsonUtility.ToJson(result,true);
        Directory.CreateDirectory(request.evidenceDirectory);
        File.WriteAllText("Library/AIUI/marine-fire-live-result.json",text);
        File.WriteAllText(Path.Combine(request.evidenceDirectory,request.requestId+".json"),text);
    }
}
#endif