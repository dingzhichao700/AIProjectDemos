using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏架构层入口（菜鸟引擎）：初始化框架并进入启动流程。
/// </summary>
public class RookieEngine : MonoBehaviour
{
    public RectTransform viewportScale;
    public RectTransform safeAreaRoot;
    public RectTransform uiPool;

    private PortraitGameViewport portraitViewport;

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
        if (GetComponent<FrameworkDevelopmentSettings>() == null) {
            gameObject.AddComponent<FrameworkDevelopmentSettings>();
        }

        Debug.Log("持久化路径：" + Application.persistentDataPath);

        LogEnableMap[EngineLogType.LOAD_INFO] = false;

        UiRuntimeHost.ConfigureRoots(portraitViewport.scaleRoot, safeAreaRoot, uiPool, portraitViewport.DesignSize);

        _timer = new Timer();
        _sceneTimer = new Timer();
        _playerTimer = new Timer();
        _enemyTimer = new Timer();
        monoPool = new ObjectPoolDelegate();

        GameEntrance.ins.StartGame();
    }

    void EnsureSceneReferences()
    {
        if (viewportScale == null)
        {
            viewportScale = FindSceneRect("canvasScale");
        }

        portraitViewport = GetComponent<PortraitGameViewport>();
        if (portraitViewport == null) {
            portraitViewport = gameObject.AddComponent<PortraitGameViewport>();
        }
        portraitViewport.Initialize(viewportScale);

        if (safeAreaRoot == null)
        {
            safeAreaRoot = SafeAreaFitter.GetOrCreate(portraitViewport.scaleRoot);
        }
        else {
            safeAreaRoot.SetParent(portraitViewport.scaleRoot, false);
        }
        safeAreaRoot.GetComponent<SafeAreaFitter>()?.Apply();

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
        portraitViewport.Refresh();
        float passTime = Time.deltaTime * 1000f;
        _timer.SyncTime(passTime);
        _sceneTimer.SyncTime(passTime);
        _playerTimer.SyncTime(passTime);
        _enemyTimer.SyncTime(passTime);

        UiRuntimeHost.Tick();
        KeyBoardControl.ins.Update();
    }

    void OnApplicationFocus(bool value)
    {
        _isFocus = value;
        UiRuntimeHost.NotifyFocus(value);
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
