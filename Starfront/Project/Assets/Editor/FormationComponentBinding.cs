using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// One-shot component migration, requested by the owning editor process.
[InitializeOnLoad]
internal static class FormationComponentBinding {
    const string Queue = "Library/AIUI/";
    const string Pending = Queue + "pending-formation-component-binding.json";
    const string DirectoryPath = "Assets/Prefab/default/mainView/components/";
    const string Art = "Assets/Art/atlas/default/mainView.png";
    const string Badge = DirectoryPath + "CellCostBadge.prefab";
    static FormationComponentBinding() { EditorApplication.update += Poll; }
    [Serializable] class Request { public int processId; public string requestId; }
    [Serializable] class Result { public string requestId, status, error; public int slots, libraryInstances; }
    static Sprite SpriteAt(string name) {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().SingleOrDefault(s => s.name == "quality-" + name);
        if (!sprite) throw new InvalidDataException(name);
        return sprite;
    }
    static void Rect(RectTransform rt, float x, float y, float w, float h) {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1);
        rt.anchoredPosition = new Vector2(x,-y);
        rt.sizeDelta = new Vector2(w,h);
    }
    static GameObject CreateBadge() {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Badge);
        if (asset) return asset;
        var root = new GameObject("CellCostBadge",typeof(RectTransform),typeof(Image));
        try {
            Rect((RectTransform)root.transform,0,0,51,52);
            var icon=root.GetComponent<Image>(); icon.sprite=SpriteAt("cost-base");icon.raycastTarget=false;
            var node=new GameObject("txtCost",typeof(RectTransform),typeof(TextMeshProUGUI));
            node.transform.SetParent(root.transform,false);
            Rect((RectTransform)node.transform,5,5,41,42);
            var label=node.GetComponent<TextMeshProUGUI>();
            label.font=AssetDatabase.LoadAssetAtPath<GameObject>(DirectoryPath+"DeckLibraryCard.prefab").GetComponentInChildren<DeckCardView>(true).level.font;
            label.fontSize=20;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.text="—";
            return PrefabUtility.SaveAsPrefabAsset(root,Badge);
        } finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    static void Bind(string name, GameObject badge) {
        var path=DirectoryPath+name+".prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try {
            var card=root.GetComponentInChildren<DeckCardView>(true);
            var child=card.transform.Find("CellCostBadge");
            if (!child) child=((GameObject)PrefabUtility.InstantiatePrefab(badge,card.transform)).transform;
            Rect((RectTransform)child,card.formation?38:0,0,51,52);
            child.SetAsLastSibling();
            card.cellCostBadge=child.gameObject;card.cellCostLabel=child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!card.formation) {
                card.qualitySprites=new[]{"green","blue","purple","gold"}.Select(k=>SpriteAt("frame-"+k)).ToArray();
                card.lockedQualitySprite=SpriteAt("frame-locked");
                var visual=(RectTransform)card.transform;
                foreach(var cardRect in new[]{(RectTransform)root.transform,visual}) cardRect.sizeDelta=new Vector2(156,198);
                visual.anchoredPosition=new Vector2(78,-99);
                var roleBase=card.transform.Find("imgRoleBadgeBg").GetComponent<Image>();
                roleBase.sprite=SpriteAt("profession-base");
                Rect(roleBase.rectTransform,119,5,32,32);
                Rect(card.roleIcon.rectTransform,119,5,32,32);
                var info=card.transform.Find("imgFormationInfo");
                if(!info) {
                    info=new GameObject("imgFormationInfo",typeof(RectTransform),typeof(Image)).transform;
                    info.SetParent(card.transform,false);
                }
                info.GetComponent<Image>().sprite=SpriteAt("information-bg");info.GetComponent<Image>().raycastTarget=false;
                Rect((RectTransform)info,6,138,144,54);
                info.SetAsLastSibling();
                Rect(card.title.rectTransform,10,140,96,22);card.title.transform.SetAsLastSibling();
                Rect(card.level.rectTransform,109,140,37,22);card.level.transform.SetAsLastSibling();
                var track=card.transform.Find("imgFragmentProgressBg").GetComponent<Image>();
                track.sprite=SpriteAt("progress-track");Rect(track.rectTransform,12,167,132,20);track.transform.SetAsLastSibling();
                card.fragmentFill.sprite=SpriteAt("progress-fill");Rect(card.fragmentFill.rectTransform,14,169,128,16);
                card.fragmentFill.transform.SetAsLastSibling();
                Rect(card.fragments.rectTransform,12,167,132,20);card.fragments.transform.SetAsLastSibling();
                roleBase.transform.SetAsLastSibling();card.roleIcon.transform.SetAsLastSibling();child.SetAsLastSibling();
                var image=card.qualityImage;
                image.sprite=card.qualitySprites[0];image.type=Image.Type.Sliced;image.fillCenter=false;image.preserveAspect=false;
                // Follow the existing visual container, including local size overrides.
                var rt=image.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
                image.transform.SetAsLastSibling();
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    static void Poll() {
        if (!File.Exists(Pending)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
        if(request.processId!=System.Diagnostics.Process.GetCurrentProcess().Id||string.IsNullOrEmpty(request.requestId))return;
        File.Delete(Pending);var result=new Result{requestId=request.requestId};
        try {
            var stagePath=DirectoryPath+"DeckFormationBase.prefab";
            var stageRoot=PrefabUtility.LoadPrefabContents(stagePath);
            try {
                var back=stageRoot.transform.Find("imgBadgeBase").GetComponent<Image>();back.sprite=SpriteAt("profession-base");
                Rect(back.rectTransform,34,27,32,32);
                stageRoot.transform.Find("imgBadgeBase/imgRole").GetComponent<RectTransform>().sizeDelta=new Vector2(32,32);
                PrefabUtility.SaveAsPrefabAsset(stageRoot,stagePath);
            } finally {PrefabUtility.UnloadPrefabContents(stageRoot);}
            var badge=CreateBadge();Bind("DeckFormationSlot",badge);Bind("DeckLibraryCard",badge);
            var main=PrefabUtility.LoadPrefabContents("Assets/Prefab/default/mainView/MainPanel.prefab");
            try {
                var view=main.GetComponentInChildren<DeckPresentationView>(true);
                if (!view || !view.library || view.slots.Length!=4) throw new InvalidDataException("ViewDeck binding");
                foreach(var slot in view.slots) {
                    if (!slot || !slot.formation || !slot.cellCostBadge || !slot.cellCostLabel || slot.qualityImage.GetComponentInParent<UIBinder>(true)?.uiList.Any(m=>m.uiName=="stageImage" && m.go==slot.qualityImage.gameObject) != true)throw new InvalidDataException("slot prefab: "+(slot?slot.name+" formation="+slot.formation+" badge="+(bool)slot.cellCostBadge+" label="+(bool)slot.cellCostLabel+" stage="+(bool)slot.qualityImage:"null"));
                    result.slots++;
                }
                foreach(var card in view.GetComponentsInChildren<DeckCardView>(true).Where(c=>!c.formation)){
                    if(!card.GetComponentInParent<UIBinder>(true))throw new InvalidDataException("card binder");
                    if(!card.cellCostBadge || !card.cellCostLabel || !card.lockedQualitySprite || card.qualitySprites.Length!=4 || card.qualityImage.type!=Image.Type.Sliced || card.qualityImage.fillCenter)throw new InvalidDataException("card prefab");
                    foreach(var frame in card.qualitySprites.Concat(new[]{card.lockedQualitySprite}))if(frame.border!=new Vector4(16,16,16,16))throw new InvalidDataException("border");
                    result.libraryInstances++;
                }
                foreach(var card in view.GetComponentsInChildren<DeckCardView>(true).Where(c=>!c.formation)) {
                    if(!card.GetComponentInParent<UIBinder>(true))throw new InvalidDataException("card binder");
                    var sprite=card.qualityImage.sprite;
                    var locked=card.lockIcon && card.lockIcon.gameObject.activeSelf;
                    if(sprite && (sprite.name.StartsWith("frame-quality-") || sprite.name.StartsWith("frame-") || sprite.name.StartsWith("quality-frame-"))) {
                        if(locked) card.qualityImage.sprite=card.lockedQualitySprite;
                        else if(sprite.name.StartsWith("frame-quality-") && int.TryParse(sprite.name.Substring(14),out var q) && q>=2 && q<=5) card.qualityImage.sprite=card.qualitySprites[q-2];
                        PrefabUtility.RecordPrefabInstancePropertyModifications(card.qualityImage);
                    }
                }
                foreach(var slot in view.slots) {
                    slot.cellCostBadge.SetActive(slot.artwork.sprite!=null);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slot.cellCostBadge);
                }
                PrefabUtility.SaveAsPrefabAsset(main,"Assets/Prefab/default/mainView/MainPanel.prefab");
                if(result.libraryInstances==0)throw new InvalidDataException("library instances");
            } finally {PrefabUtility.UnloadPrefabContents(main);}
            var probe=PrefabUtility.LoadPrefabContents(DirectoryPath+"DeckLibraryCard.prefab");
            try {
                var card=probe.GetComponentInChildren<DeckCardView>(true);
                var data=new DeckCardView.DisplayData{cardId=1101,quality=2,cellUnlockCost=45,level=1};
                card.Render(data,null);
                if(card.qualityImage.sprite!=card.qualitySprites[0] || card.cellCostLabel.text!="45")throw new InvalidDataException("owned");
                data.owned=false;data.cellUnlockCost=null;card.Render(data,null);
                if(card.qualityImage.sprite!=card.lockedQualitySprite || !card.lockIcon.gameObject.activeSelf || card.cellCostLabel.text!="—")throw new InvalidDataException("locked");
                card.Render(null,null);if(card.cellCostBadge.activeSelf)throw new InvalidDataException("pool clear");
            } finally {PrefabUtility.UnloadPrefabContents(probe);}
            result.status="passed";
        }catch(Exception e){result.status="failed";result.error=e.ToString();}
        File.WriteAllText(Queue+"formation-component-binding-result.json",JsonUtility.ToJson(result,true));
    }
}






