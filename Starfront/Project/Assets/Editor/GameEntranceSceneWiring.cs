#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 修复 GameEntrance 场景中 GameStart / GlobalAudioSources 等序列化引用。
/// </summary>
public static class GameEntranceSceneWiring
{
    const string ScenePath = "Assets/Scenes/GameEntrance.unity";

    [MenuItem("Tools/Framework/Wire GameEntrance Scene")]
    public static void WireFromMenu()
    {
        if (TryWireGameEntranceScene(true))
        {
            Debug.Log("[wire] GameEntrance scene wired.");
        }
    }

    public static bool TryWireGameEntranceScene(bool saveScene = true)
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogWarning($"[wire] Scene not found: {ScenePath}");
            return false;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var changed = false;

        changed |= WireRookieEngine(scene);
        changed |= WireGlobalAudioSources(scene);
        changed |= WireEventSystem(scene);

        if (changed && saveScene)
        {
            EditorSceneManager.SaveScene(scene);
        }

        return changed;
    }

    static bool WireRookieEngine(Scene scene)
    {
        var gameStart = FindTransform(scene, "GameStart");
        if (gameStart == null)
        {
            Debug.LogError("[wire] GameStart not found.");
            return false;
        }

        var engine = gameStart.GetComponent<RookieEngine>();
        if (engine == null)
        {
            Debug.LogError("[wire] RookieEngine missing on GameStart.");
            return false;
        }

        var viewportScale = GetRectTransform(scene, "canvasScale");
        var viewportConstant = GetRectTransform(scene, "canvasConstantPixel");
        var uiPool = GetRectTransform(scene, "uiPool");

        var changed = false;
        if (engine.viewportScale != viewportScale)
        {
            engine.viewportScale = viewportScale;
            changed = true;
        }

        if (engine.viewportConstant != viewportConstant)
        {
            engine.viewportConstant = viewportConstant;
            changed = true;
        }

        if (engine.uiPool != uiPool)
        {
            engine.uiPool = uiPool;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(engine);
            Debug.Log("[wire] RookieEngine: canvasScale / canvasConstantPixel / uiPool assigned.");
        }

        return changed;
    }

    static bool WireGlobalAudioSources(Scene scene)
    {
        var camera = FindTransform(scene, "Main Camera");
        if (camera == null)
        {
            return false;
        }

        var audio = camera.GetComponent<GlobalAudioSources>();
        if (audio == null)
        {
            return false;
        }

        var sourceBgm = FindTransform(scene, "sourceBgm")?.GetComponent<AudioSource>();
        var sourceDialogue = FindTransform(scene, "sourceDialogue")?.GetComponent<AudioSource>();
        var sourceHint = FindTransform(scene, "sourceHint")?.GetComponent<AudioSource>();

        var changed = false;
        if (audio.sourceBgm != sourceBgm)
        {
            audio.sourceBgm = sourceBgm;
            changed = true;
        }

        if (audio.sourceDialogue != sourceDialogue)
        {
            audio.sourceDialogue = sourceDialogue;
            changed = true;
        }

        if (audio.sourceHint != sourceHint)
        {
            audio.sourceHint = sourceHint;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(audio);
            Debug.Log("[wire] GlobalAudioSources: audio sources assigned.");
        }

        return changed;
    }

    static bool WireEventSystem(Scene scene)
    {
        var eventSystemGo = FindTransform(scene, "EventSystem")?.gameObject;
        if (eventSystemGo == null)
        {
            Debug.LogWarning("[wire] EventSystem not found.");
            return false;
        }

        if (eventSystemGo.GetComponent<EventSystem>() == null)
        {
            eventSystemGo.AddComponent<EventSystem>();
        }

        var changed = false;
        var standalone = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standalone != null)
        {
            Object.DestroyImmediate(standalone, true);
            changed = true;
        }

        if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(eventSystemGo);
            Debug.Log("[wire] EventSystem: StandaloneInputModule -> InputSystemUIInputModule.");
        }

        return changed;
    }

    static RectTransform GetRectTransform(Scene scene, string name)
    {
        var t = FindTransform(scene, name);
        if (t == null)
        {
            throw new System.Exception($"[wire] Missing scene node: {name}");
        }

        var rect = t.GetComponent<RectTransform>();
        if (rect == null)
        {
            throw new System.Exception($"[wire] {name} has no RectTransform.");
        }

        return rect;
    }

    static Transform FindTransform(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindInHierarchy(root.transform, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static Transform FindInHierarchy(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var found = FindInHierarchy(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
