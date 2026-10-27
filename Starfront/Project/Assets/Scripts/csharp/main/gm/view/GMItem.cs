using System;
using System.Collections.Generic;
using cfg.resource;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 显示单条 GM 指令并收集参数。
/// </summary>
/// <remarks>参数独立保存，避免滚动回收输入框后丢失用户输入。</remarks>
public class GMItem : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtTitle;
    public GameButton btnExecute;
    public ScrollList listParam;
    /******************* UIComponent Define finish ************************/

    /**当前指令配置*/
    private GMResource cfg;

    /**当前输入参数*/
    private readonly List<string> parameters = new List<string>();

    /**本条指令注册的输入回调*/
    private readonly Dictionary<TMP_InputField, UnityAction<string>> inputListeners = new Dictionary<TMP_InputField, UnityAction<string>>();

    /**注册按钮与参数项渲染回调*/
    protected override void OnInitListItem() {
        OnClick(btnExecute.gameObject, OnExecute);
        listParam.rendererHandler = OnParamItem;
    }

    /// <summary>
    /// 换绑指令时重建默认参数；重复刷新同一指令保留输入。
    /// </summary>
    protected override void OnSetData(int index, object value) {
        GMResource next = value as GMResource;
        if (next == null || next.ParamFormat.Count != next.ParamIntro.Count) {
            throw new InvalidOperationException("GM 配置无效或参数与说明数量不一致。");
        }
        if (cfg != next) {
            RemoveInputListeners();
            cfg = next;
            parameters.Clear();
            parameters.AddRange(cfg.ParamFormat);
        }
        txtTitle.text = cfg.Desc;
        listParam.gameObject.SetActive(parameters.Count > 0);
        listParam.array = parameters;
    }

    /**执行按钮使用当前参数快照*/
    private void OnExecute() {
        if (cfg != null) {
            GMControl.ins.ExecuteGM(cfg.Content, new List<string>(parameters));
        }
    }

    /**列表复用时替换本模块监听，不清空其他组件的监听*/
    private void OnParamItem(int index, object value) {
        if (cfg == null || index >= parameters.Count) {
            return;
        }
        TMP_InputField input = listParam.GetCell(index).GetComponent<TMP_InputField>();
        if (inputListeners.TryGetValue(input, out var previous)) {
            input.onValueChanged.RemoveListener(previous);
        }
        if (input.placeholder is TMP_Text placeholder) {
            placeholder.text = cfg.ParamIntro[index];
        }
        input.SetTextWithoutNotify(parameters[index]);
        UnityAction<string> listener = text => parameters[index] = text;
        inputListeners[input] = listener;
        input.onValueChanged.AddListener(listener);
        input.onSubmit.RemoveListener(OnSubmit);
        input.onSubmit.AddListener(OnSubmit);
    }

    /**键盘和移动端提交共用执行入口*/
    private void OnSubmit(string text) {
        OnExecute();
    }

    /**注销当前输入框回调*/
    private void RemoveInputListeners() {
        foreach (var entry in inputListeners) {
            if (entry.Key != null) {
                entry.Key.onValueChanged.RemoveListener(entry.Value);
                entry.Key.onSubmit.RemoveListener(OnSubmit);
            }
        }
        inputListeners.Clear();
    }

    /**回收时释放旧配置与输入，避免池化对象继续修改旧数据*/
    public override void Clear() {
        RemoveInputListeners();
        cfg = null;
        parameters.Clear();
        listParam.array = null;
        base.Clear();
    }

    /**销毁时释放输入和按钮监听*/
    public override void Destroy() {
        OffClick(btnExecute.gameObject, OnExecute);
        base.Destroy();
    }

    /**Unity 直接销毁对象时也清理输入回调*/
    private void OnDestroy() {
        RemoveInputListeners();
    }

}
