#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

public static class TetrisGamePanelGenerator {
    const string Module = "tetris";
    const string SourceDir = "Assets/Art/atlasSource/default/tetris";
    const string AtlasPath = "Assets/Art/atlas/default/tetris.png";
    const string CellSourceDir = "Assets/Art/atlasSource/default/tetriscell";
    const string CellAtlasPath = "Assets/Art/atlas/default/tetriscell.png";
    const string PrefabDir = "Assets/Prefab/default/tetris";
    const string PrefabPath = PrefabDir + "/TetrisGamePanel.prefab";
    const string ScriptPath = "Assets/Scripts/csharp/com/game/tetris/view/TetrisGamePanel.cs";
    const string Address = "default/tetris/TetrisGamePanel";

    static readonly Color Bg = Hex("#040A17");
    static readonly Color Panel = Hex("#07101D");
    static readonly Color Cell = Hex("#07101D");
    static readonly Color CellStroke = Hex("#27485F");
    static readonly Color Cyan = Hex("#22D7ED");
    static readonly Color Violet = Hex("#B94DE0");
    static readonly Color Amber = Hex("#F1A13D");
    static readonly Color Lime = Hex("#9FD94D");
    static readonly Color White = Hex("#E1F7FF");
    static readonly Color Muted = Hex("#62B8D1");

