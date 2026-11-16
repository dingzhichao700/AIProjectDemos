using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Applies reviewed visual changes to an existing prefab without recreating existing nodes.</summary>
/// <remarks>Explicit batch entry points only; no automatic content mutation on compilation.</remarks>
[InitializeOnLoad]
public static class AIUIIncrementalUpdate {
    [Serializable] public class Request {
        public string requestId;
        public string prefabPath;
        public string expectedSha256;
        public string resultPath;
        public string previewPath;
        public string spriteFolder;
        public Op[] nodes;
        public string[] hide;
    }
    [Serializable] public class Op {
        public string path;
        public string kind;
        public string sprite;
        public string text;
        public float x, y, width, height, fontSize;
        public string color;
        public float[] border;
        public int sibling = -1;
    }
    [Serializable] public class Result {
        public string requestId, status, error;
        public int preservedBindings, nodes;
    }
    static Request Read() {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-incrementalRequest");
        if (i < 0 || i + 1 >= args.Length) throw new Exception("Missing incremental request");
        var r = JsonUtility.FromJson<Request>(File.ReadAllText(args[i + 1]));
        if (string.IsNullOrEmpty(r.requestId) || r.prefabPath != "Assets/Prefab/default/mainView/MainPanel.prefab") throw new Exception("Invalid target/requestId");
        return r;
    }
    static string Hash(string path) {
        using (var sha = System.Security.Cryptography.SHA256.Create()) {
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
    static Type TypeByName(string name) {
        return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
    }
    static Component Binder(GameObject root) {
        return root.GetComponents<Component>().First(c => c != null && c.GetType().Name == "UIBinder");
    }
    static Dictionary<string, UnityEngine.Object> Bindings(GameObject root) {
        var array = new SerializedObject(Binder(root)).FindProperty("uiList");
        var values = new Dictionary<string, UnityEngine.Object>();
        for (int i = 0; i < array.arraySize; i++) {
            var e = array.GetArrayElementAtIndex(i);
            values.Add(e.FindPropertyRelative("uiName").stringValue, e.FindPropertyRelative("go").objectReferenceValue);
        }
        if (values.Count != 11 || values.Values.Any(v => v == null)) throw new Exception("Expected 11 valid original bindings");
        return values;
    }
    public static void ValidateTool() { Run(false); }
    public static void Apply() { Run(true); }
    public static void PreviewOnly() {
        var r = Read();
        var root = PrefabUtility.LoadPrefabContents(r.prefabPath);
        try {
            Bindings(root);
            Capture(root, r);
            File.WriteAllText(r.resultPath, JsonUtility.ToJson(new Result { requestId = r.requestId, status = "preview-verified", preservedBindings = 11, nodes = root.GetComponentsInChildren<Transform>(true).Length }, true));
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
    static void Run(bool write) {
        throw new InvalidOperationException("旧版阵容布局入口已退役；使用当前Prefab和FormationPageGameCheck验证。");
    }
    static void ApplyNodes(GameObject root, Request r) {
        var font = root.GetComponentsInChildren<TextMeshProUGUI>(true).First().font;
        foreach (string path in r.hide) {
            Transform old = root.transform.Find(path);
            if (old == null) throw new Exception("Missing expected hide target: " + path);
            old.gameObject.SetActive(false);
        }
        var imported = new HashSet<string>();
        foreach (Op o in r.nodes) {
            Transform t = root.transform.Find(o.path);
            if (t == null) {
                int slash = o.path.LastIndexOf('/');
                Transform parent = slash < 0 ? root.transform : root.transform.Find(o.path.Substring(0, slash));
                if (parent == null) throw new Exception("Missing parent: " + o.path);
                var go = new GameObject(o.path.Substring(slash + 1), typeof(RectTransform));
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(o.x, -o.y);
            rt.sizeDelta = new Vector2(o.width, o.height);
            rt.localScale = Vector3.one;
            if (o.sibling >= 0) rt.SetSiblingIndex(o.sibling);
            if (o.kind == "image") {
                var im = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
                if (!string.IsNullOrEmpty(o.sprite)) {
                    string p = r.spriteFolder + "/" + o.sprite;
                    var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                    if (ti == null) throw new Exception("Missing texture: " + p);
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.spritePixelsPerUnit = 100;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.spriteBorder = o.border != null && o.border.Length == 4 ? new Vector4(o.border[0],o.border[3],o.border[2],o.border[1]) : Vector4.zero;
                    if (imported.Add(p)) { ti.SaveAndReimport(); }
                    im.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (im.sprite == null) throw new Exception("Missing Sprite: " + p);
                    im.type = ti.spriteBorder != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                }
                Color tint; im.color = ColorUtility.TryParseHtmlString(o.color, out tint) ? tint : Color.white; im.raycastTarget = t.GetComponent<Button>() != null;
            } else if (o.kind == "text") {
                var text = t.GetComponent<TextMeshProUGUI>() ?? t.gameObject.AddComponent<TextMeshProUGUI>();
                text.font = font; text.text = o.text; text.fontSize = o.fontSize;
                text.fontStyle = o.fontSize > 13 ? FontStyles.Bold : FontStyles.Normal; text.enableAutoSizing = false; text.enableWordWrapping = false;
                text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                Color color; if (ColorUtility.TryParseHtmlString(o.color, out color)) text.color = color;
            } else if (o.kind == "button") {
                var im = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
                im.color = Color.clear; im.raycastTarget = true;
                var button = t.GetComponent<Button>() ?? t.gameObject.AddComponent<Button>();
                button.targetGraphic = im; button.transition = Selectable.Transition.None;
            }
        }
    }
    static void ConfigurePresentation(GameObject root, Request r) {
        throw new InvalidOperationException("旧版阵容布局入口已退役；使用当前Prefab和FormationPageGameCheck验证。");
    }
    static void Capture(GameObject root, Request r) {
        // Capture an isolated clone; layout and selection changes are not saved to the prefab.
        var clone = UnityEngine.Object.Instantiate(root);
        try {
            foreach (var c in clone.GetComponents<MonoBehaviour>()) {
                if (c != null && c.GetType().Name == "MainPanelLayout") c.enabled = false;
            }
            var rect = (RectTransform)clone.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(720,1280); rect.anchoredPosition = Vector2.zero;
            var content = (RectTransform)clone.transform.Find("boxContentViewport");
            content.anchorMin = content.anchorMax = new Vector2(0,1); content.pivot = new Vector2(0,1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(720,1160);
            foreach (Transform t in content) t.gameObject.SetActive(t.name == "viewDeck");
            var deck = (RectTransform)content.Find("viewDeck/boxDeckContent");
            deck.anchorMin = deck.anchorMax = new Vector2(0,1); deck.pivot = new Vector2(0,1); deck.anchoredPosition = Vector2.zero; deck.sizeDelta = new Vector2(720,1160); deck.localScale = Vector3.one;
            var nav = (RectTransform)clone.transform.Find("boxNavigation");
            nav.anchorMin = nav.anchorMax = new Vector2(0,1); nav.pivot = new Vector2(0,1); nav.anchoredPosition = new Vector2(0,-1160); nav.sizeDelta = new Vector2(720,120); nav.localScale = Vector3.one;
            var nc = (RectTransform)nav.Find("boxNavigationContent");
            nc.anchorMin = nc.anchorMax = new Vector2(0,1); nc.pivot = new Vector2(0,1); nc.anchoredPosition = Vector2.zero; nc.sizeDelta = new Vector2(720,120); nc.localScale = Vector3.one;
            rect.localScale = Vector3.one;
            var art = clone.GetComponent(TypeByName("MainPanelArtState"));
            art.GetType().GetMethod("SetNavigation").Invoke(art, new object[] { 1 });
            clone.transform.Find("boxNavigation/boxNavigationContent/btnDeck/imgDeckSelected").gameObject.SetActive(true);
            clone.transform.Find("boxNavigation/boxNavigationContent/btnBattle/imgBattleSelected").gameObject.SetActive(false);
            var req = new AIUIExportQueue.Request { width = 720, height = 1280, previewImagePath = r.previewPath };
            typeof(AIUIExportQueue).GetMethod("CapturePreview", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { clone, req });
        } finally { UnityEngine.Object.DestroyImmediate(clone); }
    }
    public static void ValidatePresentation() {
        throw new InvalidOperationException("旧版阵容布局入口已退役；使用当前Prefab和FormationPageGameCheck验证。");
    }    [Serializable] public class PivotRequest {
        public string requestId;
        public int processId;
        public string expectedSha256;
        public string[] paths;
    }
    [Serializable] public class PivotCheck {
        public string path;
        public Vector2 oldPivot;
        public Vector2 pivot;
        public float maxCornerMovement;
        public float pressedCenterMovement;
    }
    [Serializable] public class PivotResult {
        public string requestId;
        public string status;
        public string error;
        public PivotCheck[] checks;
    }
    static AIUIIncrementalUpdate() {
        EditorApplication.update += PollPivotRequest;
        EditorApplication.update += PollBattleFeedback;
        EditorApplication.update += PollReviewedContent;
    }
    static void PollPivotRequest() {
        const string pending = "Library/AIUI/pending-center-pivots.json";
        const string result = "Library/AIUI/center-pivots-result.json";
        if (!File.Exists(pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        PivotRequest request = null;
        GameObject root = null;
        try {
            request = JsonUtility.FromJson<PivotRequest>(File.ReadAllText(pending).TrimStart('﻿'));
            if (string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new Exception("Request identity/PID mismatch");
            }
            string path = "Assets/Prefab/default/mainView/MainPanel.prefab";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty) {
                throw new Exception("MainPanel has unsaved prefab-stage changes; not overwritten");
            }
            if (!string.Equals(Hash(path), request.expectedSha256, StringComparison.OrdinalIgnoreCase)) {
                throw new Exception("MainPanel changed since request");
            }
            root = PrefabUtility.LoadPrefabContents(path);
            var originalBindings = Bindings(root);
            var checks = new List<PivotCheck>();
            foreach (string nodePath in request.paths) {
                var rt = root.transform.Find(nodePath) as RectTransform;
                if (rt == null) {
                    throw new Exception("Missing pivot target: " + nodePath);
                }
                var before = new Vector3[4];
                rt.GetWorldCorners(before);
                Vector2 old = rt.pivot;
                Vector3 center = rt.TransformPoint(rt.rect.center);
                rt.pivot = new Vector2(.5f,.5f);
                // Move the transform origin to the prior visual center; preserve all world corners.
                rt.position = center;
                var after = new Vector3[4];
                rt.GetWorldCorners(after);
                float cornerMovement = 0;
                for (int i = 0; i < 4; i++) {
                    cornerMovement = Mathf.Max(cornerMovement, Vector3.Distance(before[i], after[i]));
                }
                Vector3 originalScale = rt.localScale;
                rt.localScale = originalScale * .95f;
                float centerMovement = Vector3.Distance(center, rt.TransformPoint(rt.rect.center));
                rt.localScale = originalScale;
                if (cornerMovement > .01f || centerMovement > .01f) {
                    throw new Exception("Pivot geometry validation failed: " + nodePath);
                }
                checks.Add(new PivotCheck { path=nodePath, oldPivot=old, pivot=rt.pivot, maxCornerMovement=cornerMovement, pressedCenterMovement=centerMovement });
            }
            foreach (var pair in originalBindings) {
                if (Bindings(root)[pair.Key] != pair.Value) {
                    throw new Exception("Binding changed: " + pair.Key);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            File.WriteAllText(result, JsonUtility.ToJson(new PivotResult { requestId=request.requestId, status="passed", checks=checks.ToArray() }, true));
            File.Delete(pending);
        } catch (Exception e) {
            File.WriteAllText(result, JsonUtility.ToJson(new PivotResult { requestId=request == null ? "unparsed" : request.requestId, status="failed", error=e.ToString() }, true));
            if (File.Exists(pending)) {
                File.Move(pending, pending + "." + DateTime.UtcNow.Ticks + ".failed");
            }
            Debug.LogException(e);
        } finally {
            if (root != null) {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }    [Serializable] public class FeedbackResult {
        public string requestId, status;
        public int gameButtonCount = 1, buttonCount = 1, scaleCases = 3, cyclesPerScale = 3, clickInvocations = 9, preservedBindings = 11;
        public bool originalButtonSerializedStateUnchanged = true;
    }
    static void PollBattleFeedback() {
        const string pending = "Library/AIUI/pending-battle-feedback.json";
        const string result = "Library/AIUI/battle-feedback-result.json";
        if (!File.Exists(pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        PivotRequest request = null;
        GameObject root = null;
        GameObject test = null;
        try {
            request = JsonUtility.FromJson<PivotRequest>(File.ReadAllText(pending).TrimStart('﻿'));
            if (string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new Exception("Invalid request/PID");
            }
            string path = "Assets/Prefab/default/mainView/MainPanel.prefab";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty) {
                throw new Exception("Unsaved MainPanel prefab stage; not overwritten");
            }
            if (!string.Equals(Hash(path), request.expectedSha256, StringComparison.OrdinalIgnoreCase)) {
                throw new Exception("Prefab changed");
            }
            root = PrefabUtility.LoadPrefabContents(path);
            var bindings = Bindings(root);
            Transform target = root.transform.Find("boxContentViewport/viewBattle/boxBattlePlayVsAI");
            var button = target.GetComponent<Button>();
            var label = target.GetComponentInChildren<TextMeshProUGUI>(true);
            string originalText = label.text;
            string originalClicks = EditorJsonUtility.ToJson(button);
            var feedback = target.GetComponent<GameButton>() ?? target.gameObject.AddComponent<GameButton>();
            var so = new SerializedObject(feedback);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("text").stringValue = originalText;
            so.FindProperty("enableHoverScale").boolValue = false;
            so.FindProperty("enablePressedScale").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            label.text = originalText;
            if (target.GetComponents<GameButton>().Length != 1 || target.GetComponents<Button>().Length != 1 || EditorJsonUtility.ToJson(button) != originalClicks) {
                throw new Exception("Unexpected button duplication or Button change");
            }
            if (((RectTransform)target).pivot != new Vector2(.5f,.5f)) {
                throw new Exception("Battle button is not centered");
            }
            // Test an isolated clone; never invoke StartBattle or any business listeners.
            test = UnityEngine.Object.Instantiate(target.gameObject);
            var effect = test.GetComponent<GameButton>();
            Type t = typeof(GameButton);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            t.GetMethod("Awake", flags).Invoke(effect, null);
            int businessCounter = 0;
            effect.clickSoundType = ButtonSoundType.None;
            test.GetComponent<Button>().onClick.AddListener(() => businessCounter++);
            foreach (float scale in new[] { .5f, 1f, 1.5f }) {
                effect.SetNormalScale(Vector3.one * scale);
                for (int cycle = 0; cycle < 3; cycle++) {
                    t.GetMethod("OnEnable", flags).Invoke(effect, null);
                    effect.OnPointerEnter(null);
                    if (((Vector3)t.GetField("_targetScale", flags).GetValue(effect) - Vector3.one * scale).sqrMagnitude > .000001f) {
                        throw new Exception("Hover must retain normal scale");
                    }
                    effect.OnPointerDown(null);
                    var pressed = (Vector3)t.GetField("_targetScale", flags).GetValue(effect);
                    if ((pressed - Vector3.one * scale * .95f).sqrMagnitude > .000001f) {
                        throw new Exception("Pressed scale mismatch");
                    }
                    effect.OnPointerExit(null);
                    var normal = (Vector3)t.GetField("_targetScale", flags).GetValue(effect);
                    if ((normal - Vector3.one * scale).sqrMagnitude > .000001f) {
                        throw new Exception("Restore scale mismatch");
                    }
                    effect.OnPointerDown(null);
                    effect.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(null) { position = new Vector2(-100000,-100000) });
                    normal = (Vector3)t.GetField("_targetScale", flags).GetValue(effect);
                    if ((normal - Vector3.one * scale).sqrMagnitude > .000001f) {
                        throw new Exception("PointerUp outside did not restore scale");
                    }
                    test.GetComponent<Button>().onClick.Invoke();
                }
            }
            if (businessCounter != 9 || label.text != originalText) {
                throw new Exception("Listener or label regression");
            }
            foreach (var kv in bindings) {
                if (Bindings(root)[kv.Key] != kv.Value) {
                    throw new Exception("Binder changed");
                }
            }
            foreach (bool hover in new[] { false, true }) foreach (bool press in new[] { false, true }) {
                t.GetField("enableHoverScale", flags).SetValue(effect, hover);
                t.GetField("enablePressedScale", flags).SetValue(effect, press);
                effect.SetNormalScale(Vector3.one);
                effect.OnPointerEnter(null);
                var actual = (Vector3)t.GetField("_targetScale", flags).GetValue(effect);
                if ((actual - Vector3.one * (hover ? 1.05f : 1f)).sqrMagnitude > .000001f) throw new Exception("Independent hover switch");
                effect.OnPointerDown(null);
                effect.OnPointerEnter(null);
                actual = (Vector3)t.GetField("_targetScale", flags).GetValue(effect);
                if ((actual - Vector3.one * (press ? .95f : 1f)).sqrMagnitude > .000001f) throw new Exception("Independent press switch");
                var rect = (RectTransform)effect.transform;
                effect.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) });
                actual = (Vector3)t.GetField("_targetScale", flags).GetValue(effect);
                if ((actual - Vector3.one * (hover ? 1.05f : 1f)).sqrMagnitude > .000001f) throw new Exception("Release inside feedback");
                effect.OnPointerExit(null);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            File.WriteAllText(result, JsonUtility.ToJson(new FeedbackResult { requestId=request.requestId, status="passed" }, true));
            File.Delete(pending);
        } catch (Exception e) {
            File.WriteAllText(result, JsonUtility.ToJson(new PivotResult { requestId=request == null ? "unparsed" : request.requestId, status="failed", error=e.ToString() },true));
            if (File.Exists(pending)) {
                File.Move(pending,pending+"."+DateTime.UtcNow.Ticks+".failed");
            }
            Debug.LogException(e);
        } finally {
            if (test != null) {
                UnityEngine.Object.DestroyImmediate(test);
            }
            if (root != null) {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }    static void PollReviewedContent() {
        const string pending = "Library/AIUI/pending-reviewed-content.json";
        if (!File.Exists(pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        Request r = null;
        GameObject root = null;
        try {
            r = JsonUtility.FromJson<Request>(File.ReadAllText(pending).TrimStart('﻿'));
            if (r.prefabPath != "Assets/Prefab/default/mainView/MainPanel.prefab" || string.IsNullOrEmpty(r.requestId)) {
                throw new Exception("Invalid reviewed-content target");
            }
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == r.prefabPath && stage.scene.isDirty) {
                throw new Exception("Unsaved prefab-stage edits; no overwrite");
            }
            if (!string.Equals(Hash(r.prefabPath), r.expectedSha256, StringComparison.OrdinalIgnoreCase)) {
                throw new Exception("Prefab changed after request preparation");
            }
            root = PrefabUtility.LoadPrefabContents(r.prefabPath);
            var original = Bindings(root);
            ApplyNodes(root,r);
            foreach (var pair in original) {
                if (Bindings(root)[pair.Key] != pair.Value) {
                    throw new Exception("Binding changed");
                }
            }
            foreach (var nav in new[] { "Shop", "Deck", "Battle", "Territory", "Ranking" }) {
                if (((RectTransform)root.transform.Find("boxNavigation/boxNavigationContent/btn"+nav)).pivot != new Vector2(.5f,.5f)) {
                    throw new Exception("Navigation pivot regression");
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root,r.prefabPath);
            Capture(root,r);
            File.WriteAllText(r.resultPath,JsonUtility.ToJson(new Result { requestId=r.requestId,status="passed",preservedBindings=11,nodes=root.GetComponentsInChildren<Transform>(true).Length },true));
            File.Delete(pending);
        } catch (Exception e) {
            File.WriteAllText("Library/AIUI/deck-content-result.json",JsonUtility.ToJson(new Result { requestId=r == null ? "unparsed" : r.requestId,status="failed",error=e.ToString() },true));
            if (File.Exists(pending)) {
                File.Move(pending,pending+"."+DateTime.UtcNow.Ticks+".failed");
            }
            Debug.LogException(e);
        } finally {
            if (root != null) {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }}








