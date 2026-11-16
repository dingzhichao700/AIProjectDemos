#if UNITY_EDITOR
using System.Linq;
using UnityEngine;

/// <summary>刺蛇双动作开发对比，不进入正式玩家界面。</summary>
public sealed class HydraliskRoarPreviewControls : MonoBehaviour {
    private bool automatic = true;
    private float elapsed;
    public int TriggerCount { get; private set; }
    public static HydraliskRoarPreviewControls Install() { return FindObjectOfType<HydraliskRoarPreviewControls>() ?? new GameObject("HydraliskRoarDeveloperControls").AddComponent<HydraliskRoarPreviewControls>(); }
    private CardArtIdleSkin[] Targets() => FindObjectsOfType<CardArtIdleSkin>().Where(x => x.CanPlaySpecial && x.Rig != null && x.GetComponent<CardArtImage>().artId == 2202 && x.GetComponent<CardArtImage>().AnimationActivated).ToArray();
    public async void FireNow() {
        elapsed = 0;
        foreach (var skin in Targets()) { if (await skin.PlaySpecialAsync()) { TriggerCount++; } }
    }
    private void Update() { foreach (var skin in Targets()) { skin.AutomaticSpecialEnabled = automatic; } }
    private void OnGUI() {
        if (Targets().Length == 0) { return; }
        GUILayout.BeginArea(new Rect(8, 8, Mathf.Min(Screen.width - 16, 570), 44), GUI.skin.box);
        GUILayout.BeginHorizontal();GUILayout.Label("Hydralisk / Idle + Roar");
        if (GUILayout.Button("ROAR", GUILayout.Width(75))) { FireNow(); }
        automatic = GUILayout.Toggle(automatic, "Auto", GUILayout.Width(55));
        if (GUILayout.Button("IDLE", GUILayout.Width(65))) { automatic = false; foreach (var skin in Targets()) { skin.AutomaticSpecialEnabled = false; skin.StopSpecial(); } }
        GUILayout.EndHorizontal();GUILayout.EndArea();
    }
}
#endif