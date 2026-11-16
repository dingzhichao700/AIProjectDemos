#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>通过真实游戏入口只读试装道具候选；只改运行实例，不改Prefab和业务数据。</summary>
[InitializeOnLoad]
public static class ItemIconRuntimePreview {
    private const string Key = "ItemIcon.RuntimePreview.R1";
    private const string Pending = "Library/AIUI/pending-item-icon-preview.json";
    [Serializable] private sealed class Request {
        public string requestId, evidenceDirectory, sourceDirectory, fragmentDirectory, releaseEvidence;
        public int processId;
    }
    [Serializable] private sealed class Result {
        public string requestId, status, error;
        public string[] checks;
        public Shot[] shots;
    }
    [Serializable] private sealed class Shot {
        public int cardId, itemId, width, height;
        public string screenshot, stage;
        public float[] materialBounds;
    }
    private static readonly List<string> checks = new List<string>();
    private static readonly List<Shot> shots = new List<Shot>();
    private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
    static ItemIconRuntimePreview() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnState;
    }
    private static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        Request request = null;
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrWhiteSpace(request.requestId) || string.IsNullOrWhiteSpace(request.releaseEvidence)) throw new InvalidOperationException("Request identity/release missing");
            File.Delete(Pending);
            Directory.CreateDirectory(request.evidenceDirectory);
            SessionState.SetString(Key, JsonUtility.ToJson(request));
            var start = EditorSceneManager.playModeStartScene;
            SessionState.SetString(Key + ".start", start == null ? "" : AssetDatabase.GetAssetPath(start));
            string entrance = AssetDatabase.FindAssets("GameEntrance t:Scene").Select(AssetDatabase.GUIDToAssetPath).First(path => Path.GetFileNameWithoutExtension(path) == "GameEntrance");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(entrance);
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            SessionState.SetInt(Key + ".builder", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = 0;
            EditorApplication.isPlaying = true;
        } catch (Exception error) { WriteResult(request, error); }
    }
    private static async void OnState(PlayModeStateChange state) {
        string text = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(text)) return;
        if (state == PlayModeStateChange.EnteredEditMode) {
            string old = SessionState.GetString(Key + ".start", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(old) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
            UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".builder", 0);
            SessionState.EraseString(Key);
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        var request = JsonUtility.FromJson<Request>(text);
        await Run(request);
    }
    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) { EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(17); }
    }
    private static void Check(bool value, string message) {
        if (!value) throw new InvalidOperationException(message);
        checks.Add(message);
    }
    private static string Digest(string path) {
        if (!File.Exists(path)) return "absent";
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    private static Sprite LoadSprite(string path) {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Check(texture.LoadImage(File.ReadAllBytes(path)), "load " + Path.GetFileName(path));
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        sprite.name = Path.GetFileNameWithoutExtension(path);
        temporary.Add(sprite); temporary.Add(texture);
        return sprite;
    }
    private static void Place(Image image, float x, float y, float width, float height) {
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        image.type = Image.Type.Simple; image.preserveAspect = true; image.color = Color.white; image.material = null;
        image.raycastTarget = false; image.enabled = true;
    }
    private static float[] Bounds(RectTransform rect) {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var points = corners.Select(v => RectTransformUtility.WorldToScreenPoint(camera, v)).ToArray();
        return new[] { points.Min(v => v.x), Screen.height - points.Max(v => v.y), points.Max(v => v.x) - points.Min(v => v.x), points.Max(v => v.y) - points.Min(v => v.y) };
    }
    private static async Task Capture(Request request, CardCultivationPanel panel, CardCultivationMaterialItem item, int cardId, int itemId, string stage) {
        await Frames(12);
        Canvas.ForceUpdateCanvases();
        string path = Path.Combine(request.evidenceDirectory, cardId + "-" + stage + ".png");
        ScreenCapture.CaptureScreenshot(path);
        long last = -1;
        for (int i = 0; i < 240; i++) {
            await Frames(1);
            if (File.Exists(path)) { long length = new FileInfo(path).Length; if (length > 0 && length == last) break; last = length; }
        }
        Check(File.Exists(path) && new FileInfo(path).Length > 0, "fresh actual GameView screenshot " + cardId + " " + stage);
        shots.Add(new Shot { cardId = cardId, itemId = itemId, width = Screen.width, height = Screen.height, screenshot = path, stage = stage, materialBounds = Bounds((RectTransform)item.transform) });
    }
    private static async Task Run(Request request) {
        checks.Clear(); shots.Clear(); temporary.Clear();
        bool oldBackground = Application.runInBackground;
        PlayerProgressControl previous = null; bool installed = false;
        var files = new[] { "playerProgress.json", "cardCollection.json" }.Select(name => Path.Combine(Application.persistentDataPath, name)).ToArray();
        var hashes = files.Select(Digest).ToArray();
        try {
            Application.runInBackground = true;
            var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).Show();
            await CfgManager.EnsureLoadedAsync();
            for (int i = 0; i < 1200 && !(PanelMgr.ins.GetPanel("default/mainView/MainPanel") is MainPanel); i++) {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login != null && login.txt != null && login.txt.gameObject.activeInHierarchy) login.SendMessage("EnterMain", SendMessageOptions.DontRequireReceiver);
                await Frames(1);
            }
            var main = PanelMgr.ins.GetPanel("default/mainView/MainPanel") as MainPanel;
            Check(main != null && main.isOpened, "GameEntrance/login reached real MainPanel");
            Check(UnityEngine.Object.FindObjectOfType<RookieEngine>() != null, "real game engine and UI clock retained");
            int writes = 0;
            var save = new PlayerProgressSaveVO { initialized = true };
            foreach (int id in new[] { 2101, 2203, 2303 }) save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = id, level = 1 });
            previous = PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(CfgManager.tables, save, ignored => writes++)); installed = true; writes = 0;
            if (!main.viewDeck.gameObject.activeSelf) main.btnDeck.onClick.Invoke();
            await Frames(30);
            var deck = main.viewDeck.GetComponentInChildren<DeckPresentationView>(); Check(deck != null, "normal deck view present");
            Sprite frame = LoadSprite(Path.Combine(request.sourceDirectory, "frame-B-97x101.png"));
            foreach (int id in new[] { 2101, 2203, 2303 }) {
                deck.RequestCardInfo(id);
                CardCultivationPanel panel = null;
                for (int i = 0; i < 600; i++) {
                    panel = PanelMgr.ins.GetPanel(CardCultivationControl.PanelId) as CardCultivationPanel;
                    if (panel != null && panel.isOpened && panel.CurrentCardId == id && panel.imgPortrait.sprite != null && panel.imgPortrait.enabled) break;
                    await Frames(1);
                }
                Check(panel != null && panel.isOpened && panel.CurrentCardId == id, "normal deck entry -> cultivation " + id);
                await Frames(30);
                var state = CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, id);
                var items = panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>().Where(v => v.gameObject.activeInHierarchy && v.imgMaterialIcon != null).OrderBy(v => v.transform.position.x).ToArray();
                Check(items.Length == state.materials.Count && items.Length > 0, "real material query/list " + id);
                var item = items[0]; int itemId = state.materials[0].itemId;
                string owned = item.txtMaterialOwned.text, required = item.txtMaterialRequired.text;
                await Capture(request, panel, item, id, itemId, "before");
                var slot = item.GetComponentsInChildren<Image>(true).Single(v => v.name == "imgMaterialSlot");
                slot.sprite = frame; Place(slot, 6, 0, 97, 101);
                Sprite content = LoadSprite(Path.Combine(request.fragmentDirectory, itemId + "-B-content.png"));
                item.imgMaterialIcon.sprite = content; Place(item.imgMaterialIcon, 6, 0, 97, 101);
                Check(item.txtMaterialOwned.text == owned && item.txtMaterialRequired.text == required, "quantity remains business-driven " + id);
                Check(item.imgMaterialIcon.rectTransform.sizeDelta == new Vector2(97, 101) && slot.rectTransform.sizeDelta == new Vector2(97, 101), "candidate frame/content at exact design dimensions " + id);
                Check(slot.material == Graphic.defaultGraphicMaterial && item.imgMaterialIcon.material == Graphic.defaultGraphicMaterial, "default UI material, no tint/shader " + id);
                await Capture(request, panel, item, id, itemId, "B-runtime");
                panel.btnReturn.onClick.Invoke(); await Frames(16);
                Check(!PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId), "return to actual deck " + id);
            }
            Check(writes == 0, "readonly progress session: zero save callbacks");
            for (int i = 0; i < files.Length; i++) Check(Digest(files[i]) == hashes[i], "real progress file unchanged " + Path.GetFileName(files[i]));
            WriteResult(request, null);
        } catch (Exception error) { WriteResult(request, error); Debug.LogException(error); }
        finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            if (installed) PlayerProgressControl.InstallValidationSession(previous);
            foreach (var obj in temporary) if (obj != null) UnityEngine.Object.Destroy(obj);
            Application.runInBackground = oldBackground;
            EditorApplication.isPlaying = false;
        }
    }
    private static void WriteResult(Request request, Exception error) {
        var result = new Result { requestId = request == null ? "unreadable" : request.requestId, status = error == null ? "passed" : "failed", error = error == null ? null : error.ToString(), checks = checks.ToArray(), shots = shots.ToArray() };
        string json = JsonUtility.ToJson(result, true);
        File.WriteAllText("Library/AIUI/item-icon-preview-result.json", json);
        if (request != null && !string.IsNullOrEmpty(request.evidenceDirectory)) File.WriteAllText(Path.Combine(request.evidenceDirectory, "runtime-result.json"), json);
    }
}
#endif
