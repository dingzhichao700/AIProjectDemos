using System.Threading.Tasks;
using UnityEngine.Scripting;

/// <summary>
/// 初始化玩家设置并启动登录阶段的界面流程。
/// </summary>
[Preserve]
public class LoginEntry : IGameModuleEntry {

    /// <summary>
    /// 读取并应用设置，然后打开开场界面。
    /// </summary>
    /// <remarks>底座必须先完成 Timer、UI 根节点及资源系统初始化。</remarks>
    [Preserve]
    public Task InitializeAsync() {
        UnityEngine.Debug.Log("[Login] 业务程序集入口已启动。");
        PersistentDataControl.ins.ReadUserSetting();
        SettingControl.ins.ApplyAllSettings();
        PanelMgr.ins.OpenPanel(UIEnum.OPENING_PANEL);
        return Task.CompletedTask;
    }

}