    [MenuItem("Tools/AIUI/Generate TetrisGamePanel Stage 7")]
    public static void Generate() {
        EnsureFolder("Assets/Editor/AIUI");
        EnsureFolder("Assets/Art/atlas/default");
        EnsureFolder(PrefabDir);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) {
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        ConfigureSourceSprites();
        TexturePackerImporter.TexturePackerTool.PackAtlasSourceAssetPath(SourceDir, trimTrans: false);
        TexturePackerImporter.TexturePackerTool.PackAtlasSourceAssetPath(CellSourceDir, trimTrans: false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Dictionary<string, Sprite> sprites = LoadAtlasSprites();
        ValidateSprites(sprites);

        GameObject root = new GameObject("TetrisGamePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(1920f, 1080f);
        rootRect.anchoredPosition = Vector2.zero;
        Image rootImage = root.GetComponent<Image>();
        rootImage.color = Bg;
        rootImage.raycastTarget = false;

        CreateText(root.transform, "txtGameTitle", "BLOCK//SHIFT", 78, 28, 650, 72, 58, White, TextAlignmentOptions.MidlineLeft);
        CreateText(root.transform, "txtSubtitle", "ARCADE GRID PROTOCOL", 83, 91, 420, 28, 20, Cyan, TextAlignmentOptions.MidlineLeft);

        GameObject boardFrame = CreateImage(root.transform, "boardFrame", 150, 125, 500, 900, sprites["frame_board"], Color.white, Image.Type.Sliced);
        GameObject gridBoard = CreateContainer(boardFrame.transform, "gridBoard", 50, 50, 400, 800);
        CreateBoardCells(gridBoard.transform, sprites);

        GameObject sideRail = CreateImage(root.transform, "sideRail", 760, 125, 1010, 900, sprites["panel_telemetry"], Color.white, Image.Type.Sliced);
        CreateText(sideRail.transform, "txtNextLabel", "NEXT", 65, 35, 250, 48, 34, White, TextAlignmentOptions.MidlineLeft);
        GameObject gridNext = CreateContainer(sideRail.transform, "gridNext", 65, 100, 240, 240);
        CreateNextCells(gridNext.transform, sprites);

        CreateDataCard(sideRail.transform, "scoreCard", 400, 60, 500, "SCORE", "txtScore", "001250", sprites);
        CreateDataCard(sideRail.transform, "highScoreCard", 400, 205, 500, "HIGH SCORE", "txtHighScore", "008900", sprites);
        CreateDataCard(sideRail.transform, "levelCard", 65, 385, 390, "LEVEL", "txtLevel", "03", sprites);
        CreateDataCard(sideRail.transform, "linesCard", 510, 385, 390, "LINES", "txtLines", "12", sprites);

        GameObject controls = CreateImage(sideRail.transform, "controlsCard", 65, 550, 835, 205, sprites["panel_data"], Color.white, Image.Type.Sliced);
        CreateText(controls.transform, "txtControlsTitle", "CONTROLS", 40, 18, 300, 35, 24, Cyan, TextAlignmentOptions.MidlineLeft);
        CreateText(controls.transform, "txtControlsRow1", "A / D  MOVE      W  ROTATE      S  SOFT DROP", 40, 65, 750, 38, 23, White, TextAlignmentOptions.MidlineLeft);
        CreateText(controls.transform, "txtControlsRow2", "SPACE  HARD DROP        ESC  PAUSE", 40, 112, 750, 38, 23, White, TextAlignmentOptions.MidlineLeft);

        GameObject pause = CreateImage(sideRail.transform, "btnPause", 550, 775, 350, 90, sprites["button_normal"], Color.white, Image.Type.Simple);
        pause.AddComponent<Button>();
        GameButton gameButton = pause.AddComponent<GameButton>();
        TextMeshProUGUI pauseLabel = CreateText(pause.transform, "txtLabel", "PAUSE", 45, 22, 260, 46, 34, White, TextAlignmentOptions.Center);
        SerializedObject buttonSerialized = new SerializedObject(gameButton);
        buttonSerialized.FindProperty("label").objectReferenceValue = pauseLabel;
        buttonSerialized.FindProperty("text").stringValue = "PAUSE";
        buttonSerialized.ApplyModifiedPropertiesWithoutUndo();

        TextMeshProUGUI countdown = CreateText(root.transform, "txtCountdown", "3", 735, 420, 450, 240, 180, White, TextAlignmentOptions.Center);
        countdown.gameObject.SetActive(false);

        UIBinder binder = root.AddComponent<UIBinder>();
        binder.csharpAssetPath = ScriptPath;
        binder.csharpAsset = string.Empty;
        binder.uiList = new List<UIBindComponentData>();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        RegisterAddressable();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WriteDiagnosticReport(sprites.Count);
        Debug.Log("[AIUI] TetrisGamePanel Stage 7 generated: " + PrefabPath);
    }

    public static void ValidateStage7() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Prefab missing: " + PrefabPath);
        if (prefab.name != "TetrisGamePanel") throw new InvalidOperationException("Unexpected root name: " + prefab.name);
        UIBinder[] binders = prefab.GetComponents<UIBinder>();
        if (binders.Length != 1) throw new InvalidOperationException("Expected one root UIBinder, found " + binders.Length);
        UIBinder binder = binders[0];
        if (binder.csharpAssetPath != ScriptPath) throw new InvalidOperationException("Binder script path mismatch: " + binder.csharpAssetPath);
        if (binder.uiList == null || binder.uiList.Count != 0) throw new InvalidOperationException("Stage 7 uiList must be empty");
        if (AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath) == null) throw new InvalidOperationException("View script asset missing");

        Transform board = prefab.transform.Find("boardFrame/gridBoard");
        Transform next = prefab.transform.Find("sideRail/gridNext");
        if (board == null || board.childCount != 200) throw new InvalidOperationException("Board must contain 200 cells");
        if (next == null || next.childCount != 16) throw new InvalidOperationException("Next grid must contain 16 cells");
        if (prefab.transform.Find("sideRail/btnPause")?.GetComponent<GameButton>() == null) throw new InvalidOperationException("btnPause GameButton missing");
        if (prefab.transform.Find("sideRail/scoreCard/txtScore")?.GetComponent<TextMeshProUGUI>() == null) throw new InvalidOperationException("txtScore missing");
        if (prefab.transform.Find("txtCountdown")?.GetComponent<TextMeshProUGUI>() == null) throw new InvalidOperationException("txtCountdown missing");

        foreach (Image image in prefab.GetComponentsInChildren<Image>(true)) {
            if (image.name == "TetrisGamePanel") continue;
            if (image.name.StartsWith("cell_") || image.name.StartsWith("nextCell_")) continue;
            if (image.sprite == null) throw new InvalidOperationException("Missing Sprite on visual Image: " + AnimationUtility.CalculateTransformPath(image.transform, prefab.transform));
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetEntry entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(PrefabPath));
        if (entry == null || entry.address != Address) throw new InvalidOperationException("Addressables entry mismatch");
        if (entry.parentGroup == null || entry.parentGroup.Name != "Default Local Group") throw new InvalidOperationException("Addressables group mismatch");
        Debug.Log("[AIUI] TetrisGamePanel Stage 7 validation passed: 200 board cells, 16 next cells, empty UIBinder, address=" + Address);
    }

