public  class MainControl {

    private static MainControl _ins;
    public static MainControl ins {
        get {
            if (_ins == null) {
                _ins = new MainControl();
            }
            return _ins;
        }
    }

    public void OnEnter() {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        TestControl.ins.TestNormal();
        #endif
    }

}
