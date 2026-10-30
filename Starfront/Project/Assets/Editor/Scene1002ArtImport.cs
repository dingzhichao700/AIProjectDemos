using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>Explicit PID-scoped import of reviewed scene1002 art; no automatic content mutation.</summary>
[InitializeOnLoad]
public static class Scene1002ArtImport {
    [Serializable] private class Entry { public string name; public float pivotX; public float pivotY; }
    [Serializable] private class Request { public string requestId; public int processId; public Entry[] assets; public bool build; }
    [Serializable] private class Result { public string requestId; public string status; public string error; public int imported; }
    static Scene1002ArtImport() { EditorApplication.update += Poll; }
    private static void Poll() {
        const string path="Library/scene1002-art-import.json";
        if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var result=new Result {status="failed"};
        try {
            var req=JsonUtility.FromJson<Request>(File.ReadAllText(path));File.Delete(path);result.requestId=req.requestId;
            if(string.IsNullOrEmpty(req.requestId)||req.processId!=System.Diagnostics.Process.GetCurrentProcess().Id)throw new Exception("Request identity mismatch");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var settings=AddressableAssetSettingsDefaultObject.Settings;var group=settings.FindGroup("scene");
            foreach(var a in req.assets) {
                if(a.name.Contains("/")||a.name.Contains(".."))throw new Exception("Invalid asset name");
                string p="Assets/Art/unpack/scene/battle/1002/"+a.name+".png";
                var ti=AssetImporter.GetAtPath(p) as TextureImporter;if(ti==null)throw new Exception("Missing texture: "+p);
                ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;
                var ts=new TextureImporterSettings();ti.ReadTextureSettings(ts);ts.spriteAlignment=(int)SpriteAlignment.Custom;ts.spritePivot=new Vector2(a.pivotX,a.pivotY);ts.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(ts);
                ti.mipmapEnabled=false;ti.npotScale=TextureImporterNPOTScale.None;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.filterMode=FilterMode.Point;ti.wrapMode=TextureWrapMode.Clamp;ti.spritePixelsPerUnit=100;ti.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(p);if(sprite==null)throw new Exception("Sprite import failed");
                var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(p),group);entry.address=p;entry.SetLabel("scene1002",true,true);
                if(Vector2.Distance(sprite.pivot,new Vector2(sprite.rect.width*a.pivotX,sprite.rect.height*a.pivotY))>.01f)throw new Exception("Pivot mismatch: "+p);
                result.imported++;
            }
            EditorUtility.SetDirty(group);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            if(req.build)AddressablesAutoBuilder.BuildPlayerContent();
            result.status="passed";
        }catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText("Library/scene1002-art-import-result.json",JsonUtility.ToJson(result,true));
    }
}
