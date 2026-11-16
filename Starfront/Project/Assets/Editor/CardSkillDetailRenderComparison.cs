#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>只读对照：正常Game入口/屏幕采集，与既有隔离相机采集使用相同开关序列。</summary>
public static partial class CardCultivationRuntimeValidation {
    [Serializable] private sealed class RenderProbe {
        public string mode, phase, skill, source, screenshot;
        public int cycle, frame, panelId, width, height;
        public float time;
        public float[] panelBounds;
        public ProbeGraphic[] graphics;
    }
    [Serializable] private sealed class ProbeGraphic {
        public string name, text, material, shader, path;
        public int id, depth, materialCount, vertices, characters;
        public bool enabled, cull, rectClipping;
        public float alpha, inheritedAlpha;
        public float[] bounds;
    }

    private static string ProgressFileDigest(string name) {
        string path = Path.Combine(Application.persistentDataPath, name);
        if (!File.Exists(path)) {
            return "absent";
        }
        using (var sha = SHA256.Create()) {
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
    }

    private static async Task RunNormalWindowComparison(string id) {
        bool background = Application.runInBackground;
        PlayerProgressControl previous = null;
        bool installed = false;
        string[] names = { "playerProgress.json", "cardCollection.json" };
        string[] before = names.Select(ProgressFileDigest).ToArray();
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        checks.Clear();
        runtimeError = null;
        try {
            var engine = UnityEngine.Object.FindObjectOfType<RookieEngine>();
            Check(engine != null, "正常GameEntrance场景RookieEngine运行，不替换UI根节点或时钟");
            var gameType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameType).Show();
            await CfgManager.EnsureLoadedAsync();
            int writes = 0;
            var save = new PlayerProgressSaveVO { initialized = true };
            save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 2101, level = 6 });
            if (SessionState.GetBool(Key + ".humanSkills", false)) foreach (int humanCard in new[] {2102,2103,2104,2105}) save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry {cardId=humanCard,level=8});
            if (SessionState.GetBool(Key + ".remainingSkills", false)) foreach (int cardId in new[] {2201,2202,2203,2204,2205,2301,2302,2303,2304,2305,2306}) save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry {cardId=cardId,level=8});
            previous = PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(CfgManager.tables, save, ignored => writes++));
            installed = true;
            for (int attempt = 0; attempt < 600 && !(PanelMgr.ins.GetPanel("default/mainView/MainPanel") is MainPanel); attempt++) {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login != null && login.txt != null && login.txt.gameObject.activeInHierarchy) {
                    login.SendMessage("EnterMain", SendMessageOptions.DontRequireReceiver);
                }
                await ComparisonFrames(true, 1);
            }
            var main = PanelMgr.ins.GetPanel("default/mainView/MainPanel") as MainPanel;
            Check(main != null && main.isOpened, "正常登录加载流程到达MainPanel");
            if (!main.viewDeck.gameObject.activeSelf) {
                ComparisonClick(main.btnDeck.GetComponent<RectTransform>());
            }
            await ComparisonFrames(true, 60);
            var deck = main.viewDeck.GetComponentInChildren<DeckPresentationView>();
            Check(deck != null, "正常卡组视图存在");
            DeckCardView card = null;
            var currentField = typeof(DeckCardView).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
            var scroll = deck.library.GetComponent<ScrollRect>();
            for (int step = 0; step <= 20 && card == null; step++) {
                scroll.verticalNormalizedPosition = 1f - step / 20f;
                await ComparisonFrames(true, 4);
                card = deck.library.GetComponentsInChildren<DeckCardView>().FirstOrDefault(value => !value.formation && (currentField.GetValue(value) as DeckCardView.DisplayData)?.cardId == 2101);
            }
            Check(card != null, "正常牌库滚动找到陆战队员2101");
            ComparisonClick(card.button.GetComponent<RectTransform>());
            for (int attempt = 0; attempt < 200 && !PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId); attempt++) {
                await ComparisonFrames(true, 1);
            }
            Check(PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId), "真实牌库按钮打开培养页");
            canvas = PanelMgr.ins.viewportScale.GetComponentInParent<Canvas>().rootCanvas;
            camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (SessionState.GetBool(Key + ".remainingSkills", false)) await RunRemainingNormalEntrances(main); else if (SessionState.GetBool(Key + ".humanSkills", false)) await RunHumanNormalEntrances(main); else await RunRenderComparisonSequence(true);
            Check(writes == 0, "正常窗口用例使用隔离等级，无存档回调");
            for (int i = 0; i < names.Length; i++) {
                Check(before[i] == ProgressFileDigest(names[i]), "真实进度文件哈希不变：" + names[i]);
            }
            if (!string.IsNullOrEmpty(runtimeError)) {
                throw new InvalidOperationException(runtimeError);
            }
            WriteResult(id, null);
        } catch (Exception error) {
            WriteResult(id, error);
            Debug.LogException(error);
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType("default/mainView/MainPanel");
            if (installed) {
                PlayerProgressControl.InstallValidationSession(previous);
            }
            Application.logMessageReceived -= OnLog;
            Application.runInBackground = background;
            EditorApplication.isPlaying = false;
        }
    }

    private static async Task RunComparisonOnInstalledStage(bool normal) {
        int writes = 0;
        var save = new PlayerProgressSaveVO { initialized = true };
        save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 2101, level = 6 });
        var previous = PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(CfgManager.tables, save, ignored => writes++));
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = 2101, orderedCardIds = new[] { 2101 }, owner = canvas, source = "render-comparison-isolated" });
            await CultivationOpened();
            await RunRenderComparisonSequence(false);
            Check(writes == 0, "隔离相机对照只读6级进度，无存档回调");
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PlayerProgressControl.InstallValidationSession(previous);
        }
    }

    private static async Task ComparisonFrames(bool normal, int count) {
        if (!normal) {
            await Frames(count);
            return;
        }
        for (int frame = 0; frame < count; frame++) {
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(16);
        }
    }

    private static void ComparisonClick(RectTransform rect) {
        // 渲染对照统一触发真实Button事件，排除GameView尺寸和滚动裁切的命中差异。
        var button = rect.GetComponent<Button>();
        if (button == null || !button.gameObject.activeInHierarchy) {
            throw new InvalidOperationException("Comparison button unavailable: " + rect.name);
        }
        button.onClick.Invoke();
    }

    private static float[] ProbeBounds(RectTransform rect, bool normal) {
        var rootCanvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var points = corners.Select(value => RectTransformUtility.WorldToScreenPoint(eventCamera, value)).ToArray();
        float x = points.Min(value => value.x);
        float y = points.Min(value => value.y);
        float right = points.Max(value => value.x);
        float top = points.Max(value => value.y);
        int height = normal ? Screen.height : target.height;
        return new[] { x, height - top, right - x, top - y };
    }

    private static RenderProbe ProbePanel(CardSkillDetailPanel detail, bool normal, int cycle, string skill, string phase, string screenshot) {
        var graphics = new List<ProbeGraphic>();
        foreach (var graphic in detail.GetComponentsInChildren<MaskableGraphic>()) {
            var text = graphic as TextMeshProUGUI;
            if (text == null && graphic.name != "imgLock") {
                continue;
            }
            var renderer = graphic.canvasRenderer;
            Material material = renderer.materialCount > 0 ? renderer.GetMaterial(0) : null;
            graphics.Add(new ProbeGraphic {
                name = graphic.name, path = AnimationUtility.CalculateTransformPath(graphic.transform, detail.transform),
                text = text == null ? null : text.text, id = graphic.GetInstanceID(), enabled = graphic.enabled,
                depth = renderer.absoluteDepth, alpha = renderer.GetAlpha(), inheritedAlpha = renderer.GetInheritedAlpha(),
                cull = renderer.cull, rectClipping = renderer.hasRectClipping, materialCount = renderer.materialCount,
                material = material == null ? null : material.name, shader = material == null ? null : material.shader.name,
                vertices = text == null ? -1 : text.mesh.vertexCount, characters = text == null ? -1 : text.textInfo.characterCount,
                bounds = ProbeBounds(graphic.rectTransform, normal)
            });
        }
        return new RenderProbe {
            mode = normal ? "normal-window" : "isolated-camera", phase = phase, skill = skill, source = "2101-level6-readonly",
            cycle = cycle, frame = Time.frameCount, time = Time.unscaledTime, panelId = detail.GetInstanceID(),
            width = normal ? Screen.width : target.width, height = normal ? Screen.height : target.height,
            screenshot = screenshot, panelBounds = ProbeBounds(detail.boxWindow, normal), graphics = graphics.ToArray()
        };
    }

    private static async Task RunRenderComparisonSequence(bool normal) {
        var source = PanelMgr.ins.GetPanel(CardCultivationControl.PanelId) as CardCultivationPanel;
        Check(source != null && source.CurrentCardId == 2101, "对照来源卡牌为2101");
        int[] ids = { 210102, 210103, 210104 };
        string[] labels = { "被动", "主动", "大招" };
        var itemField = typeof(CardCultivationSkillItem).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
        string evidence = SessionState.GetString(Key + ".evidence", "");
        Directory.CreateDirectory(evidence);
        for (int cycle = 0; cycle < 12; cycle++) {
            for (int index = 0; index < ids.Length; index++) {
                var item = source.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(value => (itemField.GetValue(value) as CardSkillStateVO)?.skillId == ids[index]);
                ComparisonClick(item.btnSkill.GetComponent<RectTransform>());
                for (int attempt = 0; attempt < 200 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); attempt++) {
                    await ComparisonFrames(normal, 1);
                }
                await Task.Delay(350);
                await ComparisonFrames(normal, 8);
                var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
                Check(detail != null && detail.isOpened && detail.txtTitle.text == (index == 0 ? "防暴护盾" : index == 1 ? "强化剂" : "弹群"), "cycle" + cycle + " " + labels[index] + "正常打开");
                var rows = detail.listStages.GetComponentsInChildren<CardSkillStageItem>().Where(row => row.gameObject.activeInHierarchy).ToArray();
                var query = CardSkillQuery.Read(CfgManager.tables, 2101, 6, index == 0 ? cfg.SkillDisplaySlot.Passive : index == 1 ? cfg.SkillDisplaySlot.Active : cfg.SkillDisplaySlot.Ultimate, ids[index]);
                Check(rows.Length == query.ranks.Count && rows.All(row => query.ranks.Any(rank => row.txtRank.text == rank.rank + "阶" && row.txtEffect.text == rank.effect && row.imgLock.enabled == !rank.unlocked)), "cycle" + cycle + " " + labels[index] + "文字/锁状态吻合查询");
                string stem = "cycle-" + cycle.ToString("D2") + "-skill-" + ids[index];
                string path = Path.Combine(evidence, stem + ".png");
                File.WriteAllText(Path.Combine(evidence, stem + "-before.json"), JsonUtility.ToJson(ProbePanel(detail, normal, cycle, ids[index].ToString(), "before", path), true));
                if (normal) {
                    ScreenCapture.CaptureScreenshot(path);
                    long lastLength = -1;
                    for (int wait = 0; wait < 200; wait++) {
                        await ComparisonFrames(true, 1);
                        if (File.Exists(path)) {
                            long length = new FileInfo(path).Length;
                            if (length > 0 && length == lastLength) {
                                break;
                            }
                            lastLength = length;
                        }
                    }
                    Check(File.Exists(path) && new FileInfo(path).Length > 0, "正常屏幕截图完成 " + stem);
                } else {
                    Capture(stem + ".png");
                }
                File.WriteAllText(Path.Combine(evidence, stem + "-after.json"), JsonUtility.ToJson(ProbePanel(detail, normal, cycle, ids[index].ToString(), "after", path), true));
                ComparisonClick(detail.btnClose.GetComponent<RectTransform>());
                await Task.Delay(300);
                await ComparisonFrames(normal, 4);
                Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && source.CurrentCardId == 2101, "cycle" + cycle + " " + labels[index] + "关闭回原卡");
            }
        }
    }
}
#endif
