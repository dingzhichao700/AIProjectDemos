#if UNITY_EDITOR
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>只存在于Editor验收现场的操作条，不进入正式UI Prefab或玩家数据。</summary>
public sealed class MarineBurstPreviewControls : MonoBehaviour {
    // Review toolbar is opt-in; normal automatic motion is driven by CardArtIdleSkin.
    public static bool ToolbarVisible { get; set; } = false;
    private bool automatic = true;
    private bool loading;
    private float elapsed = 1.8f;
    public int AutomaticBurstsStarted { get; private set; }

    public static MarineBurstPreviewControls Install() {
        var existing = FindObjectOfType<MarineBurstPreviewControls>();
        return existing != null ? existing : new GameObject("MarineBurstDeveloperControls").AddComponent<MarineBurstPreviewControls>();
    }

    public void EnableAutomatic() { automatic = true; elapsed = 2.6f; }

    private void Update() {
        foreach (var skin in FindObjectsOfType<CardArtIdleSkin>().Where(x => x.CanPlaySpecial && x.GetComponent<CardArtImage>().artId == 2101)) { skin.AutomaticSpecialEnabled = automatic; }
    }

    private async void Fire() {
        if (loading) { return; }
        loading = true;
        AutomaticBurstsStarted++;
        try { await Task.WhenAll(FindObjectsOfType<CardArtIdleSkin>().Where(x => x.CanPlaySpecial && x.GetComponent<CardArtImage>().artId == 2101 && x.Rig != null && x.isActiveAndEnabled).Select(x => x.PlayBurstAsync())); }
        finally { loading = false; }
    }

    private void OnGUI() {
        if (!ToolbarVisible) { return; }
        if (!FindObjectsOfType<CardArtIdleSkin>().Any(x => x.CanPlaySpecial && x.GetComponent<CardArtImage>().artId == 2101 && x.Rig != null && x.isActiveAndEnabled)) { return; }
        GUILayout.BeginArea(new Rect(8, 8, Mathf.Min(Screen.width - 16, 570), 44), GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Marine 2101 / 3-round burst");
        if (GUILayout.Button("FIRE x3", GUILayout.Width(85))) { Fire(); }
        automatic = GUILayout.Toggle(automatic, "Auto", GUILayout.Width(55));
        if (GUILayout.Button("IDLE", GUILayout.Width(65))) {
            automatic = false;
            foreach (var skin in FindObjectsOfType<CardArtIdleSkin>()) { if (skin.CanPlaySpecial && skin.GetComponent<CardArtImage>().artId == 2101) { skin.AutomaticSpecialEnabled = false; skin.StopSpecial(); } }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
#endif
