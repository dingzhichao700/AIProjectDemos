using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 初始化资源系统并启动业务模块。
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

    /// <summary>
    /// 底座服务就绪后，通过公共接口初始化登录模块。
    /// </summary>
    /// <remarks>热更新程序集必须先于业务入口与界面加载。</remarks>
    public async void StartGame() {
        try {
            Debug.Log("游戏开始");
            await HotUpdateBootstrap.InitializeAsync();
            PanelMgr.ins.Init();
            Type entryType = Type.GetType("LoginEntry, Login", true);
            IGameModuleEntry entry = Activator.CreateInstance(entryType) as IGameModuleEntry;
            if (entry == null) {
                throw new InvalidOperationException("LoginEntry 必须实现 IGameModuleEntry。");
            }
            await entry.InitializeAsync();
        } catch (Exception exception) {
            Debug.LogException(exception);
        }
    }

}
