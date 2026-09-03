using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗信息表现
/// </summary>
/// <remarks>
/// 集中同步战斗中的血量、生命、分数、进度和 Boss 状态。
/// </remarks>
internal sealed class BattleHudPresenter {

    private readonly Image progressFill;
    private readonly TextMeshProUGUI lifeText;
    private readonly TextMeshProUGUI scoreText;
    private readonly RectTransform bossHealthRoot;
    private readonly Image bossHealthFill;
    private readonly TextMeshProUGUI bossHealthText;
    private readonly float progressMaxWidth;
    private readonly RectTransform hudLayer;
    private readonly List<BattleFloatingTextView> floatingTexts = new List<BattleFloatingTextView>();

    public TextMeshProUGUI playerHealthText { get; private set; }

    public BattleHudPresenter(Image progressFill, TextMeshProUGUI lifeText,
        TextMeshProUGUI scoreText, RectTransform bossHealthRoot, Image bossHealthFill,
        TextMeshProUGUI bossHealthText) {
        this.progressFill = progressFill;
        this.lifeText = lifeText;
        this.scoreText = scoreText;
        this.bossHealthRoot = bossHealthRoot;
        this.bossHealthFill = bossHealthFill;
        this.bossHealthText = bossHealthText;
        hudLayer = scoreText.rectTransform.parent as RectTransform;
        progressMaxWidth = BattleConst.PlayerHealthFillMaxWidth;
    }

    public void Initialize() {
        Vector2 size = progressFill.rectTransform.sizeDelta;
        size.x = progressMaxWidth;
        progressFill.rectTransform.sizeDelta = size;
        progressFill.gameObject.SetActive(true);
        EnsurePlayerHealthText();
    }

    public void RefreshPlayer(AircraftVO player, int lifeCount) {
        if (player != null && player.maxHealth > 0) {
            SetProgress((float)player.health / player.maxHealth);
            playerHealthText.text = $"{player.health}/{player.maxHealth}";
        }
        if (lifeText != null) {
            lifeText.text = $"×{lifeCount}";
        }
    }

    public void SetProgress(float value) {
        float progress = Mathf.Clamp01(value);
        Vector2 size = progressFill.rectTransform.sizeDelta;
        bool visible = progress > 0f;
        progressFill.gameObject.SetActive(visible);
        size.x = visible ? Mathf.Max(22f, Mathf.Round(progressMaxWidth * progress)) : 22f;
        progressFill.rectTransform.sizeDelta = size;
    }

    public void SetScore(int value) {
        scoreText.text = $"得分 {Mathf.Max(0, value):000000}";
    }

    public void RefreshBoss(IReadOnlyList<AircraftVO> enemies) {
        int health = 0;
        int maximum = 0;
        foreach (AircraftVO enemy in enemies) {
            if (!enemy.isBoss) continue;
            health += Mathf.Max(0, enemy.health);
            maximum += enemy.maxHealth;
        }
        if (maximum <= 0 || bossHealthFill == null) return;
        Vector2 size = bossHealthFill.rectTransform.sizeDelta;
        size.x = Mathf.Max(20f, Mathf.Round(BattleConst.BossHealthFillSize.x * health / maximum));
        bossHealthFill.rectTransform.sizeDelta = size;
        if (bossHealthText != null) bossHealthText.text = $"{health}/{maximum}";
    }

    public void SetBossVisible(bool visible) {
        if (bossHealthRoot != null) bossHealthRoot.gameObject.SetActive(visible);
    }

    public void ResetFeedbackColors() {
        if (playerHealthText != null) playerHealthText.color = Color.white;
        if (lifeText != null) lifeText.color = Color.white;
    }

    /**在 HUD 指定坐标播放统一飘字。*/
    public void PlayFloatingText(Vector2 position, string content) {
        if (hudLayer == null || string.IsNullOrWhiteSpace(content)) {
            return;
        }
        floatingTexts.Add(new BattleFloatingTextView(hudLayer, scoreText, position, content));
    }

    /**按场景 Timer 推进并回收飘字。*/
    public void UpdateFloatingTexts(float deltaTime) {
        for (int i = floatingTexts.Count - 1; i >= 0; i--) {
            if (floatingTexts[i].Update(deltaTime)) {
                continue;
            }
            floatingTexts[i].Dispose();
            floatingTexts.RemoveAt(i);
        }
    }

    /**清理尚未播放完成的飘字。*/
    public void ClearFloatingTexts() {
        foreach (BattleFloatingTextView floatingText in floatingTexts) {
            floatingText.Dispose();
        }
        floatingTexts.Clear();
    }

    private void EnsurePlayerHealthText() {
        if (playerHealthText != null) return;
        RectTransform root = progressFill.rectTransform.parent as RectTransform;
        playerHealthText = BattleViewFactory.CreateText("txtPlayerHealth", root,
            new Vector2(250f, 35f), Vector2.zero, 18f, scoreText.font);
    }
}
