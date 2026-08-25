using cfg;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SettingConst {

    /**设置tab选项*/
    public static List<SettingType> TAB_OPTIONS = Enum.GetValues(typeof(SettingType)).Cast<SettingType>().ToList();
    /**二象性选项列表*/
    public static List<string> DUALITY_LIST = Enum.GetValues(typeof(SettingOptionDualitySelection)).Cast<SettingOptionDualitySelection>().Select(e => ((int)e).ToString()).ToList();
    /**显示方式列表*/
    public static List<string> DISPLAY_MODE_LIST = Enum.GetValues(typeof(SettingOptionSelectionDisplay)).Cast<SettingOptionSelectionDisplay>().Select(e => ((int)e).ToString()).ToList();
    /**语言列表*/
    public static List<string> LANGUAGE_LIST = Enum.GetValues(typeof(SettingOptionLanguage)).Cast<SettingOptionLanguage>().Select(e => ((int)e).ToString()).ToList();

    /**支持的所有分辨率类型*/
    private static List<string> allResolutionTypes;
    /**获取支持的所有分辨率类型*/
    public static List<string> GetResolutionList() {
        if (allResolutionTypes == null) {
            allResolutionTypes = new List<string>();

            foreach (Resolution res in Screen.resolutions) {
                string reso = res.width + "x" + res.height;
                if (allResolutionTypes.IndexOf(reso) < 0) {
                    allResolutionTypes.Add(reso);
                }
            }
        }
        return allResolutionTypes;
    }

    /**获取设置选项的名称*/
    public static string GetTabName(SettingType type) {
        string result = "";
        switch (type) {
            case SettingType.GRAPHIC:
                result = "图形";
                break;
            case SettingType.AUDIO:
                result = "音效";
                break;
            case SettingType.CONTROL:
                result = "操作";
                break;
            case SettingType.LANGUAGE:
                result = "语言";
                break;
        }
        return result;
    }

    /**获取选项名称*/
    public static string GetOptionName(SettingOptionSelection value) {
        string result = "";
        switch (value) {
            case SettingOptionSelection.GRAPHIC_RESOLUTION:
                result = "分辨率";
                break;
            case SettingOptionSelection.GRAPHIC_DISPLAY_MODE:
                result = "显示方式";
                break;
            case SettingOptionSelection.GRAPHIC_VSYNC:
                result = "垂直同步";
                break;
            case SettingOptionSelection.AUDIO_TOTAL:
                result = "总音量";
                break;
            case SettingOptionSelection.AUDIO_MUSIC:
                result = "音乐音量";
                break;
            case SettingOptionSelection.AUDIO_EFFECT:
                result = "效果音量";
                break;
            case SettingOptionSelection.KEY_MOVE_UP:
                result = "方向上";
                break;
            case SettingOptionSelection.KEY_MOVE_DOWN:
                result = "方向下";
                break;
            case SettingOptionSelection.KEY_MOVE_LEFT:
                result = "方向左";
                break;
            case SettingOptionSelection.KEY_MOVE_RIGHT:
                result = "方向右";
                break;
            case SettingOptionSelection.KEY_DASH:
                result = "冲刺";
                break;
            case SettingOptionSelection.LANGUAGE:
                result = "语言";
                break;
        }
        return result;
    }

    /// <summary>
    /// 获取设置项的选择内容列表
    /// </summary>
    /// <param name="optionType">设置项</param>
    /// <returns></returns>
    public static List<string> GetOptionSelectionList(SettingOptionSelection optionType) {
        List<string> result = null;
        switch (optionType) {
            case SettingOptionSelection.GRAPHIC_DISPLAY_MODE://显示方式
                result = DISPLAY_MODE_LIST;
                break;
            case SettingOptionSelection.GRAPHIC_RESOLUTION://分辨率
                result = GetResolutionList();
                break;
            case SettingOptionSelection.GRAPHIC_VSYNC://垂直同步
                result = DUALITY_LIST;
                break;
            case SettingOptionSelection.LANGUAGE://语言
                result = LANGUAGE_LIST;
                break;
        }
        return result;
    }

    /**获取选项内容的名称*/
    public static string GetOptionSelectionName(SettingOptionSelection optionType, string value) {
        string result = "";
        switch (optionType) {
            case SettingOptionSelection.GRAPHIC_DISPLAY_MODE://显示方式
                switch (int.Parse(value)) {
                    case (int)SettingOptionSelectionDisplay.EXCLUSIVE_FULLSCREEN:
                        result = "全屏";
                        break;
                    case (int)SettingOptionSelectionDisplay.FULLSCREEN_WINDOW:
                        result = "全屏（窗口化）";
                        break;
                    case (int)SettingOptionSelectionDisplay.WINDOW:
                        result = "窗口";
                        break;
                }
                break;
            case SettingOptionSelection.GRAPHIC_RESOLUTION: //分辨率
                result = value;
                break;
            case SettingOptionSelection.GRAPHIC_VSYNC: //垂直同步
                switch (int.Parse(value)) {
                    case (int)SettingOptionDualitySelection.ON:
                        result = "开启";
                        break;
                    case (int)SettingOptionDualitySelection.OFF:
                        result = "关闭";
                        break;
                }
                break;
            case SettingOptionSelection.LANGUAGE: //语言
                switch (int.Parse(value)) {
                    case (int)SettingOptionLanguage.CHINESE_SIMPLE:
                        result = "简体中文";
                        break;
                    case (int)SettingOptionLanguage.CHINESE_TRADITION:
                        result = "繁体中文";
                        break;
                    case (int)SettingOptionLanguage.ENGLISH:
                        result = "英文";
                        break;
                }
                break;
        }
        return result;
    }

}
