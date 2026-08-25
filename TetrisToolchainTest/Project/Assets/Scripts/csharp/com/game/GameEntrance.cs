using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 
/// </summary>
public class GameEntrance {

    private static GameEntrance _ins;

    public static GameEntrance ins {
        get {
            if (_ins == null) {
                _ins = new GameEntrance();
            }
            return _ins;
        }
    }

    public async void StartGame() {
        Debug.Log("游戏开始");

        //先等AddressAbles初始化
        await Addressables.InitializeAsync().Task;
        /*foreach (var locator in Addressables.ResourceLocators) {
            foreach (var key in locator.Keys) {
                Debug.Log($"[Key] {key}");
            }
        }*/

        PanelMgr.ins.Init();
        //PanelMgr.ins.OpenPanel(UIEnum.LOGIN_PANEL);
        PanelMgr.ins.OpenPanel(UIEnum.OPENING_PANEL);
        //PanelMgr.ins.OpenPanel(UIEnum.PERFORMANCE_MONITOR_VIEW);
    }

}
