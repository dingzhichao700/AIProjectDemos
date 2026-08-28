public class TestControl {

    private static TestControl _ins;
    public static TestControl ins {
        get {
            if (_ins == null) {
                _ins = new TestControl();
            }
            return _ins;
        }
    }

    public TestControl() {
    }

    /**常规测试*/
    public void TestNormal() {
        //new TestCaseEvent();
        //new TestCaseHandler();
        //new TestCaseTimer();
        //new TestCaseLoad();
    }

    /**测试配置表*/
    public void TestConfig() {
        //new TestCaseConfigs();
    }

}
