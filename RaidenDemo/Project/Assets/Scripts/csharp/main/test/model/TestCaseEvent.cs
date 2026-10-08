using System;
using System.Collections.Generic;
using UnityEngine;

public class TestCaseEvent {

    /// <summary>
    /// 消息监听测试
    /// </summary>
    public TestCaseEvent() {
        EventDispatcher dispatcher = new EventDispatcher();
        dispatcher.On("no arg", OnTestHandler1);
        dispatcher.On<int>("1 arg", OnTestHandler2);
        dispatcher.On<int>("1 arg", OnTestHandler3);
        dispatcher.On<int, string>("2 arg", OnTestHandler4);
        dispatcher.On<int, string, List<int>>("3 arg", OnTestHandler5);

        //测试一下中途直接掉Clear清理掉所有监听的情况
        //test.Clear();

        dispatcher.Dispatch("no arg");
        dispatcher.Dispatch("1 arg", 50);
        dispatcher.Dispatch("2 arg", 100, "100");
        dispatcher.Dispatch("2 arg", 200, "200");
        dispatcher.Dispatch("2 arg", 300, "300");
        dispatcher.Dispatch("3 arg", 1000, "1000", new List<int>() { 1, 2, 3 });

        dispatcher.Off("no arg", OnTestHandler1);
        dispatcher.Off<int>("1 arg", OnTestHandler2);
        dispatcher.Off<int>("1 arg", OnTestHandler3);
        dispatcher.Off<int, string>("2 arg", OnTestHandler4);
        dispatcher.Off<int, string, List<int>>("3 arg", OnTestHandler5);

        dispatcher.Dispatch("no arg");
        dispatcher.Dispatch("1 arg", 50);
        dispatcher.Dispatch("2 arg", 100, "200");
        dispatcher.Dispatch("3 arg", 100, "200", new List<int>() { 1, 2, 3 });
    }

    /// <summary>
    /// 2个参数的回调
    /// </summary>
    /// <param name="strParam"></param>
    /// <param name="intParam"></param>
    private void OnHandler2Arg(String strParam, int intParam) {
        Debug.Log("Handler Run, 2 arg：" + strParam + ", " + intParam);
    }

    private void OnTestHandler1() {
        Debug.Log("OnTestHandler, no arg");
    }

    private void OnTestHandler2(int argInt) {
        Debug.Log("OnTestHandler, 1 arg:" + argInt);
    }

    private void OnTestHandler3(int argInt) {
        Debug.Log("OnTestHandler, 1 arg:" + argInt);
    }

    private void OnTestHandler4(int argInt, string argStr) {
        Debug.Log("OnTestHandler, 2 arg:" + argInt + ", " + argStr);
    }

    private void OnTestHandler5(int argInt, string argStr, List<int> argList) {
        String content = "OnTestHandler, 2 arg:" + argInt + ", " + argStr + ", ";
        content += "[";
        for (int i = 0; i < argList.Count; i++) {
            content += argList[i] + (i < argList.Count - 1 ? "," : "");
        }
        content += "]";
        Debug.Log(content);
    }

}
