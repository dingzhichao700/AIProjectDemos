using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using cfg;

/// <summary>按 ID 获取并缓存类型化常量。</summary>
/// <remarks>只解析成功的配置；集合不可修改，缓存由配置生命周期清理。</remarks>
public static class ConfigValueHelper {
    private static readonly Dictionary<int, object> cache = new Dictionary<int, object>();

    public static int GetInt(int id) => Read(id, ConfigValueType.NORMAL, Parse<int>);
    public static float GetFloat(int id) => Read(id, ConfigValueType.NORMAL, Parse<float>);
    public static string GetString(int id) => Read(id, ConfigValueType.NORMAL, Parse<string>);

    public static IReadOnlyList<T> GetList<T>(int id) {
        return Read<IReadOnlyList<T>>(id, ConfigValueType.LIST, content => {
            var result = new List<T>();
            if (content.Length > 0) {
                foreach (string token in Split(content, ',')) {
                    result.Add(Parse<T>(Unescape(token)));
                }
            }
            return result.AsReadOnly();
        }, typeof(T));
    }

    public static IReadOnlyDictionary<string, T> GetMap<T>(int id) {
        return Read<IReadOnlyDictionary<string, T>>(id, ConfigValueType.MAP, content => {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            if (content.Length > 0) {
                foreach (string entry in Split(content, ',')) {
                    List<string> pair = Split(entry, ':');
                    if (pair.Count != 2) {
                        throw new FormatException("Map 条目必须包含一个未转义冒号");
                    }
                    string key = Unescape(pair[0]);
                    if (key.Length == 0 || result.ContainsKey(key)) {
                        throw new FormatException($"Map 键为空或重复：'{key}'");
                    }
                    result.Add(key, Parse<T>(Unescape(pair[1])));
                }
            }
            return new ReadOnlyDictionary<string, T>(result);
        }, typeof(T));
    }

    internal static void ClearCache() {
        cache.Clear();
    }

    private static T Read<T>(int id, ConfigValueType type, Func<string, T> parse, Type valueType = null) {
        Type expected = valueType ?? typeof(T);
        ConfigValueFormat format;
        if (expected == typeof(int)) {
            format = ConfigValueFormat.INT;
        } else if (expected == typeof(float)) {
            format = ConfigValueFormat.FLOAT;
        } else if (expected == typeof(string)) {
            format = ConfigValueFormat.STRING;
        } else {
            throw new NotSupportedException($"常量 {id} 不支持值类型 {expected.Name}");
        }
        var config = CfgManager.tables.ConfigValueObj.GetOrDefault(id);
        if (config == null) {
            throw new KeyNotFoundException($"常量 {id} 不存在，预期 {type}/{format}");
        }
        if (config.Type != type || config.ValueType != format) {
            throw new InvalidOperationException($"常量 {id} 类型不匹配：预期 {type}/{format}，实际 {config.Type}/{config.ValueType}");
        }
        if (cache.TryGetValue(id, out object cached)) {
            return (T)cached;
        }
        T result;
        try {
            result = parse(config.Content);
        } catch (OverflowException error) {
            throw new OverflowException($"常量 {id} 数值越界，预期 {type}/{format}：{error.Message}", error);
        } catch (FormatException error) {
            throw new FormatException($"常量 {id} 格式错误，预期 {type}/{format}：{error.Message}", error);
        }
        cache.Add(id, result);
        return result;
    }

    private static T Parse<T>(string text) {
        if (typeof(T) == typeof(string)) {
            return (T)(object)text;
        }
        if (typeof(T) == typeof(int)) {
            return (T)(object)int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        float value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (float.IsNaN(value) || float.IsInfinity(value)) {
            throw new OverflowException("浮点数必须为有限值");
        }
        return (T)(object)value;
    }

    // 拆分时保留转义，避免 Map 的第二轮拆分误认已转义的冒号。
    private static List<string> Split(string text, char separator) {
        var result = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++) {
            if (text[i] == '\\') {
                if (++i >= text.Length || (text[i] != '\\' && text[i] != ',' && text[i] != ':')) {
                    throw new FormatException("无效转义或末尾孤立反斜杠");
                }
            } else if (text[i] == separator) {
                result.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        result.Add(text.Substring(start));
        return result;
    }

    private static string Unescape(string text) {
        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++) {
            if (text[i] == '\\') {
                i++;
            }
            result.Append(text[i]);
        }
        return result.ToString();
    }
}
