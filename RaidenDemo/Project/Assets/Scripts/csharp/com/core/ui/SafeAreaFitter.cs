using UnityEngine;

/// <summary>
/// 将节点约束到设备安全区域，供 HUD 和关键交互控件使用。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SafeAreaFitter : MonoBehaviour
{
    Rect lastSafeArea;
    Vector2Int lastScreenSize;

    public static RectTransform GetOrCreate(RectTransform parent)
    {
        if (parent == null)
        {
            return null;
        }

        var child = parent.Find("SafeAreaRoot") as RectTransform;
        if (child == null)
        {
            var go = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(SafeAreaFitter));
            child = go.GetComponent<RectTransform>();
            child.SetParent(parent, false);
        }
        else if (child.GetComponent<SafeAreaFitter>() == null)
        {
            child.gameObject.AddComponent<SafeAreaFitter>();
        }

        return child;
    }

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize)
        {
            Apply();
        }
    }

    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        var rect = transform as RectTransform;
        var safeArea = Screen.safeArea;
        rect.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
        rect.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
