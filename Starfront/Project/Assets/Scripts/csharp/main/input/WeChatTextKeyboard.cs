using System;

#if (UNITY_WEBGL && GAMEFRAMEWORK_WECHAT) || UNITY_EDITOR
using WeChatWASM;
#endif

/// <summary>微信原生输入法适配；不修改SDK默认关闭Unity原生输入的性能策略。</summary>
public sealed class WeChatTextKeyboard : ITextKeyboard {
#if (UNITY_WEBGL && GAMEFRAMEWORK_WECHAT) || UNITY_EDITOR
    private Action<OnKeyboardInputListenerResult> input;
    private Action<OnKeyboardInputListenerResult> confirm;
    private Action<OnKeyboardInputListenerResult> complete;
#endif

    public bool IsSupported {
        get {
#if UNITY_WEBGL && GAMEFRAMEWORK_WECHAT && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public void Show(string value, int limit, bool multiline, Action<string> changed, Action<string> confirmed, Action<string> completed, Action<string> failed) {
#if (UNITY_WEBGL && GAMEFRAMEWORK_WECHAT) || UNITY_EDITOR
        input = result => changed(result.value);
        confirm = result => confirmed(result.value);
        complete = result => completed(result.value);
        WX.OnKeyboardInput(input);
        WX.OnKeyboardConfirm(confirm);
        WX.OnKeyboardComplete(complete);
        WX.ShowKeyboard(new ShowKeyboardOption {
            defaultValue = value,
            maxLength = limit > 0 ? limit : 524288,
            multiple = multiline,
            confirmHold = false,
            confirmType = "done",
            fail = result => failed(result.errMsg)
        });
#endif
    }

    public void Hide() {
#if (UNITY_WEBGL && GAMEFRAMEWORK_WECHAT) || UNITY_EDITOR
        if (input != null) {
            WX.OffKeyboardInput(input);
            WX.OffKeyboardConfirm(confirm);
            WX.OffKeyboardComplete(complete);
            input = confirm = complete = null;
            WX.HideKeyboard(new HideKeyboardOption());
        }
#endif
    }
}
