#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局提示的常驻资源接入工具。
/// </summary>
/// <remarks>只消费带PID、工具版本与目标哈希的请求；不构建发布。</remarks>
[InitializeOnLoad]
public static class GlobalToastIntegration {

    const string Pending = "Library/AIUI/pending-global-toast-install.json";
    const string Team = "Assets/Prefab/default/mainView/TeamEditPanel.prefab";
    const string View = "Assets/Scripts/csharp/login/common/view/";
    const string Folder = "Assets/Prefab/title/common/";

    [Serializable] class Request { public string requestId; public int processId; public string toolVersion; public string expectedHash; public bool removeTeamHint; }
    [Serializable] class Result { public string requestId; public string status; public string error; public int removedBindings; public int panelMembers; public int itemMembers; }

    static GlobalToastIntegration() {
        EditorApplication.update += Poll;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size) {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }

    static void Export(UIBinder binder) {
        binder.RefreshScriptBinding();
        var inspector = Editor.CreateEditor(binder);
        try {
            inspector.GetType().GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, new object[] { binder.csharpAssetPath });
        } finally {
            UnityEngine.Object.DestroyImmediate(inspector);
        }
    }

    static UIBinder Bind(GameObject root, string cls) {
        var binder = root.AddComponent<UIBinder>();
        binder.csharpAssetPath = binder.csharpAsset = View + cls + ".cs";
        return binder;
    }

    static void Member(UIBinder binder, GameObject go, string name, string type) {
        binder.uiList.Add(new UIBindComponentData { id = binder.uiList.Count + 1, uiName = name, uiTypeName = type, go = go });
    }

    static void Register(string path) {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("title");
        if (group == null) {
            throw new InvalidOperationException("title Addressables group missing");
        }
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
        entry.address = path;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
    }

    static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var result = new Result();
        GameObject item = null;
        GameObject panel = null;
        GameObject team = null;
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            result.requestId = request.requestId;
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || request.toolVersion != "0.4.11" || string.IsNullOrEmpty(request.requestId)) {
                throw new InvalidOperationException("请求身份或工具版本不匹配");
            }
            using (var sha = System.Security.Cryptography.SHA256.Create()) {
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Team))).Replace("-", "").ToLowerInvariant();
                if (hash != request.expectedHash) {
                    throw new InvalidOperationException("TeamEditPanel已发生其他修改，拒绝覆盖");
                }
            }
            item = Rect("GlobalToastItem", null, new Vector2(640, 44)).gameObject;
            item.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var bg = Rect("imgGradientBg", item.transform, new Vector2(640, 44));
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            bg.gameObject.AddComponent<CanvasRenderer>();
            bg.gameObject.AddComponent<ToastGradientGraphic>().raycastTarget = false;
            var text = Rect("txtMessage", item.transform, new Vector2(536, 44)).gameObject.AddComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(52, 0);
            text.rectTransform.offsetMax = new Vector2(-52, 0);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/font/tmpFontAsset/common/fontBodyCommon.asset");
            text.fontSharedMaterial = text.font != null ? text.font.material : null;
            if (text.font == null || text.fontSharedMaterial == null) {
                throw new InvalidOperationException("提示字体或默认材质缺失");
            }
            text.fontSize = 26;
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18;
            text.fontSizeMax = 26;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "";
            var ib = Bind(item, "GlobalToastItem");
            Member(ib, text.gameObject, "txtMessage", "TextMeshProUGUI");
            Export(ib);
            PrefabUtility.SaveAsPrefabAsset(item, Folder + "GlobalToastItem.prefab");
            panel = Rect("GlobalToastPanel", null, new Vector2(720, 1280)).gameObject;
            panel.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var messages = Rect("boxMessages", panel.transform, new Vector2(720, 1280));
            var template = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "GlobalToastItem.prefab"));
            template.transform.SetParent(messages, false);
            template.name = "itemTemplate";
            template.SetActive(false);
            var pb = Bind(panel, "GlobalToastPanel");
            Member(pb, messages.gameObject, "boxMessages", "RectTransform");
            Member(pb, template, "itemTemplate", "RectTransform");
            Export(pb);
            PrefabUtility.SaveAsPrefabAsset(panel, Folder + "GlobalToastPanel.prefab");
            if (request.removeTeamHint) {
                team = PrefabUtility.LoadPrefabContents(Team);
                var tb = team.GetComponent<UIBinder>();
                var hints = tb.uiList.Where(m => m.uiName == "txtHint").ToArray();
                result.removedBindings = hints.Length;
                foreach (var hint in hints) {
                    var go = hint.go;
                    tb.uiList.Remove(hint);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                Export(tb);
                PrefabUtility.SaveAsPrefabAsset(team, Team);
            }
            Register(Folder + "GlobalToastPanel.prefab");
            Register(Folder + "GlobalToastItem.prefab");
            AssetDatabase.SaveAssets();
            result.panelMembers = pb.uiList.Count;
            result.itemMembers = ib.uiList.Count;
            result.status = "passed";
        } catch (Exception error) {
            result.status = "failed";
            result.error = error.ToString();
        } finally {
            if (team != null) {
                PrefabUtility.UnloadPrefabContents(team);
            }
            if (panel != null) {
                UnityEngine.Object.DestroyImmediate(panel);
            }
            if (item != null) {
                UnityEngine.Object.DestroyImmediate(item);
            }
            File.WriteAllText("Library/AIUI/global-toast-install-result.json", JsonUtility.ToJson(result, true));
            File.Move(Pending, Pending + ".processed-" + DateTime.UtcNow.Ticks);
        }
    }

}
#endif
