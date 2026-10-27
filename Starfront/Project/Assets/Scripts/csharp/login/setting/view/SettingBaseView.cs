using cfg.resource;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 设置界面-视图基类
/// </summary>
public class SettingBaseView : BaseView {

    protected ScrollList listOptionList;

    /**选项*/
    protected List<SettingOptionResource> selections;


    /**是否为当前显示的视图*/
    protected bool _isCurrentView;

    public void SetSelects(List<SettingOptionResource> selections) {
        this.selections = selections;
        OnSetData();
    }

    protected virtual void OnSetData() { }


    /**是否为当前显示的视图*/
    public void SetIsCurrentView(bool value) {
        _isCurrentView = value;
        gameObject.SetActive(value);
    }

    public void OnFocus() {
        SettingControl.ins.SetFocusOptionIndex(0);
    }

    /**窗口操作*/
    public void OnOperate(PanelOperateEnum operateCode) {
        switch (operateCode) {
            case PanelOperateEnum.Up:
                OnArrowUp();
                break;
            case PanelOperateEnum.Down:
                OnArrowDown();
                break;
            case PanelOperateEnum.Left:
                OnArrowLeft();
                break;
            case PanelOperateEnum.Right:
                OnArrowRight();
                break;
            case PanelOperateEnum.SURE:
                OnSure();
                break;
            case PanelOperateEnum.ESC:
                OnEscape();
                break;
        }
    }

    private void OnArrowUp() {
        if (SettingControl.ins.selectOptionIndex == 0) {
            SettingControl.ins.SetFocusTab(true);
        } else {
            SettingControl.ins.SetFocusOptionIndex(SettingControl.ins.selectOptionIndex - 1);
        }
    }

    protected virtual void OnArrowDown() {
        if (SettingControl.ins.selectOptionIndex < selections.Count - 1) {
            SettingControl.ins.SetFocusOptionIndex(SettingControl.ins.selectOptionIndex + 1);
        }
    }

    private void OnArrowLeft() {
        if (optionItem != null) {
            optionItem.OnLeft();
        }
    }

    private void OnArrowRight() {
        if (optionItem != null) {
            optionItem.OnRight();
        }
    }

    private void OnSure() {
        if (optionItem != null) {
            optionItem.OnSure();
        }
    }

    private void OnEscape() {
        SettingControl.ins.SetFocusTab(true);
    }

    /**当前操作的虚心奶奶个item*/
    private SettingOptionItem optionItem {
        get {
            if (listOptionList != null) {
                GameObject item = listOptionList.GetCell(SettingControl.ins.selectOptionIndex);
                if (item != null) {
                    SettingOptionItem optionItem = item.GetComponent<SettingOptionItem>();
                    if (optionItem != null) {
                        return optionItem;
                    }
                }
            }
            return null;
        }
    }

}
