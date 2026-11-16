using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 上阵底座独立item的增量迁移入口。
/// </summary>
/// <remarks>仅处理显式PID及哈希请求；使用既有UIBinder导出器，保留非目标节点。</remarks>
[InitializeOnLoad]
public static class DeckFormationBaseIntegration {

    const string Main = "Assets/Prefab/default/mainView/MainPanel.prefab";
    const string Slot = "Assets/Prefab/default/mainView/components/DeckFormationSlot.prefab";
    const string Base = "Assets/Prefab/default/mainView/components/DeckFormationBase.prefab";
    const string Script = "Assets/Scripts/csharp/main/mainView/view/DeckFormationBase.cs";
    const string Pending = "Library/AIUI/pending-deck-formation-base.json";

    [Serializable] class Request { public string requestId; public int processId; public string mainHash; public string slotHash; public string baseHash; public bool alignOnly; }
    [Serializable] class Result { public string requestId; public string status; public string error; public int members; public int slots; }

    static DeckFormationBaseIntegration() {
        EditorApplication.update += Poll;
        File.WriteAllText("Library/AIUI/deck-formation-base-tool-ready.json", "{\"version\":4,\"compiled\":true}");
    }

    static string Hash(string path) {
        using (var sha = System.Security.Cryptography.SHA256.Create()) {
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }

    static void Rect(Transform t, float x, float y, float w, float h) {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
    }

    static void AlignBase(GameObject root) {
        var badge = root.transform.Find("imgBadgeBase");
        Rect(badge, 34, 22.5f, 32, 40);
        var role = root.GetComponent<UIBinder>().uiList.First(x => x.uiName == "roleIcon").go.GetComponent<Image>();
        role.transform.SetParent(badge, false);
        var rt = role.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(24, 24);
        rt.localScale = Vector3.one;
        role.preserveAspect = true;
        var level = root.transform.Find("txtLevel").GetComponent<TextMeshProUGUI>();
        Rect(level.transform, 69, 33.5f, 55, 26);
        level.alignment = TextAlignmentOptions.Left;
        level.horizontalAlignment = HorizontalAlignmentOptions.Left;
        level.verticalAlignment = VerticalAlignmentOptions.Middle;
        level.margin = Vector4.zero;
    }

    static GameObject Clone(Component source, Transform parent, string name) {
        var go = UnityEngine.Object.Instantiate(source.gameObject, parent);
        go.name = name;
        return go;
    }

    static void CreateBase(DeckCardView card) {
        if (File.Exists(Base)) {
            throw new InvalidOperationException("Base prefab already exists; no overwrite");
        }
        var root = new GameObject("DeckFormationBase", typeof(RectTransform), typeof(UIBinder));
        try {
            Rect(root.transform, 0, 0, 156, 63);
            var stage = Clone(card.qualityImage, root.transform, "imgStage").GetComponent<Image>();
            Rect(stage.transform, 0, 0, 156, 63);
            stage.color = Color.white;
            var badgeObject = new GameObject("imgBadgeBase", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObject.transform.SetParent(root.transform, false);
            Rect(badgeObject.transform, 34, 24, 32, 37);
            var badge = badgeObject.GetComponent<Image>();
            badge.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/unpack/default/card/sharedV1/badge-base.png");
            if (badge.sprite == null) {
                throw new InvalidOperationException("Approved badge-base sprite missing");
            }
            badge.preserveAspect = true;
            badge.raycastTarget = false;
            var role = Clone(card.roleIcon, root.transform, "imgRole").GetComponent<Image>();
            Rect(role.transform, 39, 30, 22, 25);
            role.sprite = null;
            role.enabled = false;
            role.color = Color.white;
            role.preserveAspect = true;
            var level = Clone(card.level, root.transform, "txtLevel").GetComponent<TextMeshProUGUI>();
            Rect(level.transform, 67, 34, 57, 26);
            level.fontSize = 18;
            level.enableAutoSizing = false;
            level.enableWordWrapping = false;
            level.alignment = TextAlignmentOptions.Center;
            level.fontStyle = FontStyles.Bold;
            level.color = Color.white;
            level.text = "Lv.—";
            var binder = root.GetComponent<UIBinder>();
            binder.csharpAssetPath = binder.csharpAsset = Script;
            var names = new[] { "stageImage", "badgeBase", "roleIcon", "level" };
            Component[] components = { stage, badge, role, level };
            binder.uiList = components.Select((c, i) => new UIBindComponentData { id = i, uiName = names[i], go = c.gameObject, uiTypeName = c.GetType().Name }).ToList();
            binder.RefreshScriptBinding();
            var inspector = Editor.CreateEditor(binder);
            try {
                typeof(UIBinderInspector).GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, new object[] { Script });
            } finally {
                UnityEngine.Object.DestroyImmediate(inspector);
            }
            AlignBase(root);
            PrefabUtility.SaveAsPrefabAsset(root, Base);
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void Wire(DeckCardView card) {
        var t = card.transform.Find("itemBase");
        if (t == null) {
            t = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base), card.transform)).transform;
            t.name = "itemBase";
        }
        Rect(t, 2, 192, 156, 63);
        t.SetAsFirstSibling();
        // 删除旧的三个视觉节点；其他角色、名称、点击及加载组件保持原身份。
        foreach (string name in new[] { "imgQuality", "imgRole", "txtLevel" }) {
            var old = card.transform.Find(name);
            if (old != null) {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
        }
        card.qualityImage = t.Find("imgStage").GetComponent<Image>();
        card.roleIcon = t.Find("imgBadgeBase/imgRole").GetComponent<Image>();
        card.level = t.Find("txtLevel").GetComponent<TextMeshProUGUI>();
        card.roleIcon.sprite = null;
        card.roleIcon.enabled = false;
        EditorUtility.SetDirty(card);
    }

    static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        Request r = null;
        try {
            r = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            File.Delete(Pending);
            if (r.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(r.requestId) || Hash(Main) != r.mainHash || Hash(Slot) != r.slotHash) {
                throw new InvalidOperationException("PID/hash mismatch");
            }
            var editing = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (editing != null && editing.scene.isDirty) {
                throw new InvalidOperationException("Unsaved prefab stage");
            }
            if (r.alignOnly) {
                if (Hash(Base) != r.baseHash) {
                    throw new InvalidOperationException("Base hash mismatch");
                }
                var root = PrefabUtility.LoadPrefabContents(Base);
                try {
                    foreach (var path in Enumerable.Range(1, 8).Select(i => "Assets/Art/unpack/default/card/tags/" + i.ToString("00") + ".png").Concat(new[] { "Assets/Art/unpack/default/card/sharedV1/badge-base.png" })) {
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null) { throw new InvalidOperationException("Missing icon: " + path); }
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePivot = new Vector2(.5f, .5f);
                        importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                    AlignBase(root);
                    PrefabUtility.SaveAsPrefabAsset(root, Base);
                } finally {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText("Library/AIUI/deck-formation-base-result.json", JsonUtility.ToJson(new Result { requestId = r.requestId, status = "passed", members = 4, slots = 4 }, true));
                return;
            }
            var slot = PrefabUtility.LoadPrefabContents(Slot);
            try {
                var card = slot.GetComponent<DeckCardView>();
                CreateBase(card);
                Wire(card);
                PrefabUtility.SaveAsPrefabAsset(slot, Slot);
            } finally {
                PrefabUtility.UnloadPrefabContents(slot);
            }
            var main = PrefabUtility.LoadPrefabContents(Main);
            try {
                var owner = main.GetComponentInChildren<DeckPresentationView>(true);
                foreach (var card in owner.slots) {
                    Wire(card);
                }
                PrefabUtility.SaveAsPrefabAsset(main, Main);
            } finally {
                PrefabUtility.UnloadPrefabContents(main);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/AIUI/deck-formation-base-result.json", JsonUtility.ToJson(new Result { requestId = r.requestId, status = "passed", members = 4, slots = 4 }, true));
            AssetDatabase.Refresh();
        } catch (Exception e) {
            File.WriteAllText("Library/AIUI/deck-formation-base-result.json", JsonUtility.ToJson(new Result { requestId = r == null ? "unparsed" : r.requestId, status = "failed", error = e.ToString() }, true));
            Debug.LogException(e);
        }
    }

}

