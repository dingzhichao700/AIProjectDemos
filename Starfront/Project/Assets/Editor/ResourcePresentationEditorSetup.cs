using UnityEditor;

/// <summary>独立编辑器预览使用项目策略；Play 始终由 LoginEntry 安装。</summary>
[InitializeOnLoad]
public static class ResourcePresentationEditorSetup
{
    static ResourcePresentationEditorSetup()
    {
        EditorApplication.delayCall += Install;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Install();
            }
        };
    }

    private static void Install()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ResourcePresentation.Install(new GameResourcePresentationPolicy());
        }
    }
}
