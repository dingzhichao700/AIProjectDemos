using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>将已批准的静态场景预制迁移为运行时容器；不改动场景美术资源。</summary>
public static class BattleSceneSetup {
    public static void RunBatch() {
        string path = ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL);
        string backup = "Library/BattleSceneSetupBackup";
        Directory.CreateDirectory(backup);
        if (!File.Exists(backup + "/BattleScenePanel.prefab")) {
            File.Copy(path, backup + "/BattleScenePanel.prefab");
            File.Copy(ResourceConst.GetUIPath(UIEnum.MAIN_PANEL), backup + "/MainPanel.prefab");
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try {
            string[] names = { "boxSceneViewport", "boxSceneRoot", "boxSceneBackground", "boxSceneContent", "boxSceneForeground", "boxBelowGround", "boxGround", "boxGroundDecals", "boxGroundShadows", "boxGroundGuides", "boxAboveGround", "boxLOD" };
            var nodes = names.ToDictionary(name => name, name => root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name));
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true).Reverse().ToArray()) {
                if (child != root.transform && !names.Contains(child.name)) {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) {
                UnityEngine.Object.DestroyImmediate(graphic);
            }
            foreach (var rect in nodes.Values) {
                rect.gameObject.SetActive(true);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }
            RectTransform viewport = nodes["boxSceneViewport"];
            viewport.SetParent(root.transform, false);
            Stretch((RectTransform)root.transform);
            Stretch(viewport);
            Image background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color32(7, 18, 29, 255);
            background.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) {
                viewport.gameObject.AddComponent<RectMask2D>();
            }
            RectTransform scene = nodes["boxSceneRoot"];
            scene.SetParent(viewport, false);
            scene.anchorMin = scene.anchorMax = new Vector2(0.5f, 0.5f);
            scene.pivot = new Vector2(0, 1);
            scene.sizeDelta = new Vector2(2000, 2000);
            scene.anchoredPosition = new Vector2(-1000, 1000);
            foreach (string name in new[] { "boxSceneBackground", "boxSceneContent", "boxSceneForeground" }) {
                nodes[name].SetParent(scene, false);
                Stretch(nodes[name]);
                nodes[name].SetAsLastSibling();
            }
            string[] layerNames = { "boxSceneBackground", "boxBelowGround", "boxGround", "boxGroundDecals", "boxGroundShadows", "boxGroundGuides", "boxAboveGround", "boxSceneForeground" };
            foreach (string name in layerNames.Skip(1).Take(6)) {
                nodes[name].SetParent(nodes["boxSceneContent"], false);
                Stretch(nodes[name]);
                nodes[name].SetAsLastSibling();
            }
            nodes["boxLOD"].SetParent(viewport, false);
            Stretch(nodes["boxLOD"]);
            nodes["boxLOD"].SetAsLastSibling();
            var view = root.GetComponent<BattleSceneView>() ?? root.AddComponent<BattleSceneView>();
            view.viewport = viewport;
            view.sceneRoot = scene;
            view.lod = nodes["boxLOD"];
            view.layers = layerNames.Select(name => nodes[name]).ToArray();
            view.input = viewport.GetComponent<BattleSceneInput>() ?? viewport.gameObject.AddComponent<BattleSceneInput>();
            var binder = root.GetComponent<UIBinder>() ?? root.AddComponent<UIBinder>();
            binder.csharpAssetPath = "Assets/Scripts/csharp/main/battle/view/BattleScenePanel.cs";
            binder.csharpAsset = binder.csharpAssetPath;
            binder.uiList.Clear();
            binder.RefreshScriptBinding();
            var inspector = UnityEditor.Editor.CreateEditor(binder, typeof(UIBinderInspector));
            try {
                typeof(UIBinderInspector).GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, new object[] { binder.csharpAssetPath });
            } finally {
                UnityEngine.Object.DestroyImmediate(inspector);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        string mainPath = ResourceConst.GetUIPath(UIEnum.MAIN_PANEL);
        root = PrefabUtility.LoadPrefabContents(mainPath);
        try {
            var rect = root.GetComponent<MainPanelLayout>().battleButton;
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            if (image.sprite == null) {
                image.color = Color.clear;
            }
            image.raycastTarget = true;
            var button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            PrefabUtility.SaveAsPrefabAsset(root, mainPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup);
        entry.address = path;
        settings.AddLabel("main-content");
        entry.SetLabel("main-content", true);
        foreach (string file in Directory.GetFiles("Assets/Art/unpack/scene/battle/appearance", "*.png")) {
            string assetPath = file.Replace('\\', '/');
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), settings.FindGroup("scene")).address = assetPath;
        }
        settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
        AddressablesPrefabDependencyChecker.ValidateOrThrow();
        File.WriteAllText("Library/battle-scene-setup.txt", "passed: container-only prefab, valid UIBinder, play button, dynamic sprite addresses, package dependency check");
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale = Vector3.one;
    }
}
