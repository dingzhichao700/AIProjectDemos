using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>2102原生制作专用。由明确请求提供治理核准路径；不登记AA或改页面。</summary>
[InitializeOnLoad]
public static class MedivacSkinAssetSetup {
    const string Pending = "Library/AIUI/pending-medivac-native.json";
    [Serializable] public class Request { public string requestId, textureDirectory, prefabPath, outputDirectory, layerDirectory; public int processId; }
    [Serializable] public class Result { public string requestId, status, error, prefabPath; public int boneCount, layerCount, verticesPerLayer; public string[] assets; }
    static readonly string[] Layers = { "body", "thruster-left", "thruster-right" };
    static MedivacSkinAssetSetup() { EditorApplication.update += Poll; }
    static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        Request r = null;
        try {
            r = JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
            File.Delete(Pending);
            if (r.processId != System.Diagnostics.Process.GetCurrentProcess().Id || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Wrong editor PID or Play state");
            if (!r.textureDirectory.StartsWith("Assets/Art/skeletalAnimation/default/cardDisplayAnimation/") || !r.prefabPath.StartsWith("Assets/Prefab/default/cardDisplayAnimation/")) throw new InvalidOperationException("Ungoverned asset root");
            Build(r);
        } catch (Exception e) { if (r != null) Write(r, new Result { status="failed",error=e.ToString() }); Debug.LogException(e); }
    }
    static void Write(Request r, Result result) { result.requestId=r.requestId;Directory.CreateDirectory(r.outputDirectory);File.WriteAllText(Path.Combine(r.outputDirectory,"native-build.json"),JsonUtility.ToJson(result,true)); }
    static void Build(Request r) {
        const string sourcePath="Assets/Art/unpack/default/card/rosterV3/2102/portrait.png";
        var source=AssetDatabase.LoadAssetAtPath<Sprite>(sourcePath);
        if (source==null || source.rect.size!=new Vector2(318,250) || Vector2.Distance(source.pivot/source.rect.size,new Vector2(.52240044f,.12958243f))>.000001f) throw new InvalidOperationException("Approved input drift");
        var pivot=source.pivot/source.rect.size;float ppu=source.pixelsPerUnit;
        // Source pixels, bottom-left origin. Outer semantic root remains fixed.
        Vector2[] points={source.pivot,new Vector2(166.12334f,135),new Vector2(95,158),new Vector2(217,133)};
        int[] parents={-1,0,1,1};string[] names={"hover-reference","rigid-body","left-plume","right-plume"};
        var sprites=new Sprite[3];var paths=new List<string>();
        Directory.CreateDirectory(r.textureDirectory);Directory.CreateDirectory(Path.GetDirectoryName(r.prefabPath));
        for(int l=0;l<3;l++) {
            string path=r.textureDirectory+"/"+Layers[l]+".png";File.Copy(Path.Combine(r.layerDirectory,Layers[l]+".png"),path,true);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=ppu;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var rects=provider.GetSpriteRects();rects[0].pivot=pivot;rects[0].alignment=SpriteAlignment.Custom;provider.SetSpriteRects(rects);var id=rects[0].spriteID;
            var bones=new List<SpriteBone>();for(int i=0;i<4;i++)bones.Add(new SpriteBone{name=names[i],parentId=parents[i],position=parents[i]<0?points[i]:points[i]-points[parents[i]],rotation=Quaternion.identity,length=i<2?20:35});
            provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(id,bones);
            var vertices=new Vertex2DMetaData[4];int weightedBone=l==0?1:l+1;
            Vector2[] corners={Vector2.zero,new Vector2(318,0),new Vector2(0,250),new Vector2(318,250)};
            for(int v=0;v<4;v++)vertices[v]=new Vertex2DMetaData{position=corners[v],boneWeight=new BoneWeight{boneIndex0=weightedBone,weight0=1}};
            var mesh=provider.GetDataProvider<ISpriteMeshDataProvider>();mesh.SetVertices(id,vertices);mesh.SetIndices(id,new[]{0,2,1,1,2,3});mesh.SetEdges(id,new Vector2Int[0]);provider.Apply();importer.SaveAndReimport();
            sprites[l]=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprites[l].GetVertexCount()!=4 || sprites[l].GetBones().Length!=4)throw new InvalidOperationException("Native skin persistence failed");paths.Add(path);
        }
        var root=new GameObject("MedivacDisplayRig");root.SetActive(false);
        try {
            var transforms=new Transform[4];for(int i=0;i<4;i++){transforms[i]=new GameObject(names[i]).transform;transforms[i].SetParent(parents[i]<0?root.transform:transforms[parents[i]],false);transforms[i].localPosition=(Vector3)((parents[i]<0?Vector2.zero:points[i]-points[parents[i]])/ppu);}
            var skins=new SpriteSkin[3];for(int l=0;l<3;l++){
                var go=new GameObject(Layers[l],typeof(SpriteRenderer));go.transform.SetParent(root.transform,false);var sr=go.GetComponent<SpriteRenderer>();sr.sprite=sprites[l];sr.sortingOrder=l==0?2:0;
                var skin=go.AddComponent<SpriteSkin>();skins[l]=skin;var so=new SerializedObject(skin);so.FindProperty("m_RootBone").objectReferenceValue=transforms[0];var list=so.FindProperty("m_BoneTransforms");list.arraySize=4;for(int i=0;i<4;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=transforms[i];so.FindProperty("m_Bounds").boundsValue=new Bounds(Vector3.zero,Vector3.one*10);so.FindProperty("m_AlwaysUpdate").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            }
            var rig=root.AddComponent<MedivacDisplayRig>();rig.body=transforms[1];rig.leftPlume=transforms[2];rig.rightPlume=transforms[3];rig.skins=skins;rig.pixelsPerUnit=ppu;
            // Save inactive template; preview instantiation activates after references deserialize.
            PrefabUtility.SaveAsPrefabAsset(root,r.prefabPath);paths.Add(r.prefabPath);AssetDatabase.SaveAssets();
        } finally { UnityEngine.Object.DestroyImmediate(root); }
        Write(r,new Result{status="passed",prefabPath=r.prefabPath,boneCount=4,layerCount=3,verticesPerLayer=4,assets=paths.ToArray()});
    }
}
