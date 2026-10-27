using System;
using System.IO;
using cfg.cfgObj;
using SimpleJSON;
using TMPro;
using UnityEngine;

/// <summary>
/// 功能开启业务回归与真实 MainPanel 导航验证。
/// </summary>
/// <remarks>业务测试只操作 Library 内独立存档，不给玩家档增加等级、通关或解锁。</remarks>
public static class FunctionUnlockValidation {

    public static void Run(MainPanel main) {
        string path = Path.GetFullPath("Library/AIUI/function-unlock-test-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            ValidateRules(path);
            ValidateNavigation(main);
            File.WriteAllText("Library/AIUI/function-unlock-validation.txt", "passed: empty/AND/level boundary/exact completed mission/disabled/missing ID/unknown condition; persistent reload and tightened configuration; real navigation clicks, lock hints, selection preservation, reopen, TMP glyph coverage");
        } finally {
            if (File.Exists(path)) {
                File.Delete(path);
            }
            if (File.Exists(path + ".pending")) {
                File.Delete(path + ".pending");
            }
        }
    }

    private static FunctionObj Config(string json) {
        return new FunctionObj(JSON.Parse(json));
    }

    private static void ValidateRules(string path) {
        const string json = "[{\"id\":1,\"name\":\"等级\",\"openCondition\":[{\"$type\":\"PlayerLevelCondition\",\"level\":5}]},{\"id\":2,\"name\":\"空条件\",\"openCondition\":[]},{\"id\":3,\"name\":\"混合\",\"openCondition\":[{\"$type\":\"PlayerLevelCondition\",\"level\":5},{\"$type\":\"MissionProgressCondition\",\"missionIndex\":20}]},{\"id\":4,\"name\":\"关闭\",\"openCondition\":[{\"$type\":\"DisabledCondition\"}]}]";
        var table = Config(json);
        var model = new FunctionModel(table.DataList, path);
        var control = new FunctionControl(model);
        int events = 0;
        control.On(FunctionConst.UnlockChanged, () => events++);
        control.Init();
        Require(model.IsUnlocked(2) && !model.IsUnlocked(1) && !model.IsUnlocked(3) && !model.IsUnlocked(4), "empty and unavailable progress");
        control.SetProgressSource(() => 4, id => id == 20);
        Require(!model.IsUnlocked(1) && !model.IsUnlocked(3), "level below threshold AND");
        control.SetProgressSource(() => 5, id => id == 21);
        Require(model.IsUnlocked(1) && !model.IsUnlocked(3), "exact completed mission; level equality");
        control.SetProgressSource(() => 5, id => id == 20);
        Require(model.IsUnlocked(3) && !model.IsUnlocked(4), "all conditions; disabled");
        control.RefreshUnlocks();
        Require(events == 3, "only successful transitions emit events");
        var reloaded = new FunctionModel(table.DataList, path);
        Require(reloaded.IsUnlocked(1) && reloaded.IsUnlocked(2) && reloaded.IsUnlocked(3), "persistent reload");
        var tightened = Config("[{\"id\":1,\"name\":\"已开启\",\"openCondition\":[{\"$type\":\"DisabledCondition\"}]}]");
        var permanent = new FunctionControl(new FunctionModel(tightened.DataList, path));
        Require(permanent.TryEnter(1, out _), "permanent record before tightened conditions");
        bool missingRejected = false;
        try {
            permanent.TryEnter(999, out _);
        } catch (InvalidOperationException) {
            missingRejected = true;
        }
        Require(missingRejected, "missing ID");
        bool unknownRejected = false;
        try {
            Config("[{\"id\":5,\"name\":\"未知\",\"openCondition\":[{\"$type\":\"UnknownCondition\"}]}]");
        } catch (Exception) {
            unknownRejected = true;
        }
        Require(unknownRejected, "unknown condition rejected by generated Bean");
    }

    private static void ValidateNavigation(MainPanel main) {
        Require(main.btnShop.interactable && main.btnTerritory.interactable && main.btnRanking.interactable, "locked buttons accept hint clicks");
        main.btnDeck.onClick.Invoke();
        Require(main.imgDeckSelected.gameObject.activeSelf, "deck selection");
        CheckLockedClick(main, FunctionConst.Shop, main.btnShop);
        CheckLockedClick(main, FunctionConst.Territory, main.btnTerritory);
        CheckLockedClick(main, FunctionConst.Ranking, main.btnRanking);
        main.btnBattle.onClick.Invoke();
        Require(main.imgBattleSelected.gameObject.activeSelf, "battle selection");
        main.OnClose();
        main.OnOpen();
        main.btnDeck.onClick.Invoke();
        Require(main.imgDeckSelected.gameObject.activeSelf, "reopen navigation");
        main.btnBattle.onClick.Invoke();
        foreach (var resource in CfgManager.tables.FunctionObj.DataList) {
            string text = FunctionControl.ins.model.GetLockedHint(resource.Id) + resource.Name + "已解锁\n功能页面尚未开放";
            if (!main.txtNavBattleLabel.font.HasCharacters(text, out uint[] missing, true, true)) {
                throw new InvalidOperationException("Function hint font missing glyphs: " + string.Join(",", missing));
            }
        }
    }

    private static void CheckLockedClick(MainPanel main, int id, UnityEngine.UI.Button button) {
        if (FunctionControl.ins.model.IsUnlocked(id)) {
            return;
        }
        button.onClick.Invoke();
        Transform hint = main.transform.Find("boxFunctionHint");
        Require(hint != null && hint.gameObject.activeSelf, "visible locked hint " + id);
        Require(hint.GetComponentInChildren<TMP_Text>().text == FunctionControl.ins.model.GetLockedHint(id), "correct hint " + id);
        Require(main.imgDeckSelected.gameObject.activeSelf, "locked click preserves selection");
    }

    private static void Require(bool result, string message) {
        if (!result) {
            throw new InvalidOperationException("Function validation failed: " + message);
        }
    }

}
