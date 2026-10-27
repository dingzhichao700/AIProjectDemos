using System;
using System.Globalization;
using cfg;
using TMPro;
using UnityEngine;

/// <summary>已确认的固定HUD，仅显示权威局内数据，不计时或修改余额。</summary>
public sealed class BattleInfoView : MonoBehaviour {
    [SerializeField] private float territoryFullLength;
    [SerializeField] private float territoryInsetX;
    private BattleInfoPanel panel;
    public TextMeshProUGUI friendlyCurrency => panel.txtCurrency;
    public TextMeshProUGUI countdown => panel.txtCountdown;
    public float fullTerritoryLength => territoryFullLength;

    public void Bind(BattleInfoPanel panel) {
        if (territoryFullLength <= 0) {
            throw new InvalidOperationException("占地条缺少已确认的完整设计长度。");
        }
        this.panel = panel;
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true)) {
            text.fontStyle = FontStyles.Normal;
        }
    }

    public void Refresh(BattleSceneModel model) {
        int friendly = model.GetTerritoryCount(BattleSceneCamp.FRIENDLY);
        int enemy = model.GetTerritoryCount(BattleSceneCamp.ENEMY);
        int total = friendly + enemy;
        float ratio = total > 0 ? Mathf.Clamp01(friendly / (float)total) : 0;
        panel.imgFriendlyFill.fillAmount = ratio;
        panel.imgEnemyFill.fillAmount = total > 0 ? 1 - ratio : 0;
        panel.imgDivider.gameObject.SetActive(total > 0);
        Vector2 position = panel.imgDivider.rectTransform.anchoredPosition;
        position.x = territoryInsetX + territoryFullLength * ratio;
        panel.imgDivider.rectTransform.anchoredPosition = position;
        panel.txtFriendlyTerritory.text = friendly.ToString(CultureInfo.InvariantCulture) + "格";
        panel.txtEnemyTerritory.text = enemy.ToString(CultureInfo.InvariantCulture) + "格";
        panel.txtFriendlyName.text = BattleConst.FriendlyName;
        panel.txtEnemyName.text = BattleConst.EnemyName;
        panel.txtFriendlyPopulation.text = Population(model, BattleSceneCamp.FRIENDLY);
        panel.txtEnemyPopulation.text = Population(model, BattleSceneCamp.ENEMY);
        int seconds = model.displayRemainingSeconds;
        panel.txtCountdown.text = (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        panel.txtCurrency.text = model.camps[BattleSceneCamp.FRIENDLY].currency.ToString(CultureInfo.InvariantCulture);
    }

    private static string Population(BattleSceneModel model, BattleSceneCamp camp) {
        return model.GetUnitCount(camp).ToString(CultureInfo.InvariantCulture) + " / " + model.camps[camp].unitLimit.ToString(CultureInfo.InvariantCulture);
    }

    public void Clear() {
        if (panel == null) {
            return;
        }
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true)) {
            text.text = string.Empty;
        }
        panel.imgFriendlyFill.fillAmount = 0;
        panel.imgEnemyFill.fillAmount = 0;
    }
}
