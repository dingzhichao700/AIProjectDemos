using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using cfg;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleOccupationRuntimeValidation {
    private const string Key = "Starfront.OccupationRuntime";
    static BattleOccupationRuntimeValidation() {
        EditorApplication.playModeStateChanged += OnPlay;
    }
    public static void Start(string requestId) {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/GameEntrance.unity") {
            throw new InvalidOperationException("Requires idle GameEntrance.");
        }
        SessionState.SetString(Key, requestId);
        EditorApplication.isPlaying = true;
    }
    private static void OnPlay(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(Key, ""))) {
            Run();
        }
    }
    private static void Check(bool value, string text) {
        if (!value) { throw new InvalidOperationException("Occupation runtime: " + text); }
    }
    private static async Task Until(Func<bool> ready) {
        double deadline = EditorApplication.timeSinceStartup + 90;
        while (!ready()) {
            Check(EditorApplication.timeSinceStartup < deadline, "timeout");
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(100);
        }
    }
    private static Color32[] Read(RenderTexture source) {
        var previous = RenderTexture.active;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try {
            RenderTexture.active = source;
            texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
            texture.Apply();
            return texture.GetPixels32();
        } finally {
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(texture);
        }
    }
    private static void CheckRenderedCoverage(RawImage image, BattleSceneModel model) {
        var material = new Material(image.materialForRendering);
        material.DisableKeyword("UNITY_UI_CLIP_RECT");
        material.SetInt("_StencilComp", 8);
        var target = RenderTexture.GetTemporary(2000, 2000, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var pixels = new Texture2D(2000, 2000, TextureFormat.RGBA32, false);
        try {
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();
            material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
            GL.End();
            GL.PopMatrix();
            pixels.ReadPixels(new Rect(0,0,2000,2000),0,0);
            pixels.Apply();
            var covered = model.cells.Values.First(c => c.camp==BattleSceneCamp.FRIENDLY && !c.unlocked);
            int x = Mathf.RoundToInt(covered.position.x), y = 1999-Mathf.RoundToInt(covered.position.y-model.config.GroundThickness);
            Check(pixels.GetPixel(x,y).a > .1f, "GPU actually renders occupied cell");
            var empty = model.cells.Values.First(c => c.camp==BattleSceneCamp.UNOWNED);
            x=Mathf.RoundToInt(empty.position.x);y=1999-Mathf.RoundToInt(empty.position.y-model.config.GroundThickness);
            Check(pixels.GetPixel(x,y).a < .01f, "GPU hides unowned cell");
        } finally {
            RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(pixels);
            UnityEngine.Object.Destroy(material);
        }
    }
    private static async void Run() {
        string requestId = SessionState.GetString(Key, "");
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        string error = null;
        try {
            await Until(() => UnityEngine.Object.FindObjectOfType<LoginPanel>() != null
                && UnityEngine.Object.FindObjectOfType<LoginPanel>().isOpened
                && UnityEngine.Object.FindObjectOfType<LoginPanel>().txt.text == "点击屏幕或按任意键开始");
            var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
            await Until(() => PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) != null && PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL).isOpened);
            await ResourceLoader.LoadListAsync(new System.Collections.Generic.List<ResLoadInfo> {
                new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL), ResType.Prefab),
                new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL), ResType.Prefab)
            });
            foreach (int id in new[] {1001,1002,1001,1002}) {
                await BattleControl.ins.PrepareAsync(id);
                var model = BattleControl.ins.model;
                model.StopTimeFlow();
                PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.Close();
                PanelMgr.ins.OpenPanel(MainUIEnum.BATTLE_SCENE_PANEL);
                await Task.Delay(300);
                model.StopTimeFlow();
                var panel = (BattleScenePanel)PanelMgr.ins.GetPanel(MainUIEnum.BATTLE_SCENE_PANEL);
                var view = panel.GetComponent<BattleSceneView>();
                Check(model.buildings.Count == 2, "bases only");
                Check(model.cells.Values.Any(c => c.camp == BattleSceneCamp.UNOWNED), "new config loaded");
                var raw = view.layers[(int)BattleSceneVisualLayer.GROUND_DECAL-1].GetComponentsInChildren<RawImage>().Single(i => i.name == "imgTerritoryCoverage");
                Check(!raw.raycastTarget && raw.material.shader.name == "Starfront/BattleCoverage", "independent coverage material");
                Check(raw.materialForRendering.GetVector("_SceneSize") == raw.material.GetVector("_SceneSize"), "masked material keeps scene size");
                Check(raw.materialForRendering.GetVector("_Origin") == raw.material.GetVector("_Origin"), "masked material keeps origin");
                var states = (Texture2D)raw.material.GetTexture("_CellStates");
                foreach(var cell in model.cells.Values) {
                    bool expected = !cell.unlocked && cell.capturable && (cell.camp == BattleSceneCamp.FRIENDLY || cell.camp == BattleSceneCamp.ENEMY);
                    Check((states.GetPixel(cell.q+64,cell.k+64).a>.5f)==expected, "coverage state matrix");
                }
                CheckRenderedCoverage(raw, model);
                Check(view.GetComponentsInChildren<Image>(true).All(i => !i.name.StartsWith("unlockGlow")), "old second glow removed");
                var costs = view.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.name=="cost").ToArray();
                Check(costs.Length>0 && costs.All(t => (t.fontStyle & FontStyles.Bold)!=0 && t.outlineWidth>0), "bold outlined cost text");
                var original = Read(view.groundTexture);
                var frontier = model.cells.Values.First(c => c.camp==BattleSceneCamp.FRIENDLY && model.unlocks.CanUnlock(c.id,c.camp,out _));
                Check(BattleControl.ins.TryUnlockCell(frontier.id,out string reason), "real control unlock: "+reason);
                await Task.Delay(200);
                Check(states.GetPixel(frontier.q+64,frontier.k+64).a<.5f, "unlock removes coverage");
                Check(!view.GetComponentsInChildren<RectTransform>().Any(t => t.name=="unlock_"+frontier.id), "unlock removes info");
                Check(original.SequenceEqual(Read(view.groundTexture)), "unlock and occupation never recolor ground");
                var poor = model.cells.Values.First(c => c.camp==BattleSceneCamp.FRIENDLY && model.unlocks.IsRevealed(c));
                typeof(BattleCellVO).GetMethod("SetUnlockConfig",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(poor,new object[]{CfgManager.tables.CellUnlockObj.Get(5)});
                Check(!BattleControl.ins.TryUnlockCell(poor.id,out reason) && reason=="货币不足", "insufficient funds rejected");
                await Task.Delay(200);
                var cost = view.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name=="cost" && t.transform.parent.name=="unlock_"+poor.id);
                cost.ForceMeshUpdate();
                ScreenCapture.CaptureScreenshot("Library/occupation-cost-debug.png");
                await Task.Delay(200);
                Check(cost.color.r>.9f && cost.color.g<.5f && !cost.isTextOverflowing, "red 250: color="+cost.color+" overflow="+cost.isTextOverflowing+" preferred="+cost.preferredWidth+"x"+cost.preferredHeight+" rect="+cost.rectTransform.rect+" text="+cost.text);
                if(id==1002) {
                    ScreenCapture.CaptureScreenshot("Library/occupation-runtime-1002.png");
                    await Task.Delay(200);
                }
                panel.Close();
                Check(BattleControl.ins.model==null, "close clears model/resources");
                PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
                await Task.Delay(100);
            }
        } catch(Exception e) {
            error=e.ToString();
            Debug.LogException(e);
        } finally {
            Application.runInBackground=background;
            SessionState.EraseString(Key);
            File.WriteAllText("Library/battle-occupation-runtime-result.json", JsonUtility.ToJson(new Result {requestId=requestId,status=error==null?"passed":"failed",error=error},true));
            EditorApplication.isPlaying=false;
        }
    }
    [Serializable] private class Result { public string requestId; public string status; public string error; }
}



