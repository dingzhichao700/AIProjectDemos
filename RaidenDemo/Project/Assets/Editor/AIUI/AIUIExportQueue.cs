#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 常驻 AIUI 预览导出队列。请求写入 Library，不通过增删 Editor 脚本触发导出。
/// </summary>
[InitializeOnLoad]
public static class AIUIExportQueue {
    const string ToolVersion = "0.4.1";
    const string StatusPath = "Library/AIUI/editor-status.json";
    const string ControlPendingPath = "Library/AIUI/pending-control.json";
    const string ControlResultPath = "Library/AIUI/control-result.json";
    const string PendingPath = "Library/AIUI/pending-preview.json";
    const string ResultPath = "Library/AIUI/preview-result.json";
    const string FinalizePendingPath = "Library/AIUI/pending-finalize.json";
    const string FinalizeResultPath = "Library/AIUI/finalize-result.json";
    const string CleanupPendingPath = "Library/AIUI/pending-cleanup.json";
    const string CleanupResultPath = "Library/AIUI/cleanup-result.json";
    static bool running;
    static string activeRequestId = string.Empty;
    static string activeStage = "idle";
    static string lastError = string.Empty;
    static double nextStatusWrite;

    [Serializable]
    public class Request {
        public string requestId;
        public string prefabPath;
        public string rootName;
        public string fontAssetPath;
        public float width = 720;
        public float height = 1280;
        public Node[] nodes;
        public bool initializeBinding;
        public Binding binding;
        public ChildBinding[] childBindings;
    }

    [Serializable]
    public class Node {
        public string kind;
        public string name;
        public string parent;
        public string assetPath;
        public string text;
        public float x;
        public float y;
        public float width;
        public float height;
        public float rotation;
        public bool rotateAroundCenter;
        public string imageType;
        public float borderLeft;
        public float borderBottom;
        public float borderRight;
        public float borderTop;
        public bool inactive;
        public float fontSize;
        public string color = "#FFFFFFFF";
        public string shadowColor = "#00000000";
        public float shadowOffsetY;
        public bool omitLabel;
        public float labelX;
        public float labelY;
        public float labelWidth;
        public float labelHeight;
        public string labelColor = "#FFFFFFFF";
        public string contentName;
        public string itemTemplate;
        public int horiLimitNum;
        public int vertLimitNum;
        public int horiGap;
        public int vertGap;
        public int previewCloneCount;
    }

    [Serializable]
    public class Binding {
        public string csharpAssetPath;
        public string summary;
        public string panelLayer = "SCALE_PANEL_FIRST";
        public Member[] members;
    }

    [Serializable]
    public class Member {
        public string node;
        public string type;
        public bool isCustomClass;
    }

    [Serializable]
    public class ChildBinding {
        public string node;
        public string csharpAssetPath;
        public Member[] members;
        public SpriteField[] spriteFields;
    }

    [Serializable]
    public class SpriteField {
        public string name;
        public string assetPath;
    }

    [Serializable]
    public class FinalizeRequest {
        public string requestId;
        public string prefabPath;
        public string[] prefabPaths;
        public string atlasSourcePath;
        public string atlasPath;
        public bool keepTransparentBounds = true;
        public string[] spriteNames;
        public SpriteBorderSetting[] spriteBorders;
        public LooseSpriteReplacement[] looseSpriteReplacements;
        public AssetMove[] assetMoves;
        public string addressableGroupName;
        public string[] addressableAssetPaths;
    }

    [Serializable]
    public class AssetMove {
        public string sourcePath;
        public string targetPath;
    }

    [Serializable]
    public class SpriteBorderSetting {
        public string assetPath;
        public float left;
        public float bottom;
        public float right;
        public float top;
    }

    [Serializable]
    public class LooseSpriteReplacement {
        public string spriteName;
        public string assetPath;
    }

    [Serializable]
    public class CleanupRequest {
        public string requestId;
        public string[] prefabPaths;
        public bool removeCanvasGroups;
    }

    [Serializable]
    public class ControlRequest {
        public string requestId;
        public string action;
    }

    static AIUIExportQueue() {
        EditorApplication.update += Poll;
        PublishStatus(true);
    }

