using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 管理通用 GM 指令、快捷键与场景计时调试。
/// </summary>
/// <remarks>项目业务通过注册接口扩展指令，模板不引用具体游戏模块。</remarks>
public sealed class GMControl {

    /**模块单例*/
    public static GMControl ins { get; } = new GMControl();

    /**可调整的计时器，不包含界面使用的 COMMON*/
    private readonly TimerType[] timerTypes = { TimerType.SCENE, TimerType.PLAYER, TimerType.ENEMY };

    /**沿用原 GM 的速度档位*/
    private readonly float[] speedSteps = { 0.01f, 0.025f, 0.05f, 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f, 3f, 4f, 8f };

    /**指令内容与处理函数映射*/
    private readonly Dictionary<string, Func<IReadOnlyList<string>, bool>> commands = new Dictionary<string, Func<IReadOnlyList<string>, bool>>(StringComparer.Ordinal);

    /**退出模块前恢复原有倍率*/
    private readonly float[] initialScales = new float[3];

    /**暂停前各计时器的独立倍率*/
    private readonly float[] resumeScales = new float[3];

    /**快捷键是否已启用*/
    private bool initialized;

    /**当前选择的速度档位*/
    public float timeScale { get; private set; } = 1f;

    /**是否由 GM 暂停*/
    public bool isPause { get; private set; }

    /**注册模板通用指令*/
    private GMControl() {
        RegisterCommand("log", values => {
            RequireCount(values, 1);
            Debug.Log(values[0]);
            return true;
        });
        RegisterCommand("timeScale", values => {
            RequireCount(values, 1);
            SetTimeScale(float.Parse(values[0], CultureInfo.InvariantCulture));
            return true;
        });
        RegisterCommand("timePause", values => {
            RequireCount(values, 0);
            SetPaused(true);
            return true;
        });
        RegisterCommand("timeResume", values => {
            RequireCount(values, 0);
            SetPaused(false);
            return true;
        });
        RegisterCommand("closeGM", values => {
            RequireCount(values, 0);
            PanelMgr.ins.ClosePanelByType(UIEnum.GM_PANEL);
            return true;
        });
    }

    /// <summary>
    /// 正式内容就绪后启用 GM，重复调用不会重复监听。
    /// </summary>
    public void Init() {
        if (initialized) {
            return;
        }
        for (int i = 0; i < timerTypes.Length; i++) {
            initialScales[i] = RookieEngine.GetTimer(timerTypes[i]).scale;
        }
        timeScale = initialScales[0];
        initialized = true;
        KeyBoardControl.ins.OnAnyKeyDown(OnKeyDown);
    }

    /// <summary>
    /// 退出正式内容时注销快捷键、关闭面板并恢复进入前的倍率。
    /// </summary>
    public void Shutdown() {
        if (!initialized) {
            return;
        }
        KeyBoardControl.ins.OffAnyKeyDown(OnKeyDown);
        PanelMgr.ins.ClosePanelByType(UIEnum.GM_PANEL);
        for (int i = 0; i < timerTypes.Length; i++) {
            RookieEngine.GetTimer(timerTypes[i]).scale = initialScales[i];
        }
        initialized = false;
        isPause = false;
    }

    /**打开面板，供键盘、鼠标和触摸共用*/
    public void OpenPanel() {
        if (!initialized) {
            Debug.LogError("GM 尚未初始化，请先进入正式内容。");
            return;
        }
        PanelMgr.ins.OpenPanel(UIEnum.GM_PANEL);
    }

    /// <summary>
    /// 注册业务指令；重复名称直接报错，避免静默覆盖。
    /// </summary>
    /// <param name="content">与 GMResource.content 一致的指令名。</param>
    /// <param name="handler">接收参数并返回是否成功的处理函数。</param>
    public void RegisterCommand(string content, Func<IReadOnlyList<string>, bool> handler) {
        if (string.IsNullOrWhiteSpace(content) || handler == null) {
            throw new ArgumentException("GM 指令名和处理函数不能为空。");
        }
        commands.Add(content, handler);
    }

    /**注销业务模块拥有的指令*/
    public void UnregisterCommand(string content) {
        commands.Remove(content);
    }

    /// <summary>
    /// 执行配置指令，参数错误与业务异常输出具体原因。
    /// </summary>
    /// <param name="content">已注册的指令名。</param>
    /// <param name="param">用户填写的参数。</param>
    public bool ExecuteGM(string content, List<string> param = null) {
        if (!initialized) {
            Debug.LogError("GM 尚未初始化。");
            return false;
        }
        if (string.IsNullOrWhiteSpace(content) || !commands.TryGetValue(content, out var handler)) {
            Debug.LogError($"GM 指令未注册：{content}");
            return false;
        }
        try {
            bool result = handler(param ?? new List<string>());
            if (result) {
                Debug.Log($"GM 执行成功：{content}");
            } else {
                Debug.LogError($"GM 执行失败：{content}");
            }
            return result;
        } catch (Exception exception) {
            Debug.LogError($"GM 执行失败：{content}，{exception}");
            return false;
        }
    }

    /**参数框输入期间不响应时间调试快捷键*/
    private void OnKeyDown(KeyCode key) {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.GetComponentInParent<TMP_InputField>() != null) {
            return;
        }
        switch (key) {
            case KeyCode.Home:
                OpenPanel();
                break;
            case KeyCode.Minus:
                ChangeSpeed(false);
                break;
            case KeyCode.Equals:
                ChangeSpeed(true);
                break;
            case KeyCode.P:
                SetPaused(!isPause);
                break;
        }
    }

    /**切换相邻速度档位*/
    private void ChangeSpeed(bool increase) {
        for (int offset = 0; offset < speedSteps.Length; offset++) {
            int index = increase ? offset : speedSteps.Length - 1 - offset;
            float speed = speedSteps[index];
            if ((increase && speed > timeScale) || (!increase && speed < timeScale)) {
                SetTimeScale(speed);
                break;
            }
        }
    }

    /**调整场景相关计时器倍率，暂停时只更新恢复值*/
    private void SetTimeScale(float value) {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) {
            throw new ArgumentOutOfRangeException(nameof(value), "倍率必须是大于 0 的有限数值。");
        }
        timeScale = value;
        for (int i = 0; i < timerTypes.Length; i++) {
            resumeScales[i] = value;
            if (!isPause) {
                RookieEngine.GetTimer(timerTypes[i]).scale = value;
            }
        }
    }

    /**暂停或恢复各计时器原有倍率，界面计时器继续运行*/
    private void SetPaused(bool paused) {
        if (isPause == paused) {
            return;
        }
        for (int i = 0; i < timerTypes.Length; i++) {
            Timer timer = RookieEngine.GetTimer(timerTypes[i]);
            if (paused) {
                resumeScales[i] = timer.scale;
                timer.scale = 0f;
            } else {
                timer.scale = resumeScales[i];
            }
        }
        isPause = paused;
    }

    /**检查通用指令参数数量*/
    private static void RequireCount(IReadOnlyList<string> values, int count) {
        if (values.Count != count) {
            throw new ArgumentException($"需要 {count} 个参数，实际为 {values.Count}。");
        }
    }

}
