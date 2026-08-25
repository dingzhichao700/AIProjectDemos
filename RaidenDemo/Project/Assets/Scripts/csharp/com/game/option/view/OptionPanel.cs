using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class OptionPanel : BasePanel
{
    public ScrollList listOption;
    public CanvasGroup canvasMask;

    static readonly List<OptionEnum> BaseOptions = new List<OptionEnum>
    {
        OptionEnum.NEWGAME,
        OptionEnum.SETTING,
        OptionEnum.EXIT
    };

    public OptionPanel()
    {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen()
    {
        OptionControl.ins.curSelectOption = BaseOptions[0];
        listOption.array = BaseOptions;
        canvasMask.DOFade(0, 1.5f);
    }

    void OnArrowUp()
    {
        int curIndex = BaseOptions.IndexOf(OptionControl.ins.curSelectOption);
        if (curIndex > 0)
        {
            OptionControl.ins.curSelectOption = BaseOptions[curIndex - 1];
        }
    }

    void OnArrowDown()
    {
        int curIndex = BaseOptions.IndexOf(OptionControl.ins.curSelectOption);
        if (curIndex < BaseOptions.Count - 1)
        {
            OptionControl.ins.curSelectOption = BaseOptions[curIndex + 1];
        }
    }

    void OnSure()
    {
        switch (OptionControl.ins.curSelectOption)
        {
            case OptionEnum.NEWGAME:
                Close();
                PanelMgr.ins.OpenPanel(UIEnum.HOME_PANEL);
                break;
            case OptionEnum.SETTING:
                PanelMgr.ins.OpenPanel(UIEnum.SETTING_PANEL);
                break;
            case OptionEnum.EXIT:
                RookieEngine.QuitGame();
                break;
        }
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode)
    {
        switch (operateCode)
        {
            case PanelOperateEnum.Up:
                OnArrowUp();
                break;
            case PanelOperateEnum.Down:
                OnArrowDown();
                break;
            case PanelOperateEnum.SURE:
                OnSure();
                break;
        }
    }

    public override void OnClose()
    {
        AudioManager.ins.StopBgm();
    }
}
