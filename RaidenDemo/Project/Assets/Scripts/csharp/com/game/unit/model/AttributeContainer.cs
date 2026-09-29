using cfg;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 单位属性容器。
/// </summary>
/// <remarks>叶子容器保存属性，父容器汇总子容器；两种职责不能混用。</remarks>
public sealed class AttributeContainer : EventDispatcher {

    private readonly Dictionary<AttributeType, int> _summaryMap = new Dictionary<AttributeType, int>();
    private readonly List<AttributeContainer> _childContainerList = new List<AttributeContainer>();
    private readonly ReadOnlyDictionary<AttributeType, int> readonlySummaryMap;
    private readonly ReadOnlyCollection<AttributeContainer> readonlyChildContainerList;
    private AttributeContainer parent;
    private bool storesOwnAttributes;
    private bool aggregatesChildren;

    /**是否为单位最终属性容器*/
    public bool isFinal { get; }

    /**子属性容器列表*/
    public IReadOnlyList<AttributeContainer> childContainerList => readonlyChildContainerList;

    /**当前容器的属性汇总*/
    public IReadOnlyDictionary<AttributeType, int> summaryMap => readonlySummaryMap;

    public AttributeContainer(bool isFinal = false) {
        this.isFinal = isFinal;
        readonlySummaryMap = new ReadOnlyDictionary<AttributeType, int>(_summaryMap);
        readonlyChildContainerList = _childContainerList.AsReadOnly();
    }

    public AttributeContainer(IReadOnlyDictionary<AttributeType, int> attributeMap, bool isFinal = false) : this(isFinal) {
        InitByCfg(attributeMap);
    }

    /**使用配置属性初始化叶子容器*/
    public void InitByCfg(IReadOnlyDictionary<AttributeType, int> attributeMap) {
        if (attributeMap == null) throw new ArgumentNullException(nameof(attributeMap));
        EnsureLeaf();
        storesOwnAttributes = true;
        var changedTypes = new HashSet<AttributeType>(_summaryMap.Keys);
        _summaryMap.Clear();
        foreach (KeyValuePair<AttributeType, int> attribute in attributeMap) {
            _summaryMap.Add(attribute.Key, attribute.Value);
            changedTypes.Add(attribute.Key);
        }
        Dispatch(AttributeContainerEvent.RECALCULATED);
        foreach (AttributeType type in changedTypes) Dispatch(AttributeContainerEvent.ATTR_CHANGED, type);
    }

    /**累加一项叶子属性*/
    public void AddAttr(AttributeType type, int value) {
        EnsureLeaf();
        storesOwnAttributes = true;
        _summaryMap[type] = checked(GetAttr(type) + value);
        Dispatch(AttributeContainerEvent.ATTR_CHANGED, type);
    }

    /**设置一项叶子属性*/
    public void SetAttr(AttributeType type, int value) {
        EnsureLeaf();
        storesOwnAttributes = true;
        if (_summaryMap.TryGetValue(type, out int current) && current == value) return;
        _summaryMap[type] = value;
        Dispatch(AttributeContainerEvent.ATTR_CHANGED, type);
    }

    /**移除一项叶子属性*/
    public void RemoveAttr(AttributeType type) {
        EnsureLeaf();
        if (_summaryMap.Remove(type)) Dispatch(AttributeContainerEvent.ATTR_CHANGED, type);
    }

    /**获取一项当前汇总属性；未配置时返回零*/
    public int GetAttr(AttributeType type) => _summaryMap.TryGetValue(type, out int value) ? value : 0;

    /**挂载一个子属性容器*/
    public void AddChild(AttributeContainer child) {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (storesOwnAttributes) throw new InvalidOperationException("保存自身属性的叶子容器不能添加子容器");
        if (ReferenceEquals(child, this) || child.Contains(this)) throw new InvalidOperationException("属性容器不能形成循环引用");
        if (child.parent != null) throw new InvalidOperationException("属性容器已经挂载到其他父容器");
        if (_childContainerList.Contains(child)) throw new InvalidOperationException("不能重复添加子属性容器");
        child.parent = this;
        child.On<AttributeType>(AttributeContainerEvent.ATTR_CHANGED, OnChildAttrChanged);
        child.On(AttributeContainerEvent.CHILD_CHANGED, OnChildChildChanged);
        aggregatesChildren = true;
        _childContainerList.Add(child);
        CalculateSummary();
        DispatchStructureChanged();
    }

    /**移除一个子属性容器*/
    public void RemoveChild(AttributeContainer child) {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (!_childContainerList.Remove(child)) throw new InvalidOperationException("找不到需要移除的子属性容器");
        child.Off<AttributeType>(AttributeContainerEvent.ATTR_CHANGED, OnChildAttrChanged);
        child.Off(AttributeContainerEvent.CHILD_CHANGED, OnChildChildChanged);
        child.parent = null;
        CalculateSummary();
        DispatchStructureChanged();
    }

    /**移除全部子属性容器*/
    public void RemoveAllChildren() {
        for (int index = _childContainerList.Count - 1; index >= 0; index--) {
            RemoveChild(_childContainerList[index]);
        }
    }

    public override void Clear() {
        if (parent != null) parent.RemoveChild(this);
        RemoveAllChildren();
        _summaryMap.Clear();
        parent = null;
        storesOwnAttributes = false;
        aggregatesChildren = false;
        base.Clear();
    }

    private void EnsureLeaf() {
        if (aggregatesChildren) throw new InvalidOperationException("汇总子容器的父容器不能直接设置属性");
    }

    private bool Contains(AttributeContainer target) {
        if (ReferenceEquals(this, target)) return true;
        foreach (AttributeContainer child in _childContainerList) {
            if (child.Contains(target)) return true;
        }
        return false;
    }

    private void OnChildAttrChanged(AttributeType type) {
        CalculateSummary(type);
        Dispatch(AttributeContainerEvent.ATTR_CHANGED, type);
    }

    private void OnChildChildChanged() {
        CalculateSummary();
        DispatchStructureChanged();
    }

    private void CalculateSummary() {
        _summaryMap.Clear();
        foreach (AttributeContainer child in _childContainerList) {
            foreach (KeyValuePair<AttributeType, int> attribute in child.summaryMap) {
                _summaryMap[attribute.Key] = checked(GetAttr(attribute.Key) + attribute.Value);
            }
        }
        Dispatch(AttributeContainerEvent.RECALCULATED);
    }

    private void CalculateSummary(AttributeType type) {
        int value = 0;
        bool found = false;
        foreach (AttributeContainer child in _childContainerList) {
            if (child.summaryMap.TryGetValue(type, out int childValue)) {
                value = checked(value + childValue);
                found = true;
            }
        }
        if (found) _summaryMap[type] = value;
        else _summaryMap.Remove(type);
    }

    private void DispatchStructureChanged() {
        if (!isFinal) Dispatch(AttributeContainerEvent.CHILD_CHANGED);
    }
}
