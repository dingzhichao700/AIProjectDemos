using System;
using System.Collections.Generic;
using System.IO;
using cfg;
using cfg.resource;

/// <summary>
/// 功能配置、条件查询及永久解锁记录。
/// </summary>
/// <remarks>进度通过业务查询委托读取，缺少来源时不会推断玩家等级或通关状态。</remarks>
public sealed class FunctionModel {

    private readonly Dictionary<int, FunctionResource> configs = new Dictionary<int, FunctionResource>();

    private readonly HashSet<int> unlockedIds;

    private readonly string savePath;

    private Func<int?> getPlayerLevel;

    private Func<int, bool> isMissionCompleted;

    public FunctionModel(IEnumerable<FunctionResource> resources, string path) {
        savePath = path;
        foreach (FunctionResource resource in resources) {
            configs.Add(resource.Id, resource);
        }
        FunctionSaveVO saved = JsonFileUtil.Load<FunctionSaveVO>(path);
        unlockedIds = new HashSet<int>(saved?.unlockedIds ?? new List<int>());
    }

    /**只提供配置查询，不把可修改集合暴露给 View。*/
    public FunctionResource GetConfig(int id) {
        if (!configs.TryGetValue(id, out FunctionResource resource)) {
            throw new InvalidOperationException("FunctionResource 缺少功能 ID：" + id);
        }
        return resource;
    }

    public bool IsUnlocked(int id) {
        GetConfig(id);
        return unlockedIds.Contains(id);
    }

    /**仅由 Control 绑定权威进度来源；通关查询必须区分已解锁与已完成。*/
    internal void SetProgressSource(Func<int?> levelReader, Func<int, bool> completionReader) {
        getPlayerLevel = levelReader;
        isMissionCompleted = completionReader;
    }

    /**所有条件都满足才能开启；未知条件失败关闭，不能因新 Bean 被忽略而误解锁。*/
    private bool MeetsConditions(FunctionResource resource) {
        foreach (var condition in resource.OpenCondition) {
            if (condition is PlayerLevelCondition level) {
                int? current = getPlayerLevel?.Invoke();
                if (!current.HasValue || current.Value < level.Level) {
                    return false;
                }
            } else if (condition is MissionProgressCondition mission) {
                if (isMissionCompleted == null || !isMissionCompleted(mission.MissionIndex)) {
                    return false;
                }
            } else if (condition is DisabledCondition) {
                return false;
            } else {
                throw new InvalidOperationException("不支持的功能开启条件：" + condition?.GetType().Name);
            }
        }
        return true;
    }

    /**先落盘并回读确认，再发布新状态；已保存的解锁不受后续配置收紧影响。*/
    internal bool RefreshUnlocks() {
        List<int> next = new List<int>(unlockedIds);
        foreach (FunctionResource resource in configs.Values) {
            if (!unlockedIds.Contains(resource.Id) && MeetsConditions(resource)) {
                next.Add(resource.Id);
            }
        }
        if (next.Count == unlockedIds.Count) {
            return false;
        }
        next.Sort();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath)));
        string pendingPath = savePath + ".pending";
        JsonFileUtil.Save(pendingPath, new FunctionSaveVO { unlockedIds = next });
        FunctionSaveVO written = JsonFileUtil.Load<FunctionSaveVO>(pendingPath);
        if (written?.unlockedIds == null || !new HashSet<int>(written.unlockedIds).SetEquals(next)) {
            throw new IOException("功能解锁记录保存校验失败：" + pendingPath);
        }
        if (File.Exists(savePath)) {
            File.Copy(pendingPath, savePath, true);
            File.Delete(pendingPath);
        } else {
            File.Move(pendingPath, savePath);
        }
        unlockedIds.UnionWith(next);
        return true;
    }

    /**展示完整 AND 条件，供各入口使用统一说明。*/
    public string GetLockedHint(int id) {
        FunctionResource resource = GetConfig(id);
        List<string> descriptions = new List<string>();
        foreach (var condition in resource.OpenCondition) {
            if (condition is PlayerLevelCondition level) {
                descriptions.Add("玩家等级达到 " + level.Level + " 级");
            } else if (condition is MissionProgressCondition mission) {
                descriptions.Add("通关主线第 " + mission.MissionIndex + " 关");
            } else if (condition is DisabledCondition) {
                descriptions.Add("暂未开放");
            } else {
                descriptions.Add("开启条件暂不支持");
            }
        }
        return resource.Name + "：\n" + string.Join("\n", descriptions);
    }

}
