#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(GameButton))]
public class GameButtonEditor : Editor {
    const string COMMON_ATLAS_PATH = "Assets/Art/atlas/default/common/common.png";

    SerializedProperty interactableProp;
    SerializedProperty overSoundTypeProp;
    SerializedProperty clickSoundTypeProp;
    SerializedProperty skinTypeProp;
    SerializedProperty labelProp;
    SerializedProperty textProp;
    SerializedProperty enableHoverScaleProp;

    void OnEnable() {
        interactableProp = serializedObject.FindProperty("_interactable");
        overSoundTypeProp = serializedObject.FindProperty("overSoundType");
        clickSoundTypeProp = serializedObject.FindProperty("clickSoundType");
        skinTypeProp = serializedObject.FindProperty("skinType");
        labelProp = serializedObject.FindProperty("label");
        textProp = serializedObject.FindProperty("text");
        enableHoverScaleProp = serializedObject.FindProperty("enableHoverScale");
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();

        var btn = (GameButton)target;
        int oldSkin = skinTypeProp.enumValueIndex;

        // ==== 交互 ====
        EditorGUILayout.LabelField("交互", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(interactableProp, new GUIContent("可点击"));

        EditorGUILayout.Space(4);

        // ==== 音效 ====
        EditorGUILayout.LabelField("音效", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(overSoundTypeProp, new GUIContent("划过音效"));
        EditorGUILayout.PropertyField(clickSoundTypeProp, new GUIContent("点击音效"));

        EditorGUILayout.Space(4);

        // ==== 外观 ====
        EditorGUILayout.LabelField("外观", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(skinTypeProp, new GUIContent("按钮皮肤"));
        if (enableHoverScaleProp != null) {
            EditorGUILayout.PropertyField(enableHoverScaleProp, new GUIContent("启用划过缩放效果"));
        }

        EditorGUILayout.Space(4);

        // ==== 文本 ====
        EditorGUILayout.LabelField("文本", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(labelProp, new GUIContent("文本组件 (TextMeshProUGUI)"));
        EditorGUILayout.PropertyField(textProp, new GUIContent("显示文本"), GUILayout.MinHeight(40));

        EditorGUILayout.Space(4);

        // ==== 皮肤应用 & 提示 ====
        Image image = btn.GetComponent<Image>();
        if (image == null) {
            EditorGUILayout.HelpBox("当前 GameButton 上没有 Image，将作为纯文本按钮使用（不应用皮肤贴图）。", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Skin (Editor Only)")) {
            ApplySkinSprite(btn);
        }
        if (GUILayout.Button("Refresh Text (Editor Only)")) {
            // 手动刷新一下文本到 TMP
            btn.Label = btn.text;
            EditorUtility.SetDirty(btn);
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();

        // 皮肤变更时自动应用
        if (skinTypeProp.enumValueIndex != oldSkin) {
            ApplySkinSprite(btn);
        }
    }

    static void ApplySkinSprite(GameButton btn) {
        Image image = btn.GetComponent<Image>();
        if (image == null) {
            // 纯文字按钮就不处理
            return;
        }

        string spriteName = GetSpriteNameForSkin(btn.skinType);
        if (string.IsNullOrEmpty(spriteName))
            return;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(COMMON_ATLAS_PATH);
        foreach (var a in assets) {
            if (a is Sprite s && s.name == spriteName) {
                image.sprite = s;
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(btn);
                return;
            }
        }

        Debug.LogWarning($"Sprite '{spriteName}' not found in {COMMON_ATLAS_PATH}");
    }

    /** 获取按钮皮肤名称 */
    static string GetSpriteNameForSkin(ButtonSkinType type) {
        switch (type) {
            case ButtonSkinType.Default:
                return "btn_common1";
            case ButtonSkinType.Confirm:
                return "btn_green";
            case ButtonSkinType.Cancel:
                return "btn_orange";
            default:
                return null;
        }
    }
}
#endif
