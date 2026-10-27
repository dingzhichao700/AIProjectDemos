using System;
using System.Collections.Generic;

/// <summary>
/// 图层测试模块专属GM指令。
/// </summary>
/// <remarks>跟随正式主界面注册和注销，不进入通用GM模块或模板基线。</remarks>
public static class LayerEffectTestGM {

    public const string Command = "openLayerEffectTest";

    private static bool initialized;

    public static void Init() {
        if (initialized) {
            return;
        }
        GMControl.ins.RegisterCommand(Command, Open);
        initialized = true;
    }

    public static void Shutdown() {
        if (!initialized) {
            return;
        }
        GMControl.ins.UnregisterCommand(Command);
        PanelMgr.ins.ClosePanelByType(UIEnum.LAYER_EFFECT_TEST_PANEL);
        initialized = false;
    }

    /**无参数测试入口，关闭GM覆盖层后走标准异步窗口加载流程。*/
    private static bool Open(IReadOnlyList<string> values) {
        if (values.Count != 0) {
            throw new ArgumentException("打开图层效果测试界面不需要参数。");
        }
        PanelMgr.ins.ClosePanelByType(UIEnum.GM_PANEL);
        PanelMgr.ins.OpenPanel(UIEnum.LAYER_EFFECT_TEST_PANEL);
        return true;
    }

}
