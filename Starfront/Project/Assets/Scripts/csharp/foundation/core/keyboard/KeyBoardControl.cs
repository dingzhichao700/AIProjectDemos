using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 键盘管理
/// </summary>
public class KeyBoardControl {

    /**指定按键按下回调map*/
    private Dictionary<KeyCode, List<Action>> keyDownActionMap;
    /**指定按键松开回调map*/
    private Dictionary<KeyCode, List<Action>> keyUpActionMap;
    /**任意按键按下回调map*/
    private List<Action<KeyCode>> anyKeyDownActionList;
    /**任意按键松开回调map*/
    private List<Action<KeyCode>> anyKeyUpActionList;

    private Dictionary<KeyCode, float> keyNextTimeMap = new();

    public float HOLD_INTERVAL = 0.15f; // 触发间隔（秒）

    private static KeyBoardControl _ins;

    public static KeyBoardControl ins {
        get {
            if (_ins == null) {
                _ins = new KeyBoardControl();
            }
            return _ins;
        }
    }

    public void Update() {
        if (Input.anyKeyDown) { // 检测任意键被按下
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) { // 如果想知道按下的是哪个键，可以遍历所有键
                                                                       //if (Input.GetKeyDown(key)) {
                if (Input.GetKey(key)) {

                    if (!keyNextTimeMap.ContainsKey(key)) {
                        keyNextTimeMap[key] = 0f;
                    }

                    if (Time.time >= keyNextTimeMap[key]) {
                        keyNextTimeMap[key] = Time.time + HOLD_INTERVAL;
                        //Debug.Log("KeyDown:" + key.ToString());// 打印按下的键
                        KeyDownHandler(key);
                    }
                } else {
                    keyNextTimeMap[key] = 0f; // 松开后重置
                }
            }
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (Input.GetKeyUp(key)) {
                //Debug.Log("KeyUp:" + key.ToString());// 打印松开的键
                KeyUpHandler(key);
            }
        }
    }

    /**监听任意按键按下*/
    public void OnAnyKeyDown(Action<KeyCode> action) {
        if (anyKeyDownActionList == null) {
            anyKeyDownActionList = new List<Action<KeyCode>>();
        }
        if (!anyKeyDownActionList.Contains(action)) {
            anyKeyDownActionList.Add(action);
        }
    }

    /**移除任意按键按下*/
    public void OffAnyKeyDown(Action<KeyCode> action) {
        if (anyKeyDownActionList != null && anyKeyDownActionList.Contains(action)) {
            anyKeyDownActionList.Remove(action);
        }
    }

    /**监听任意按键松开*/
    public void OnAnyKeyUp(Action<KeyCode> action) {
        if (anyKeyUpActionList == null) {
            anyKeyUpActionList = new List<Action<KeyCode>>();
        }
        if (!anyKeyUpActionList.Contains(action)) {
            anyKeyUpActionList.Add(action);
        }
    }

    /**移除任意按键松开*/
    public void OffAnyKeyUp(Action<KeyCode> action) {
        if (anyKeyUpActionList != null && anyKeyUpActionList.Contains(action)) {
            anyKeyUpActionList.Remove(action);
        }
    }

    /**设置指定按键按下的回调*/
    public void OnKeyDown(KeyCode key, Action action) {
        if (keyDownActionMap == null) {
            keyDownActionMap = new Dictionary<KeyCode, List<Action>>();
        }
        keyDownActionMap.TryGetValue(key, out List<Action> actions);
        if (actions == null) {
            actions = new List<Action>();
            keyDownActionMap.Add(key, actions);
        }
        if (!actions.Contains(action)) {
            actions.Add(action);
        } else {
            Debug.LogError("重复绑定按键按下回调：" + action);
        }
    }

    /**释放指定按键按下的回调*/
    public void OffKeyDown(KeyCode key, Action action) {
        if (keyDownActionMap != null) {
            keyDownActionMap.TryGetValue(key, out List<Action> actions);
            if (actions != null && actions.Contains(action)) {
                actions.Remove(action);
            }
        }
    }

    /**设置指定按键松开的回调*/
    public void OnKeyUp(KeyCode key, Action action) {
        if (keyUpActionMap == null) {
            keyUpActionMap = new Dictionary<KeyCode, List<Action>>();
        }
        keyUpActionMap.TryGetValue(key, out List<Action> actions);
        if (actions == null) {
            actions = new List<Action>();
            keyUpActionMap.Add(key, actions);
        }
        if (!actions.Contains(action)) {
            actions.Add(action);
        } else {
            Debug.LogError("重复绑定按键松开回调：" + action);
        }
    }

    /**释放指定按键按下的回调*/
    public void OffKeyUp(KeyCode key, Action action) {
        if (keyUpActionMap != null) {
            keyUpActionMap.TryGetValue(key, out List<Action> actions);
            if (actions != null && actions.Contains(action)) {
                actions.Remove(action);
            }
        }
    }

    /**某个按键被按下*/
    private void KeyDownHandler(KeyCode key) {
        if (keyDownActionMap != null && keyDownActionMap.ContainsKey(key)) {
            keyDownActionMap.TryGetValue(key, out List<Action> actions);
            if (actions != null) {
                for (int i = 0; i < actions.Count; i++) {
                    actions[i].Invoke();
                }
            }
        }
        if (anyKeyDownActionList != null) {
            for (int i = 0; i < anyKeyDownActionList.Count; i++) {
                anyKeyDownActionList[i].Invoke(key);
            }
        }
    }

    /**按键松开*/
    private void KeyUpHandler(KeyCode key) {
        if (keyUpActionMap != null && keyUpActionMap.ContainsKey(key)) {
            keyUpActionMap.TryGetValue(key, out List<Action> actions);
            if (actions != null) {
                for (int i = 0; i < actions.Count; i++) {
                    actions[i].Invoke();
                }
            }
        }
        if (anyKeyUpActionList != null) {
            for (int i = 0; i < anyKeyUpActionList.Count; i++) {
                anyKeyUpActionList[i].Invoke(key);
            }
        }
    }

}
