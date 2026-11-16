using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>原设定立绘与展台派生的定向刷新，仅响应指定编辑器请求。</summary>
[InitializeOnLoad]
public static class ApprovedPortraitRefresh
{
    [Serializable] private class Request { public string requestId; public int processId; }
    [Serializable] private class Outcome { public string requestId; public string status; public string error; }
    private const string Pending = "Library/AIUI/pending-approved-portrait-refresh.json";
    static ApprovedPortraitRefresh()
    {
        EditorApplication.update += Poll;
    }
    private static void Poll()
    {
        if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        Request r = null;
        try
        {
            r = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            File.Delete(Pending);
            if (r.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(r.requestId))
            {
                throw new InvalidOperationException("Invalid request identity");
            }
            foreach (int id in new[] { 1, 2, 5 })
            {
                foreach (string name in new[] { "portrait", "formation" })
                {
                    string path = "Assets/Art/unpack/default/card/colonial/" + id.ToString("00") + "/" + name + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        throw new InvalidOperationException("Missing texture: " + path);
                    }
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    var options = new TextureImporterSettings();
                    importer.ReadTextureSettings(options);
                    options.spriteMeshType = SpriteMeshType.FullRect;
                    if (name == "formation")
                    {
                        options.spriteAlignment = (int)SpriteAlignment.Custom;
                        options.spritePivot = new Vector2(80f / 161, 99f / 335);
                    }
                    importer.SetTextureSettings(options);
                    importer.SaveAndReimport();
                }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/AIUI/approved-portrait-refresh-result.json", JsonUtility.ToJson(new Outcome { requestId = r.requestId, status = "passed" }, true));
        }
        catch (Exception e)
        {
            File.WriteAllText("Library/AIUI/approved-portrait-refresh-result.json", JsonUtility.ToJson(new Outcome { requestId = r == null ? "unparsed" : r.requestId, status = "failed", error = e.ToString() }, true));
            Debug.LogException(e);
        }
    }
}
