using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>主阵容的职业与等级统一前景层；不改变角色承托点或其他窗口的上阵组件。</summary>
public sealed class FormationSlotInfo : MonoBehaviour {
    public RectTransform overlay;
    public RectTransform plate;
    public RectTransform support;
    public Image badge;
    public Image role;
    public Image shadow;
    public TextMeshProUGUI title;
    private bool initialized;
    private bool occupied;
    private Vector3 localOrigin;
    private Vector3 sourceScale;

    public void Refresh(bool hasCard, int roleId) {
        if (!initialized) {
            localOrigin = support.InverseTransformPoint(plate.position) + Vector3.down * 1f;
            sourceScale = plate.localScale;
            Vector2 size = plate.rect.size;
            // 保留原组件绑定，仅把整块信息放到所有角色之后的共同父层。
            plate.SetParent(overlay, true);
            plate.anchorMin = plate.anchorMax = new Vector2(0, 1);
            plate.sizeDelta = size;
            initialized = true;
        }
        occupied = hasCard;
        title.gameObject.SetActive(false);
        float scale = roleId == 103 || roleId == 104 ? .8f : 1f;
        role.rectTransform.localScale = Vector3.one * scale;
        plate.gameObject.SetActive(occupied && isActiveAndEnabled);
        Follow();
    }

    private void OnEnable() {
        if (initialized) {
            plate.gameObject.SetActive(occupied);
            Follow();
        }
    }

    private void OnDisable() {
        if (initialized && plate != null) {
            plate.gameObject.SetActive(false);
        }
    }

    private void LateUpdate() {
        if (initialized) {
            Follow();
        }
    }

    private void Follow() {
        plate.position = support.TransformPoint(localOrigin);
        Vector3 a = support.lossyScale;
        Vector3 b = overlay.lossyScale;
        plate.localScale = new Vector3(sourceScale.x * a.x / b.x, sourceScale.y * a.y / b.y, 1);
        title.gameObject.SetActive(false);
    }

    private void OnDestroy() {
        if (initialized && plate != null) {
            Destroy(plate.gameObject);
        }
    }
}
