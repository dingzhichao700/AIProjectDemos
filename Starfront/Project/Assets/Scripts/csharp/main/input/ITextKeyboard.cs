using System;

/// <summary>平台文字输入会话；界面仅接收完整文本，不依赖SDK回调类型。</summary>
public interface ITextKeyboard {
    bool IsSupported { get; }
    void Show(string value, int limit, bool multiline, Action<string> changed, Action<string> confirmed, Action<string> completed, Action<string> failed);
    void Hide();
}