    static void Poll() {
        PublishStatus(false);
        if (TryHandleControl()) return;
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(PendingPath) && !File.Exists(FinalizePendingPath) && !File.Exists(CleanupPendingPath)) return;
        string activePath = File.Exists(PendingPath) ? PendingPath :
            File.Exists(FinalizePendingPath) ? FinalizePendingPath : CleanupPendingPath;
        string resultPath = activePath == PendingPath ? ResultPath :
            activePath == FinalizePendingPath ? FinalizeResultPath : CleanupResultPath;
        running = true;
        activeStage = activePath == PendingPath ? "preview" : activePath == FinalizePendingPath ? "finalize" : "cleanup";
        activeRequestId = TryExtractRequestId(activePath);
        lastError = string.Empty;
        PublishStatus(true);
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try {
            if (File.Exists(PendingPath)) {
                Request request = JsonUtility.FromJson<Request>(ReadRequestText(PendingPath));
                activeRequestId = request.requestId;
                Validate(request);
                GeneratePreview(request);
                WriteResult(ResultPath, request.requestId, "passed", null, stopwatch.ElapsedMilliseconds);
                File.Delete(PendingPath);
            } else if (File.Exists(FinalizePendingPath)) {
                FinalizeRequest request = JsonUtility.FromJson<FinalizeRequest>(ReadRequestText(FinalizePendingPath));
                activeRequestId = request.requestId;
                Finalize(request);
                WriteResult(FinalizeResultPath, request.requestId, "passed", null, stopwatch.ElapsedMilliseconds);
                File.Delete(FinalizePendingPath);
            } else {
                CleanupRequest request = JsonUtility.FromJson<CleanupRequest>(ReadRequestText(CleanupPendingPath));
                activeRequestId = request.requestId;
                Cleanup(request);
                WriteResult(CleanupResultPath, request.requestId, "passed", null, stopwatch.ElapsedMilliseconds);
                File.Delete(CleanupPendingPath);
            }
        } catch (IOException exception) {
            lastError = exception.Message;
            activeStage = "waiting-for-request-file";
            return;
        } catch (Exception exception) {
            lastError = exception.ToString();
            WriteResult(resultPath, activeRequestId, "failed", exception.ToString(), stopwatch.ElapsedMilliseconds);
            Debug.LogException(exception);
            if (File.Exists(activePath)) {
                string failedPath = activePath + ".failed";
                if (File.Exists(failedPath)) File.Delete(failedPath);
                File.Move(activePath, failedPath);
            }
        } finally {
            running = false;
            if (activeStage != "waiting-for-request-file") activeStage = "idle";
            PublishStatus(true);
        }
    }

    static bool TryHandleControl() {
        if (running || !File.Exists(ControlPendingPath)) return false;
        string requestId = TryExtractRequestId(ControlPendingPath);
        try {
            ControlRequest request = JsonUtility.FromJson<ControlRequest>(ReadRequestText(ControlPendingPath));
            requestId = request.requestId;
            if (request.action != "stopPlay")
                throw new InvalidOperationException("Unsupported AIUI control action: " + request.action);
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                EditorApplication.isPlaying = false;
                return true;
            }
            WriteResult(ControlResultPath, requestId, "passed", null, 0);
            File.Delete(ControlPendingPath);
        } catch (IOException) {
            return true;
        } catch (Exception exception) {
            WriteResult(ControlResultPath, requestId, "failed", exception.ToString(), 0);
            MoveFailed(ControlPendingPath);
        }
        PublishStatus(true);
        return true;
    }

    static string ReadRequestText(string path) {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader reader = new StreamReader(stream)) return reader.ReadToEnd();
    }

    static string TryExtractRequestId(string path) {
        try {
            Match match = Regex.Match(ReadRequestText(path), "\\\"requestId\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        } catch { return string.Empty; }
    }

    static void MoveFailed(string path) {
        if (!File.Exists(path)) return;
        string failedPath = path + ".failed";
        if (File.Exists(failedPath)) File.Delete(failedPath);
        File.Move(path, failedPath);
    }

    static void PublishStatus(bool force) {
        double now = EditorApplication.timeSinceStartup;
        if (!force && now < nextStatusWrite) return;
        nextStatusWrite = now + 0.5d;
        string status = "{\n" +
            "  \"toolVersion\": \"" + ToolVersion + "\",\n" +
            "  \"processId\": " + System.Diagnostics.Process.GetCurrentProcess().Id + ",\n" +
            "  \"isPlaying\": " + Bool(EditorApplication.isPlayingOrWillChangePlaymode) + ",\n" +
            "  \"isCompiling\": " + Bool(EditorApplication.isCompiling) + ",\n" +
            "  \"isUpdating\": " + Bool(EditorApplication.isUpdating) + ",\n" +
            "  \"isRunning\": " + Bool(running) + ",\n" +
            "  \"stage\": \"" + Escape(activeStage) + "\",\n" +
            "  \"requestId\": \"" + Escape(activeRequestId) + "\",\n" +
            "  \"lastError\": \"" + Escape(lastError) + "\"\n" +
            "}";
        try { WriteTextAtomic(StatusPath, status); } catch (IOException) { }
    }

    static string Bool(bool value) { return value ? "true" : "false"; }
    static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n"); }

    static void WriteTextAtomic(string path, string content) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporaryPath, content, new System.Text.UTF8Encoding(false));
        try {
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
        } catch {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    static void Cleanup(CleanupRequest request) {
        if (request == null || request.prefabPaths == null || request.prefabPaths.Length == 0)
            throw new InvalidOperationException("AIUI cleanup request requires prefabPaths");
        if (!request.removeCanvasGroups)
            throw new InvalidOperationException("AIUI cleanup request has no approved cleanup operation");
        int removed = 0;
        foreach (string prefabPath in request.prefabPaths) {
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith("Assets/Prefab/"))
                throw new InvalidOperationException("Every cleanup prefab path must be under Assets/Prefab");
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                foreach (CanvasGroup canvasGroup in root.GetComponentsInChildren<CanvasGroup>(true)) {
                    UnityEngine.Object.DestroyImmediate(canvasGroup);
                    removed++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AIUI Cleanup] removed CanvasGroup components: " + removed);
    }

    static void Finalize(FinalizeRequest request) {
        if (request == null) throw new InvalidOperationException("AIUI finalize request is empty");
        string[] prefabPaths = request.prefabPaths != null && request.prefabPaths.Length > 0
            ? request.prefabPaths
            : new[] { request.prefabPath };
        foreach (string prefabPath in prefabPaths) {
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith("Assets/Prefab/"))
                throw new InvalidOperationException("Every prefab path must be under Assets/Prefab");
        }
        if (string.IsNullOrEmpty(request.atlasPath) || !request.atlasPath.StartsWith("Assets/Art/atlas/"))
            throw new InvalidOperationException("atlasPath must be under Assets/Art/atlas");
        if (string.IsNullOrEmpty(request.atlasSourcePath) || !request.atlasSourcePath.StartsWith("Assets/Art/atlasSource/"))
            throw new InvalidOperationException("atlasSourcePath must be under Assets/Art/atlasSource");
        if (request.spriteNames == null || request.spriteNames.Length == 0)
            throw new InvalidOperationException("spriteNames are required");

        ApplyAssetMoves(request.assetMoves);

        ApplySpriteBorders(request.spriteBorders);

        Dictionary<string, Sprite> looseSprites = new Dictionary<string, Sprite>();
        if (request.looseSpriteReplacements != null) {
            foreach (LooseSpriteReplacement replacement in request.looseSpriteReplacements) {
                if (string.IsNullOrEmpty(replacement.spriteName))
                    throw new InvalidOperationException("Loose Sprite replacement requires spriteName");
                if (string.IsNullOrEmpty(replacement.assetPath) ||
                    !replacement.assetPath.StartsWith("Assets/Art/unpack/"))
                    throw new InvalidOperationException("Loose Sprite must be under Assets/Art/unpack: " + replacement.assetPath);
                EnsureSpriteImporter(replacement.assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(replacement.assetPath);
                if (sprite == null) throw new InvalidOperationException("Loose Sprite not found: " + replacement.assetPath);
                looseSprites[replacement.spriteName] = sprite;
            }
        }

        HashSet<string> looseReplaced = new HashSet<string>();
        if (looseSprites.Count > 0) {
            foreach (string prefabPath in prefabPaths) {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try {
                    foreach (Image image in root.GetComponentsInChildren<Image>(true)) {
                        if (image.sprite == null || !looseSprites.TryGetValue(image.sprite.name, out Sprite looseSprite))
                            continue;
                        looseReplaced.Add(image.sprite.name);
                        image.sprite = looseSprite;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                } finally {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            foreach (string spriteName in looseSprites.Keys) {
                if (!looseReplaced.Contains(spriteName))
                    throw new InvalidOperationException("No Prefab uses loose Sprite replacement: " + spriteName);
            }
        }

        TexturePackerImporter.TexturePackerTool.PackAtlasSourceAssetPath(
            request.atlasSourcePath, trimTrans: !request.keepTransparentBounds);
        AssetDatabase.ImportAsset(request.atlasPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(request.atlasPath) == null)
            throw new InvalidOperationException("Atlas generation failed: " + request.atlasPath);
        RegisterAddressable(request.atlasPath, request.addressableGroupName);
        if (request.addressableAssetPaths != null) {
            foreach (string assetPath in request.addressableAssetPaths)
                RegisterAddressable(assetPath, request.addressableGroupName);
        }

        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(request.atlasPath)) {
            if (asset is Sprite sprite) sprites[sprite.name] = sprite;
        }
        foreach (string spriteName in request.spriteNames) {
            if (!sprites.ContainsKey(spriteName)) throw new InvalidOperationException("Atlas Sprite not found: " + spriteName);
        }

        HashSet<string> expected = new HashSet<string>(request.spriteNames);
        HashSet<string> replaced = new HashSet<string>();
        foreach (string prefabPath in prefabPaths) {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                foreach (Image image in root.GetComponentsInChildren<Image>(true)) {
                    if (image.sprite == null || !expected.Contains(image.sprite.name)) continue;
                    image.sprite = sprites[image.sprite.name];
                    replaced.Add(image.sprite.name);
                }
                NormalizeRectTransformGeometry(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        foreach (string spriteName in expected) {
            if (!replaced.Contains(spriteName)) throw new InvalidOperationException("No Prefab uses Sprite: " + spriteName);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AIUI Finalize] atlas references applied to " + prefabPaths.Length + " Prefab(s)");
    }

    static void ApplyAssetMoves(AssetMove[] moves) {
        if (moves == null || moves.Length == 0) return;
        foreach (AssetMove move in moves) {
            if (string.IsNullOrEmpty(move.sourcePath) || string.IsNullOrEmpty(move.targetPath) ||
                !move.sourcePath.StartsWith("Assets/Art/atlasSource/") ||
                !move.targetPath.StartsWith("Assets/Art/atlasSource/"))
                throw new InvalidOperationException("Atlas source moves must stay under Assets/Art/atlasSource");
            if (AssetDatabase.LoadMainAssetAtPath(move.sourcePath) == null)
                throw new InvalidOperationException("Atlas source asset not found: " + move.sourcePath);
            if (AssetDatabase.LoadMainAssetAtPath(move.targetPath) != null)
                throw new InvalidOperationException("Atlas source target already exists: " + move.targetPath);
            string targetDirectory = Path.GetDirectoryName(move.targetPath).Replace("\\", "/");
            EnsureAssetFolder(targetDirectory);
            string error = AssetDatabase.MoveAsset(move.sourcePath, move.targetPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Atlas source move failed: " + move.sourcePath + " -> " +
                    move.targetPath + "\n" + error);
        }
        AssetDatabase.SaveAssets();
    }

    static void EnsureAssetFolder(string assetFolder) {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string parent = Path.GetDirectoryName(assetFolder).Replace("\\", "/");
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
    }

    static void RegisterAddressable(string assetPath, string groupName) {
        if (string.IsNullOrEmpty(groupName)) return;
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings are missing");
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null) throw new InvalidOperationException("Addressables group not found: " + groupName);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Atlas GUID not found: " + assetPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = assetPath;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
    }

    static void NormalizeRectTransformGeometry(GameObject root) {
        foreach (RectTransform rectTransform in root.GetComponentsInChildren<RectTransform>(true)) {
            Vector2 anchoredPosition = rectTransform.anchoredPosition;
            Vector2 sizeDelta = rectTransform.sizeDelta;
            rectTransform.anchoredPosition = new Vector2(
                Mathf.Round(anchoredPosition.x), Mathf.Round(anchoredPosition.y));
            rectTransform.sizeDelta = new Vector2(
                Mathf.Round(sizeDelta.x), Mathf.Round(sizeDelta.y));
            EditorUtility.SetDirty(rectTransform);
        }
        EditorUtility.SetDirty(root);
    }

    static void Validate(Request request) {
        if (request == null) throw new InvalidOperationException("AIUI preview request is empty");
        if (string.IsNullOrEmpty(request.prefabPath) || !request.prefabPath.StartsWith("Assets/Prefab/"))
            throw new InvalidOperationException("prefabPath must be under Assets/Prefab");
        if (string.IsNullOrEmpty(request.rootName)) throw new InvalidOperationException("rootName is required");
        if (request.nodes == null) throw new InvalidOperationException("nodes are required");
        HashSet<string> names = new HashSet<string> { request.rootName };
        foreach (Node node in request.nodes) {
            if (string.IsNullOrEmpty(node.name)) throw new InvalidOperationException("Every node requires a name");
            if (!names.Add(node.name)) throw new InvalidOperationException("Duplicate node name: " + node.name);
            if (!string.IsNullOrEmpty(node.parent) && !names.Contains(node.parent))
                throw new InvalidOperationException("Parent must appear before child: " + node.parent + " -> " + node.name);
            if (node.kind == "scrollList") {
                if (string.IsNullOrEmpty(node.contentName))
                    throw new InvalidOperationException("scrollList contentName is required: " + node.name);
                if (!names.Add(node.contentName))
                    throw new InvalidOperationException("Duplicate scrollList contentName: " + node.contentName);
            }
            if (node.kind == "image" || node.kind == "button") EnsureSpriteImporter(node.assetPath);
        }
        if (request.initializeBinding) {
            if (request.binding == null) throw new InvalidOperationException("binding is required when initializeBinding is true");
            if (string.IsNullOrEmpty(request.binding.csharpAssetPath) ||
                !request.binding.csharpAssetPath.StartsWith("Assets/Scripts/"))
                throw new InvalidOperationException("binding.csharpAssetPath must be under Assets/Scripts");
            if (request.binding.members == null) throw new InvalidOperationException("binding.members are required");
            foreach (Member member in request.binding.members) {
                if (!names.Contains(member.node)) throw new InvalidOperationException("Binding node not found: " + member.node);
                if (string.IsNullOrEmpty(member.type)) throw new InvalidOperationException("Binding type is required: " + member.node);
            }
        }
    }

    static void EnsureSpriteImporter(string path) {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null && File.Exists(path)) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
        bool changed = importer.textureType != TextureImporterType.Sprite ||
                       importer.spriteImportMode != SpriteImportMode.Single ||
                       importer.mipmapEnabled || !importer.alphaIsTransparency;
        if (!changed) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static void ApplySpriteBorders(SpriteBorderSetting[] settings) {
        if (settings == null) return;
        foreach (SpriteBorderSetting setting in settings) {
            if (string.IsNullOrEmpty(setting.assetPath) ||
                !setting.assetPath.StartsWith("Assets/Art/atlasSource/"))
                throw new InvalidOperationException("Sprite Border asset must be under Assets/Art/atlasSource: " + setting.assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(setting.assetPath) as TextureImporter;
            if (importer == null) {
                AssetDatabase.ImportAsset(setting.assetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(setting.assetPath) as TextureImporter;
            }
            if (importer == null)
                throw new InvalidOperationException("Sprite Border texture not found: " + setting.assetPath);

            Vector4 expected = new Vector4(setting.left, setting.bottom, setting.right, setting.top);
            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (textureSettings.spriteBorder == expected) continue;
            textureSettings.spriteBorder = expected;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(setting.assetPath);
            if (sprite == null || sprite.border != expected)
                throw new InvalidOperationException("Sprite Border import verification failed: " + setting.assetPath);
        }
    }

    static void EnsureSpriteBorder(string assetPath, float left, float bottom, float right, float top) {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Sprite Border texture not found: " + assetPath);
        Vector4 expected = new Vector4(left, bottom, right, top);
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteBorder == expected) return;
        settings.spriteBorder = expected;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static void GeneratePreview(Request request) {
        GameObject root = new GameObject(request.rootName, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(request.width, request.height);
        rootRect.anchoredPosition = Vector2.zero;
        Dictionary<string, RectTransform> parents = new Dictionary<string, RectTransform> { [request.rootName] = rootRect };

        foreach (Node node in request.nodes) {
            RectTransform parent = string.IsNullOrEmpty(node.parent) ? rootRect : parents[node.parent];
            RectTransform created;
            switch (node.kind) {
                case "image": created = CreateImage(parent, node); break;
                case "text": created = CreateText(parent, node, request.fontAssetPath); break;
                case "button": created = CreateButton(parent, node, request.fontAssetPath); break;
                case "container": created = CreateContainer(parent, node); break;
                case "scrollList": created = CreateScrollList(parent, node, parents); break;
                default: throw new InvalidOperationException("Unsupported node kind: " + node.kind);
            }
            parents[node.name] = created;
            if (node.inactive) created.gameObject.SetActive(false);
        }

        if (request.childBindings != null) {
            foreach (ChildBinding childBinding in request.childBindings)
                ConfigureChildBinding(parents, childBinding);
        }
        ConfigureScrollLists(parents, request.nodes);
        if (request.initializeBinding) ConfigureBinding(root, parents, request.binding);
        NormalizeRectTransformGeometry(root);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string prefabDirectory = Path.GetDirectoryName(request.prefabPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.Combine(projectRoot, prefabDirectory));
        PrefabUtility.SaveAsPrefabAsset(root, request.prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[AIUI Preview] generated: " + request.prefabPath);
    }

    static void ConfigureBinding(GameObject root, Dictionary<string, RectTransform> nodes, Binding binding) {
        EnsureViewClass(root.name, binding);
        UIBinder binder = root.AddComponent<UIBinder>();
        binder.csharpAssetPath = binding.csharpAssetPath;
        binder.csharpAsset = binding.csharpAssetPath;
        binder.uiList = new List<UIBindComponentData>();
        int id = 1;
        foreach (Member member in binding.members) {
            GameObject target = nodes[member.node].gameObject;
            Component targetComponent = target.GetComponent(member.type);
            if (targetComponent == null && member.isCustomClass) {
                Type memberType = FindComponentType(member.type);
                if (memberType != null) targetComponent = target.GetComponent(memberType);
            }
            if (targetComponent == null)
                throw new InvalidOperationException("Component " + member.type + " not found on " + member.node);
            binder.uiList.Add(new UIBindComponentData {
                id = id++, uiName = member.node, uiTypeName = member.type,
                isCustomClass = member.isCustomClass,
                customClassName = member.isCustomClass ? member.type : string.Empty,
                go = target
            });
        }

        Editor inspector = Editor.CreateEditor(binder);
        try {
            MethodInfo generate = inspector.GetType().GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic);
            if (generate == null) throw new MissingMethodException(inspector.GetType().FullName, "GenerateUiBind");
            generate.Invoke(inspector, new object[] { binding.csharpAssetPath });
        } finally {
            UnityEngine.Object.DestroyImmediate(inspector);
        }
    }

    static void ConfigureChildBinding(Dictionary<string, RectTransform> nodes, ChildBinding binding) {
        if (binding == null || string.IsNullOrEmpty(binding.node) || !nodes.TryGetValue(binding.node, out RectTransform target))
            throw new InvalidOperationException("AIUI child binding node is missing: " + binding?.node);

        string className = Path.GetFileNameWithoutExtension(binding.csharpAssetPath);
        Type classType = FindComponentType(className);
        if (classType == null) throw new InvalidOperationException("AIUI child binding class is unavailable: " + className);
        Component component = target.GetComponent(classType) ?? target.gameObject.AddComponent(classType);

        UIBinder binder = target.GetComponent<UIBinder>() ?? target.gameObject.AddComponent<UIBinder>();
        binder.csharpAssetPath = binding.csharpAssetPath;
        binder.csharpAsset = binding.csharpAssetPath;
        binder.uiList = new List<UIBindComponentData>();
        int id = 1;
        if (binding.members != null) {
            foreach (Member member in binding.members) {
                GameObject memberTarget = nodes[member.node].gameObject;
                binder.uiList.Add(new UIBindComponentData {
                    id = id++, uiName = member.node, uiTypeName = member.type,
                    isCustomClass = member.isCustomClass,
                    customClassName = member.isCustomClass ? member.type : string.Empty,
                    go = memberTarget
                });
            }
        }

        if (binding.spriteFields != null) {
            SerializedObject serialized = new SerializedObject(component);
            foreach (SpriteField spriteField in binding.spriteFields) {
                SerializedProperty property = serialized.FindProperty(spriteField.name);
                if (property == null) throw new InvalidOperationException(className + " has no Sprite field: " + spriteField.name);
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(spriteField.assetPath);
                if (property.objectReferenceValue == null)
                    throw new InvalidOperationException("Sprite asset not found: " + spriteField.assetPath);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        Editor inspector = Editor.CreateEditor(binder);
        try {
            MethodInfo generate = inspector.GetType().GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic);
            if (generate == null) throw new MissingMethodException(inspector.GetType().FullName, "GenerateUiBind");
            generate.Invoke(inspector, new object[] { binding.csharpAssetPath });
        } finally {
            UnityEngine.Object.DestroyImmediate(inspector);
        }
    }

    static Type FindComponentType(string typeName) {
        Type type = Type.GetType(typeName);
        if (type != null) return type;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            type = assembly.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }

    static void EnsureViewClass(string className, Binding binding) {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string relative = binding.csharpAssetPath.Replace('/', Path.DirectorySeparatorChar);
        string absolute = Path.Combine(projectRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        if (File.Exists(absolute)) return;
        string summary = string.IsNullOrEmpty(binding.summary) ? className : binding.summary;
        string source =
            "/// <summary>\n/// " + summary + "\n/// </summary>\n" +
            "public class " + className + " : BasePanel {\n" +
            "    public " + className + "() {\n" +
            "        layer = PanelLayer." + binding.panelLayer + ";\n" +
            "    }\n\n" +
            "    public override void OnOpen() {\n    }\n\n" +
            "    public override void OnClose() {\n    }\n" +
            "}\n";
        File.WriteAllText(absolute, source, new System.Text.UTF8Encoding(false));
    }

    static RectTransform CreateContainer(RectTransform parent, Node node) {
        GameObject go = new GameObject(node.name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopLeft(rect, parent, node.x, node.y, node.width, node.height);
        return rect;
    }

    static RectTransform CreateScrollList(RectTransform parent, Node node, Dictionary<string, RectTransform> parents) {
        if (string.IsNullOrEmpty(node.contentName))
            throw new InvalidOperationException("AIUI scrollList requires contentName: " + node.name);
        GameObject go = new GameObject(node.name, typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(ScrollList));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopLeft(rect, parent, node.x, node.y, node.width, node.height);

        GameObject contentObject = new GameObject(node.contentName, typeof(RectTransform));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(rect, false);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(node.width, node.height);
        parents[node.contentName] = content;

        ScrollRect scrollRect = go.GetComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = rect;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 30;
        return rect;
    }

    static void ConfigureScrollLists(Dictionary<string, RectTransform> nodes, Node[] nodeConfigs) {
        foreach (Node node in nodeConfigs) {
            if (node.kind != "scrollList") continue;
            if (!nodes.TryGetValue(node.name, out RectTransform listRect))
                throw new InvalidOperationException("AIUI ScrollList node is missing: " + node.name);
            if (string.IsNullOrEmpty(node.itemTemplate) || !nodes.TryGetValue(node.itemTemplate, out RectTransform templateRect))
                throw new InvalidOperationException("AIUI ScrollList itemTemplate is missing: " + node.itemTemplate);

            ScrollList list = listRect.GetComponent<ScrollList>();
            list.horiLimitNum = node.horiLimitNum;
            list.vertLimitNum = node.vertLimitNum;
            list.horiGap = node.horiGap;
            list.vertGap = node.vertGap;
            list.itemTemplate = templateRect.gameObject;
            templateRect.gameObject.SetActive(false);

            RectTransform content = listRect.GetComponent<ScrollRect>().content;
            for (int index = 0; index < node.previewCloneCount; index++) {
                GameObject clone = UnityEngine.Object.Instantiate(templateRect.gameObject, content);
                clone.name = templateRect.name + "(Clone)";
                clone.SetActive(true);
                RectTransform cloneRect = clone.GetComponent<RectTransform>();
                cloneRect.anchorMin = cloneRect.anchorMax = cloneRect.pivot = new Vector2(0, 1);
                cloneRect.anchoredPosition = new Vector2(index * (templateRect.sizeDelta.x + node.horiGap), 0);
            }
        }
    }

    static RectTransform CreateImage(RectTransform parent, Node node) {
        GameObject go = new GameObject(node.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopLeft(rect, parent, node.x, node.y, node.width, node.height);
        Image image = go.GetComponent<Image>();
        if (string.Equals(node.imageType, "Sliced", StringComparison.OrdinalIgnoreCase))
            EnsureSpriteBorder(node.assetPath, node.borderLeft, node.borderBottom, node.borderRight, node.borderTop);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(node.assetPath);
        image.type = string.Equals(node.imageType, "Sliced", StringComparison.OrdinalIgnoreCase)
            ? Image.Type.Sliced
            : Image.Type.Simple;
        image.raycastTarget = false;
        ApplyRotation(rect, node);
        return rect;
    }

    static RectTransform CreateText(RectTransform parent, Node node, string fontPath) {
        GameObject go = new GameObject(node.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopLeft(rect, parent, node.x, node.y, node.width, node.height);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        text.text = node.text ?? string.Empty;
        text.fontSize = node.fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = ParseColor(node.color);
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        if (ParseColor(node.shadowColor).a > 0) {
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = ParseColor(node.shadowColor);
            shadow.effectDistance = new Vector2(0, -node.shadowOffsetY);
        }
        return rect;
    }

    static RectTransform CreateButton(RectTransform parent, Node node, string fontPath) {
        GameObject go = new GameObject(node.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(GameButton));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetTopLeft(rect, parent, node.x, node.y, node.width, node.height);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition += new Vector2(node.width * 0.5f, -node.height * 0.5f);
        Image image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(node.assetPath);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        GameButton gameButton = go.GetComponent<GameButton>();
        gameButton.text = node.text;
        if (!node.omitLabel) {
            Node labelNode = new Node {
                name = "txtLabel", kind = "text", text = node.text,
                x = node.labelX, y = node.labelY, width = node.labelWidth, height = node.labelHeight,
                fontSize = node.fontSize, color = node.labelColor,
                shadowColor = node.shadowColor, shadowOffsetY = node.shadowOffsetY
            };
            RectTransform labelRect = CreateText(rect, labelNode, fontPath);
            SerializedObject serialized = new SerializedObject(gameButton);
            serialized.FindProperty("label").objectReferenceValue = labelRect.GetComponent<TextMeshProUGUI>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return rect;
    }

    static void SetTopLeft(RectTransform rect, RectTransform parent, float x, float y, float width, float height) {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    static void ApplyRotation(RectTransform rect, Node node) {
        if (Mathf.Approximately(node.rotation, 0)) return;
        if (node.rotateAroundCenter) {
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition += new Vector2(node.width * 0.5f, -node.height * 0.5f);
        }
        rect.localEulerAngles = new Vector3(0, 0, -node.rotation);
    }

    static Color ParseColor(string value) {
        return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
    }

    static void WriteResult(string path, string requestId, string status, string error, long durationMs) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string safeError = string.IsNullOrEmpty(error) ? string.Empty : error.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        File.WriteAllText(path,
            "{\n  \"requestId\": \"" + (requestId ?? string.Empty) + "\",\n  \"status\": \"" + status +
            "\",\n  \"durationMs\": " + durationMs + ",\n  \"error\": \"" + safeError + "\"\n}");
    }
}
#endif
