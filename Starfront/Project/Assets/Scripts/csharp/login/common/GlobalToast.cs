using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Login与Main共用的滚动提示入口。
/// </summary>
/// <remarks>仅承载瞬时反馈；不替代需要选择的确认框，不处理业务或玩家存档。</remarks>
public static class GlobalToast {

    public const string PanelId = "title/common/GlobalToastPanel";

    /**配置加载前的启动提示使用3条；就绪后由常量1300管理，不另缓存业务配置。*/
    public static int MaxVisible {
        get {
            if (!CfgManager.IsInitialized) {
                return 3;
            }
            int value = ConfigValueHelper.GetInt(1300);
            if (value <= 0) {
                throw new InvalidOperationException("常量1300滚动提示同时显示条数必须为正整数");
            }
            return value;
        }
    }

    public const int MaxPending = 10;

    public const float EnterSeconds = .2f;

    public const float HoldSeconds = 1;

    public const float ExitSeconds = .35f;

    public const float Lifetime = EnterSeconds + HoldSeconds + ExitSeconds;

    /**旧提示让位的OutQuad时长，与每条自己的入退场计时独立。*/
    public const float ReflowSeconds = .18f;

    private struct Message {
        public string text;
        public bool warning;
    }

    private static readonly List<Message> pending = new List<Message>();

    private static Task loading;

    private static int generation;

    public static GlobalToastPanel Host { get; private set; }

    public static int PendingCount => pending.Count;

    public static void Show(string text) {
        Enqueue(text, false);
    }

    public static void Warn(string text) {
        Enqueue(text, true);
    }

    /**空文本忽略，换行归一为空格；每次调用独立入队，相同内容也允许逐条向上叠加。*/
    private static void Enqueue(string text, bool warning) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (pending.Count >= MaxPending) {
            Debug.LogWarning("滚动提示等待队列已满，忽略新提示");
            return;
        }
        pending.Add(new Message { text = text, warning = warning });
        if (Host != null && Host.isOpened) {
            Host.Pump();
        } else {
            _ = EnsureReadyAsync();
        }
    }

    public static bool Contains(string text, bool warning = true) {
        text = text?.Replace("\r", " ").Replace("\n", " ").Trim();
        if (Host != null && Host.Contains(text, warning)) {
            return true;
        }
        return pending.Exists(item => item.text == text && item.warning == warning);
    }

    /**合并同一资源在途请求；代次阻止清空、重置或关闭后的迟到加载重新打开提示层。*/
    public static Task EnsureReadyAsync() {
        if (Host != null && Host.isOpened) {
            return Task.CompletedTask;
        }
        if (loading == null || loading.IsCompleted) {
            loading = LoadAsync(generation);
        }
        return loading;
    }

    private static async Task LoadAsync(int version) {
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(PanelId), ResType.Prefab) });
            if (version == generation && !PanelMgr.ins.IsPanelOpen(PanelId)) {
                PanelMgr.ins.OpenPanel(PanelId);
            }
        } catch (Exception error) {
            if (version == generation) {
                pending.Clear();
                Debug.LogWarning("滚动提示资源加载失败，可重试：" + error.Message);
            }
        }
    }

    public static void Attach(GlobalToastPanel host) {
        Host = host;
    }

    public static void Detach(GlobalToastPanel host) {
        if (Host == host) {
            Host = null;
            pending.Clear();
            generation++;
            loading = null;
        }
    }

    public static bool TryTake(out string text, out bool warning) {
        text = null;
        warning = false;
        if (pending.Count == 0) {
            return false;
        }
        var message = pending[0];
        pending.RemoveAt(0);
        text = message.text;
        warning = message.warning;
        return true;
    }

    public static void Clear() {
        generation++;
        loading = null;
        pending.Clear();
        if (Host != null) {
            Host.ResetItems();
        }
    }

    /**登出或UI系统重新初始化时调用；普通页面切换只保留宿主并播完现有消息。*/
    public static void Shutdown() {
        Clear();
        if (Host != null) {
            Host.Close();
        }
    }

}
