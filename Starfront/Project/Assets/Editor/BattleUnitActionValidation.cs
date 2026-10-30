using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>通用动作选择与实际裁剪锚点回归，不从表现触发伤害。</summary>
public static class BattleUnitActionValidation {
    private static void Check(bool value, string name) {
        if (!value) {
            throw new InvalidOperationException("Unit action validation: " + name);
        }
    }
    private static void Set(object target, string property, object value) {
        target.GetType().GetProperty(property).SetValue(target, value);
    }
    private static void Call(object target, string method, params object[] args) {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }

    public static void RegisterAssets() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("scene");
        foreach (string direction in new[] { "Up", "UpRight", "Right", "DownRight", "Down" }) {
            string path = "Assets/Art/frameAnimation/scene/Colonial/Infantry/Idle_" + direction;
            foreach (string ext in new[] { ".png", ".json" }) {
                AssetDatabase.ImportAsset(path + ext, ImportAssetOptions.ForceSynchronousImport);
                Check(AssetDatabase.LoadMainAssetAtPath(path + ext) != null, "idle asset exists");
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path + ext), group).SetAddress(path + ext);
            }
        }
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(group);
        AssetDatabase.SaveAssets();
    }

    public static void Validate() {
        var t = CfgManager.tables;
        var scene = new BattleSceneModel(t.BattleSceneObj.Get(1001), t.BattleSceneCellObj.DataList);
        var root = new GameObject("UnitActionFixture", typeof(RectTransform)).GetComponent<RectTransform>();
        var shadowRoot = new GameObject("ShadowFixture", typeof(RectTransform)).GetComponent<RectTransform>();
        var view = new BattleUnitView(scene, root, shadowRoot);
        var unit = new BattleUnitVO(900001, t.UnitObj.Get(1101), BattleSceneCamp.FRIENDLY, new Vector2(500, 500));
        scene.AddElement(unit);
        var starts = (Dictionary<long, float>)typeof(BattleUnitView).GetField("idleStarts", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        var full = new Rect(0, 0, 2000, 2000);
        var empty = new Rect(-10000, -10000, 1, 1);
        var headings = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };
        int checkedFrames = 0;
        try {
            foreach (var heading in headings) {
                Call(unit, "Face", unit.position + heading);
                foreach (var action in new[] { UnitActionType.IDLE, UnitActionType.MOVE, UnitActionType.ATTACK_1 }) {
                    Call(unit, "StopMoving");
                    Set(unit, "activeCast", null);
                    if (action == UnitActionType.MOVE) {
                        Call(unit, "SetRoute", new List<Vector2> { unit.position + heading * 1000 });
                    } else if (action == UnitActionType.ATTACK_1) {
                        var cast = (BattleSkillCastVO)Activator.CreateInstance(typeof(BattleSkillCastVO), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L, unit.skills[0], unit.id, 900002L, 0d }, null);
                        Set(unit, "activeCast", cast);
                    }
                    var appearance = unit.config.Appearances.Single(a => a.Action == action && a.Direction == BattleAim.ResolveDirection(heading));
                    var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(appearance.Resource));
                    Check(animation != null, "preloaded " + action);
                    double time = 0;
                    for (int frame = 0; frame < animation.sprites.Length; frame++) {
                        double sample = time + animation.durations[frame] / 2d;
                        if (action == UnitActionType.IDLE) {
                            starts[unit.id] = RookieEngine.playerTimer.curTime - (float)sample;
                        } else {
                            Set(unit, action == UnitActionType.MOVE ? "animationMs" : "skillTimeMs", sample);
                        }
                        view.Refresh(full);
                        var image = root.Find("unit_" + unit.id).GetComponent<Image>();
                        Check(image.sprite == animation.sprites[frame], "frame " + action + "/" + frame);
                        Vector2 foot = Vector2.Scale(animation.sourceSizes[frame], animation.pivot) - animation.frameOffsets[frame];
                        var actual = image.rectTransform.TransformPoint(image.rectTransform.rect.min + foot);
                        var expected = root.TransformPoint(new Vector3(root.rect.xMin + unit.position.x, root.rect.yMax - unit.position.y + scene.config.GroundThickness, 0));
                        Check(Vector3.Distance(actual, expected) < .001f, "cross-action trimmed anchor");
                        Check(Mathf.Sign(image.rectTransform.localScale.x) == (heading.x < 0 ? -1 : 1), "direction mirror");
                        foreach (float height in new[] { 0f, 40f, 80f }) {
                            Call(unit, "SetAltitude", height);
                            view.Refresh(full);
                            var shadow = shadowRoot.Find("shadow_" + unit.id).GetComponent<BattleUnitShadowGraphic>();
                            Check(shadow.mainTexture == image.sprite.texture, "same frame texture");
                            Check(!shadow.raycastTarget && shadow.transform.parent == shadowRoot && image.transform.parent == root, "independent layers without input");
                            Check(Mathf.Abs(image.rectTransform.anchoredPosition.y - (-unit.position.y + scene.config.GroundThickness + height)) < .001f, "body altitude");
                            using (var mesh = new VertexHelper()) {
                                typeof(BattleUnitShadowGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(shadow, new object[] { mesh });
                                var vertex = new UIVertex();
                                Vector2[] source = { Vector2.zero, new Vector2(0, image.sprite.rect.height), image.sprite.rect.size, new Vector2(image.sprite.rect.width, 0) };
                                for (int i = 0; i < 4; i++) {
                                    mesh.PopulateUIVertex(ref vertex, i);
                                    Vector2 local = source[i] - foot;
                                    Vector2 expectedProjection = new Vector2(unit.position.x + (heading.x < 0 ? -local.x : local.x) - Mathf.Cos(BattleConst.ShadowAngle * Mathf.Deg2Rad) * BattleConst.ShadowLength * (local.y + height), -unit.position.y + scene.config.GroundThickness - Mathf.Sin(BattleConst.ShadowAngle * Mathf.Deg2Rad) * BattleConst.ShadowLength * (local.y + height));
                                    Check(Vector2.Distance(shadow.rectTransform.anchoredPosition + (Vector2)vertex.position, expectedProjection) < .001f, "projected vertex and foot anchor");
                                    Check(vertex.color.a == ((Color32)new Color(0, 0, 0, BattleConst.ShadowAlpha)).a, "shadow alpha");
                                }
                            }
                        }
                        Call(unit, "SetAltitude", 0f);
                        time += animation.durations[frame];
                        checkedFrames++;
                    }
                    if (action == UnitActionType.ATTACK_1) {
                        Set(unit, "skillTimeMs", 10000d);
                        view.Refresh(full);
                        Check(root.Find("unit_" + unit.id).GetComponent<Image>().sprite == animation.sprites.Last(), "hold while release occupies unit");
                    } else if (action == UnitActionType.IDLE) {
                        starts[unit.id] = RookieEngine.playerTimer.curTime - animation.totalDuration - 1;
                        view.Refresh(empty);
                        Check(view.visibleCount == 0 && starts.ContainsKey(unit.id), "offscreen retains idle clock");
                        view.Refresh(full);
                        Check(root.Find("unit_" + unit.id).GetComponent<Image>().sprite == animation.sprites[0], "idle loop survives pool recycle");
                    }
                }
                Set(unit, "activeCast", null);
                Set(unit, "state", BattleUnitState.Attacking);
                view.Refresh(full);
                Check(BattleUnitView.ResolveAction(unit) == UnitActionType.IDLE && starts.ContainsKey(unit.id), "cooldown waiting returns idle even in attacking state");
                Check(unit.hp == unit.maxHp, "view never damages");
            }
            Call(unit, "SetAltitude", 4000f);
            view.Refresh(new Rect(400, 400, 200, 200));
            Check(view.visibleCount == 0, "both body and offset shadow offscreen");
            var ground = unit.position - new Vector2(0, scene.config.GroundThickness);
            view.Refresh(new Rect(ground.x - Mathf.Cos(BattleConst.ShadowAngle * Mathf.Deg2Rad) * BattleConst.ShadowLength * 4000 - 100, ground.y + Mathf.Sin(BattleConst.ShadowAngle * Mathf.Deg2Rad) * BattleConst.ShadowLength * 4000 - 100, 200, 200));
            Check(view.visibleCount == 1, "shadow-only visibility survives culling");
            Check(unit.position == new Vector2(500, 500), "altitude does not move logical ground point");
            Call(unit, "SetAltitude", 0f);
            try {
                Call(unit, "SetAltitude", float.NaN);
                throw new Exception("NaN altitude was accepted");
            } catch (TargetInvocationException e) {
                Check(e.InnerException is ArgumentOutOfRangeException, "invalid altitude rejected");
            }
        } finally {
            view.Dispose();
            Check(root.GetComponentsInChildren<Image>(true).All(i => i.sprite == null), "references cleared");
            Check(shadowRoot.GetComponentsInChildren<BattleUnitShadowGraphic>(true).All(v => !v.gameObject.activeSelf && v.mainTexture == Texture2D.whiteTexture), "shadow pool clears texture references");
            UnityEngine.Object.Destroy(shadowRoot.gameObject);
            scene.Dispose();
            UnityEngine.Object.Destroy(root.gameObject);
        }
        File.WriteAllText("Library/battle-unit-action-validation.txt", "passed: " + checkedFrames + " directional frames x heights 0/40/80; projected vertices; same texture; shadow-only culling; invalid height; shadow cleanup; configured move/attack/idle; trimmed foot pivot; left mirror; one-shot hold during cast; idle after release/cancel; cooldown waiting; idle loop; offscreen recovery; pool cleanup; no presentation damage");
    }
}
