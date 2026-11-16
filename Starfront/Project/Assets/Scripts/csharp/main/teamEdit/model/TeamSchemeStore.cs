using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// 方案的独立原子存储。
/// </summary>
/// <remarks>不修改成长与背包文件；损坏文件保留，不静默重建。</remarks>
public sealed class TeamSchemeStore {

    private readonly string path;

    public TeamSchemeStore(string savePath) {
        path = Path.GetFullPath(savePath);
    }

    public TeamSchemeSaveVO Load() {
        if (!File.Exists(path)) {
            if (File.Exists(path + ".bak")) {
                throw new InvalidDataException("队伍存档缺失但存在备份，请恢复存档");
            }
            return null;
        }
        var value = JsonConvert.DeserializeObject<TeamSchemeSaveVO>(File.ReadAllText(path));
        TeamSchemeModel.ValidateShape(value);
        // 兼容旧带间隙快照，仅归一化读取副本，显式保存前不回写原文件。
        value.schemes = value.schemes.Select(TeamSchemeModel.CompactSlots).ToArray();
        return value;
    }

    public void Save(TeamSchemeSaveVO value) {
        TeamSchemeModel.ValidateShape(value);
        value = new TeamSchemeSaveVO { schemaVersion = value.schemaVersion, revision = value.revision, selected = value.selected, schemes = value.schemes.Select(TeamSchemeModel.CompactSlots).ToArray() };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string pending = path + ".pending";
        string json = JsonConvert.SerializeObject(value, Formatting.Indented);
        File.WriteAllText(pending, json, new UTF8Encoding(false));
        string readback = File.ReadAllText(pending);
        TeamSchemeModel.ValidateShape(JsonConvert.DeserializeObject<TeamSchemeSaveVO>(readback));
        if (readback != json) {
            throw new IOException("队伍存档回读失败");
        }
        if (File.Exists(path)) {
            File.Replace(pending, path, path + ".bak");
        } else {
            File.Move(pending, path);
        }
    }

}
