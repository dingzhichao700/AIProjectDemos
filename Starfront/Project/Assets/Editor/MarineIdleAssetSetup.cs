using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>从陆战队员现行静态源建立独立可编辑蒙皮资产，不改原图和源Pivot。</summary>
/// <remarks>动画导入契约需要独立派生；两使用端共用一个Prefab及一个骨骼Sprite。</remarks>
public static class MarineIdleAssetSetup {

    public const string Source = "Assets/Art/unpack/default/card/rosterV3/2101/portrait.png";

    public const string SkinTexture = "Assets/Art/unpack/default/card/idle/2101/portrait-skin.png";

    public static void Build() {
        var source = AssetDatabase.LoadAssetAtPath<Sprite>(Source);
        if (source == null) {
            throw new InvalidOperationException("正式陆战队员立绘不存在");
        }
        Vector2 pivot = source.pivot / source.rect.size;
        Directory.CreateDirectory(Path.GetDirectoryName(SkinTexture));
        File.Copy(Source, SkinTexture, true);
        AssetDatabase.ImportAsset(SkinTexture);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SkinTexture);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = source.pixelsPerUnit;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinTexture);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = provider.GetSpriteRects();
        rects[0].pivot = pivot;
        rects[0].alignment = SpriteAlignment.Custom;
        provider.SetSpriteRects(rects);
        const int columns = 21;
        const int rows = 31;
        float ppu = source.pixelsPerUnit;
        float[] heights = { 0, 1.15f, 1.95f, 2.45f };
        string[] names = { "feetRoot", "pelvis", "chest", "head" };
        var bones = new List<SpriteBone>();
        for (int i = 0; i < names.Length; i++) {
            var point = new Vector3(0, i == 0 ? 0 : heights[i] - heights[i - 1], 0) * ppu;
            if (i == 0) {
                point += new Vector3(pivot.x * texture.width, pivot.y * texture.height, 0);
            }
            bones.Add(new SpriteBone { name = names[i], parentId = i - 1, position = point, rotation = Quaternion.identity, length = (i == 3 ? .2f : heights[i + 1] - heights[i]) * ppu });
        }
        var vertices = new Vertex2DMetaData[columns * rows];
        for (int y = 0; y < rows; y++) {
            for (int x = 0; x < columns; x++) {
                Vector2 pixel = new Vector2(x * texture.width / (columns - 1f), y * texture.height / (rows - 1f));
                float localY = (pixel.y - pivot.y * texture.height) / ppu;
                int lower;
                float mix;
                if (localY < 1.3f) {
                    lower = 0;
                    mix = Mathf.InverseLerp(.75f, 1.3f, localY);
                } else if (localY < 2.15f) {
                    lower = 1;
                    mix = Mathf.InverseLerp(1.3f, 2.15f, localY);
                } else {
                    lower = 2;
                    mix = Mathf.InverseLerp(2.15f, 2.6f, localY);
                }
                vertices[y * columns + x] = new Vertex2DMetaData { position = pixel, boneWeight = new BoneWeight { boneIndex0 = lower, weight0 = 1 - mix, boneIndex1 = lower + 1, weight1 = mix } };
            }
        }
        var triangles = new int[(columns - 1) * (rows - 1) * 6];
        int ti = 0;
        for (int y = 0; y < rows - 1; y++) {
            for (int x = 0; x < columns - 1; x++) {
                int a = y * columns + x;
                triangles[ti++] = a;
                triangles[ti++] = a + columns;
                triangles[ti++] = a + 1;
                triangles[ti++] = a + 1;
                triangles[ti++] = a + columns;
                triangles[ti++] = a + columns + 1;
            }
        }
        var id = rects[0].spriteID;
        provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(id, bones);
        var mesh = provider.GetDataProvider<ISpriteMeshDataProvider>();
        mesh.SetVertices(id, vertices);
        mesh.SetIndices(id, triangles);
        mesh.SetEdges(id, new Vector2Int[0]);
        provider.Apply();
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkinTexture);
        if (sprite.GetVertexCount() != vertices.Length || Vector2.Distance(sprite.pivot / sprite.rect.size, pivot) > .00001f) {
            throw new InvalidOperationException("陆战队员蒙皮网格或锚点保存失败");
        }
        var root = new GameObject("MarineIdleRig", typeof(SpriteRenderer));
        root.SetActive(false);
        try {
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.forceRenderingOff = true;
            var transforms = new Transform[4];
            for (int i = 0; i < 4; i++) {
                transforms[i] = new GameObject(names[i]).transform;
                transforms[i].SetParent(i == 0 ? root.transform : transforms[i - 1], false);
                transforms[i].localPosition = new Vector3(0, i == 0 ? 0 : heights[i] - heights[i - 1], 0);
            }
            var skin = root.AddComponent<SpriteSkin>();
            var serialized = new SerializedObject(skin);
            serialized.FindProperty("m_RootBone").objectReferenceValue = transforms[0];
            var array = serialized.FindProperty("m_BoneTransforms");
            array.arraySize = 4;
            for (int i = 0; i < 4; i++) {
                array.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
            }
            serialized.FindProperty("m_Bounds").boundsValue = sprite.bounds;
            serialized.FindProperty("m_AlwaysUpdate").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var rig = root.AddComponent<MarineIdleRig>();
            rig.skin = skin;
            rig.pelvis = transforms[1];
            rig.chest = transforms[2];
            rig.head = transforms[3];
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, CardArtIdleSkin.MarinePrefabAddress);
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
        }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(CardArtIdleSkin.MarinePrefabAddress), settings.FindGroup("Default Local Group")).SetAddress(CardArtIdleSkin.MarinePrefabAddress);
        AssetDatabase.SaveAssets();
    }

}