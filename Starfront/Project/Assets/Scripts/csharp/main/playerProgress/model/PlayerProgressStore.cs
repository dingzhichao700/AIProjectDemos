using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// 局外共享进度的单文件原子存储及旧卡牌快照读取。
/// </summary>
/// <remarks>仅新路径可写。旧文件保持原样；新文件损坏时禁止回退旧余额。</remarks>
public sealed class PlayerProgressStore {

    private readonly string path;

    private readonly string legacyPath;

    public PlayerProgressStore(string savePath, string oldPath = null) {
        path = savePath;
        legacyPath = oldPath;
        if (oldPath != null && string.Equals(Path.GetFullPath(savePath), Path.GetFullPath(oldPath), StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("迁移目标必须与旧存档分开");
        }
    }

    public PlayerProgressSaveVO Load() {
        if (File.Exists(path)) {
            return Read(path, 2);
        }
        if (File.Exists(path + ".bak")) {
            throw new InvalidDataException("共享存档缺失但备份存在，禁止重建或重复迁移：" + path);
        }
        if (legacyPath != null && File.Exists(legacyPath)) {
            return Read(legacyPath, 1);
        }
        if (legacyPath != null && File.Exists(legacyPath + ".bak")) {
            throw new InvalidDataException("旧存档缺失但备份存在，禁止重发初始奖励：" + legacyPath);
        }
        return null;
    }

    private static PlayerProgressSaveVO Read(string source, int schema) {
        var saved = JsonConvert.DeserializeObject<PlayerProgressSaveVO>(File.ReadAllText(source));
        if (saved == null || !saved.initialized || saved.schemaVersion != schema) {
            throw new InvalidDataException("存档格式无效，保留原文件：" + source);
        }
        return saved;
    }

    public void Save(PlayerProgressSaveVO data) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string pending = path + ".pending";
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(pending, json, new UTF8Encoding(false));
        if (File.ReadAllText(pending) != json || JsonConvert.DeserializeObject<PlayerProgressSaveVO>(json) == null) {
            throw new IOException("共享存档回读校验失败");
        }
        if (File.Exists(path)) {
            File.Replace(pending, path, path + ".bak");
        } else {
            File.Move(pending, path);
        }
    }

}
