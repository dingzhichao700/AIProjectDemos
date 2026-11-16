using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI对象工厂类
/// </summary>
/// <remarks>
/// 负责：
/// 1.通过ResourceManager接口，对UI类资源对象进行管理（发起UI类预制的加载请求，响应加载完成）；
/// 2.管理UI对象池；
/// </remarks>
public class UIFactory {

    /**UI预制对象池<UI枚举名，实例列表>*/
    private static Dictionary<string, List<BaseView>> prefabPool = new Dictionary<string, List<BaseView>>();

    /**单一加载资源*/
    public static async void LoadUIPrefab(string uiEnum, Action<string> action, Action<Exception> onFailed = null) {
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo>() { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(uiEnum), ResType.Prefab) });
        } catch (Exception exception) {
            if (onFailed != null) {
                onFailed(exception);
            } else {
                Debug.LogException(exception);
            }
            return;
        }
        action.Invoke(uiEnum);
    }

    /**取出池实例，同时撤销唯一池登记。*/
    public static BasePanel TakePooledPanel(string uiEnum) {
        if (prefabPool.TryGetValue(uiEnum, out List<BaseView> list) && list.Count > 0) {
            BasePanel panel = list[0] as BasePanel;
            list.RemoveAt(0);
            return panel;
        }
        return null;
    }

    /**获取UI对象实例（这个接口是同步的，必须提前加载完成才能调用）*/
    public static BaseView GetUIInstance(string uiEnum) {
        GameObject prefabGo = ResourceManager.GetLoadedPrefab(ResourcePresentation.Policy.ResolveUI(uiEnum));
        if (prefabGo == null) { //拿不到就说明资源管理那边没加载这种预制
            Debug.LogError("获取UI预制失败，该预制还未加载完成：" + uiEnum);
            return null;
        }
        BaseView baseView = null;
        if (prefabPool.ContainsKey(uiEnum)) {
            //先尝试从池中取
            List<BaseView> compList = prefabPool[uiEnum];
            if (compList.Count > 0) {
                baseView = compList[0];
                compList.RemoveAt(0);
            }
        }
        if (baseView == null) {
            //取不到的话，再从已加载的预制再实例化一个
            GameObject go = GameObject.Instantiate(prefabGo);
            baseView = go.GetComponent<BaseView>();
        }
        return baseView;
    }

    /**回收UI实例到池*/
    public static void Recover(string uiEnum, BaseView comp) {
        prefabPool.TryGetValue(uiEnum, out List<BaseView> componentList);
        if (componentList == null) {
            componentList = new List<BaseView>();
            prefabPool.Add(uiEnum, componentList);
        }
        if (!componentList.Contains(comp)) {
            componentList.Add(comp);
        }

        // The completed opening must not retain business objects while its panel is pooled.
        // OnClose can synchronously reopen a panel; preserve that new opening's parameters.
        if (comp is BasePanel panel && panel.isClosing) {
            panel.openParams = null;
        }

        comp.transform.SetParent(PanelMgr.ins.uiPool);
        comp.transform.localScale = Vector3.one;
        if (componentList.Count > 150) {
            Debug.LogWarning("对象池实例数超过警戒数量：" + uiEnum + "，当前数量：" + componentList.Count);
        }
    }

}
