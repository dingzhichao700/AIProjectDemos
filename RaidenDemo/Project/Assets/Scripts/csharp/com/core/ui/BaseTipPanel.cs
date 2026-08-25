
using UnityEngine;
using UnityEngine.EventSystems;
/// <summary>
/// Tip窗口基类
/// </summary>
public class BaseTipPanel : BasePanel {

    /**源自舞台哪个位置展示（锚点在左上角的一个矩形）*/
    public Rect sourceStageRect;

    override public void OnOpen() {
    }

    override public void OnPostOpen() {
        UpdatePosition();
    }

    private void UpdatePosition() {
        float stageWidth = PanelMgr.ins.stageWidth;
        float stageHeight = PanelMgr.ins.stageHeight;
        float selfWidth = trans.rect.width;
        float selfHeight = trans.rect.height;
        float posX;
        float posY;
        if (sourceStageRect.x + sourceStageRect.width + selfWidth < stageWidth / 2) {
            //右边空间够，放右边
            posX = sourceStageRect.x + sourceStageRect.width;
        } else if (selfWidth < (sourceStageRect.x + stageWidth / 2)) {
            //左边空间够，放右边
            posX = sourceStageRect.x - selfWidth;
        } else {
            //左右都不够，放右边，但是右边缘靠拢到舞台边缘
            posX = stageWidth / 2 - selfWidth;
        }
        if(-sourceStageRect.y + selfHeight< stageHeight / 2) {
            //向下空间足够，横向齐平向下摆放
            posY = sourceStageRect.y;
        } else {
            posY = -stageHeight / 2 + selfHeight;
        }
        trans.localPosition = new Vector3(Mathf.RoundToInt(posX), Mathf.RoundToInt(posY));
    }

}
