using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>TMP输入框到平台键盘的独占会话；处理焦点切换、回收和迟到回调。</summary>
public sealed class PlatformInputFieldKeyboard : MonoBehaviour, IPointerClickHandler, ISelectHandler, IDeselectHandler {
    private static PlatformInputFieldKeyboard active;
    private TMP_InputField field;
    private ITextKeyboard driver;
    private int version;
    private bool opened;

    public static PlatformInputFieldKeyboard Install(TMP_InputField input) {
        var bridge = input.GetComponent<PlatformInputFieldKeyboard>() ?? input.gameObject.AddComponent<PlatformInputFieldKeyboard>();
        if (bridge.field == null) {
            bridge.Bind(input, new WeChatTextKeyboard());
        }
        return bridge;
    }

    /**界面可传入其他平台提供器；桌面不拦截原生编辑行为。*/
    public void Bind(TMP_InputField input, ITextKeyboard keyboard) {
        Close();
        field = input;
        driver = keyboard;
        if (driver.IsSupported) {
            field.shouldHideSoftKeyboard = true;
        }
    }

    public void OnSelect(BaseEventData data) {
        Open();
    }

    public void OnPointerClick(PointerEventData data) {
        if (data.button == PointerEventData.InputButton.Left) {
            Open();
        }
    }

    public void OnDeselect(BaseEventData data) {
        Close();
    }

    private bool IsValid(int token) {
        return this != null && opened && version == token && isActiveAndEnabled && field != null && field.IsActive() && field.interactable && !field.readOnly;
    }

    private void Open() {
        if (opened || driver == null || !driver.IsSupported || field == null || !field.IsActive() || !field.interactable || field.readOnly) {
            return;
        }
        if (active != null && active != this) {
            active.Close();
        }
        active = this;
        opened = true;
        int token = ++version;
        driver.Show(field.text, field.characterLimit, field.multiLine, value => {
            if (IsValid(token)) {
                field.text = value ?? "";
            }
        }, value => {
            if (!IsValid(token)) {
                return;
            }
            field.text = value ?? "";
            Close();
            field.onSubmit.Invoke(field.text);
        }, value => {
            if (IsValid(token)) {
                field.text = value ?? "";
                Close();
            }
        }, reason => {
            if (IsValid(token)) {
                Close();
                Debug.LogWarning("[Keyboard] " + reason);
                GlobalToast.Warn("输入法打开失败：" + reason);
            }
        });
    }

    public void Close() {
        if (!opened) {
            return;
        }
        // 先使旧回调失效，再Hide，防止系统同步Complete触发第二次提交。
        opened = false;
        version++;
        if (active == this) {
            active = null;
        }
        driver.Hide();
    }

    private void OnDisable() {
        Close();
    }

    private void OnDestroy() {
        Close();
    }
}
