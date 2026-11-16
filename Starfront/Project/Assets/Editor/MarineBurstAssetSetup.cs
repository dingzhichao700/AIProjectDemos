using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>保存拆件图集、七骨权重和Prefab，不覆盖R2待机源。</summary>
public static class MarineBurstAssetSetup {
    public const string AtlasPath = "Assets/Art/atlas/default/card/burst/2101/marine-burst-atlas.png";
    private static readonly string[] Names = { "feetRoot", "pelvis", "chest", "head", "weapon", "nearArm", "farArm" };
    private static readonly int[] Parents = { -1, 0, 1, 2, 2, 2, 2 };
    private static readonly Vector2[] Points = { new Vector2(110, 19), new Vector2(110, 134), new Vector2(110, 214), new Vector2(110, 264), new Vector2(110, 172), new Vector2(40, 208), new Vector2(167, 208) };
    private static readonly string[] Layers = { "body", "far-arm", "rifle", "near-arm" };

    public static void Build() {
        AssetDatabase.ImportAsset(AtlasPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        if (importer == null) { throw new InvalidOperationException("拆件图集未就绪"); }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var rects = new SpriteRect[4];
        for (int layer = 0; layer < 4; layer++) {
            var prior = old.FirstOrDefault(x => x.name == Layers[layer]);
            rects[layer] = new SpriteRect { name = Layers[layer], rect = new Rect(layer * 240, 218, 238, 294), alignment = SpriteAlignment.Custom, pivot = new Vector2(110f / 238, 19f / 294), spriteID = prior == null ? GUID.Generate() : prior.spriteID };
        }
        provider.SetSpriteRects(rects);
        var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        for (int layer = 0; layer < 4; layer++) {
            var bones = new List<SpriteBone>();
            for (int i = 0; i < Names.Length; i++) {
                Vector2 position = Parents[i] < 0 ? Points[i] : Points[i] - Points[Parents[i]];
                bones.Add(new SpriteBone { name = Names[i], parentId = Parents[i], position = position, rotation = Quaternion.identity, length = i == 0 ? 115 : 45 });
            }
            int columns = layer == 0 ? 21 : 11;
            int rows = layer == 0 ? 31 : 15;
            var vertices = new Vertex2DMetaData[columns * rows];
            for (int y = 0; y < rows; y++) {
                for (int x = 0; x < columns; x++) {
                    Vector2 pixel = new Vector2(x * 238f / (columns - 1), y * 294f / (rows - 1));
                    vertices[y * columns + x] = new Vertex2DMetaData { position = pixel, boneWeight = Weight(layer, pixel) };
                }
            }
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            int ti = 0;
            for (int y = 0; y < rows - 1; y++) {
                for (int x = 0; x < columns - 1; x++) {
                    int a = y * columns + x;
                    triangles[ti++] = a; triangles[ti++] = a + columns; triangles[ti++] = a + 1;
                    triangles[ti++] = a + 1; triangles[ti++] = a + columns; triangles[ti++] = a + columns + 1;
                }
            }
            provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(rects[layer].spriteID, bones);
            var mesh = provider.GetDataProvider<ISpriteMeshDataProvider>();
            mesh.SetVertices(rects[layer].spriteID, vertices);
            mesh.SetIndices(rects[layer].spriteID, triangles);
            mesh.SetEdges(rects[layer].spriteID, new Vector2Int[0]);
        }
        provider.Apply();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
        if (sprites.Length != 4) { throw new InvalidOperationException("拆件图集Sprite数错误"); }
        var root = new GameObject("MarineBurstRig");
        root.SetActive(false);
        try {
            var bones = new Transform[7];
            for (int i = 0; i < bones.Length; i++) {
                bones[i] = new GameObject(Names[i]).transform;
                bones[i].SetParent(Parents[i] < 0 ? root.transform : bones[Parents[i]], false);
                bones[i].localPosition = (Vector3)((Parents[i] < 0 ? Vector2.zero : Points[i] - Points[Parents[i]]) / 100);
            }
            var skins = new SpriteSkin[4];
            for (int layer = 0; layer < 4; layer++) {
                var sprite = sprites.Single(x => x.name == Layers[layer]);
                int expected = layer == 0 ? 651 : 165;
                if (sprite.GetVertexCount() != expected) { throw new InvalidOperationException("拆件权重未持久化：" + sprite.name); }
                var child = new GameObject(Layers[layer], typeof(SpriteRenderer));
                child.transform.SetParent(root.transform, false);
                var renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.forceRenderingOff = true;
                skins[layer] = child.AddComponent<SpriteSkin>();
                var serialized = new SerializedObject(skins[layer]);
                serialized.FindProperty("m_RootBone").objectReferenceValue = bones[0];
                var array = serialized.FindProperty("m_BoneTransforms");
                array.arraySize = bones.Length;
                for (int i = 0; i < bones.Length; i++) { array.GetArrayElementAtIndex(i).objectReferenceValue = bones[i]; }
                serialized.FindProperty("m_Bounds").boundsValue = sprite.bounds;
                serialized.FindProperty("m_AlwaysUpdate").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var muzzle = new GameObject("muzzle").transform;
            muzzle.SetParent(bones[4], false);
            muzzle.localPosition = new Vector3(1.08f, -.23f, 0);
            muzzle.localRotation = Quaternion.Euler(0, 0, -21.8f);
            var rig = root.AddComponent<MarineBurstRig>();
            rig.layers = skins; rig.pelvis = bones[1]; rig.chest = bones[2]; rig.head = bones[3]; rig.weapon = bones[4]; rig.nearArm = bones[5]; rig.farArm = bones[6]; rig.muzzle = muzzle;
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, MarineBurstGraphic.PrefabAddress);
        } finally { UnityEngine.Object.DestroyImmediate(root); }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(MarineBurstGraphic.PrefabAddress), settings.FindGroup("Default Local Group")).SetAddress(MarineBurstGraphic.PrefabAddress);
        AssetDatabase.SaveAssets();
    }

    private static BoneWeight Weight(int layer, Vector2 pixel) {
        if (layer == 2) { return new BoneWeight { boneIndex0 = 4, weight0 = 1 }; }
        if (layer == 3) {
            float mix = Mathf.InverseLerp(48, 99, pixel.x);
            return new BoneWeight { boneIndex0 = 5, weight0 = 1 - mix, boneIndex1 = 4, weight1 = mix };
        }
        if (layer == 1) { return new BoneWeight { boneIndex0 = pixel.y > 174 ? 6 : 4, weight0 = 1 }; }
        float localY = (pixel.y - 19) / 100;
        int lower; float blend;
        if (localY < 1.3f) { lower = 0; blend = Mathf.InverseLerp(.75f, 1.3f, localY); }
        else if (localY < 2.15f) { lower = 1; blend = Mathf.InverseLerp(1.3f, 2.15f, localY); }
        else { lower = 2; blend = Mathf.InverseLerp(2.15f, 2.6f, localY); }
        return new BoneWeight { boneIndex0 = lower, weight0 = 1 - blend, boneIndex1 = lower + 1, weight1 = blend };
    }
}