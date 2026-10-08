using TMPro;

/// <summary>
/// 性能监视视图
/// </summary>
public class PerformanceMonitorView : BasePanel {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtInfo;
    /******************* UIComponent Define finish ************************/

    public PerformanceMonitorView() {
        layer = PanelLayer.SCALE_TOP_MONITOR;
    }

    override public void OnOpen() {
        AddLis();
    }

    public void AddLis() {
        RookieEngine.timer.Loop(this, 500, UpdateMemShow);
    }

    /**更新显存占用显示*/
    public void UpdateMemShow() {
        string content = "";
        //内存
        content += $"GFX Mem: {UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / 1024 / 1024} mb";
        txtInfo.text = content;
    }

    private void RemoveLis() {
        RookieEngine.timer.Clear(this, UpdateMemShow);
    }

    override public void OnClose() {
        RemoveLis();
    }

}
