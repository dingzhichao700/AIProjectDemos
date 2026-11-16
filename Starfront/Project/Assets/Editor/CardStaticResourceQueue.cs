#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;

/// <summary>按显式清单入库批准的卡牌静态资源，并验证正式界面消费；不写业务状态。</summary>
[InitializeOnLoad]
internal static class CardStaticResourceQueue {
    const string Queue = "Library/AIUI/";
    const string Pending = Queue + "pending-card-static-resources.json";
    const string Session = "CardStaticResources.Runtime";
    [Serializable] internal class Entry { public int cardId, artId, width, height; public string path, usage, sha256; public float pivotX, pivotY; }
    [Serializable] internal class Manifest { public Entry[] assets; }
    [Serializable] internal class Request { public string requestId, action, manifestPath, outputDirectory; public int processId; }
    [Serializable] internal class Result { public string requestId, status, error, scene; public int processId, imported, loaded, libraryCards, detailsChecked; public bool stateUnchanged; public string[] screenshots; }
    static bool running;
    static CardStaticResourceQueue() { EditorApplication.update += Tick; }
    static void Require(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
    static string Hash(string path) { using (var h = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static Manifest Read(Request r) { return JsonUtility.FromJson<Manifest>(File.ReadAllText(r.manifestPath).TrimStart('\uFEFF')); }
    static void Write(Request r, Result result) {
        result.requestId = r.requestId; result.processId = Process.GetCurrentProcess().Id;
        Directory.CreateDirectory(r.outputDirectory);
        File.WriteAllText(Path.Combine(r.outputDirectory, r.action + "-result.json"), JsonUtility.ToJson(result, true));
    }
    static void Tick() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || running) return;
        if (SessionState.GetString(Session, "") != "" && EditorApplication.isPlaying) {
            var request = JsonUtility.FromJson<Request>(SessionState.GetString(Session, ""));
            SessionState.EraseString(Session); RunRuntime(request); return;
        }
        if (!File.Exists(Pending)) return;
        Request r = null;
        try {
            r = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF')); File.Delete(Pending);
            Require(r != null && r.processId == Process.GetCurrentProcess().Id && !string.IsNullOrEmpty(r.requestId), "Request PID/id mismatch");
            if (r.action == "toolCheck") { Write(r, new Result { status = "passed" }); return; }
            if (r.action == "import") {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Import requires edit mode");
                Import(r); return;
            }
            Require(r.action == "runtime", "Unknown action");
            if (!EditorApplication.isPlaying) { SessionState.SetString(Session, JsonUtility.ToJson(r)); EditorApplication.isPlaying = true; }
            else RunRuntime(r);
        } catch (Exception e) {
            if (r != null) Write(r, new Result { status = "failed", error = e.ToString() });
            UnityEngine.Debug.LogException(e);
        }
    }
    static void Import(Request r) {
        var manifest = Read(r);
        Require(manifest.assets.Length > 0 && manifest.assets.Select(x => x.path).Distinct().Count() == manifest.assets.Length, "Empty/duplicate paths");
        var tables = new cfg.Tables(name => JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json")));
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("Default Local Group"); Require(group != null, "Missing loose-sprite group");
        // Preflight all files/config mappings before any importer/Addressables mutation.
        foreach (var e in manifest.assets) {
            Require(e.path.StartsWith("Assets/Art/unpack/default/card/rosterV3/") && File.Exists(e.path) && Hash(e.path) == e.sha256, "Source/hash mismatch: " + e.path);
            var card = tables.CardObj.Get(e.cardId); var art = tables.CardArtObj.Get(e.artId);
            Require(card.ArtId == e.artId && CardArtImage.PathFor(art, (CardArtImage.Usage)Enum.Parse(typeof(CardArtImage.Usage), e.usage)) == e.path && art.CardFaceReady, "Config mapping mismatch: " + e.path);
        }
        foreach (var e in manifest.assets) {
            AssetDatabase.ImportAsset(e.path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(e.path) as TextureImporter; Require(importer != null, "Missing importer");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.isReadable = false; importer.sRGBTexture = true;
            importer.alphaIsTransparency = e.usage != "Card"; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
            var options = new TextureImporterSettings(); importer.ReadTextureSettings(options);
            options.spriteMeshType = SpriteMeshType.FullRect; options.spriteAlignment = (int)SpriteAlignment.Custom;
            options.spritePivot = new Vector2(e.pivotX, e.pivotY); importer.SetTextureSettings(options); importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.path);
            Require(sprite != null && sprite.rect.width == e.width && sprite.rect.height == e.height, "Sprite size mismatch");
            Require(Vector2.Distance(sprite.pivot / sprite.rect.size, new Vector2(e.pivotX, e.pivotY)) < .0001f, "Sprite pivot mismatch");
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(e.path), group); entry.address = e.path; entry.SetLabel("default", true, true);
        }
        Require(manifest.assets.Select(x => AssetDatabase.AssetPathToGUID(x.path)).Distinct().Count() == manifest.assets.Length, "Duplicate GUID");
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Write(r, new Result { status = "passed", imported = manifest.assets.Length });
    }
    static async Task Wait(Func<bool> condition, int milliseconds = 20000) {
        var until = EditorApplication.timeSinceStartup + milliseconds / 1000d;
        while (!condition()) { Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup < until, "Runtime wait timeout"); EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(60); }
    }
    static string State(int[] ids) {
        return string.Join(";", ids.Select(id => { var state = CardCollectionControl.ins.model.GetState(id); return id + ":" + state.level + ":" + state.owned; }));
    }
    static async Task Capture(string path) {
        Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(path);
        await Wait(() => File.Exists(path), 5000); await Task.Delay(160);
    }
    static async void RunRuntime(Request r) {
        running = true; var result = new Result { status = "failed" }; bool prior = Application.runInBackground;
        var screenshots = new List<string>(); GameObject probe = null;
        try {
            Application.runInBackground = true;
            var game = typeof(Editor).Assembly.GetType("UnityEditor.GameView"); EditorWindow.GetWindow(game).Show();
            bool loginRequested = false;
            await Wait(() => {
                var main = UnityEngine.Object.FindObjectOfType<MainPanel>(); if (main && main.isOpened) return true;
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login && !loginRequested && login.txt && login.txt.gameObject.activeInHierarchy) { loginRequested = true; login.SendMessage("EnterMain", SendMessageOptions.DontRequireReceiver); }
                return false;
            }, 90000);
            var mainPanel = UnityEngine.Object.FindObjectOfType<MainPanel>();
            if (!mainPanel.viewDeck.gameObject.activeSelf) mainPanel.btnDeck.onClick.Invoke();
            DeckPresentationView deck = null;
            await Wait(() => (deck = UnityEngine.Object.FindObjectOfType<DeckPresentationView>()) != null && deck.library.arraySource.Count == 16);
            var manifest = Read(r); var ids = manifest.assets.Select(x => x.cardId).Distinct().OrderBy(x => x).ToArray(); var stateBefore = State(ids);
            // Probe the existing loader, not direct AssetDatabase sprites or runtime showcase overrides.
            probe = new GameObject("CardStaticResourceLoadingProbe", typeof(RectTransform), typeof(Image));
            var loader = probe.AddComponent<CardArtImage>(); loader.autoLoadOnEnable = false;
            foreach (var e in manifest.assets) {
                await loader.BindAsync(e.artId, (CardArtImage.Usage)Enum.Parse(typeof(CardArtImage.Usage), e.usage));
                var sprite = probe.GetComponent<Image>().sprite;
                Require(sprite && sprite.rect.width == e.width && sprite.rect.height == e.height, "Formal loader size: " + e.path);
                Require(Vector2.Distance(sprite.pivot / sprite.rect.size, new Vector2(e.pivotX, e.pivotY)) < .0001f, "Formal loader pivot: " + e.path);
                result.loaded++; loader.Clear();
            }
            UnityEngine.Object.Destroy(probe); probe = null;
            deck.filters[0].onClick.Invoke();
            var scroll = deck.library.GetComponent<ScrollRect>(); float oldPosition = scroll.verticalNormalizedPosition;
            var directory = Path.GetFullPath(r.outputDirectory); Directory.CreateDirectory(directory);
            foreach (float position in new[] { 1f, .5f, 0f, 1f }) {
                scroll.verticalNormalizedPosition = position; Canvas.ForceUpdateCanvases(); await Task.Delay(450);
                var views = deck.library.GetComponentsInChildren<DeckCardView>(false).Where(x => !x.formation).ToArray();
                await Task.WhenAll(views.Select(x => x.ArtLoading));
                Require(views.All(x => x.artwork.enabled && x.artwork.sprite), "Visible library art missing after scroll");
                var screen = Path.Combine(directory, "game-library-" + screenshots.Count + ".png"); await Capture(screen); screenshots.Add(screen);
            }
            result.libraryCards = deck.library.arraySource.Count;
            // Open every unit from the real library handler and verify portrait navigation through the live panel.
            foreach (var id in ids) {
                deck.RequestCardInfo(id); CardCultivationPanel panel = null;
                await Wait(() => (panel = UnityEngine.Object.FindObjectOfType<CardCultivationPanel>()) != null && panel.isOpened && panel.imgPortrait.enabled && panel.imgPortrait.sprite != null);
                var expected = manifest.assets.First(x => x.cardId == id && x.usage == "Portrait");
                var loaded = panel.imgPortrait.sprite; Require(loaded.rect.width == expected.width && loaded.rect.height == expected.height, "Detail portrait not refreshed: " + id);
                if (id == 2102 || id == 2205 || id == 2306) { var screen = Path.Combine(directory, "game-detail-" + id + ".png"); await Capture(screen); screenshots.Add(screen); }
                panel.btnReturn.onClick.Invoke(); await Wait(() => UnityEngine.Object.FindObjectOfType<CardCultivationPanel>() == null || !UnityEngine.Object.FindObjectOfType<CardCultivationPanel>().isOpened);
                result.detailsChecked++;
            }
            scroll.verticalNormalizedPosition = oldPosition;
            result.stateUnchanged = stateBefore == State(ids); Require(result.stateUnchanged, "Player ownership/level changed");
            result.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; result.status = "passed";
        } catch (Exception e) { result.error = e.ToString(); UnityEngine.Debug.LogException(e); }
        finally {
            if (probe) UnityEngine.Object.Destroy(probe);
            Application.runInBackground = prior; result.screenshots = screenshots.ToArray(); Write(r, result); running = false;
        }
    }
}
#endif
