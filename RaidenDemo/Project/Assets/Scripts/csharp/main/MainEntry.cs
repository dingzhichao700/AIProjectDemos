using System.Threading.Tasks;
using UnityEngine.Scripting;

/// <summary>
/// 正式内容加载完成后的业务入口。
/// </summary>
[Preserve]
public class MainEntry : IGameModuleEntry {

    /// <summary>
    /// 确认配置就绪并打开正式内容主界面。
    /// </summary>
    [Preserve]
    public Task InitializeAsync() {
        _ = CfgManager.tables;
        PanelMgr.ins.OpenPanel(UIEnum.HOME_PANEL);
        return Task.CompletedTask;
    }
}
