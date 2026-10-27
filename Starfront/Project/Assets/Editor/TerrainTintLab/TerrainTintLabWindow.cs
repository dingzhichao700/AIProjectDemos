using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 原图、灰度与六边形红蓝染色的独立编辑器实验窗口。
/// </summary>
/// <remarks>参数和格子画笔仅影响窗口预览，可导出PNG，不写入战斗场景。</remarks>
public sealed class TerrainTintLabWindow : EditorWindow {

    private static readonly string[] Modes = { "原图", "灰度", "正片叠底", "叠加", "柔光", "滤色", "线性减淡（添加）" };

    private static readonly string[] Layouts = { "上下红蓝分界", "全部红色", "全部蓝色", "红蓝交替" };

    [SerializeField] private Texture2D source;

    [SerializeField] private TerrainTintLabSettings settings = new TerrainTintLabSettings();

    [SerializeField] private List<Vector3Int> painted = new List<Vector3Int>();

    [SerializeField] private int brush = 1;

    [SerializeField] private float previewZoom = .4f;

    private Vector2 previewScroll;

    private Vector2 controlsScroll;

    private TerrainTintLabRenderer renderer;

    private RenderTexture grayscale;

    private RenderTexture tinted;

    private bool dirty = true;

    private string error;

    [MenuItem("Tools/Starfront/美术/地表染色实验")]
    public static void Open() {
        var window = GetWindow<TerrainTintLabWindow>("地表染色实验");
        window.minSize = new Vector2(1060, 720);
        if (window.position.width < 1060) {
            window.position = new Rect(60, 60, 1300, 860);
        }
        window.Show();
        window.Focus();
    }

