using System;

/// <summary>本页显示输入订阅口；由真实业务适配器映射，不负责匹配或加载。</summary>
/// <remarks>每个实例只服务一个请求，所有通知在Unity主线程发出；重试绑定新实例。</remarks>
public interface IPlayerMatchingPresentationSource {
    PlayerMatchingPresentation Current { get; }
    event Action<PlayerMatchingPresentation> Changed;
    void Dispatch(PlayerMatchingUiAction action, string requestId);
}
