#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static partial class CultivationMaxStateReviewValidation {
    // Offline prepared; execute only after coordinator releases the formal single-writer slot.
    private static void TunePowerNumbersR2() {
        const string path = "Assets/Prefab/default/common/PowerDisplay.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            var display = root.GetComponent<PowerDisplay>();
            display.glyphHeight = 34.435f * .92f;
            display.nominalAdvance = 34.435f * .92f;
            display.trackingReduction = 1.565f * .92f;
            float width = 156.52f * .92f;
            display.digitsArea.sizeDelta = new Vector2(width, 40);
            display.digitsArea.anchoredPosition = new Vector2(254 - 12 - width, 0);
            display.SetValue(249);
            Check(Mathf.Abs(display.digitsArea.anchoredPosition.x + width - 242) < .001f, "图片数字右边界左移12设计像素");
            Check(Mathf.Abs(display.glyphHeight / 34.435f - .92f) < .001f, "数字字号及步进缩小8%，长位数同缩放");
            Check(display.digitsArea.anchoredPosition.x >= 97.5f, "最长数字仍与战力标题留有间隔");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (string consumer in new[] { "Assets/Prefab/default/mainView/CardCultivationPanel.prefab", "Assets/Prefab/default/mainView/MainPanel.prefab" }) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(consumer);
            var display = prefab.GetComponentInChildren<PowerDisplay>(true);
            Check(display != null && Mathf.Abs(display.glyphHeight - 34.435f * .92f) < .001f, "两消费端继承同一缩小数字参数=" + consumer);
            Check(Mathf.Abs(display.digitsArea.anchoredPosition.x + display.digitsArea.sizeDelta.x - 242) < .001f, "两消费端继承同一左移边界=" + consumer);
        }
    }
}
#endif