    private void OnEnable() {
        if (source == null) {
            source = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTintLabRenderer.SourcePath);
        }
        dirty = true;
    }

    private void OnDisable() {
        TerrainTintLabRenderer.Release(grayscale);
        TerrainTintLabRenderer.Release(tinted);
        grayscale = tinted = null;
        renderer?.Dispose();
        renderer = null;
    }

    private void OnGUI() {
        using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280))) {
                controlsScroll = EditorGUILayout.BeginScrollView(controlsScroll);
                DrawControls();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope()) {
                EditorGUILayout.LabelField("同一灰度地表 · 红蓝六边形染色", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("透明处显示棋盘；右侧结果可直接用画笔逐格切换颜色。", EditorStyles.miniLabel);
                RenderIfNeeded();
                if (!string.IsNullOrEmpty(error)) {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                if (source != null && grayscale != null && tinted != null) {
                    previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
                    using (new EditorGUILayout.HorizontalScope()) {
                        DrawPreview("原图", source, false);
                        DrawPreview("去色后的灰度", grayscale, false);
                        DrawPreview("染色结果 · " + Modes[settings.mode], tinted, true);
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }
    }

    private void DrawControls() {
        EditorGUILayout.LabelField("地表染色实验", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        source = (Texture2D)EditorGUILayout.ObjectField("地表图片", source, typeof(Texture2D), false);
        settings.mode = EditorGUILayout.Popup("结果模式", settings.mode, Modes);
        settings.layout = EditorGUILayout.Popup("配色布局", settings.layout, Layouts);
        settings.red = EditorGUILayout.ColorField("红方颜色", settings.red);
        settings.blue = EditorGUILayout.ColorField("蓝方颜色", settings.blue);
        settings.strength = EditorGUILayout.Slider("染色强度", settings.strength, 0, 1);
        settings.brightness = EditorGUILayout.Slider("灰度亮度", settings.brightness, .25f, 2.5f);
        settings.contrast = EditorGUILayout.Slider("灰度对比度", settings.contrast, .25f, 2.5f);
        settings.boundary = EditorGUILayout.Slider("红蓝分界", settings.boundary, 0, 1);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("六边形对齐", EditorStyles.boldLabel);
        settings.hexSize.x = Mathf.Max(8, EditorGUILayout.FloatField("格子宽度", settings.hexSize.x));
        settings.hexSize.y = Mathf.Max(8, EditorGUILayout.FloatField("格子高度", settings.hexSize.y));
        settings.origin = EditorGUILayout.Vector2Field("网格基点（左上坐标）", settings.origin);
        settings.showGrid = EditorGUILayout.Toggle("显示网格辅助线", settings.showGrid);
        if (EditorGUI.EndChangeCheck()) {
            dirty = true;
        }
        brush = EditorGUILayout.Popup("点击画笔", brush, new[] { "恢复自动配色", "涂为红色", "涂为蓝色" });
        EditorGUILayout.LabelField("已手动指定 " + painted.Count + " 格", EditorStyles.miniLabel);
        if (GUILayout.Button("清除逐格配色")) {
            painted.Clear();
            dirty = true;
        }
        EditorGUILayout.Space();
        previewZoom = EditorGUILayout.Slider("预览缩放", previewZoom, .15f, 1.25f);
        using (new EditorGUILayout.HorizontalScope()) {
            if (GUILayout.Button("适应窗口") && source != null) {
                previewZoom = Mathf.Clamp(Mathf.Min((position.width - 340) / (3 * source.width), (position.height - 90) / source.height), .15f, 1.25f);
            }
            if (GUILayout.Button("1:1")) {
                previewZoom = 1;
            }
        }
        if (GUILayout.Button("恢复实验默认值")) {
            settings = new TerrainTintLabSettings();
            painted.Clear();
            dirty = true;
        }
        using (new EditorGUI.DisabledScope(grayscale == null || tinted == null)) {
            if (GUILayout.Button("导出灰度透明PNG")) {
                Export(grayscale, "Terrain_Gray");
            }
            if (GUILayout.Button("导出染色透明PNG")) {
                Export(tinted, "Terrain_Tinted");
            }
        }
        EditorGUILayout.HelpBox("去色保留原图明暗；原图中的红蓝明度差也会保留。可用亮度、对比度和不同混合模式比较。", MessageType.Info);
        EditorGUILayout.LabelField("本窗口仅为材质实验。", EditorStyles.miniLabel);
    }

    private void RenderIfNeeded() {
        if (!dirty || source == null) {
            return;
        }
        try {
            if (renderer == null) {
                renderer = new TerrainTintLabRenderer();
            }
            renderer.SetPaint(painted);
            TerrainTintLabRenderer.Release(grayscale);
            TerrainTintLabRenderer.Release(tinted);
            grayscale = tinted = null;
            grayscale = renderer.Render(source, settings, 1);
            tinted = renderer.Render(source, settings, settings.mode);
            error = null;
        } catch (Exception exception) {
            error = exception.Message;
        }
        dirty = false;
    }

    private void DrawPreview(string label, Texture texture, bool paintable) {
        float width = texture.width * previewZoom;
        float height = texture.height * previewZoom;
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(width + 12))) {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(width));
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            EditorGUI.DrawTextureTransparent(rect, texture, ScaleMode.StretchToFill);
            var input = Event.current;
            if (paintable && input.button == 0 && (input.type == EventType.MouseDown || input.type == EventType.MouseDrag) && rect.Contains(input.mousePosition)) {
                Vector2 point = (input.mousePosition - rect.position) / previewZoom;
                var cell = TerrainTintLabRenderer.CellAt(point, settings);
                painted.RemoveAll(value => value.x == cell.x && value.y == cell.y);
                if (brush != 0) {
                    painted.Add(new Vector3Int(cell.x, cell.y, brush));
                }
                dirty = true;
                input.Use();
                Repaint();
            }
        }
    }

    private static void Export(RenderTexture texture, string name) {
        string path = EditorUtility.SaveFilePanel("导出实验PNG", "", name, "png");
        if (string.IsNullOrEmpty(path)) {
            return;
        }
        var image = TerrainTintLabRenderer.Readback(texture);
        try {
            File.WriteAllBytes(path, image.EncodeToPNG());
        } finally {
            DestroyImmediate(image);
        }
    }

}
