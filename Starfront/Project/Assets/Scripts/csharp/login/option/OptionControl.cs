public class OptionControl : EventDispatcher {

    /**当前选中的选项*/
    private OptionEnum _curSelectOption;
    public OptionEnum curSelectOption {
        get {
            return _curSelectOption;
        }
        set {
            _curSelectOption = value;
            Dispatch(OptionEvent.FOCUS_OPTION);
        }
    }

    private static OptionControl _ins;

    public static OptionControl ins {
        get {
            if (_ins == null) {
                _ins = new OptionControl();
            }
            return _ins;
        }
    }

}
