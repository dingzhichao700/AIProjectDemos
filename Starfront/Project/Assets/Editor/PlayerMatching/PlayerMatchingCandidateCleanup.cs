#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>仅清除匹配页候选中的固定显示样例，保留模板、绑定及静态画面资源。</summary>
public static class PlayerMatchingCandidateCleanup {
    [Serializable] public sealed class Plan { public string prefab; }
    [Serializable] public sealed class Plans { public Plan[] plans; }
    [Serializable] public sealed class Report { public string status; public string[] prefabs; public int imageReferencesCleared,labelsCleared; public string[] rootDependencies; }

    private static void EnsureTeamCapacity(GameObject root) {
        var binder=root.GetComponent<UIBinder>();
        if (binder==null || !binder.csharpAssetPath.EndsWith("PlayerMatchingTeamView.cs",StringComparison.Ordinal)) return;
        string side=root.name.Contains("enemy") ? "enemy" : "friendly";
        string path="Assets/Prefab/uiBattle/battle/playerMatching/components/Team_"+side+"_full.prefab";
        GameObject reference=PrefabUtility.LoadPrefabContents(path);
        try {
            for (int i=1;i<=4;i++) {
                string alias="card"+i;
                if (binder.uiList.Exists(member=>member.uiName==alias)) continue;
                var template=(RectTransform)reference.transform.Find("itemCard_"+i);
                GameObject card=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/uiBattle/battle/playerMatching/components/Card_Base.prefab"),root.transform);
                card.name=template.name;
                var rect=(RectTransform)card.transform;
                rect.anchorMin=template.anchorMin; rect.anchorMax=template.anchorMax; rect.pivot=template.pivot;
                rect.sizeDelta=template.sizeDelta; rect.anchoredPosition=template.anchoredPosition;
                rect.localScale=template.localScale; rect.localRotation=template.localRotation;
                card.SetActive(false);
                binder.uiList.Add(new UIBindComponentData {id=binder.uiList.Count+1,uiName=alias,uiTypeName="PlayerMatchingCardView",isCustomClass=true,customClassName="PlayerMatchingCardView",go=card});
            }
            binder.RefreshScriptBinding(); EditorUtility.SetDirty(binder);
        } finally { PrefabUtility.UnloadPrefabContents(reference); }
    }

    public static string Run(string planPath) {
        Plans plan=JsonUtility.FromJson<Plans>(File.ReadAllText(planPath));
        var paths=new List<string>(); int images=0,labels=0;
        foreach (Plan item in plan.plans) {
            if (!item.prefab.StartsWith("Assets/Prefab/uiBattle/battle/playerMatching/",StringComparison.Ordinal)) throw new ArgumentException("清理超出本页范围");
            GameObject root=PrefabUtility.LoadPrefabContents(item.prefab);
            try {
                if (root.name=="Profession_vanguard") {
                    Image icon=root.transform.Find("imgProfessionIcon").GetComponent<Image>();
                    if (icon.sprite!=null) images++;
                    icon.sprite=null; icon.enabled=false; EditorUtility.SetDirty(icon);
                }
                // 空/少卡/排除态只是数据不同；每方保留相同4卡容量，不能由样例锁死运行槽位。
                EnsureTeamCapacity(root);
                foreach (UIBinder binder in root.GetComponentsInChildren<UIBinder>(true)) {
                    foreach (var member in binder.uiList) {
                        if (member.go==null) throw new InvalidOperationException("缺少实际绑定对象："+member.uiName);
                        if (member.uiName=="imgArtwork" || member.uiName=="imgQuality" || member.uiName=="imgProfession" || member.uiName=="imgPortrait") {
                            Image image=member.go.GetComponent<Image>();
                            if (image==null) throw new InvalidOperationException("缺少Image："+member.uiName);
                            if (image.sprite!=null) images++;
                            image.sprite=null; image.enabled=false; EditorUtility.SetDirty(image);
                        }
                        if (member.uiName=="txtCost" || member.uiName=="txtName" || member.uiName=="txtIdentity" || member.uiName=="txtLevel" || member.uiName=="txtPercent" || member.uiName=="txtStatus" || member.uiName=="txtExcluded") {
                            var text=member.go.GetComponent<TextMeshProUGUI>();
                            if (text==null) throw new InvalidOperationException("缺少动态文本："+member.uiName);
                            if (!string.IsNullOrEmpty(text.text)) labels++;
                            text.text=string.Empty; EditorUtility.SetDirty(text);
                        }
                        if (member.uiName=="fillMask") member.go.SetActive(false);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,item.prefab); paths.Add(item.prefab);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return JsonUtility.ToJson(new Report {status="passed",prefabs=paths.ToArray(),imageReferencesCleared=images,labelsCleared=labels,rootDependencies=AssetDatabase.GetDependencies("Assets/Prefab/uiBattle/battle/playerMatching/PlayerMatchingPanel.prefab",true)},true);
    }
}
#endif
