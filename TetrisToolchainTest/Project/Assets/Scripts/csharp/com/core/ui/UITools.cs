using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

class UITools {

    /// <summary>
    /// 设置图片资源
    /// </summary>
    /// <param name="imgObj">图片组件</param>
    /// <param name="path">图片路径</param>
    public static void SetImage(Image imgObj, string path) {
        /*if (path.Contains(ResourceConst.PATH_ATLAS)) {
            //图集
            string nameWithoutRoot = path.Replace(ResourceConst.PATH_ATLAS, "");
            //图集名
            string atlasName = nameWithoutRoot.Substring(0, nameWithoutRoot.IndexOf("/"));
            //精灵图名
            string spriteName = nameWithoutRoot.Substring(nameWithoutRoot.IndexOf("/") + 1);
            if (ResourceManager.HasLoadedAtlas(atlasName)) {
                Sprite sp = ResourceManager.GetAtlasSprite(atlasName, spriteName);
                if (sp != null) {
                    imgObj.sprite = sp;
                } else {
                    SetImage(imgObj, ResourceConst.PATH_ATLAS_COMMON + "default");
                }
            } else {
                ResourceManager.LoadAtlas(atlasName, () => {
                    Sprite sp = ResourceManager.GetAtlasSprite(atlasName, spriteName);
                    if (sp != null) {
                        imgObj.sprite = sp;
                    } else { 
                        SetImage(imgObj, ResourceConst.PATH_ATLAS_COMMON + "default");
                    }
                });
            }
        } else if (path.Contains(ResourceConst.PATH_UNPACK_IMAGE)) {
            //散图
            if (ResourceManager.HasLoadedUnpackImage(path)) {
                imgObj.sprite = ResourceManager.GetUnpackImage(path);
                //imgObj.GetComponent<RectTransform>().sizeDelta = new Vector2(tex2D.width, tex2D.height);
            } else {
                ResourceManager.LoadUnpackImage(path, (string imagePath, Sprite sp) => {
                    imgObj.sprite = ResourceManager.GetUnpackImage(imagePath);
                });
            }
        } else {
            Debug.LogError("图片路径异常，请检查：" + path);
        }*/
        LoadImage(path, () => {
            imgObj.sprite = GetSprite(path);
        });
    }

    /**加载图片*/
    public async static void LoadImage(string path, Action action) {
        if (path.Contains(ResourceConst.PATH_ATLAS)) {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path.Substring(0, path.LastIndexOf("/")), ResType.Atlas) });
            action.Invoke();
        } else if (path.Contains(ResourceConst.PATH_UNPACK_IMAGE)) {
            //散图
            if (ResourceManager.HasLoadedUnpackImage(path)) {
                action.Invoke();
            } else {
                await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path, ResType.UnpackImage) });
                action.Invoke();
            }
        } else {
            Debug.LogError("图片路径异常，请检查：" + path);
        }
    }

    /**获取精灵对象*/
    public static Sprite GetSprite(string path) {
        if (path.Contains(ResourceConst.PATH_ATLAS)) {
            //图集路径
            string atlasPath = path.Substring(0, path.LastIndexOf("/"));
            //精灵图名
            string spriteName = path.Replace(atlasPath + "/", "");
            if (ResourceManager.HasLoadedAtlas(atlasPath)) {
                return ResourceManager.GetAtlasSprite(atlasPath, spriteName);
            }
        } else if (path.Contains(ResourceConst.PATH_UNPACK_IMAGE)) {
            //散图
            if (ResourceManager.HasLoadedUnpackImage(path)) {
                return ResourceManager.GetUnpackImage(path);
            }
        }
        Debug.LogError("图片路径异常，或图集尚未加载完成，请检查：" + path);
        return null;
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
