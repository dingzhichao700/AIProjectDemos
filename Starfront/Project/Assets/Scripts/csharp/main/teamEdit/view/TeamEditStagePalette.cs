using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 队伍编辑共用底座的四品质及空位显示资源；不持有卡牌或方案业务状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class TeamEditStagePalette : MonoBehaviour {

    public Sprite normal;
    public Sprite rare;
    public Sprite epic;
    public Sprite legend;
    public Material emptyMaterial;

    public Sprite GetSprite(cfg.Quality? quality) {
        if (!quality.HasValue) {
            return normal;
        }
        switch (quality.Value) {
            case cfg.Quality.NORMAL:
            case cfg.Quality.GREAT:
                return normal;
            case cfg.Quality.RARE:
                return rare;
            case cfg.Quality.EPIC:
                return epic;
            case cfg.Quality.LEGEND:
            case cfg.Quality.LEGEND_PLUS:
                return legend;
            default:
                throw new ArgumentOutOfRangeException(nameof(quality));
        }
    }

    /// <summary>只切换Sprite和材质，保留槽位尺寸及单位脚底锚点。</summary>
    public void Apply(Image stage, cfg.Quality? quality) {
        stage.gameObject.SetActive(true);
        stage.sprite = GetSprite(quality);
        stage.material = quality.HasValue ? null : emptyMaterial;
        stage.color = quality.HasValue ? Color.white : new Color(.7f, .7f, .7f, 1);
    }
}
