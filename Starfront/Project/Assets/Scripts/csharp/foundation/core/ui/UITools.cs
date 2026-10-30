using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITools {

    /// <summary>
    /// 设置图片资源
    /// </summary>
    /// <param name="imgObj">图片组件</param>
    /// <param name="path">图片路径</param>
    public static void SetImage(Image imgObj, string path, bool syncSpritePivot = false,
        bool useNativeSize = false) {
        SetImage(imgObj, ResourcePresentation.Policy.ResolveImage(path), syncSpritePivot, useNativeSize);
    }

    public static void SetImage(Image imgObj, ImageResource resource, bool syncSpritePivot = false, bool useNativeSize = false)
    {
        LoadImage(resource, () => {
            if (imgObj == null) {
                return;
            }
            Sprite sprite = GetSprite(resource);
            if (sprite == null) {
                return;
            }
            imgObj.sprite = sprite;
            if (syncSpritePivot) {
                RectTransform rectTransform = imgObj.rectTransform;
                Vector2 spritePivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                rectTransform.pivot = spritePivot;
            }
            if (useNativeSize) {
                imgObj.SetNativeSize();
            }
        });
    }

    public static void LoadImage(string path, Action action)
    {
        LoadImage(ResourcePresentation.Policy.ResolveImage(path), action);
    }

    public static async void LoadImage(ImageResource resource, Action action)
    {
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(resource.Address, resource.Type) });
        action?.Invoke();
    }

    public static Sprite GetSprite(string path)
    {
        return GetSprite(ResourcePresentation.Policy.ResolveImage(path));
    }

    public static Sprite GetSprite(ImageResource resource)
    {
        if (resource.Type == ResType.Atlas)
        {
            return ResourceManager.GetAtlasSprite(resource.Address, resource.SpriteName);
        }
        return ResourceManager.GetUnpackImage(resource.Address);
    }

    /// <summary>
    /// 设置对象透明度
    /// </summary>
    /// <param name="target"></param>
    /// <param name="alpha"></param>
    public static void SetAlpha(GameObject target, float alpha) {
        if (target == null)
            return;

        // CanvasGroup 优先
        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup != null) {
            canvasGroup.alpha = alpha;
            return;
        }

        // Image
        var image = target.GetComponent<Image>();
        if (image != null) {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        // TextMeshProUGUI
        var tmp = target.GetComponent<TextMeshProUGUI>();
        if (tmp != null) {
            Color c = tmp.color;
            c.a = alpha;
            tmp.color = c;
        }
    }

}
