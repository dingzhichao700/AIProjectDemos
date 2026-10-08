using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>验证常量配置是否存在、声明是否合法并能按声明类型解析。</summary>
/// <remarks>ID 2 是故意保留的非法整数样例，单独验证其解析失败。</remarks>
public static class TestCaseConfigs {

    /**配置读取测试要求存在的示例 ID。*/
    private static readonly int[] RequiredIds = {1,2,3,4,5,6,7,8};

    /**故意配置为非法内容、必须解析失败的测试样例 ID。*/
    private static readonly HashSet<int> ExpectedInvalidIds = new HashSet<int> {2};

    public static void Run(Action<string> report = null) {
        Action<string> log = report ?? (message => Debug.Log(message));
        var errors = new List<Exception>();
        ValidateRequiredIds(log, errors);
        ValidateAllConfigs(log, errors);
        if (errors.Count > 0) {
            throw new AggregateException("常量配置测试失败", errors);
        }
        log("PASS: 常量配置存在性与内容规范测试全部通过");
    }

    /**验证业务与测试依赖的配置项没有缺失。*/
    private static void ValidateRequiredIds(Action<string> log, List<Exception> errors) {
        foreach (int id in RequiredIds) {
            Check($"配置 ID {id} 存在", () => {
                if (CfgManager.tables.ConfigValueObj.GetOrDefault(id) == null) {
                    throw new InvalidOperationException($"缺少常量配置：{id}");
                }
            }, log, errors);
        }
    }

    /**遍历整张表，验证 ID 唯一且内容符合声明格式。*/
    private static void ValidateAllConfigs(Action<string> log, List<Exception> errors) {
        var ids = new HashSet<int>();
        foreach (ConfigValueResource config in CfgManager.tables.ConfigValueObj.DataList) {
            string checkName = ExpectedInvalidIds.Contains(config.Id) ? $"配置 ID {config.Id} 预期非法" : $"配置 ID {config.Id} 内容合规";
            Check(checkName, () => {
                if (!ids.Add(config.Id)) {
                    throw new InvalidOperationException($"常量配置 ID 重复：{config.Id}");
                }
                if (ExpectedInvalidIds.Contains(config.Id)) {
                    ValidateExpectedInvalid(config, log);
                    return;
                }
                string value = ReadAndFormat(config);
                log($"常量配置 {config.Id}：type={config.Type}，valueType={config.ValueType}，content={value}");
            }, log, errors);
        }
    }

    /**验证故意保留的非法样例确实无法按声明格式解析。*/
    private static void ValidateExpectedInvalid(ConfigValueResource config, Action<string> log) {
        try {
            ReadAndFormat(config);
        } catch (Exception error) {
            log($"常量配置 {config.Id}：预期非法，{error.Message}");
            return;
        }
        throw new InvalidOperationException($"常量配置 {config.Id} 应当是非法测试样例");
    }

    /**根据配置声明调用对应读取接口，并返回统一的日志文本。*/
    private static string ReadAndFormat(ConfigValueResource config) {
        switch (config.Type) {
            case ConfigValueType.NORMAL:
                return FormatNormal(config);
            case ConfigValueType.LIST:
                return FormatList(config);
            case ConfigValueType.MAP:
                return FormatMap(config);
            default:
                throw new InvalidOperationException($"常量 {config.Id} 使用了不支持的内容格式：{config.Type}");
        }
    }

    private static string FormatNormal(ConfigValueResource config) {
        switch (config.ValueType) {
            case ConfigValueFormat.STRING:
                return $"\"{ConfigValueHelper.GetString(config.Id)}\"";
            case ConfigValueFormat.INT:
                return ConfigValueHelper.GetInt(config.Id).ToString(CultureInfo.InvariantCulture);
            case ConfigValueFormat.FLOAT:
                return ConfigValueHelper.GetFloat(config.Id).ToString("R", CultureInfo.InvariantCulture);
            default:
                throw new InvalidOperationException($"常量 {config.Id} 使用了不支持的值格式：{config.ValueType}");
        }
    }

    private static string FormatList(ConfigValueResource config) {
        switch (config.ValueType) {
            case ConfigValueFormat.STRING:
                return $"[\"{string.Join("\", \"", ConfigValueHelper.GetList<string>(config.Id))}\"]";
            case ConfigValueFormat.INT:
                return $"[{string.Join(", ", ConfigValueHelper.GetList<int>(config.Id))}]";
            case ConfigValueFormat.FLOAT:
                return $"[{string.Join(", ", ConfigValueHelper.GetList<float>(config.Id).Select(value => value.ToString("R", CultureInfo.InvariantCulture)))}]";
            default:
                throw new InvalidOperationException($"常量 {config.Id} 使用了不支持的值格式：{config.ValueType}");
        }
    }

    private static string FormatMap(ConfigValueResource config) {
        switch (config.ValueType) {
            case ConfigValueFormat.STRING:
                return FormatPairs(ConfigValueHelper.GetMap<string>(config.Id), value => $"\"{value}\"");
            case ConfigValueFormat.INT:
                return FormatPairs(ConfigValueHelper.GetMap<int>(config.Id), value => value.ToString(CultureInfo.InvariantCulture));
            case ConfigValueFormat.FLOAT:
                return FormatPairs(ConfigValueHelper.GetMap<float>(config.Id), value => value.ToString("R", CultureInfo.InvariantCulture));
            default:
                throw new InvalidOperationException($"常量 {config.Id} 使用了不支持的值格式：{config.ValueType}");
        }
    }

    private static string FormatPairs<T>(IReadOnlyDictionary<string,T> values, Func<T,string> formatValue) {
        return "{" + string.Join(", ", values.Select(pair => $"\"{pair.Key}\": {formatValue(pair.Value)}")) + "}";
    }

    private static void Check(string name, Action test, Action<string> log, List<Exception> errors) {
        try {
            test();
            log("PASS: " + name);
        } catch (Exception error) {
            errors.Add(new Exception(name, error));
            log("FAIL: " + name + " — " + error.Message);
        }
    }
}
