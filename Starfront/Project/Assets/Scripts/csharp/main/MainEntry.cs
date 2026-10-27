using System.Threading.Tasks;
using UnityEngine.Scripting;

/// <summary>
/// 核心业务模块的模板入口。
/// </summary>
[Preserve]
public sealed class MainEntry : IGameModuleEntry {

    /// <summary>
    /// 正式内容准备完成后打开模板主界面。
    /// </summary>
    [Preserve]
    public Task InitializeAsync() {
        PanelMgr.ins.ClosePanelByType(UIEnum.OPTION_PANEL);
        PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
        return Task.CompletedTask;
    }

}
