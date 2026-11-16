using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>复用原有两页隔离内存入口，只增加短点射检查及开发预览。</summary>
public static partial class MarineIdleIntegrationReview {
    [MenuItem("Tools/开发验收/陆战队员短点射/打开培养预览")]
    public static void OpenFireReview() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || File.Exists(Pending)) {
            Debug.LogWarning("请在Edit空闲状态打开短点射预览。");
            return;
        }
        var request = new Request { requestId = "marine-fire-user-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), processId = System.Diagnostics.Process.GetCurrentProcess().Id, action = "fire-review", evidenceDirectory = Path.GetFullPath("Library/AIUI/MarineFireUserReview") };
        string temporary = Pending + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(request));
        File.Move(temporary, Pending);
    }

    private static async Task RunFire(string id, DeckPresentationView deck, Image formation) {
        string action = SessionState.GetString(Key + ".action", "");
        if (action == "fire-review") {
            deck.RequestCardInfo(2101);
            var previewPanel = await CultivationOpened(2101);
            await previewPanel.imgPortrait.GetComponent<CardArtImage>().AnimationLoading;
            var controls = MarineBurstPreviewControls.Install();
            Check(controls != null && !MarineBurstPreviewControls.ToolbarVisible, "开发控制组件存在但顶部操作条默认隐藏");
            target.Release();
            UnityEngine.Object.Destroy(target);
            target = null;
            camera.targetTexture = null;
            var game = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(game).Show();
            EditorWindow.GetWindow(game).Focus();
            var previewSkin = previewPanel.imgPortrait.GetComponent<CardArtIdleSkin>();
            previewSkin.AutomaticSpecialEnabled = false; previewSkin.StopSpecial();
            Check(await previewSkin.PlayBurstAsync(), "培养实际加载新帧火光");
            double previewDeadline = EditorApplication.timeSinceStartup + 8;
            while ((previewSkin.Burst == null || previewSkin.Burst.MuzzleFlash == null || previewSkin.Burst.MuzzleFlash.FrameIndex != 1) && EditorApplication.timeSinceStartup < previewDeadline) { await Frames(1); }
            Check(previewSkin.Burst != null && previewSkin.Burst.MuzzleFlash != null && previewSkin.Burst.MuzzleFlash.FrameIndex == 1, "可见预览实际到达帧动画峰值");
            previewSkin.Burst.Rig.paused = true; previewSkin.Rig.paused = true;
            if (!string.IsNullOrEmpty(runtimeError)) { throw new InvalidOperationException(runtimeError); }
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Key + ".out", ""), "preview-game-view.png"));
            await Frames(8);
            previewSkin.Burst.Rig.paused = false; previewSkin.Rig.paused = false;
            controls.EnableAutomatic(); previewSkin.AutomaticSpecialEnabled = true;
            double autoDeadline = EditorApplication.timeSinceStartup + 22;
            while (previewSkin.Burst != null && EditorApplication.timeSinceStartup < autoDeadline) { await Frames(1); }
            for (int cycle = 0; cycle < 2; cycle++) {
                while (previewSkin.Burst == null && EditorApplication.timeSinceStartup < autoDeadline) { await Frames(1); }
                Check(previewSkin.Burst != null && previewSkin.AutomaticSpecialEnabled, "隐藏条后自动进入攻击周期=" + cycle);
                while (previewSkin.Burst != null && EditorApplication.timeSinceStartup < autoDeadline) { await Frames(1); }
                Check(previewSkin.Burst == null && previewSkin.Rig != null, "隐藏条后自动回空闲周期=" + cycle);
            }
            Check(!MarineBurstPreviewControls.ToolbarVisible, "两轮后顶部控制条仍隐藏");
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Key + ".out", ""), "idle-without-toolbar.png")); await Frames(6);
            WriteResult(id, "ready-for-user", null);
            return;
        }
        Check(!await formation.GetComponent<CardArtIdleSkin>().PlayBurstAsync(), "阵容拒绝点射并保持空闲");
        deck.RequestCardInfo(2101);
        var panel = await CultivationOpened(2101);
        await CheckMountedFlash(panel.imgPortrait);
        await CheckBurst(panel.imgPortrait, "cultivation-fire");
        await CheckBurstUI(panel.imgPortrait);
        var formationSkin = formation.GetComponent<CardArtIdleSkin>();
        var portraitSkin = panel.imgPortrait.GetComponent<CardArtIdleSkin>();
        Check(!await formationSkin.PlayBurstAsync() && await portraitSkin.PlayBurstAsync(), "培养点射且阵容仍空闲");
        await Frames(3);
        Check(MarineBurstGraphic.ActiveLeases == 1, "只有培养点射持有资源");
        panel.btnNext.onClick.Invoke();
        panel = await CultivationOpened(2102);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Burst == null, "切到其他角色无射击残留");
        Check(MarineBurstGraphic.ActiveLeases <= 1, "切卡释放培养点射持有");
        panel.btnPrevious.onClick.Invoke();
        panel = await CultivationOpened(2101);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig != null, "切回陆战队员保留R2待机");
        var image = panel.imgPortrait.GetComponent<CardArtImage>();
        var pending = panel.imgPortrait.GetComponent<CardArtIdleSkin>().PlayBurstAsync();
        image.Clear();
        await pending;
        await Frames(4);
        Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>().Burst == null, "加载途中关闭不回填射击实例");
        await image.BindAsync(2101, CardArtImage.Usage.Portrait);
        await Frames(5);
        await panel.imgPortrait.GetComponent<CardArtIdleSkin>().PlayBurstAsync();
        panel.btnReturn.onClick.Invoke();
        await Frames(15);
        Check(formationSkin.Rig != null, "关闭培养后阵容待机仍有效");
        PanelMgr.ins.ClosePanelByType("default/mainView/MainPanel");
        await Frames(15);
        Check(UnityEngine.Object.FindObjectsOfType<MountedFrameGraphic>().Length == 0 && !ResourceManager.HasLoadedUnpackImage(MarineBurstGraphic.FlashAddress + ".png") && MarineBurstGraphic.ActiveLeases == 0 && CardArtIdleSkin.ActiveLeases == 0 && UnityEngine.Object.FindObjectsOfType<MarineBurstRig>().Length == 0, "全部关闭射击与待机资源归零");
        Check(writes == 0, "无真实存档写入");
        if (!string.IsNullOrEmpty(runtimeError)) { throw new InvalidOperationException(runtimeError); }
        WriteResult(id, "passed", null);
        EditorApplication.isPlaying = false;
    }


    private static async Task CheckBurstUI(Image image) {
        var skin = image.GetComponent<CardArtIdleSkin>();
        Check(await skin.PlayBurstAsync(), "培养分层UI检查启动");
        for (int i = 0; i < 10 && skin.Burst != null && !skin.Burst.IsReady; i++) { await Frames(1); }
        Check(skin.Burst != null && skin.Burst.IsReady, "培养分层UI就绪");
        var burst = skin.Burst;
        burst.Rig.paused = true;
        skin.Rig.paused = true;
        await Frames(2);
        Check(burst.canvasRenderer.GetMesh().vertexCount >= 1146 && image.canvasRenderer.GetMesh().vertexCount == 0, "四层合批绘制且原Image不叠画");
        var group = image.gameObject.AddComponent<CanvasGroup>();
        group.alpha = .35f;
        await Frames(2);
        Check(Math.Abs(burst.canvasRenderer.GetInheritedAlpha() - .35f) < .02, "分层射击继承CanvasGroup透明");
        Capture("burst-ui-alpha.png");
        group.alpha = 1;
        UnityEngine.Object.Destroy(group);
        var testMask = new GameObject("burstMaskProbe", typeof(RectTransform), typeof(Image), typeof(Mask));
        var parent = image.transform.parent;
        int index = image.transform.GetSiblingIndex();
        testMask.transform.SetParent(parent, false);
        var mask = (RectTransform)testMask.transform;
        mask.position = image.rectTransform.position;
        mask.sizeDelta = new Vector2(400, 400);
        testMask.GetComponent<Mask>().showMaskGraphic = false;
        image.transform.SetParent(mask, true);
        await Frames(2);
        Check(burst.materialForRendering != null && burst.materialForRendering.GetInt("_Stencil") > 0 && burst.Rig != null, "分层射击继承Stencil遮罩");
        Capture("burst-ui-mask.png");
        image.transform.SetParent(parent, true);
        image.transform.SetSiblingIndex(index);
        UnityEngine.Object.Destroy(testMask);
        skin.Rig.paused = false;
        burst.Cancel();
        await Frames(2);
        Check(image.canvasRenderer.GetMesh().vertexCount == 651, "透明遮罩检查后原待机恢复");
    }

    private static async Task CheckBurst(Image image, string label) {
        await Frames(12);
        var skin = image.GetComponent<CardArtIdleSkin>();
        skin.AutomaticSpecialEnabled=false;skin.StopSpecial();await Frames(2);
        var idle = skin.Rig;
        Check(Mathf.Abs(idle.pelvisDegrees - 2.4f) < .0001f && Mathf.Abs(idle.chestLift - .048f) < .0001f, label + "保留已认可R2幅度");
        Vector2 size = image.rectTransform.sizeDelta;
        Vector3 anchor = image.rectTransform.position;
        Check(await skin.PlayBurstAsync(), label + "短点射加载成功");
        for (int i = 0; i < 12 && skin.Burst != null && !skin.Burst.IsReady; i++) { await Frames(1); }
        Check(skin.Burst != null && skin.Burst.IsReady, label + "四层原生蒙皮有效");
        var rig = skin.Burst.Rig;
        var first = rig.layers[0].GetDeformedVertexPositionData().First();
        var grip = rig.weapon.localPosition;
        float maxRecoil = 0;
        float minLength = float.PositiveInfinity;
        float maxLength = 0;
        var shots = new HashSet<int>();
        bool flash = false;
        bool raisedBeforeShot = false;
        bool loweredAfterShot = false;
        float maxAim = 0;
        int frames = 0;
        double deadline = EditorApplication.timeSinceStartup + 5;
        Directory.CreateDirectory(Path.Combine(SessionState.GetString(Key + ".out", ""), label));
        while (skin.Burst != null && EditorApplication.timeSinceStartup < deadline) {
            if (skin.Burst.IsReady && rig != null) {
                if (rig.ShotIndex >= 0) { shots.Add(rig.ShotIndex); }
                maxAim = Mathf.Max(maxAim, rig.Aim);
                raisedBeforeShot |= rig.ShotIndex < 0 && rig.Aim > .3f && rig.Flash == 0;
                loweredAfterShot |= rig.Elapsed > 1.25f && rig.Aim < .5f;
                flash |= rig.Flash > 0;
                maxRecoil = Mathf.Max(maxRecoil, Vector3.Distance(grip, rig.weapon.localPosition));
                var vertices = rig.layers[0].GetDeformedVertexPositionData();
                Check(Vector3.Distance(vertices.First(), first) < .0001f, label + "脚底顶点原地固定");
                foreach (var layer in rig.layers) {
                    Check(layer.GetComponent<SpriteRenderer>().forceRenderingOff && layer.HasCurrentDeformedVertices(), label + "原生四层不重复绘制");
                    foreach (var point in layer.GetDeformedVertexPositionData()) { Check(!float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y), label + "有限顶点="); }
                }
                var gun = rig.layers[2].GetDeformedVertexPositionData().ToArray();
                float length = Vector3.Distance(gun[0], gun[10]);
                minLength = Mathf.Min(minLength, length); maxLength = Mathf.Max(maxLength, length);
                Capture(label + "/" + frames.ToString("D3") + ".png");
                frames++;
            }
            await Frames(1);
        }
        Check(skin.Burst == null, label + "单次点射完成自动回待机");
        Check(shots.Count == 3 && flash && maxRecoil > .025f, label + "三次射击枪焰与快速后坐实际发生");
        Check(raisedBeforeShot && maxAim > .95f && loweredAfterShot, label + "明显抬枪后才开火并放枪恢复");
        Check(maxLength - minLength < .0001f, label + "枪械保持刚性不拉弯");
        Check(skin.Rig == idle && image.canvasRenderer.GetMesh().vertexCount == 651, label + "原有待机实例和网格恢复");
        Check(Vector2.Distance(size, image.rectTransform.sizeDelta) < .001f && Vector3.Distance(anchor, image.rectTransform.position) < .001f, label + "尺寸锚点不变");
    }
}