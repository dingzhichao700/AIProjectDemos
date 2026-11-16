using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面的美术状态：导航图标两态与文字颜色。
/// </summary>
/// <remarks>仅维护表现状态，不改变功能解锁、卡牌数据或分类业务。</remarks>
[DisallowMultipleComponent]
public sealed class MainPanelArtState : MonoBehaviour {

    public Image[] icons;
    public TextMeshProUGUI[] labels;
    public Sprite[] normalIcons;
    public Sprite[] selectedIcons;
    /**保留功能层设置的图标灰化，仅替换两态Sprite和文字颜色。*/
    public void SetNavigation(int selected) {
        if (icons == null || labels == null || normalIcons == null || selectedIcons == null) {
            return;
        }
        for (int i = 0; i < icons.Length; i++) {
            icons[i].sprite = i == selected ? selectedIcons[i] : normalIcons[i];
            labels[i].gameObject.SetActive(true);
            labels[i].color = i == selected ? new Color32(232, 251, 255, 255) : new Color32(159, 182, 203, 255);
        }
    }

}
