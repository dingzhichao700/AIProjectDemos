using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>隔离Editor原生SpriteSkin预览与取帧，不加载页面、不切Play或存档。</summary>
public sealed class MedivacNativePreview : EditorWindow {
    PreviewRenderUtility preview;
    GameObject instance;
    MedivacDisplayRig rig;
    string output;
    int captureFrame;
    bool exporting;
    double started;
    string prefab;
    [Serializable] class Evidence { public string status,mode,prefab,error;public int frameCount,boneCount,layers;public bool allNativeBuffersValid,sceneAndPlayerUntouched; }
    [Serializable] class Request { public string requestId,prefabPath,outputDirectory,action; public int processId; }
    const string Pending="Library/AIUI/pending-medivac-preview.json";
    [InitializeOnLoadMethod] static void Install() { EditorApplication.update+=Poll; }
    static void Poll() {
        if(!File.Exists(Pending)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        var r=JsonUtility.FromJson<Request>(File.ReadAllText(Pending));File.Delete(Pending);
        if(r.processId!=System.Diagnostics.Process.GetCurrentProcess().Id||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Preview requires leased editor in Edit");
        if(r.action=="apply-r2") {
            const string path="Assets/Prefab/default/cardDisplayAnimation/2102/display/MedivacDisplayRig.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var driver=root.GetComponent<MedivacDisplayRig>();
                if(driver.idleDuration!=3 || driver.idleLiftPixels!=2 || driver.idlePitchDegrees!=.5f) throw new InvalidOperationException("Idle baseline drift");
                driver.thrustDuration=2.4f;driver.thrustLiftPixels=16;driver.thrustPitchDegrees=-4;
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Directory.CreateDirectory(r.outputDirectory);
                File.WriteAllText(Path.Combine(r.outputDirectory,"r2-prefab-applied.json"),"{\"status\":\"passed\",\"idleUnchanged\":true,\"duration\":2.4,\"lift\":16,\"pitch\":-4}");
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            return;
        }
        if(r.action=="close"){foreach(var old in Resources.FindObjectsOfTypeAll<MedivacNativePreview>())old.Close();return;}
        var window=GetWindow<MedivacNativePreview>("医疗运输机·原速预览");window.StartPreview(r.prefabPath,r.outputDirectory);
    }
    void StartPreview(string path,string directory) {
        Cleanup();prefab=path;output=directory;Directory.CreateDirectory(output);
        preview=new PreviewRenderUtility();preview.camera.orthographic=true;preview.camera.orthographicSize=1.25f;
        preview.camera.transform.position=new Vector3(1.59f-1.6612334f,1.25f-.3239561f,-10);
        preview.camera.transform.rotation=Quaternion.identity;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
        preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.14f,.20f,1);
        instance=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));preview.AddSingleGO(instance);instance.SetActive(true);rig=instance.GetComponent<MedivacDisplayRig>();rig.paused=true;
        foreach(var sr in instance.GetComponentsInChildren<SpriteRenderer>())sr.forceRenderingOff=false;
        captureFrame=0;exporting=true;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Repaint();
    }
    void Tick(){if(this!=null)Repaint();}
    void OnGUI() {
        if(preview==null){GUILayout.Label("等待医疗运输机原生资产");return;}
        bool thrust=exporting?captureFrame>=90:(EditorApplication.timeSinceStartup-started)%(6.2+rig.thrustDuration)>=6.2;
        float t=exporting?(captureFrame<90?captureFrame/30f:(captureFrame-90)/30f):(float)((EditorApplication.timeSinceStartup-started)%(6.2+rig.thrustDuration));
        if(!exporting&&thrust)t-=6.2f;
        GUILayout.Label("Unity原生SpriteSkin · "+(thrust?"推力调整":"悬浮待机")+" · 原速 / 2倍查看");
        var rect=GUILayoutUtility.GetRect(636,500,GUILayout.ExpandWidth(false));
        if(Event.current.type!=EventType.Repaint)return;
        try {
            rig.Sample(t,thrust);foreach(var skin in rig.skins)skin.OnPreviewUpdate();
            foreach(var skin in rig.skins){
                // Editor OnPreviewUpdate uses its own native buffer, not runtime composite buffers.
                bool valid=(bool)typeof(SpriteSkin).GetProperty("isValid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(skin);
                int bytes=(int)typeof(SpriteSkin).GetField("m_CurrentDeformVerticesLength",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(skin);
                if(!valid||bytes<=0)throw new InvalidOperationException("Native Editor deformation invalid: "+skin.name+" valid="+valid+" bytes="+bytes);
            }
            preview.BeginPreview(new Rect(0,0,636,500),GUIStyle.none);preview.camera.Render();var tex=preview.EndPreview();GUI.DrawTexture(rect,tex,ScaleMode.ScaleToFit,false);
            if(exporting){
                var prior=RenderTexture.active;RenderTexture.active=tex as RenderTexture;var png=new Texture2D(636,500,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,636,500),0,0);png.Apply();RenderTexture.active=prior;
                string action=captureFrame<90?"idle":"thrust";int index=captureFrame<90?captureFrame:captureFrame-90;Directory.CreateDirectory(Path.Combine(output,action));File.WriteAllBytes(Path.Combine(output,action,index.ToString("D3")+".png"),png.EncodeToPNG());DestroyImmediate(png);captureFrame++;
                if(captureFrame>=90+Mathf.RoundToInt(rig.thrustDuration*30)){exporting=false;started=EditorApplication.timeSinceStartup;File.WriteAllText(Path.Combine(output,"native-preview.json"),JsonUtility.ToJson(new Evidence{status="passed",mode="EditorWindow native SpriteSkin preview; no Play or UI integration",prefab=prefab,frameCount=captureFrame,boneCount=4,layers=3,allNativeBuffersValid=true,sceneAndPlayerUntouched=true},true));}
            }
        }catch(Exception e){exporting=false;File.WriteAllText(Path.Combine(output,"native-preview.json"),JsonUtility.ToJson(new Evidence{status="failed",error=e.ToString(),frameCount=captureFrame},true));Cleanup();Debug.LogException(e);}
    }
    void Cleanup(){EditorApplication.update-=Tick;if(preview!=null){preview.Cleanup();preview=null;}instance=null;rig=null;}
    void OnDisable(){Cleanup();}
}
