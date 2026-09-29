using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 基础属性配置转换工具。
/// </summary>
public static class AttributeHelper {

    /**将配置属性转换为单位基础属性map*/
    public static IReadOnlyDictionary<AttributeType, int> CreateBaseAttributeMap(IReadOnlyList<AttributeValue> attributes, string ownerName) {
        var result = new Dictionary<AttributeType, int>();
        foreach (AttributeValue attribute in attributes) {
            int attributeId = (int)attribute.AttributeId;
            AttributeResource definition = CfgManager.tables.AttributeObj.GetOrDefault(attributeId);
            if (definition == null) {
                throw new InvalidOperationException($"{ownerName} 引用了不存在的属性 {attributeId}");
            }
            if (definition.Name != attribute.AttributeId) {
                throw new InvalidOperationException($"{ownerName} 的属性 {attributeId} 与属性定义不一致");
            }
            if (definition.ValueType != AttributeValueFormat.VALUE) {
                throw new InvalidOperationException($"{ownerName} 的基础属性 {attributeId} 必须使用绝对值");
            }
            if (result.ContainsKey(attribute.AttributeId)) {
                throw new InvalidOperationException($"{ownerName} 重复配置了属性 {attribute.AttributeId}");
            }
            result.Add(attribute.AttributeId, attribute.Value);
        }
        return new ReadOnlyDictionary<AttributeType, int>(result);
    }

    /**获取必需的基础属性*/
    public static int GetRequired(IReadOnlyDictionary<AttributeType, int> attributes, AttributeType type, string ownerName) {
        if (!attributes.TryGetValue(type, out int value)) {
            throw new InvalidOperationException($"{ownerName} 缺少属性 {type}");
        }
        return value;
    }

}
