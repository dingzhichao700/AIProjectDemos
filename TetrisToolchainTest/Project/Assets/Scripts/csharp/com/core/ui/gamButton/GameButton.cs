using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 自定义按钮
/// </summary>
[RequireComponent(typeof(Button))]
public class GameButton : BaseView, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler {

    /**划过时通用缩放大小*/
    const float HOVER_SCALE = 1.05f;
    /**按下时通用缩放大小*/
    const float PRESSED_SCALE = 0.95f;
    /**缩放表现速度*/
    const float SCALE_SPEED = 20f;

    // 交互
    public bool _interactable = true;

    // 音效
    public ButtonSoundType overSoundType = ButtonSoundType.None;
    public ButtonSoundType clickSoundType = ButtonSoundType.DEFAULT_CLICK;

    // 皮肤
    public ButtonSkinType skinType = ButtonSkinType.Default;

    // 文本
    [SerializeField]
    private TextMeshProUGUI label;
    public string text;

    // 视觉表现配置（是否开启划过缩放）
    [SerializeField]
    bool enableHoverScale = true;   // Inspector 显示交给 Editor

    Image _image;
    Button _button;
    CanvasGroup _canvasGroup;
    Vector3 _normalScale;
    Vector3 _targetScale;
    bool _isPointerDown = false;
    bool _isPointerInside = false;

    [SerializeField] private float disabledColorMultiplier = 0.5f;
    private Color _defaultColor;
    private Color _disabledColor;

    void Awake() {
        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _image = GetComponent<Image>();   // 可为空：支持纯文字按钮

        if (label == null) {
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        _normalScale = transform.localScale;
        _targetScale = _normalScale;

        _defaultColor = _image.color;
        _disabledColor = _defaultColor * disabledColorMultiplier;

        if (_button != null) {
            _button.onClick.AddListener(OnButtonClickedInternal);
        }

        ApplyTextToLabel();
    }

    void OnEnable() {
        _targetScale = _normalScale;
        transform.localScale = _normalScale;
        ApplyTextToLabel();
        UpdateInteractableState();
    }

    void Update() {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            _targetScale,
            Time.deltaTime * SCALE_SPEED
        );
    }

    /** 设置按钮图片 */
    public void SetImage(string path) {
        if (_image != null) {
            UITools.SetImage(_image, path);
        }
    }

    // ============ 点击时自动调用的内部回调 ============
    void OnButtonClickedInternal() {
        PlayClickSound();
    }

    // ==================== 统一的音效播放入口 ====================

    public void PlayClickSound() {
        PlayButtonSound(clickSoundType);
    }

    void PlayOverSound() {
        PlayButtonSound(overSoundType);
    }

    void PlayButtonSound(ButtonSoundType type) {
        if (type == ButtonSoundType.None)
            return;

        int soundId = GetSoundId(type);
        if (soundId != 0) {
            AudioManager.ins.PlaySoundById(AudioBusType.HINT, soundId);
        }
    }

    int GetSoundId(ButtonSoundType type) {
        if (!_interactable)
            return AudioConst.SOUND_CLICK_DISABLE_1;

        switch (type) {
            case ButtonSoundType.DEFAULT_CLICK:
                return AudioConst.SOUND_CLICK_ENABLE_1;
            case ButtonSoundType.DEFAULT_HOVER:
                return AudioConst.SOUND_HOVER_1;
            case ButtonSoundType.ENSURE:
                return AudioConst.SOUND_CLICK_ENABLE_2;
            case ButtonSoundType.CANCEL:
                return AudioConst.SOUND_CLICK_ENABLE_3;
            default:
                return 0;
        }
    }

    // ============ 交互 ============
    /*public void SetInteractable(bool value) {
        _interactable = value;
        ApplyInteractableToButton();
    }*/
    public bool Interactable {
        get => _interactable;
        set {
            _interactable = value;
            UpdateInteractableState();
        }
    }

    private void UpdateInteractableState() {
        if (_image == null) return;

        if (_interactable) {
            _image.color = _defaultColor;
        } else {
            _image.color = _disabledColor;
        }
    }

    // ============ 视觉接口：缩放 ============
    void ApplyHoverVisual() {
        if (!enableHoverScale) {
            _targetScale = _normalScale;
            return;
        }
        if (!_isPointerDown)
            _targetScale = _normalScale * HOVER_SCALE;
    }

    void ApplyNormalVisual() {
        _targetScale = _normalScale;
    }

    void ApplyPressedVisual() {
        if (!enableHoverScale) {
            _targetScale = _normalScale;
            return;
        }
        _targetScale = _normalScale * PRESSED_SCALE;
    }

    // ============ Pointer 事件 ============
    public void OnPointerEnter(PointerEventData eventData) {
        _isPointerInside = true;
        if (!_interactable) return;

        PlayOverSound();
        ApplyHoverVisual();
    }

    public void OnPointerExit(PointerEventData eventData) {
        _isPointerInside = false;
        _isPointerDown = false;
        if (!_interactable) return;

        ApplyNormalVisual();
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (!_interactable) return;

        _isPointerDown = true;
        ApplyPressedVisual();
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (!_interactable) return;

        _isPointerDown = false;

        bool inside = RectTransformUtility.RectangleContainsScreenPoint(
            transform as RectTransform,
            eventData.position,
            eventData.enterEventCamera
        );
        _isPointerInside = inside;

        if (inside)
            ApplyHoverVisual();
        else
            ApplyNormalVisual();
    }

    // ============ 文本接口 & 同步逻辑 ============
    public string Label {
        set {
            text = value;
            ApplyTextToLabel();
        }
    }

    public Color LabelColor {
        set {
            label.color = value;
        }
    }

    public string GetText() {
        return text;
    }

    void ApplyTextToLabel() {
        if (label != null) {
            label.text = text ?? string.Empty;
        }
    }

#if UNITY_EDITOR
    void OnValidate() {
        if (!Application.isPlaying) {
            if (label == null) {
                label = GetComponentInChildren<TextMeshProUGUI>(true);
            }
            ApplyTextToLabel();
        }
    }
#endif
}
