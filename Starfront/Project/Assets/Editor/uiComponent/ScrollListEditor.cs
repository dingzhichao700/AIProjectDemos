using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(ScrollList))]
public class ScrollListEditor : Editor {

    private ScrollList _target;

    private SerializedObject binding;

    private SerializedProperty propHoriNum;

    private SerializedProperty propVertNum;

    private SerializedProperty propHoriGap;

    private SerializedProperty propVertGap;

    private SerializedProperty propItemTemplate;

    /**列表内容容器*/
    private RectTransform boxContent;

    /**生成数量*/
    private int generateNum;

    private void OnEnable() {
        _target = target as ScrollList;
        binding = new SerializedObject(target);
        propHoriNum = binding.FindProperty("horiLimitNum");
        propVertNum = binding.FindProperty("vertLimitNum");
        propHoriGap = binding.FindProperty("horiGap");
        propVertGap = binding.FindProperty("vertGap");
        propItemTemplate = binding.FindProperty("itemTemplate");
        ScrollRect scrollRect = _target.GetComponent<ScrollRect>();
        if (scrollRect != null) {
            boxContent = scrollRect.content;
        }
    }

    //OnInspectorGUI中的GUI控件将显示在Test类的Inspector窗口
    public override void OnInspectorGUI() {
        //base.OnInspectorGUI();

        GUILayout.BeginVertical();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("横向数量", GUILayout.MaxWidth(100));
        int.TryParse(GUILayout.TextField(propHoriNum.intValue + ""), out int horiNum);
        if (propHoriNum.intValue != horiNum) {
            propHoriNum.intValue = horiNum;
            binding.ApplyModifiedProperties();
        }
        GUILayout.Label("间距", GUILayout.MaxWidth(100));
        int.TryParse(GUILayout.TextField(propHoriGap.intValue + ""), out int horiGap);
        if (propHoriGap.intValue != horiGap) {
            propHoriGap.intValue = horiGap;
            binding.ApplyModifiedProperties();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("纵向数量", GUILayout.MaxWidth(100));
        int.TryParse(GUILayout.TextField(propVertNum.intValue + ""), out int vertNum);
        if (propVertNum.intValue != vertNum) {
            propVertNum.intValue = vertNum;
            binding.ApplyModifiedProperties();
        }
        GUILayout.Label("间距", GUILayout.MaxWidth(100));
        int.TryParse(GUILayout.TextField(propVertGap.intValue + ""), out int vertGap);
        if (propVertGap.intValue != vertGap) {
            propVertGap.intValue = vertGap;
            binding.ApplyModifiedProperties();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GameObject obj = EditorGUILayout.ObjectField("列表Item预制:", propItemTemplate.objectReferenceValue, typeof(Object), true) as GameObject;
        if (propItemTemplate.objectReferenceValue != obj) {
            propItemTemplate.objectReferenceValue = obj;
            binding.ApplyModifiedProperties();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        int.TryParse(GUILayout.TextField(generateNum + "", GUILayout.MaxWidth(50)), out generateNum);
        generateNum = Mathf.Min(generateNum, 100);
        if (GUILayout.Button("一键填充")) {
            if (propItemTemplate.objectReferenceValue != null) {
                ClearAllItems();
                GenerateItems(generateNum);
                binding.ApplyModifiedProperties();
            } else {
                Debug.LogError("无法填充，原因：item模板为空");
            }
        }
        if (GUILayout.Button("一键清除")) {
            ClearAllItems();
            binding.ApplyModifiedProperties();
            // 获取这个物体对应的预制体 Asset
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(_target.gameObject);
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            //// 保存修改后的预制体
            //PrefabUtility.SaveAsPrefabAsset(_target.gameObject, path);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    /// <summary>
    /// 一键填充
    /// </summary>
    /// <param name="generateNum">填充数量</param>
    private void GenerateItems(int generateNum) {
        ScrollRect scrollRect = _target.GetComponent<ScrollRect>();
        if (scrollRect != null) {
            Rect viewportSize = (_target.transform as RectTransform).rect;
            GameObject itemTemplate = propItemTemplate.objectReferenceValue as GameObject;
            int itemWidth = (int)(itemTemplate.transform as RectTransform).sizeDelta.x;
            int itemHeight = (int)(itemTemplate.transform as RectTransform).sizeDelta.y;
            int horiGap = propHoriGap.intValue;
            int vertGap = propVertGap.intValue;

            int[] horiAndVertNum = ScrollList.CalcuGenerateNum(viewportSize.width, viewportSize.height, itemWidth, itemHeight, propHoriNum.intValue, propVertNum.intValue, horiGap, vertGap, scrollRect.horizontal, generateNum);
            float horiRealNum = horiAndVertNum[0];
            float vertRealNum = horiAndVertNum[1];
            //根据生成数量，设置content容器的尺寸
            float contentW = horiRealNum * (itemWidth + horiGap) - horiGap;
            float contentH = vertRealNum * (itemHeight + vertGap) - vertGap;
            boxContent.sizeDelta = new Vector2(contentW, contentH);

            int generateIndex = 0;
            for (int i = 0; i < vertRealNum; i++) {
                for (int j = 0; j < horiRealNum; j++) {
                    if (generateIndex < generateNum) {
                        GameObject item = GameObject.Instantiate(itemTemplate);
                        item.SetActive(true);
                        item.transform.SetParent(boxContent.transform);
                        (item.transform).localPosition = new Vector3((itemWidth + horiGap) * j, -(itemHeight + vertGap) * i, 0);
                        generateIndex++;
                    }
                }
            }

        }
    }

    /// <summary>
    /// 一键清除
    /// </summary>
    private void ClearAllItems() {
        for (int i = boxContent.childCount - 1; i >= 0; i--) {
            GameObject child = boxContent.GetChild(i).gameObject;
            //Debug.Log("子对象：" + child.name);
            //指定的item模板外，全删掉
            if (propItemTemplate.objectReferenceValue == null || !propItemTemplate.objectReferenceValue.Equals(child)) {
                GameObject.DestroyImmediate(child);
            }
        }
    }

}
