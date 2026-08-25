#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TetrisGamePanelCellBitmapUpdater {
    const string SourceDir = "Assets/Art/atlasSource/default/tetris";
    const string AtlasPath = "Assets/Art/atlas/default/tetris.png";
    const string PrefabPath = "Assets/Prefab/default/tetris/TetrisGamePanel.prefab";
    const string RequestPath = "Library/AIUI/TetrisGamePanelCellBitmap.request";

    [InitializeOnLoadMethod]
    static void ApplyRequestedUpdate() {
        if (!File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        EditorApplication.delayCall += ApplyAndValidate;
    }

    public static void ApplyAndValidate() {
        ConfigureCellSprites();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Sprite boardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SourceDir + "/cell_empty_36.png");
        Sprite nextSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SourceDir + "/cell_empty_54.png");
        if (boardSprite == null) throw new InvalidOperationException("Missing source sprite: cell_empty_36");
        if (nextSprite == null) throw new InvalidOperationException("Missing source sprite: cell_empty_54");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            int boardUpdated = 0;
            int nextUpdated = 0;
            foreach (Image image in root.GetComponentsInChildren<Image>(true)) {
                if (image.name.StartsWith("cell_", StringComparison.Ordinal) && image.sprite == null) {
                    image.sprite = boardSprite;
                    image.color = Color.white;
                    image.type = Image.Type.Simple;
                    boardUpdated++;
                } else if (image.name.StartsWith("nextCell_", StringComparison.Ordinal) && image.sprite == null) {
                    image.sprite = nextSprite;
                    image.color = Color.white;
                    image.type = Image.Type.Simple;
                    nextUpdated++;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[AIUI] Applied bitmap cells: board={boardUpdated}, next={nextUpdated}");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Validate();
    }

    static void ConfigureCellSprites() {
        foreach (string path in new[] {
            SourceDir + "/cell_empty_36.png",
            SourceDir + "/cell_empty_54.png"
        }) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture importer missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }
    }

    static Dictionary<string, Sprite> LoadAtlasSprites() {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AtlasPath)) {
            if (asset is Sprite sprite) result[sprite.name] = sprite;
        }
        return result;
    }

    static void Validate() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Prefab missing: " + PrefabPath);

        int boardCount = 0;
        int nextCount = 0;
        foreach (Image image in prefab.GetComponentsInChildren<Image>(true)) {
            if (image.name.StartsWith("cell_", StringComparison.Ordinal)) {
                boardCount++;
                if (image.sprite == null) throw new InvalidOperationException("Board cell Sprite missing: " + image.name);
            } else if (image.name.StartsWith("nextCell_", StringComparison.Ordinal)) {
                nextCount++;
                if (image.sprite == null) throw new InvalidOperationException("Next cell Sprite missing: " + image.name);
            }
        }
        if (boardCount != 200) throw new InvalidOperationException("Expected 200 board cells, found " + boardCount);
        if (nextCount != 16) throw new InvalidOperationException("Expected 16 next cells, found " + nextCount);
        UIBinder binder = prefab.GetComponent<UIBinder>();
        if (binder == null) throw new InvalidOperationException("Root UIBinder missing");
        int bindingCount = binder.uiList == null ? 0 : binder.uiList.Count;
        Debug.Log($"[AIUI] Bitmap cell validation passed: board=200, next=16, no missing Sprite, bindings={bindingCount}.");
    }
}
#endif
