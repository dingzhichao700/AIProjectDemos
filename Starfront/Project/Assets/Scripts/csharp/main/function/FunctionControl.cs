using System;

/// <summary>
/// 功能开启的统一业务入口。
/// </summary>
/// <remarks>负责初始化、条件重算、永久记录和解锁消息；不负责具体页面。</remarks>
public sealed class FunctionControl : EventDispatcher {

    private static FunctionControl instance;

    public static FunctionControl ins => instance ?? (instance = new FunctionControl());

    public FunctionModel model { get; private set; }

    private FunctionControl() {
    }

    /**允许业务宿主提供独立存档的 Model，避免测试或多存档相互污染。*/
    public FunctionControl(FunctionModel functionModel) {
        model = functionModel ?? throw new ArgumentNullException(nameof(functionModel));
    }

    /**登录配置就绪后初始化；窗口重开不会覆盖本次会话中的业务来源。*/
    public void Init() {
        if (model == null) {
            model = new FunctionModel(CfgManager.tables.FunctionObj.DataList, FunctionConst.SavePath);
        }
        RefreshUnlocks();
    }

    /**等级与关卡模块就绪时绑定，进度变化后调用 RefreshUnlocks。*/
    public void SetProgressSource(Func<int?> getPlayerLevel, Func<int, bool> isMissionCompleted) {
        Init();
        model.SetProgressSource(getPlayerLevel, isMissionCompleted);
        RefreshUnlocks();
    }

    public void RefreshUnlocks() {
        if (model == null) {
            throw new InvalidOperationException("FunctionControl 尚未初始化。");
        }
        if (model.RefreshUnlocks()) {
            Dispatch(FunctionConst.UnlockChanged);
        }
    }

    /**入口点击重新判断，不依赖可能过期的按钮外观。*/
    public bool TryEnter(int id, out string hint) {
        Init();
        if (model.IsUnlocked(id)) {
            hint = string.Empty;
            return true;
        }
        hint = model.GetLockedHint(id);
        return false;
    }

}
