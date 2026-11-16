using System;
using UnityEngine;

/// <summary>只传递场景根节点，不暴露热更窗口或业务类型。</summary>
public sealed class UiRootContext {
    public RectTransform ViewportRoot { get; }
    public RectTransform SafeAreaRoot { get; }
    public RectTransform PoolRoot { get; }
    public Vector2 DesignSize { get; }
    public UiRootContext(RectTransform viewport, RectTransform safeArea, RectTransform pool, Vector2 design) {
        if (viewport == null || safeArea == null || pool == null) throw new InvalidOperationException("UI根节点未就绪。");
        ViewportRoot = viewport; SafeAreaRoot = safeArea; PoolRoot = pool; DesignSize = design;
    }
}

/// <summary>稳定宿主契约；实现由登录前装载的HotUpdateCore提供。</summary>
public interface IUiRuntime {
    void Initialize(UiRootContext roots);
    void Tick();
    void OnApplicationFocus(bool focused);
}

public static class UiRuntimeHost {
    public const int ContractVersion = 1;
    public static UiRootContext Roots { get; private set; }
    private static IUiRuntime runtime;
    private static bool hasFocusObservation;
    private static bool focused;
    public static bool IsReady => runtime != null;
    public static void ConfigureRoots(RectTransform viewport, RectTransform safeArea, RectTransform pool, Vector2 design) {
        if (Roots != null) throw new InvalidOperationException("UI根节点不可重复配置。");
        Roots = new UiRootContext(viewport, safeArea, pool, design);
    }
    public static void Install(IUiRuntime value) {
        if (value == null || Roots == null) throw new InvalidOperationException("UI服务或宿主未就绪。");
        if (runtime != null) throw new InvalidOperationException("UI服务不可重复安装。");
        value.Initialize(Roots);
        runtime = value;
        if (hasFocusObservation) value.OnApplicationFocus(focused);
    }
    public static void Tick() { runtime?.Tick(); }
    public static void NotifyFocus(bool value) { focused = value; hasFocusObservation = true; runtime?.OnApplicationFocus(value); }
}
