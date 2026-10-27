using TMPro;
using UnityEngine.UI;

/// <summary>
/// 显示模板中的单条按键提示。
/// </summary>
public sealed class KeyHintItem : ScrollListItem {

    public Image imgBg;
    public TextMeshProUGUI txtLabel;

    protected override void OnSetData(int index, object value) {
        txtLabel.text = value?.ToString() ?? string.Empty;
    }

}