    static void ConfigureSourceSprites() {
        List<string> paths = new List<string>();
        paths.AddRange(Directory.GetFiles(SourceDir, "*.png", SearchOption.TopDirectoryOnly));
        paths.AddRange(Directory.GetFiles(CellSourceDir, "*.png", SearchOption.TopDirectoryOnly));
        foreach (string rawPath in paths) {
            string path = rawPath.Replace("\\", "/");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == "frame_board") importer.spriteBorder = new Vector4(160, 160, 160, 160);
            else if (name == "panel_telemetry") importer.spriteBorder = new Vector4(80, 150, 80, 150);
            else if (name == "panel_data") importer.spriteBorder = new Vector4(150, 100, 150, 100);
            else importer.spriteBorder = Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    static Dictionary<string, Sprite> LoadAtlasSprites() {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AtlasPath)) {
            if (asset is Sprite sprite) result[sprite.name] = sprite;
        }
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(CellAtlasPath)) {
            if (asset is Sprite sprite) result[sprite.name] = sprite;
        }
        return result;
    }

    static void ValidateSprites(Dictionary<string, Sprite> sprites) {
        foreach (string name in new[] { "frame_board", "panel_telemetry", "panel_data", "block_cyan", "block_violet", "cell_empty_36", "cell_empty_54", "button_normal", "button_pressed", "button_disabled" }) {
            if (!sprites.ContainsKey(name)) throw new InvalidOperationException("Missing atlas sprite: " + name);
        }
    }

    static void CreateBoardCells(Transform parent, Dictionary<string, Sprite> sprites) {
        for (int row = 0; row < 20; row++) {
            for (int col = 0; col < 10; col++) {
                GameObject cell = CreateImage(parent, $"cell_{row:00}_{col:00}", col * 40 + 2, row * 40 + 2, 36, 36, sprites["cell_empty_36"], Color.white, Image.Type.Simple);
                cell.GetComponent<Image>().raycastTarget = false;
            }
        }
    }

    static void CreateNextCells(Transform parent, Dictionary<string, Sprite> sprites) {
        for (int row = 0; row < 4; row++) {
            for (int col = 0; col < 4; col++) {
                CreateImage(parent, $"nextCell_{row:00}_{col:00}", col * 60 + 3, row * 60 + 3, 54, 54, sprites["cell_empty_54"], Color.white, Image.Type.Simple);
            }
        }
    }

    static void CreateDataCard(Transform parent, string name, float x, float y, float width, string label, string valueName, string value, Dictionary<string, Sprite> sprites) {
        GameObject card = CreateImage(parent, name, x, y, width, 130, sprites["panel_data"], Color.white, Image.Type.Sliced);
        CreateText(card.transform, "txtLabel", label, 28, 16, width - 56, 30, 21, Muted, TextAlignmentOptions.MidlineLeft);
        CreateText(card.transform, valueName, value, 28, 48, width - 56, 56, 38, White, TextAlignmentOptions.MidlineLeft);
    }

    static GameObject CreateContainer(Transform parent, string name, float x, float y, float width, float height) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        return go;
    }

    static GameObject CreateImage(Transform parent, string name, float x, float y, float width, float height, Sprite sprite, Color color, Image.Type type) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite == null ? Image.Type.Simple : type;
        image.raycastTarget = false;
        return go;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string value, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        SetTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static void SetTopLeft(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    static void RegisterAddressable() {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("AddressableAssetSettings missing");
        AddressableAssetGroup group = settings.FindGroup("Default Local Group");
        if (group == null) throw new InvalidOperationException("Default Local Group missing");
        string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = Address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    static void WriteDiagnosticReport(int spriteCount) {
        string dir = "Library/AIUI";
        Directory.CreateDirectory(dir);
        string json = "{\n" +
            "  \"artifactRole\": \"diagnostic-execution-report\",\n" +
            "  \"authoritative\": false,\n" +
            "  \"screen\": \"TetrisGamePanel\",\n" +
            "  \"stage\": 7,\n" +
            "  \"boardCellCount\": 200,\n" +
            "  \"nextCellCount\": 16,\n" +
            "  \"atlasSpriteCount\": " + spriteCount + ",\n" +
            "  \"uiListCount\": 0,\n" +
            "  \"address\": \"" + Address + "\"\n" +
            "}\n";
        File.WriteAllText(Path.Combine(dir, "TetrisGamePanel.export.json"), json);
    }

    static void EnsureFolder(string path) {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++) {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static Color Hex(string value) {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}
#endif
