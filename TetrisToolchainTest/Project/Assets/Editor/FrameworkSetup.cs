#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Audio;

public static class FrameworkSetup
{
    const string GameEntranceScenePath = "Assets/Scenes/GameEntrance.unity";
    const string UIEditorScenePath = "Assets/Scenes/UIEditor.unity";

    [MenuItem("Tools/Framework/Setup Baseline")]
    public static void SetupBaseline()
    {
        CreateFolders();
        ConfigureInputSystem();
        EnsureAddressables();
        CreateGameEntranceScene();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GameFrameworkTest baseline setup completed (Built-in pipeline).");
    }

    static void CreateFolders()
    {
        string[] bundleGroups = { "opening", "title", "default", "scene", "cutScene" };
        string[] artTypes = { "atlasSource", "atlas", "unpack", "frameAnimation", "audio", "material" };

        foreach (string group in bundleGroups)
        {
            EnsureFolder($"Assets/Prefab/{group}");
            foreach (string artType in artTypes)
            {
                EnsureFolder($"Assets/Art/{artType}/{group}");
            }
        }

        string[] extras =
        {
            "Assets/Art/atlasSource/richTextImage",
            "Assets/Art/shader",
            "Assets/Art/audioMixer",
            "Assets/ConfigBin",
            "Assets/UIReference",
            "Assets/Scenes",
            "Assets/Scripts/lua",
            "Assets/Plugins"
        };

        foreach (string folder in extras)
        {
            EnsureFolder(folder);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            return;
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    static void ConfigureInputSystem()
    {
        var props = typeof(PlayerSettings).GetProperty("activeInputHandler",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        props?.SetValue(null, 2);
    }

    /// <summary>导出 batch / 脚本调用：避免首次打开 Editor 弹出 Input System 启用提示。</summary>
    public static void EnsureInputSystemPlayerSettings()
    {
        ConfigureInputSystem();
    }

    static void EnsureAddressables()
    {
        AddressablesProjectBootstrap.EnsureReady(log: false);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("Failed to create AddressableAssetSettings.");
            return;
        }

        CreateBundledGroup(settings, "opening", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        CreateBundledGroup(settings, "title", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        CreateBundledGroup(settings, "scene", BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel);
        CreateBundledGroup(settings, "cutScene", BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel);
    }

    /// <summary>仅初始化 Addressables Settings + 五组（供导出管线 / 菜单调用，不重建场景）。</summary>
    public static void EnsureAddressablesOnly()
    {
        EnsureAddressables();
        AssetDatabase.SaveAssets();
        Debug.Log("Addressables initialized (5 groups: opening/title/scene/cutScene + Default Local Group).");
    }

    static void CreateBundledGroup(AddressableAssetSettings settings, string groupName,
        BundledAssetGroupSchema.BundlePackingMode packingMode)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(groupName, false, false, true, null);
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
        {
            schema = group.AddSchema<BundledAssetGroupSchema>();
        }

        schema.BundleMode = packingMode;
        EditorUtility.SetDirty(settings);
    }

    static void CreateGameEntranceScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraGo = new GameObject("Main Camera");
        Camera camera = cameraGo.AddComponent<Camera>();
        cameraGo.tag = "MainCamera";
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // canvasScale: main UI root (RookieEngine.viewportScale)
        GameObject canvasScale = new GameObject("canvasScale");
        SetupCanvas(canvasScale, true);

        // canvasConstantPixel: legacy pixel-fixed UI layer (RookieEngine.viewportConstant); baseline keeps node only
        GameObject canvasConstantPixel = new GameObject("canvasConstantPixel");
        SetupCanvas(canvasConstantPixel, true);

        GameObject uiPool = new GameObject("uiPool");
        SetupCanvas(uiPool, false);

        GameObject gameStart = new GameObject("GameStart");
        RookieEngine engine = gameStart.AddComponent<RookieEngine>();
        gameStart.AddComponent<UnityMainThreadDispatcher>();

        GameObject audioBus = new GameObject("GlobalAudioSources");
        GlobalAudioSources gas = audioBus.AddComponent<GlobalAudioSources>();
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Art/audioMixer/mixerMain.mixer");
        if (mixer != null)
        {
            gas.mixer = mixer;
            gas.sourceBgm = CreateAudioChild(audioBus, "sourceBgm");
            gas.sourceDialogue = CreateAudioChild(audioBus, "sourceDialogue");
            gas.sourceHint = CreateAudioChild(audioBus, "sourceHint");
        }

        SerializedObject engineSo = new SerializedObject(engine);
        engineSo.FindProperty("viewportScale").objectReferenceValue = canvasScale.GetComponent<RectTransform>();
        engineSo.FindProperty("viewportConstant").objectReferenceValue = canvasConstantPixel.GetComponent<RectTransform>();
        engineSo.FindProperty("uiPool").objectReferenceValue = uiPool.GetComponent<RectTransform>();
        engineSo.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, GameEntranceScenePath);
    }

    static void SetupCanvas(GameObject go, bool withRaycaster)
    {
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;

        if (withRaycaster)
        {
            go.AddComponent<GraphicRaycaster>();
        }
    }

    static AudioSource CreateAudioChild(GameObject parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child.AddComponent<AudioSource>();
    }

    static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(GameEntranceScenePath, true)
        };
    }
}
#endif
