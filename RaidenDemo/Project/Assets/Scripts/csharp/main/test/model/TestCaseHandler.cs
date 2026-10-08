using System;
using UnityEngine;

/// <summary>
/// 测试Handler回调
/// </summary>
public class TestCaseHandler {

    /**测试用的动态参数*/
    private int dynamicValue;

    private void TestHandler() {
        dynamicValue = 10;
        Handler handlerNoParam = Handler.Create(this, OnHandlerNoArg);

        Handler handler1ParamStr1 = Handler.Create(this, OnHandler1Arg, "11");
        Handler handler1ParamStr2 = Handler.Create(this, OnHandler1Arg, "22");
        Handler handler1ParamInt = Handler.Create(this, OnHandler1Arg2, 12);
        Handler handler1ParamInt2 = Handler.Create(this, OnHandler1Arg2, GetDynamicValue());

        Handler handler2Param = Handler.Create(this, OnHandler2Arg, "22", 23);

        //这里尝试动态改变某变量值，再触发handler1ParamInt2回调时调用GetDynamicValue()方法来获取该变量，结果获取到的是该变量改变前的值，因为Handler参数列表暂不支持表达式，后续再优化
        dynamicValue = 20;

        handlerNoParam.Run();
        handler1ParamStr1.Run();
        handler1ParamStr2.Run();
        handler1ParamInt.Run();
        handler1ParamInt2.Run();
        handler2Param.Run();
    }

    /// <summary>
    /// 无参回调
    /// </summary>
    private void OnHandlerNoArg() {
        Debug.Log("Handler Run, no arg");
    }

    /// <summary>
    /// 1个参数（String）的回调
    /// </summary>
    /// <param name="strParam"></param>
    private void OnHandler1Arg(String strParam) {
        Debug.Log("Handler Run, 1 string arg：" + strParam);
    }

    /// <summary>
    /// 1个参数（int）的回调
    /// </summary>
    /// <param name="intParam"></param>
    private void OnHandler1Arg2(int intParam) {
        Debug.Log("Handler Run, 1 int arg：" + intParam);
    }

    /// <summary>
    /// 2个参数的回调
    /// </summary>
    /// <param name="strParam"></param>
    /// <param name="intParam"></param>
    private void OnHandler2Arg(String strParam, int intParam) {
        Debug.Log("Handler Run, 2 arg：" + strParam + ", " + intParam);
    }

    private int GetDynamicValue() {
        return dynamicValue;
    }

}