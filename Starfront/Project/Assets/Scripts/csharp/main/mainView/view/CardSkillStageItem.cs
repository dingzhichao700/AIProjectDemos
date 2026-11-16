using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能各阶效果与解锁状态的列表复用项。
/// </summary>
/// <remarks>条件与当前生效标记完全来自传入的业务快照。</remarks>
public sealed class CardSkillStageItem : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public Image imgStageCurrent;
    public TextMeshProUGUI txtRank;
    public TextMeshProUGUI txtEffect;
    public TextMeshProUGUI txtCondition;
    public Image imgStageNormal;
    public Image imgLock;
    /******************* UIComponent Define finish ************************/

    protected override void OnInitListItem() {
        txtEffect.enableWordWrapping = true;
        txtEffect.alignment = TextAlignmentOptions.TopLeft;
        txtEffect.overflowMode = TextOverflowModes.Ellipsis;
        txtCondition.alignment = TextAlignmentOptions.Center;
        txtCondition.margin = new Vector4(6, 0, 6, 0);
        txtCondition.enableAutoSizing = true;
        txtCondition.fontSizeMin = 14;
        txtCondition.overflowMode = TextOverflowModes.Ellipsis;
        Clear();
    }

    protected override void OnSetData(int index, object value) {
        var stage = value as CardSkillStageVO;
        if (stage == null) {
            Clear();
            return;
        }
        bool current = stage.unlocked && stage.current;
        // 两张底板互斥，避免半透明阴影叠画；只展示查询结果，不计算解锁等级和效果累加。
        imgStageCurrent.enabled = current;
        imgStageNormal.enabled = !current;
        imgLock.enabled = !stage.unlocked;
        txtRank.text = stage.rank + "阶";
        txtRank.color = stage.unlocked ? Color.white : new Color32(134, 181, 209, 255);
        txtEffect.text = stage.effect;
        // 字号上限按本界面各状态保留，文本框按最长状态留足空间，不按字数分配字号。
        LayoutCondition(stage.unlocked);
        txtCondition.fontSizeMax = current ? 17 : stage.unlocked ? 14 : 15;
        txtCondition.fontSize = txtCondition.fontSizeMax;
        txtCondition.color = current ? new Color32(0, 239, 120, 255) : stage.unlocked ? new Color32(168, 207, 223, 255) : Color.white;
        txtCondition.text = current ? "当前" : stage.unlocked ? "已解锁" : stage.unlockCondition ?? "解锁条件缺失";
    }

    /**状态文字在固定框内居中，锁与文本框共享水平中心；保留各状态原纵坐标。*/
    private void LayoutCondition(bool unlocked) {
        txtCondition.rectTransform.anchoredPosition = new Vector2(356, unlocked ? -34 : -58);
        txtCondition.rectTransform.sizeDelta = new Vector2(112, 36);
        imgLock.rectTransform.anchoredPosition = new Vector2(393.5f, -17);
    }

    /**回收时恢复普通底板，清理高亮、锁和文字，防止复用残留。*/
    public override void Clear() {
        data = null;
        imgStageCurrent.enabled = false;
        imgStageNormal.enabled = true;
        imgLock.enabled = false;
        txtRank.text = txtEffect.text = txtCondition.text = string.Empty;
        LayoutCondition(true);
    }

}