using UnityEditor;
using UnityEngine;

/// <summary>
/// 资源导入处理
/// </summary>
public class AssetsImport : AssetPostprocessor {
    private void OnPreprocessTexture() {
        Debug.Log("纹理前处理:" + this.assetPath);

        /**图集、参考图*/
        if (assetPath.Contains(ResourceConst.PATH_ATLAS) || assetPath.Contains(ResourceConst.PATH_UI_REFERENCE)) {
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.streamingMipmaps = true;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteGenerateFallbackPhysicsShape = false;

            TextureImporterPlatformSettings standalongSettings = importer.GetPlatformTextureSettings("Standalone");
            standalongSettings.overridden = false;
            importer.SetPlatformTextureSettings(standalongSettings);

            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.format = TextureImporterFormat.Automatic;
            defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(defaultSettings);

            importer.SaveAndReimport();
        }

        if (assetPath.Contains(ResourceConst.PATH_ATLAS_SOURCE) ||
            assetPath.Contains(ResourceConst.PATH_UNPACK_IMAGE) ||
            assetPath.Contains(ResourceConst.PATH_UI_REFERENCE) ||
            assetPath.Contains(ResourceConst.PATH_FRAME_ANIMATION)) {
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.streamingMipmaps = false;
            importer.mipmapEnabled = false;
            importer.isReadable = assetPath.Contains(ResourceConst.PATH_FRAME_ANIMATION);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteImportMode = SpriteImportMode.Single;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteGenerateFallbackPhysicsShape = false;

            TextureImporterPlatformSettings standalongSettings = importer.GetPlatformTextureSettings("Standalone");
            standalongSettings.overridden = false;
            importer.SetPlatformTextureSettings(standalongSettings);

            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(defaultSettings);

            importer.SaveAndReimport();
        }
    }

    public void OnPostprocessTexture(Texture2D tex) {
        Debug.Log("纹理后处理:" + this.assetPath);

        if (assetPath.Contains(ResourceConst.PATH_ATLAS_SOURCE_RICHTEXTIMAGE)) {
            TextureImporter importer = (TextureImporter)assetImporter;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0, 0);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(assetPath);
        }
    }
}
