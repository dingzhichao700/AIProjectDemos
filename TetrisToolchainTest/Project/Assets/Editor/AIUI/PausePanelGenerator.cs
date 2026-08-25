using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PausePanelGenerator
{
    private const string PrefabDirectory = "Assets/Prefab/default/tetris";
    private const string PrefabPath = PrefabDirectory + "/PausePanel.prefab";
    private const string AssetDirectory = "Assets/Art/atlasSource/default/tetris";
    private const string ReportPath = "Library/AIUI/PausePanel.export.json";
    private const string Address = "default/tetris/PausePanel";
    private const string SessionKey = "AIUI.PausePanel.Stage6.BitmapV3.Achievements";

    [MenuItem("Tools/AIUI/Generate Tetris2/PausePanel (Stage 6)")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(AssetDirectory)) throw new InvalidOperationException("Missing bitmap asset directory: " + AssetDirectory);
        var sprites = LoadSprites();
        foreach (var name in new[] { "pause_scrim", "pause_modal_frame", "pause_button_normal", "pause_button_pressed", "pause_button_disabled" })
            if (!sprites.ContainsKey(name)) throw new InvalidOperationException("Missing bitmap Sprite: " + name);

        EnsureFolder(PrefabDirectory);
        var root = CreateObject("PausePanel", null, Vector2.zero, new Vector2(1920, 1080));
        AddImage(root, "imgScrim", new Vector2(0, 0), new Vector2(1920, 1080), sprites["pause_scrim"]);
        var modal = CreateObject("imgModal", root.transform, new Vector2(580, 170), new Vector2(760, 740));
        AddImage(modal, "imgModalSurface", Vector2.zero, new Vector2(760, 740), sprites["pause_modal_frame"]);
        AddText(modal.transform, "txtTitle", "SYSTEM PAUSED", new Vector2(110, 64), new Vector2(540, 70), 52, TextAlignmentOptions.Center);
        AddText(modal.transform, "txtSubtitle", "GRID PROCESS SUSPENDED", new Vector2(155, 140), new Vector2(450, 32), 22, TextAlignmentOptions.Center);
        AddButton(modal.transform, "btnResume", "RESUME", new Vector2(120, 210), sprites["pause_button_pressed"]);
        AddButton(modal.transform, "btnRestart", "RESTART", new Vector2(120, 320), sprites["pause_button_normal"]);
        AddButton(modal.transform, "btnQuitToTitle", "QUIT TO TITLE", new Vector2(120, 430), sprites["pause_button_normal"]);
        AddButton(modal.transform, "btnAchievements", "ACHIEVEMENTS", new Vector2(120, 540), sprites["pause_button_normal"]);
        AddText(modal.transform, "txtFooterHint", "ESC  RESUME", new Vector2(205, 650), new Vector2(350, 32), 22, TextAlignmentOptions.Center);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        RegisterAddressable();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WriteReport();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] PausePanel Stage 6 generated: " + PrefabPath + " (address: " + Address + ")");
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        var result = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(AssetDirectory, "*.png"))
        {
            var assetPath = path.Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) result[Path.GetFileNameWithoutExtension(assetPath)] = sprite;
        }
        return result;
    }

    private static GameObject CreateObject(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(position.x, -position.y); rect.sizeDelta = size;
        return go;
    }

    private static Image AddImage(GameObject parent, string name, Vector2 position, Vector2 size, Sprite sprite)
    {
        var go = CreateObject(name, parent.transform, position, size);
        var image = go.AddComponent<Image>(); image.sprite = sprite; image.color = Color.white; image.raycastTarget = false; image.type = Image.Type.Simple;
        return image;
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        var go = CreateObject(name, parent, position, size); var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value; text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.color = Hex("#E1F7FF");
        text.alignment = alignment; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false; return text;
    }

    private static void AddButton(Transform parent, string name, string label, Vector2 position, Sprite sprite)
    {
        var button = CreateObject(name, parent, position, new Vector2(520, 90));
        button.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        button.GetComponent<RectTransform>().anchoredPosition += new Vector2(260, -45);
        var image = button.AddComponent<Image>(); image.sprite = sprite; image.color = Color.white; image.raycastTarget = true;
        button.AddComponent<Button>(); var gameButton = button.AddComponent<GameButton>();
        var labelObject = CreateObject("txtLabel", button.transform, new Vector2(70, 24), new Vector2(380, 42));
        var labelText = labelObject.AddComponent<TextMeshProUGUI>(); labelText.text = label; labelText.font = TMP_Settings.defaultFontAsset; labelText.fontSize = 32; labelText.color = Hex("#E1F7FF"); labelText.alignment = TextAlignmentOptions.Center; labelText.enableWordWrapping = false; labelText.raycastTarget = false;
        var serialized = new SerializedObject(gameButton); serialized.FindProperty("label").objectReferenceValue = labelText; serialized.FindProperty("text").stringValue = label; serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RegisterAddressable()
    {
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings; if (settings == null) throw new InvalidOperationException("Addressable settings unavailable.");
        var guid = AssetDatabase.AssetPathToGUID(PrefabPath); var group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup; var entry = settings.CreateOrMoveEntry(guid, group); entry.address = Address; settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    private static void WriteReport()
    {
        var absolute = Path.GetFullPath(ReportPath); Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, "{\n  \"stage\": 6,\n  \"authoritative\": false,\n  \"source\": \"approved PausePanel bitmap importer artifacts\",\n  \"prefab\": \"" + PrefabPath + "\",\n  \"address\": \"" + Address + "\",\n  \"bitmapSurfaces\": true,\n  \"bindingGenerated\": false,\n  \"addressablesBuilt\": false\n}\n");
    }

    private static void EnsureFolder(string path) { var parts = path.Split('/'); var current = parts[0]; for (var i = 1; i < parts.Length; i++) { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; } }
    private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var color); return color; }
}
