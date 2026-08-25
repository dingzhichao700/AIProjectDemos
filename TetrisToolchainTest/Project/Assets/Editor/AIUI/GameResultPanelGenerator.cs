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

[InitializeOnLoad]
public static class GameResultPanelGenerator
{
    const string SourceDir = "Assets/Art/atlasSource/default/tetris";
    const string PrefabDir = "Assets/Prefab/default/tetris";
    const string PrefabPath = PrefabDir + "/GameResultPanel.prefab";
    const string Address = "default/tetris/GameResultPanel";
    const string SessionKey = "AIUI.GameResultPanel.Stage6.QwenFigmaStyleR1";
    static readonly Color White = Hex("#E1F7FF");
    static readonly Color Cyan = Hex("#22D7ED");
    static readonly Color Gold = Hex("#F1A13D");

    static GameResultPanelGenerator() { EditorApplication.delayCall += TryGenerateOnce; }

    [MenuItem("Tools/AIUI/Generate Tetris/GameResultPanel (Stage 6)")]
    public static void Generate()
    {
        var sprites = LoadSprites();
        foreach (var name in new[] { "pause_scrim", "pause_modal_frame", "button_normal" })
            if (!sprites.ContainsKey(name)) throw new InvalidOperationException("Missing bitmap Sprite: " + name);
        EnsureFolder(PrefabDir);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) AssetDatabase.DeleteAsset(PrefabPath);

        var root = CreateObject("GameResultPanel", null, Vector2.zero, new Vector2(1920, 1080));
        AddImage(root, "imgScrim", new Vector2(0, 0), new Vector2(1920, 1080), sprites["pause_scrim"]);
        var modal = CreateObject("imgModalFrame", root.transform, new Vector2(360, 145), new Vector2(1200, 790));
        AddImage(modal, "imgModalSurface", Vector2.zero, new Vector2(1200, 790), sprites["pause_modal_frame"]);
        AddText(modal.transform, "txtTitle", "GAME OVER", new Vector2(180, 80), new Vector2(840, 90), 72, White, TextAlignmentOptions.Center);
        AddText(modal.transform, "txtResult", "RESULT", new Vector2(115, 225), new Vector2(560, 52), 38, Cyan, TextAlignmentOptions.Left);
        AddText(modal.transform, "txtScoreLabel", "SCORE", new Vector2(115, 295), new Vector2(560, 42), 30, White, TextAlignmentOptions.Left);
        AddText(modal.transform, "txtScore", "000000", new Vector2(115, 337), new Vector2(560, 96), 76, Gold, TextAlignmentOptions.Center);
        AddText(modal.transform, "txtLinesLabel", "LINES", new Vector2(115, 470), new Vector2(260, 44), 32, Cyan, TextAlignmentOptions.Left);
        AddText(modal.transform, "txtLines", "000", new Vector2(425, 470), new Vector2(250, 44), 38, White, TextAlignmentOptions.Right);
        AddText(modal.transform, "txtLevelLabel", "LEVEL", new Vector2(115, 540), new Vector2(260, 44), 32, Cyan, TextAlignmentOptions.Left);
        AddText(modal.transform, "txtLevel", "01", new Vector2(425, 540), new Vector2(250, 44), 38, White, TextAlignmentOptions.Right);
        AddButton(modal.transform, "btnRetry", "RETRY", new Vector2(755, 300), sprites["button_normal"]);
        AddButton(modal.transform, "btnQuitToTitle", "TITLE", new Vector2(755, 475), sprites["button_normal"]);

        var binder = root.AddComponent<UIBinder>();
        binder.csharpAssetPath = "Assets/Scripts/csharp/com/game/tetris/view/GameResultPanel.cs";
        binder.csharpAsset = string.Empty;
        binder.uiList = new List<UIBindComponentData>();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        RegisterAddressable();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] GameResultPanel Stage 6 generated: " + PrefabPath + " (address: " + Address + ")");
    }

    static void TryGenerateOnce() { if (!SessionState.GetBool(SessionKey, false)) { try { Generate(); } catch (Exception e) { Debug.LogError("[AIUI] GameResultPanel generation failed: " + e); } } }
    static Dictionary<string, Sprite> LoadSprites() { var r = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase); foreach (var raw in Directory.GetFiles(SourceDir, "*.png")) { var p = raw.Replace('\\','/'); AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport); var i = AssetImporter.GetAtPath(p) as TextureImporter; if (i == null) continue; i.textureType=TextureImporterType.Sprite; i.spriteImportMode=SpriteImportMode.Single; i.alphaIsTransparency=true; i.mipmapEnabled=false; i.SaveAndReimport(); var s=AssetDatabase.LoadAssetAtPath<Sprite>(p); if(s!=null)r[Path.GetFileNameWithoutExtension(p)]=s; } return r; }
    static GameObject CreateObject(string name, Transform parent, Vector2 pos, Vector2 size) { var go=new GameObject(name,typeof(RectTransform)); if(parent!=null)go.transform.SetParent(parent,false); var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1); rt.anchoredPosition=new Vector2(pos.x,-pos.y); rt.sizeDelta=size; return go; }
    static Image AddImage(GameObject parent,string name,Vector2 pos,Vector2 size,Sprite sprite){var go=CreateObject(name,parent.transform,pos,size);var i=go.AddComponent<Image>();i.sprite=sprite;i.color=Color.white;i.raycastTarget=false;return i;}
    static TextMeshProUGUI AddText(Transform parent,string name,string value,Vector2 pos,Vector2 size,float fs,Color color,TextAlignmentOptions align){var go=CreateObject(name,parent,pos,size);var t=go.AddComponent<TextMeshProUGUI>();t.text=value;t.font=TMP_Settings.defaultFontAsset;t.fontSize=fs;t.color=color;t.alignment=align;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;return t;}
    static void AddButton(Transform parent,string name,string label,Vector2 pos,Sprite sprite){var go=CreateObject(name,parent,pos,new Vector2(340,112));var i=go.AddComponent<Image>();i.sprite=sprite;i.color=Color.white;i.raycastTarget=true;go.AddComponent<Button>();var gb=go.AddComponent<GameButton>();var text=AddText(go.transform,"txtLabel",label,new Vector2(42,30),new Vector2(256,48),38,White,TextAlignmentOptions.Center);var so=new SerializedObject(gb);so.FindProperty("label").objectReferenceValue=text;so.FindProperty("text").stringValue=label;so.ApplyModifiedPropertiesWithoutUndo();}
    static void RegisterAddressable(){var s=AddressableAssetSettingsDefaultObject.Settings;if(s==null)throw new InvalidOperationException("Addressables unavailable");var g=s.FindGroup("Default Local Group")??s.DefaultGroup;var e=s.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath),g);e.address=Address;s.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved,e,true);}
    static void EnsureFolder(string path){var p=path.Split('/');var c=p[0];for(var i=1;i<p.Length;i++){var n=c+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(c,p[i]);c=n;}}
    static Color Hex(string h){ColorUtility.TryParseHtmlString(h,out var c);return c;}
}
#endif
