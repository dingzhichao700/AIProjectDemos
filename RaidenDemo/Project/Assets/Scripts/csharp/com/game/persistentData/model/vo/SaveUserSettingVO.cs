using cfg;
using System;
using System.Collections.Generic;

/// <summary>
/// 用户设置数据vo
/// </summary>
[Serializable]
public class SaveUserSettingVO : SerializableSaveData {

    /// <summary>
    /// 设置项数据map<设置项类型，存储的数值（字符串格式）>
    /// </summary>
    /// value会视情况设置，如：
    ///分辨率为1920x1080，3840x2160
    ///键位绑定为
    public Dictionary<SettingOptionSelection, string> settingMap;

    /**获取某个用户设置*/
    public string GetSetting(SettingOptionSelection optionType) {
        settingMap.TryGetValue(optionType, out string strValue);
        return strValue;
    }

    /// <summary>
    /// 设置某个选项选择的值
    /// </summary>
    /// <param name="selection"></param>
    /// <param name="value"></param>
    /// <returns>是否成功设置</returns>
    public bool SetOptionSelectValue(SettingOptionSelection selection, string value) {
        if (!settingMap.ContainsKey(selection) || settingMap[selection] != value) {
            settingMap[selection] = value;
            SettingControl.ins.Dispatch(SettingEvent.SELECTION_VALUE_UPDATE, selection);
            return true;
        }
        return false;
    }

    /**校正设置*/
    public void SettingCorret() {
        if (settingMap == null) {
            settingMap = new Dictionary<SettingOptionSelection, string>();
        }

        /**画面*/
        //分辨率
        CorrectResolution();
        //显示方式
        CorrectDisplayType();
        //垂直同步
        CorrectVSync();

        /**音量*/
        CorrectVolume();

        /**控制*/
        //方向上
        //方向下
        //方向左
        //方向右
        //冲刺

        /**语言*/
        CorrectLanguage();
    }

    /**修正分辨率*/
    private void CorrectResolution() {
        List<string> allResos = SettingConst.GetResolutionList();
        //未设置or设置的分辨率不在当前屏幕支持的分辨率中
        if (!settingMap.ContainsKey(SettingOptionSelection.GRAPHIC_RESOLUTION) || !allResos.Contains(settingMap[SettingOptionSelection.GRAPHIC_RESOLUTION])) {
            SetOptionSelectValue(SettingOptionSelection.GRAPHIC_RESOLUTION, allResos[allResos.Count - 1]); //默认设为当前屏幕最大分辨率
        }
    }

    /**修正显示方式*/
    private void CorrectDisplayType() {
        if (!settingMap.ContainsKey(SettingOptionSelection.GRAPHIC_DISPLAY_MODE)) {
            SetOptionSelectValue(SettingOptionSelection.GRAPHIC_DISPLAY_MODE, (int)SettingOptionSelectionDisplay.EXCLUSIVE_FULLSCREEN + "");//默认设为全屏
        }
    }

    /**垂直同步*/
    private void CorrectVSync() {
        if (!settingMap.ContainsKey(SettingOptionSelection.GRAPHIC_VSYNC)) {
            SetOptionSelectValue(SettingOptionSelection.GRAPHIC_VSYNC, (int)SettingOptionDualitySelection.ON + "");//默认设为开启
        }
    }

    /**修正音量*/
    private void CorrectVolume() {
        if (!settingMap.ContainsKey(SettingOptionSelection.AUDIO_TOTAL)) {
            SetOptionSelectValue(SettingOptionSelection.AUDIO_TOTAL, "100");//默认设为100
        }
        if (!settingMap.ContainsKey(SettingOptionSelection.AUDIO_MUSIC)) {
            SetOptionSelectValue(SettingOptionSelection.AUDIO_MUSIC, "100");//默认设为100
        }
        if (!settingMap.ContainsKey(SettingOptionSelection.AUDIO_EFFECT)) {
            SetOptionSelectValue(SettingOptionSelection.AUDIO_EFFECT, "100");//默认设为100
        }
    }

    /**修正语言*/
    private void CorrectLanguage() {
        if (!settingMap.ContainsKey(SettingOptionSelection.LANGUAGE)) {
            SetOptionSelectValue(SettingOptionSelection.LANGUAGE, (int)SettingOptionLanguage.CHINESE_SIMPLE + "");//默认设为简中
        }
    }

}
