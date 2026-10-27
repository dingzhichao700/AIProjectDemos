using cfg;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 设置界面
/// </summary>
public class SettingPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public CommonPanelView panelView;
    public ScrollList listTab;
    public SettingGraphicView graphicView;
    public SettingAudioView audioView;
    public SettingControlView controlView;
    public SettingLanguageView languageView;
    public KeyHintView hintView;
    /******************* UIComponent Define finish ************************/

    /**当前选中的视图*/
    private SettingBaseView curView;

    private int tabIndex => SettingControl.ins.selectTabIndex;

    public SettingPanel() {
        layer = PanelLayer.SCALE_TOP_THIRD;
    }

    override protected void PlayOpen() {
        panelView.PlayOpen(PlayOpenComplete);
        SetTabIndex(0);
        SettingControl.ins.SetFocusTab(true);
        listTab.array = SettingConst.TAB_OPTIONS;
    }

    override public void OnOpen() {
        AddLis();
        UpdateView();
    }

    private void AddLis() {
        SettingControl.ins.On(SettingEvent.FOCUS_STATE_CHANGED, OnFocusUpdate);
    }

    private void RemoveLis() {
        SettingControl.ins.Off(SettingEvent.FOCUS_STATE_CHANGED, OnFocusUpdate);
    }

    private void OnFocusUpdate() {
    }

    /**设置选中第几个标签按钮*/
    private void SetTabIndex(int value) {
        SettingControl.ins.SetTabIndex(value);
        UpdateView();
    }

    private void UpdateView() {
        SettingType curSelectTab = SettingConst.TAB_OPTIONS[tabIndex];
        switch (curSelectTab) {
            case SettingType.GRAPHIC:
                curView = graphicView;
                break;
            case SettingType.AUDIO:
                curView = audioView;
                break;
            case SettingType.CONTROL:
                curView = controlView;
                break;
            case SettingType.LANGUAGE:
                curView = languageView;
                break;
        }

        /**所有视图*/
        List<SettingBaseView> allViews = new List<SettingBaseView>() { graphicView, audioView, controlView, languageView };
        for (int i = 0; i < allViews.Count; i++) {
            SettingBaseView view = allViews[i];
            bool isSelect = view == curView;
            view.SetIsCurrentView(isSelect);
            if (isSelect) {
                view.SetSelects(SettingOptionCfgMgr.GetCfgByType(curSelectTab));
            }
        }
    }

    /**窗口操作*/
    override public void OnPanelOperate(PanelOperateEnum operateCode) {
        if (SettingControl.ins.isFocusTab) {
            switch (operateCode) {
                case PanelOperateEnum.Down:
                    if (curView != null) {
                        SettingControl.ins.SetFocusTab(false);
                        curView.OnFocus();
                    }
                    break;
                case PanelOperateEnum.Left:
                    OnArrowLeft();
                    break;
                case PanelOperateEnum.Right:
                    OnArrowRight();
                    break;
                case PanelOperateEnum.ESC:
                    OnExit();
                    break;
            }
        } else {
            curView.OnOperate(operateCode);
        }
    }

    private void OnArrowLeft() {
        if (tabIndex > 0) {
            SetTabIndex(tabIndex - 1);
        }
    }

    private void OnArrowRight() {
        if (tabIndex < SettingConst.TAB_OPTIONS.Count - 1) {
            SetTabIndex(tabIndex + 1);
        }
    }

    private void OnExit() {
        Close();
    }

    override protected void PlayClose() {
        panelView.PlayClose(PlayCloseComplete);

        for (int i = 0; i < listTab.arraySource.Count; i++) {
            GameObject go = listTab.GetCell(i);
            if (go != null) {
                go.GetComponent<SettingTabItem>().PlayExit(listTab.arraySource.Count - i);
            }
        }
    }

    override public void OnClose() {
        RemoveLis();
    }

}
