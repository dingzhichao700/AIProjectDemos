using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏架构层入口（菜鸟引擎）：初始化框架并进入启动流程。
/// </summary>
public class RookieEngine : MonoBehaviour
{
    public RectTransform viewportScale;
    public RectTransform viewportConstant;
    public RectTransform safeAreaRoot;
    public RectTransform uiPool;

    public static ObjectPoolDelegate monoPool;

    static Timer _timer;
    public static Timer timer => _timer;

    static Timer _sceneTimer;
    public static Timer sceneTimer => _sceneTimer;

    static Timer _playerTimer;
    public static Timer playerTimer => _playerTimer;

    static Timer _enemyTimer;
    public static Timer enemyTimer => _enemyTimer;

    public static bool isFocus => _isFocus;
    static bool _isFocus;

    static readonly Dictionary<EngineLogType, bool> LogEnableMap = new Dictionary<EngineLogType, bool>();

    void Awake()
    {
        EnsureSceneReferences();

        Debug.Log("持久化路径：" + Application.persistentDataPath);

        LogEnableMap[EngineLogType.LOAD_INFO] = false;

        PanelMgr.ins.SetUIRoot(viewportScale, viewportConstant, safeAreaRoot, uiPool);

        _timer = new Timer();
        _sceneTimer = new Timer();
        _playerTimer = new Timer();
        _enemyTimer = new Timer();
        monoPool = new ObjectPoolDelegate();

        // Setting correction dispatches events that use timer, so read it only after core services exist.
        PersistentDataControl.ins.ReadUserSetting();

        GameEntrance.ins.StartGame();
    }

    void EnsureSceneReferences()
    {
        if (viewportScale == null)
        {
            viewportScale = FindSceneRect("canvasScale");
        }

        if (viewportConstant == null)
        {
            viewportConstant = FindSceneRect("canvasConstantPixel");
        }

        if (safeAreaRoot == null)
        {
            safeAreaRoot = SafeAreaFitter.GetOrCreate(viewportScale);
        }

        if (uiPool == null)
        {
            uiPool = FindSceneRect("uiPool");
        }
    }

    static RectTransform FindSceneRect(string nodeName)
    {
        var rects = Object.FindObjectsOfType<RectTransform>(true);
        for (var i = 0; i < rects.Length; i++)
        {
            var rect = rects[i];
            if (rect.name == nodeName && rect.gameObject.scene.isLoaded)
            {
                return rect;
            }
        }

        return null;
    }

    void Update()
    {
        float passTime = Mathf.Floor(Time.deltaTime * 1000);
        _timer.SyncTime(passTime);
        _sceneTimer.SyncTime(passTime);
        _playerTimer.SyncTime(passTime);
        _enemyTimer.SyncTime(passTime);

        PanelMgr.ins.Update();
        KeyBoardControl.ins.Update();
    }

    void OnApplicationFocus(bool value)
    {
        _isFocus = value;
        PanelMgr.ins.Dispatch(PanelEvent.APPLICATION_FOCUS_UPDATE);
    }

    public static Timer GetTimer(TimerType type)
    {
        switch (type)
        {
            case TimerType.SCENE: return sceneTimer;
            case TimerType.PLAYER: return playerTimer;
            case TimerType.ENEMY: return enemyTimer;
            default: return timer;
        }
    }

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void PrintLog(string content, EngineLogType type)
    {
        if (LogEnableMap.TryGetValue(type, out bool enabled) && enabled)
        {
            Debug.Log(content);
        }
    }
}
