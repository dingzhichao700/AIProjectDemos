using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class OptionPanel : BasePanel
{
    public ScrollList listOption;
    public CanvasGroup canvasMask;

    /**阻止重复提交正式内容加载请求*/
    private bool enteringGame;

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
        enteringGame = false;
        OptionControl.ins.curSelectOption = BaseOptions[0];
        listOption.array = BaseOptions;
        // 遮罩只负责淡出表现，不能在透明后继续拦截点击。
        canvasMask.blocksRaycasts = false;
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

    /// <summary>
    /// 选中点击的选项，并复用键盘确认逻辑。
    /// </summary>
    /// <param name="option">点击的选项</param>
    public void SelectAndConfirm(OptionEnum option) {
        if (!isOpened || !BaseOptions.Contains(option)) {
            return;
        }
        OptionControl.ins.curSelectOption = option;
        OnSure();
    }

    void OnSure()
    {
        if (!isOpened || enteringGame) {
            return;
        }
        switch (OptionControl.ins.curSelectOption)
        {
            case OptionEnum.NEWGAME:
                enteringGame = true;
                MainContentLoader.Enter(() => enteringGame = false, exception => {
                    enteringGame = false;
                    Debug.LogError("进入游戏失败，请重新选择新游戏重试：" + exception);
                    if (!isOpened) {
                        PanelMgr.ins.OpenPanel(UIEnum.OPTION_PANEL);
                    }
                });
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
