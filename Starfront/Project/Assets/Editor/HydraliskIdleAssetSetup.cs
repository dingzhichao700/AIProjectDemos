using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>独立下颌与原身体蒙皮共图集，不改正式静态立绘。</summary>
public static class HydraliskIdleAssetSetup {
    public const string Source = "Assets/Art/unpack/default/card/rosterV3/2202/portrait.png";
    public const string SkinTexture = "Assets/Art/atlas/default/card/idle/2202/hydralisk-mouth-atlas.png";
    private static readonly string[] Names = { "supportRoot", "abdomen", "neck", "crown", "mandible" };
    private static readonly int[] Parents = { -1, 0, 1, 2, 3 };
    private static readonly Vector2[] Points = { new Vector2(138,22),new Vector2(138,107),new Vector2(138,177),new Vector2(128,178),new Vector2(101,191) };
    public static void Build() {
        var source=AssetDatabase.LoadAssetAtPath<Sprite>(Source);
        if(source==null || source.rect.size!=new Vector2(258,280)){throw new InvalidOperationException("刺蛇源画布改变，需重新定标");}
        AssetDatabase.ImportAsset(SkinTexture);
        var importer=(TextureImporter)AssetImporter.GetAtPath(SkinTexture);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects();string[] layerNames={"body","mandible"};var rects=new SpriteRect[2];
        for(int layer=0;layer<2;layer++){var prior=old.FirstOrDefault(x=>x.name==layerNames[layer]);rects[layer]=new SpriteRect{name=layerNames[layer],rect=new Rect(layer*260,232,258,280),alignment=SpriteAlignment.Custom,pivot=source.pivot/source.rect.size,spriteID=prior==null?GUID.Generate():prior.spriteID};}
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(x=>new SpriteNameFileIdPair(x.name,x.spriteID)));
        for(int layer=0;layer<2;layer++){
            var bones=new List<SpriteBone>();for(int i=0;i<5;i++){bones.Add(new SpriteBone{name=Names[i],parentId=Parents[i],position=Parents[i]<0?Points[i]:Points[i]-Points[Parents[i]],rotation=Quaternion.identity,length=i==0?85:i==1?70:i==2?60:i==3?20:35});}
            int columns=layer==0?21:2,rows=layer==0?31:2;var vertices=new Vertex2DMetaData[columns*rows];
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){
                Vector2 pixel=new Vector2(x*258f/(columns-1),y*280f/(rows-1));BoneWeight weight;
                if(layer==1){weight=new BoneWeight{boneIndex0=4,weight0=1};}
                else{float h=(pixel.y-22)/100;int lower;float mix;if(h<1.05f){lower=0;mix=Mathf.InverseLerp(.4f,1.05f,h);}else if(h<1.85f){lower=1;mix=Mathf.InverseLerp(1.05f,1.85f,h);}else{lower=2;mix=Mathf.InverseLerp(1.85f,2.3f,h);}if((pixel.x<258*.31f&&h<1.15f)||(pixel.x>258*.75f&&h<.8f)){lower=0;mix=0;}weight=new BoneWeight{boneIndex0=lower,weight0=1-mix,boneIndex1=lower+1,weight1=mix};if(pixel.y>145){float headMix=Mathf.InverseLerp(145,185,pixel.y);weight=new BoneWeight{boneIndex0=2,weight0=1-headMix,boneIndex1=3,weight1=headMix};}}
                vertices[y*columns+x]=new Vertex2DMetaData{position=pixel,boneWeight=weight};
            }
            var triangles=new int[(columns-1)*(rows-1)*6];int t=0;for(int y=0;y<rows-1;y++)for(int x=0;x<columns-1;x++){int a=y*columns+x;triangles[t++]=a;triangles[t++]=a+columns;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=a+columns;triangles[t++]=a+columns+1;}
            var id=rects[layer].spriteID;provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(id,bones);var mesh=provider.GetDataProvider<ISpriteMeshDataProvider>();mesh.SetVertices(id,vertices);mesh.SetIndices(id,triangles);mesh.SetEdges(id,new Vector2Int[0]);
        }
        provider.Apply();importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(SkinTexture).OfType<Sprite>().ToArray();if(sprites.Length!=2){throw new InvalidOperationException("口部图集不是两层");}
        var root=new GameObject("HydraliskIdleRig",typeof(SpriteRenderer));root.SetActive(false);
        try{
            var bones=new Transform[5];for(int i=0;i<5;i++){bones[i]=new GameObject(Names[i]).transform;bones[i].SetParent(Parents[i]<0?root.transform:bones[Parents[i]],false);bones[i].localPosition=(Vector3)((Parents[i]<0?Vector2.zero:Points[i]-Points[Parents[i]])/100);}
            var skins=new SpriteSkin[2];for(int layer=0;layer<2;layer++){
                var sprite=sprites.Single(x=>x.name==layerNames[layer]);if(sprite.GetVertexCount()!=(layer==0?651:4)){throw new InvalidOperationException("口部网格未保存");}
                var go=layer==0?root:new GameObject("mandibleSkin",typeof(SpriteRenderer));if(layer!=0)go.transform.SetParent(root.transform,false);
                var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.forceRenderingOff=true;
                var skin=go.AddComponent<SpriteSkin>();skins[layer]=skin;var serialized=new SerializedObject(skin);serialized.FindProperty("m_RootBone").objectReferenceValue=bones[0];var array=serialized.FindProperty("m_BoneTransforms");array.arraySize=5;for(int i=0;i<5;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=bones[i];serialized.FindProperty("m_Bounds").boundsValue=sprite.bounds;serialized.FindProperty("m_AlwaysUpdate").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var idle=root.AddComponent<MarineIdleRig>();idle.skin=skins[0];idle.pelvis=bones[1];idle.chest=bones[2];idle.head=bones[3];idle.durationSeconds=3.2f;idle.pelvisDegrees=3;idle.chestDegrees=-1.4f;idle.headDegrees=1.6f;idle.chestLift=.04f;
            var mouth=root.AddComponent<HydraliskMouthRig>();mouth.idle=idle;mouth.jawSkin=skins[1];mouth.mandible=bones[4];mouth.idleJawAmplitudeDegrees=2.5f;mouth.idleJawPeriodSeconds=1.6f;
            root.SetActive(true);PrefabUtility.SaveAsPrefabAsset(root,CardArtIdleSkin.HydraliskPrefabAddress);
        }finally{UnityEngine.Object.DestroyImmediate(root);}
        var settings=AddressableAssetSettingsDefaultObject.Settings;settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(CardArtIdleSkin.HydraliskPrefabAddress),settings.FindGroup("Default Local Group")).SetAddress(CardArtIdleSkin.HydraliskPrefabAddress);AssetDatabase.SaveAssets();
    }
}