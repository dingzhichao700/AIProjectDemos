using cfg;
using cfg.resource;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置界面，设置项item
/// </summary>
public class SettingOptionItem : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtOptionName;
    public TextMeshProUGUI txtOptionValue;
    public Slider slider;
    public Image sliderHandle;
    /******************* UIComponent Define finish ************************/

    private SettingOptionResource cfg;

    /**选项类型是否为选项*/
    private bool isSelection => cfg.OptionType == SettingOptionType.SWITCH_OPTIONS || cfg.OptionType == SettingOptionType.SWITCH_BOOL;
    /**选项类型是否为滑动条*/
    private bool isSlider => cfg.OptionType == SettingOptionType.DRAG_BAR;
    /**选项类型是否为键位绑定*/
    private bool isKeyBind => cfg.OptionType == SettingOptionType.KEY_BIND;

    /**选项类型为内容选择型时的，选择内容列表*/
    private List<string> optionSelectList;
    /**选项类型为内容选择型时的，选择内容的当前索引*/
    private int curOptionSelectionIndex;

    override protected void OnInitListItem() {
        SettingControl.ins.On(SettingEvent.FOCUS_OPTION_UPDATE, UpdateFocus);
        SettingControl.ins.On<SettingOptionSelection>(SettingEvent.SELECTION_VALUE_UPDATE, OnSelectionValueUpdate);
        slider.minValue = 0;
        slider.maxValue = 100;
    }

    override protected void OnSetData(int index, object value) {
        cfg = value as SettingOptionResource;
        txtOptionName.text = SettingConst.GetOptionName(cfg.OptionSelection) + ":";
        txtOptionValue.gameObject.SetActive(!isSlider);
        slider.gameObject.SetActive(isSlider);

        if (isSelection) {
            optionSelectList = SettingConst.GetOptionSelectionList(cfg.OptionSelection);
            curOptionSelectionIndex = optionSelectList.IndexOf(PersistentDataControl.ins.saveModel.GetSetting(cfg.OptionSelection));
        }
        UpdateFocus();
        UpdateSelecction();
    }

    /**更新聚焦显示*/
    private void UpdateFocus() {
        bool isFocus = SettingControl.ins.selectTabIndex == SettingConst.TAB_OPTIONS.IndexOf(cfg.Type) && SettingControl.ins.selectOptionIndex == listIndex;
        /*if (isFocus) {
            if (isSelection) {
                Debug.Log("内容选项：" + string.Join(", ", optionSelectList));
            }
        }*/
        if (txtOptionValue.gameObject.activeSelf) {
            txtOptionValue.color = isFocus ? ColorConst.RED : ColorConst.BLACK;
        }
        if (slider.gameObject.activeSelf) {
            UITools.SetImage(sliderHandle, ResourceConst.PATH_ATLAS_TITLE + (isFocus ? "sliderDot_red" : "sliderDot_black"));
        }
    }

    /**选项的值发生变化*/
    private void OnSelectionValueUpdate(SettingOptionSelection selection) {
        if (selection == cfg.OptionSelection) {
            UpdateSelecction();
        }
    }

    /**更新选项显示*/
    private void UpdateSelecction() {
        if (isSlider) {
            slider.value = int.Parse(PersistentDataControl.ins.saveModel.GetSetting(cfg.OptionSelection));
        } else if (isSelection) {
            curOptionSelectionIndex = optionSelectList.IndexOf(PersistentDataControl.ins.saveModel.GetSetting(cfg.OptionSelection));
            txtOptionValue.text = SettingConst.GetOptionSelectionName(cfg.OptionSelection, optionSelectList[curOptionSelectionIndex]);
        }
    }

    public void OnLeft() {
        if (isSlider) {
            int curValue = int.Parse(PersistentDataControl.ins.saveModel.GetSetting(cfg.OptionSelection));
            if (curValue > 0) {
                PersistentDataControl.ins.saveModel.SetOptionSelectValue(cfg.OptionSelection, Mathf.Max(0, curValue - 5).ToString());
            }
        } else if (isSelection) {
            if (curOptionSelectionIndex > 0) {
                PersistentDataControl.ins.saveModel.SetOptionSelectValue(cfg.OptionSelection, optionSelectList[curOptionSelectionIndex - 1]);
            }
        }
        //Debug.Log(SettingConst.GetOptionName(cfg.OptionSelection) + "左");
    }

    public void OnRight() {
        if (isSlider) {
            int curValue = int.Parse(PersistentDataControl.ins.saveModel.GetSetting(cfg.OptionSelection));
            if (curValue < 100) {
                PersistentDataControl.ins.saveModel.SetOptionSelectValue(cfg.OptionSelection, Mathf.Min(100, curValue + 5).ToString());
            }
        } else if (isSelection) {
            if (curOptionSelectionIndex < optionSelectList.Count - 1) {
                PersistentDataControl.ins.saveModel.SetOptionSelectValue(cfg.OptionSelection, optionSelectList[curOptionSelectionIndex + 1]);
            }
        }
        //Debug.Log(SettingConst.GetOptionName(cfg.OptionSelection) + "右");
    }

    public void OnSure() {
        if (isKeyBind) {

        }
    }

    override public void Clear() {
        SettingControl.ins.Off(SettingEvent.FOCUS_OPTION_UPDATE, UpdateFocus);
        SettingControl.ins.Off<SettingOptionSelection>(SettingEvent.SELECTION_VALUE_UPDATE, OnSelectionValueUpdate);
    }

}
