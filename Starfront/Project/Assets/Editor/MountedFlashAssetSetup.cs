using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public static class MountedFlashAssetSetup {
    public static void Build() {
        // Committed migration owns importer and registration; this legacy setup must not reset them.
        if (MarineBurstGraphic.FlashAddress == "Assets/Art/frameAnimation/default/cardDisplayAnimation/2101/burst/muzzle-flash") {
            ResourceGovernanceGate.ValidateOrThrow("mounted-flash-target-check");
            if (!File.Exists(MarineBurstGraphic.FlashAddress + ".png") || !File.Exists(MarineBurstGraphic.FlashAddress + ".json")) throw new InvalidOperationException("Migrated flash pair missing; use the controlled resource transaction.");
            return;
        }

        string path = MarineBurstGraphic.FlashAddress + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh();
        foreach (string extension in new[] { ".png", ".json" }) {
            string oldPath = "Assets/Art/atlas/default/card/burst/2101/muzzle-flash" + extension;
            string destination = MarineBurstGraphic.FlashAddress + extension;
            if (File.Exists(oldPath) && !File.Exists(destination)) { string error = AssetDatabase.MoveAsset(oldPath, destination); if (!string.IsNullOrEmpty(error)) { throw new InvalidOperationException(error); } }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) { throw new InvalidOperationException("枪口火光图集不存在"); }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        foreach (string asset in new[] { path, MarineBurstGraphic.FlashAddress + ".json" }) {
            AssetDatabase.ImportAsset(asset);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(asset), settings.FindGroup("Default Local Group")).SetAddress(asset);
        }
        AssetDatabase.SaveAssets();
    }
}
