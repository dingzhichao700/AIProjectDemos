using UnityEngine;

/// <summary>
/// 测试计时器
/// </summary>
public class TestCaseTimer {

    public TestCaseTimer() {
        //各种一次性延迟函数
        RookieEngine.timer.Once(this, 1000, OnceFunc);
        RookieEngine.timer.Once(this, 2000, OnceFunc, false);
        RookieEngine.timer.Once<string>(this, 4000, OnceFunc1Param, "1");
        RookieEngine.timer.Once<string, string>(this, 5000, OnceFunc2Param, "11", "22");
        RookieEngine.timer.Once<string, string, int>(this, 6000, OnceFunc3Param, "11", "22", 33);

        RookieEngine.timer.Clear(this, OnceFunc);
        RookieEngine.timer.Clear<string>(this, OnceFunc1Param);
        RookieEngine.timer.Clear<string, string>(this, OnceFunc2Param);

        //循环函数
        loopTime = 0;
        RookieEngine.timer.Loop(this, 1000, LoopFunc);
        RookieEngine.timer.Once(this, 8500, OnClearLoop);

        //延后一帧执行，要确保同个对象的同个函数，只执行1次
        RookieEngine.timer.CallLater(this, CallLaterFunc);
        RookieEngine.timer.CallLater(this, CallLaterFunc);
        RookieEngine.timer.CallLater(this, CallLaterFunc);
    }
    private void CallLaterFunc() {
        Debug.Log(RookieEngine.timer.curTime + "，帧推迟执行");
    }

    private int loopTime;

    /// <summary>
    /// 循环
    /// </summary>
    private void LoopFunc() {
        loopTime++;
        Debug.Log(RookieEngine.timer.curTime + "，循环触发次数：" + loopTime);
    }

    /// <summary>
    /// 清理循环
    /// </summary>
    private void OnClearLoop() {
        Debug.Log(RookieEngine.timer.curTime + "，清理循环函数");
        RookieEngine.timer.Clear(this, LoopFunc);
    }

    private void OnceFunc() {
        Debug.Log(RookieEngine.timer.curTime + "，延迟函数");
        //RookieEngine.timer.Once(this, 4000, TimerOnceMethod1); //错误写法典范，延迟函数中再调用延迟函数来嵌套该函数自身，是不允许的
        //RookieEngine.timer.Once(this, 2000, TimerOnceMethod2);
    }

    private void OnceFunc1Param(string str1) {
        Debug.Log(RookieEngine.timer.curTime + "，延迟函数1：" + str1);
    }

    private void OnceFunc2Param(string str1, string str2) {
        Debug.Log(RookieEngine.timer.curTime + "，延迟函数2：" + str1 + "，" + str2);
    }

    private void OnceFunc3Param(string str1, string str2, int int1) {
        Debug.Log(RookieEngine.timer.curTime + "，延迟函数3：" + str1 + "," + str2 + "," + int1);
    }

}
