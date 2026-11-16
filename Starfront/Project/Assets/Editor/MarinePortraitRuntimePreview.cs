#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>单次蓝甲卡面运行时审核；只替换UI展示数据，不改配置、持有或存档。</summary>
[InitializeOnLoad]
internal static class MarinePortraitRuntimePreview {
    const string Pending = "Library/AIUI/pending-marine-portrait-preview.json";
    const string Key = "MarinePortraitRuntimePreview.v1";
    [Serializable] class Request { public string requestId, pngPath, outputDirectory, portraitPngPath; public int processId; }
    [Serializable] class Result { public string requestId,status,error,scene,pngSource,naturalScreenshot,inspectionScreenshot; public int processId,cardId,artId,screenWidth,screenHeight,sourceWidth,sourceHeight; public bool owned,onlyRuntimeUiOverride,configChanged,saveChanged;
        public string portraitSource; public int portraitWidth,portraitHeight; public float[] portraitPivot; public float[] cardBoundsTopLeft; }
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static Texture2D texture;
    static Sprite sprite;
    static Texture2D portraitTexture;
    static Sprite portraitSprite;
    static Request active;
    static Result result;
    static DeckCardView target;
    static DeckCardView.DisplayData targetData;
    static DeckCardView formationTarget;
    static DeckCardView.DisplayData originalFormationData;
    static Action<int> originalFormationCallback;
    static double readyAt;
    static int phase;
    static bool loginRequested, priorBackground;
    static MarinePortraitRuntimePreview() { EditorApplication.update += Tick; }
    static void Tick() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try {
            if (File.Exists(Pending)) {
                var r = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
                if (r.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(r.requestId)) throw new InvalidDataException("Wrong editor PID/requestId");
                if (!File.Exists(r.pngPath)) throw new FileNotFoundException(r.pngPath);
                File.Delete(Pending); SessionState.SetString(Key, JsonUtility.ToJson(r)); SessionState.SetFloat(Key+"start",(float)EditorApplication.timeSinceStartup);
                if (!EditorApplication.isPlaying) { EditorApplication.isPlaying = true; return; }
            }
            string saved = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(saved) || !EditorApplication.isPlaying) return;
            if (active == null) {
                active = JsonUtility.FromJson<Request>(saved); Directory.CreateDirectory(active.outputDirectory);
                result = new Result { requestId=active.requestId,processId=active.processId,status="running",cardId=2101,artId=2101,pngSource=active.pngPath,onlyRuntimeUiOverride=true };
                result.naturalScreenshot=Path.Combine(active.outputDirectory,"game-deck-natural.png");result.inspectionScreenshot=Path.Combine(active.outputDirectory,"game-deck-art-inspection.png");
                priorBackground=Application.runInBackground;Application.runInBackground=true;
                var game=typeof(Editor).Assembly.GetType("UnityEditor.GameView"); EditorWindow.GetWindow(game).Show();
            }
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"start",0)>100) throw new TimeoutException("Normal MainPanel/card2101 not ready");
            var main=UnityEngine.Object.FindObjectOfType<MainPanel>();
            if (!main || !main.isOpened) {
                var login=UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login && !loginRequested && login.txt && login.txt.gameObject.activeInHierarchy) { loginRequested=true;login.SendMessage("EnterMain",SendMessageOptions.DontRequireReceiver); }
                return;
            }
            if (!main.viewDeck.gameObject.activeSelf) { main.btnDeck.onClick.Invoke();readyAt=EditorApplication.timeSinceStartup+2;return; }
            var view=main.viewDeck.GetComponentInChildren<DeckPresentationView>();
            if (!view || view.library.arraySource.Count==0 || EditorApplication.timeSinceStartup<readyAt) return;
            if (phase==0) {
                targetData=view.library.arraySource.Cast<object>().OfType<DeckCardView.DisplayData>().FirstOrDefault(x=>x.cardId==2101);
                if (targetData==null) throw new InvalidDataException("Explicit configured Marine card2101 absent");
                texture=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!texture.LoadImage(File.ReadAllBytes(active.pngPath)))throw new InvalidDataException("PNG decode");texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
                sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);sprite.name="MarineM01_RuntimeArtPreview";
                targetData.artwork=sprite;targetData.cardFaceReady=true;
                target=view.library.GetComponentsInChildren<DeckCardView>(false).FirstOrDefault(x=>!x.formation && ((DeckCardView.DisplayData)typeof(DeckCardView).GetField("current",Flags).GetValue(x))?.cardId==2101);
                if (!target) return;
                RenderTarget();
                portraitTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!portraitTexture.LoadImage(File.ReadAllBytes(active.portraitPngPath)))throw new InvalidDataException("Portrait PNG decode");portraitTexture.filterMode=FilterMode.Bilinear;portraitTexture.wrapMode=TextureWrapMode.Clamp;
                var portraitPivot=new Vector2(110f/238f,1f-277f/294f);portraitSprite=Sprite.Create(portraitTexture,new Rect(0,0,portraitTexture.width,portraitTexture.height),portraitPivot,100);portraitSprite.name="MarineV3_FormationPreview";
                formationTarget=view.slots[0];originalFormationData=(DeckCardView.DisplayData)typeof(DeckCardView).GetField("current",Flags).GetValue(formationTarget);originalFormationCallback=(Action<int>)typeof(DeckCardView).GetField("clicked",Flags).GetValue(formationTarget);
                var formationData=new DeckCardView.DisplayData {cardId=targetData.cardId,artId=targetData.artId,artPreview=true,title=targetData.title,quality=targetData.quality,qualityLabel=targetData.qualityLabel,tagId=targetData.tagId,level=targetData.level,cellUnlockCost=targetData.cellUnlockCost,owned=true,artwork=portraitSprite,cardFaceReady=true};
                formationTarget.Render(formationData,null);
                result.portraitSource=active.portraitPngPath;result.portraitWidth=portraitTexture.width;result.portraitHeight=portraitTexture.height;result.portraitPivot=new[]{portraitPivot.x,portraitPivot.y};
                Canvas.ForceUpdateCanvases();readyAt=EditorApplication.timeSinceStartup+1;phase=1;return;
            }
            if (phase==1) {
                Canvas.ForceUpdateCanvases();result.owned=targetData.owned;result.artId=targetData.artId;result.sourceWidth=texture.width;result.sourceHeight=texture.height;result.screenWidth=Screen.width;result.screenHeight=Screen.height;result.scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var corners=new Vector3[4];((RectTransform)target.transform).GetWorldCorners(corners);var canvas=target.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var pts=corners.Select(x=>RectTransformUtility.WorldToScreenPoint(cam,x)).ToArray();result.cardBoundsTopLeft=new[]{pts.Min(x=>x.x),Screen.height-pts.Max(x=>x.y),pts.Max(x=>x.x)-pts.Min(x=>x.x),pts.Max(x=>x.y)-pts.Min(x=>x.y)};
                ScreenCapture.CaptureScreenshot(result.naturalScreenshot);readyAt=EditorApplication.timeSinceStartup+.8;phase=2;return;
            }
            if(phase==2){
                if(!File.Exists(result.naturalScreenshot))return;
                // Only a diagnostic view: remove temporary occlusion for art inspection, not ownership.
                target.artwork.color=Color.white;if(target.lockIcon)target.lockIcon.gameObject.SetActive(false);if(target.placeholder)target.placeholder.gameObject.SetActive(false);
                target.qualityImage.material=null;target.qualityImage.sprite=target.qualitySprites[Mathf.Clamp(targetData.quality-2,0,target.qualitySprites.Length-1)];Canvas.ForceUpdateCanvases();readyAt=EditorApplication.timeSinceStartup+.5;phase=3;return;
            }
            if(phase==3){ScreenCapture.CaptureScreenshot(result.inspectionScreenshot);readyAt=EditorApplication.timeSinceStartup+.8;phase=4;return;}
            if(phase==4){if(!File.Exists(result.inspectionScreenshot))return;RenderTarget();if(formationTarget)formationTarget.Render(originalFormationData,originalFormationCallback);Finish("passed","");}
        } catch(Exception e) { if(target && targetData!=null)RenderTarget();Finish("failed",e.ToString());Debug.LogException(e); }
    }
    static void RenderTarget(){var callback=(Action<int>)typeof(DeckCardView).GetField("clicked",Flags).GetValue(target);target.Render(targetData,callback);}
    static void Finish(string status,string error){
        if(result!=null){result.status=status;result.error=error;File.WriteAllText(Path.Combine(active.outputDirectory,"runtime-result.json"),JsonUtility.ToJson(result,true));}
        SessionState.EraseString(Key);Application.runInBackground=priorBackground;active=null;result=null;target=null;targetData=null;formationTarget=null;originalFormationData=null;originalFormationCallback=null;phase=0;
    }
}
#endif
