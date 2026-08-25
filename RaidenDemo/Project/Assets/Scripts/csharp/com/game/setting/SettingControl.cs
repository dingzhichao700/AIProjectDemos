using cfg;
using System;
using UnityEngine;

public class SettingControl : EventDispatcher {

    /**当前焦点是否在tab上*/
    public bool isFocusTab;

    /**当前选择tab的索引*/
    public int selectTabIndex;

    /**当前选择选项的索引*/
    public int selectOptionIndex = -1;

    private static SettingControl _ins;
    public static SettingControl ins {
        get {
            if (_ins == null) {
                _ins = new SettingControl();
            }
            return _ins;
        }
    }

    public SettingControl() {
        On<SettingOptionSelection>(SettingEvent.SELECTION_VALUE_UPDATE, OnSelectionValueUpdate);
    }

    /**设置是否聚焦于tab按钮*/
    public void SetFocusTab(bool value) {
        if (isFocusTab != value) {
            isFocusTab = value;
            Dispatch(SettingEvent.FOCUS_STATE_CHANGED);
            SetFocusOptionIndex(-1);
        }
    }

    /**设置当前选中第几个tab按钮*/
    public void SetTabIndex(int value) {
        if (selectTabIndex != value) {
            selectTabIndex = value;
            Dispatch(SettingEvent.FOCUS_TAB_UPDATE);
        }
    }

    /**设置当前选中第几个选项*/
    public void SetFocusOptionIndex(int value) {
        if (selectOptionIndex != value) {
            selectOptionIndex = value;
            Dispatch(SettingEvent.FOCUS_OPTION_UPDATE);
        }
    }

    /**设置项的值发生变化*/
    private void OnSelectionValueUpdate(SettingOptionSelection selection) {
        string value = PersistentDataControl.ins.saveModel.GetSetting(selection);
        //Debug.Log("设置项【" + selection.ToString() + "】的值发生变化：" + value);
        switch (selection) {
            case SettingOptionSelection.GRAPHIC_DISPLAY_MODE:
            case SettingOptionSelection.GRAPHIC_RESOLUTION:
                RookieEngine.timer.CallLater(this, SyncDisplayModeAndResolution);
                break;
            case SettingOptionSelection.GRAPHIC_VSYNC:
                QualitySettings.vSyncCount = int.Parse(value);
                break;
            case SettingOptionSelection.AUDIO_TOTAL:
                AudioManager.ins.SetVolume(AudioMixerType.TOTAL, int.Parse(value));
                break;
            case SettingOptionSelection.AUDIO_MUSIC:
                AudioManager.ins.SetVolume(AudioMixerType.BGM, int.Parse(value));
                break;
            case SettingOptionSelection.AUDIO_EFFECT:
                AudioManager.ins.SetVolume(AudioMixerType.EFFECT, int.Parse(value));
                break;
            case SettingOptionSelection.LANGUAGE:
                break;
        }
    }

    /**同步显示方式和分辨率*/
    public void SyncDisplayModeAndResolution() {
        /**显示方式*/
        FullScreenMode displayMode = FullScreenMode.ExclusiveFullScreen;
        Enum.TryParse(PersistentDataControl.ins.saveModel.GetSetting(SettingOptionSelection.GRAPHIC_DISPLAY_MODE), out SettingOptionSelectionDisplay cacheValue);
        switch (cacheValue) {
            case SettingOptionSelectionDisplay.EXCLUSIVE_FULLSCREEN:
                displayMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case SettingOptionSelectionDisplay.FULLSCREEN_WINDOW:
                displayMode = FullScreenMode.FullScreenWindow;
                break;
            case SettingOptionSelectionDisplay.WINDOW:
                displayMode = FullScreenMode.Windowed;
                break;
        }
        //Screen.fullScreenMode = targetMode;

        string resoStr = PersistentDataControl.ins.saveModel.GetSetting(SettingOptionSelection.GRAPHIC_RESOLUTION);
        int.TryParse(resoStr.Split("x")[0], out int width);
        int.TryParse(resoStr.Split("x")[1], out int height);
        //Debug.Log("设置显示方式为：" + displayMode.ToString() + ", 分辨率：" + width + "x" + height);
        Screen.SetResolution(width, height, displayMode);
    }

}
