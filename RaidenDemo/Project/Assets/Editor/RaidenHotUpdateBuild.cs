#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 准备热更新构建输入并发布与底座匹配的远程资源。
/// </summary>
public static class RaidenHotUpdateBuild {

    public const string BytesRoot = "Assets/HotUpdateBytes";
    public const string ServerDirectory = "E:/testcdn/raiden";
    public const string BootstrapUrl = "https://dingzhichao700.github.io/testcdn/raiden/bootstrap.json";
    private const string StatePath = "Library/AIUI/hotupdate-build-state.json";

    /// <summary>
    /// 安装项目内运行时并配置热更新程序集。
    /// </summary>
    public static void Prepare() {
        var settings = HybridCLRSettings.Instance;
        settings.enable = true;
        settings.useGlobalIl2cpp = false;
        settings.hotUpdateAssemblyDefinitions = new[] {
            AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>("Assets/Scripts/csharp/main/Main.asmdef"),
            AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>("Assets/Scripts/csharp/login/Login.asmdef")
        };
        settings.hotUpdateAssemblies = Array.Empty<string>();
        settings.outputAOTGenericReferenceFile = "Editor/HybridCLRGenerate/AOTGenericReferences.cs";
        settings.outputLinkFile = "HybridCLRGenerate/link.xml";
        HybridCLRSettings.Save();
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WebGL, ApiCompatibilityLevel.NET_Unity_4_8);
        var installer = new InstallerController();
        if (!installer.HasInstalledHybridCLR()) {
            installer.InstallDefaultHybridCLR();
        }
        if (!installer.HasInstalledHybridCLR()) {
            throw new InvalidOperationException("HybridCLR 项目内运行时安装失败。");
        }
    }

    /// <summary>
    /// 生成底座配套代码及程序集资源。
    /// </summary>
    public static void Generate() {
        var wx = WeChatWASM.WXConvertCore.config;
        wx.CompileOptions.Il2CppOptimizeSize = false;
        EditorUtility.SetDirty(wx);
        AssetDatabase.SaveAssets();
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSpeed);
        PrebuildCommand.GenerateAll();
        string baseVersion = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        Directory.CreateDirectory("Assets/Resources");
        File.WriteAllText("Assets/Resources/RaidenHotUpdateBase.txt", JsonUtility.ToJson(new HotUpdateBootConfig { baseVersion = baseVersion, bootstrapUrl = BootstrapUrl }, true));
        string generated = File.ReadAllText("Assets/" + HybridCLRSettings.Instance.outputAOTGenericReferenceFile);
        string aotSection = generated.Split(new[] { "// }}" }, StringSplitOptions.None)[0];
        string[] aot = Regex.Matches(aotSection, "\"([^\"]+\\.dll)\"").Cast<Match>().Select(match => match.Groups[1].Value).Distinct().ToArray();
        var manifest = new HotUpdateManifest { baseVersion = baseVersion, release = baseVersion, resourceRoot = baseVersion + "/", aot = aot, assemblies = new[] { "Login.dll", "Main.dll" } };
        File.WriteAllText(StatePath, JsonUtility.ToJson(manifest, true));
        Stage(manifest, true);
    }

    /// <summary>
    /// 在底座不变时重新编译业务 DLL。
    /// </summary>
    public static void GenerateContent() {
        HotUpdateManifest manifest = ReadState();
        manifest.assemblies = new[] { "Login.dll", "Main.dll" };
        CompileDllCommand.CompileDll(BuildTarget.WebGL, EditorUserBuildSettings.development);
        manifest.release = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        manifest.resourceRoot = manifest.release + "/";
        File.WriteAllText(StatePath, JsonUtility.ToJson(manifest, true));
        Stage(manifest, false);
    }

    /// <summary>
    /// 将 DLL 作为 TextAsset 注册到对应远程 Group。
    /// </summary>
    private static void Stage(HotUpdateManifest manifest, bool copyAot) {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.AddLabel("main-content");
        var contentGroup = settings.FindGroup("Default Local Group");
        if (contentGroup == null) {
            throw new InvalidOperationException("缺少正式内容资源组 Default Local Group。");
        }
        foreach (var group in settings.groups) {
            if (group == null) {
                continue;
            }
            foreach (var entry in group.entries) {
                entry.SetLabel("main-content", group == contentGroup);
            }
        }
        string profile = settings.activeProfileId;
        settings.profileSettings.SetValue(profile, "Remote.BuildPath", "ServerData/[BuildTarget]");
        settings.profileSettings.SetValue(profile, "Remote.LoadPath", HotUpdateBootstrap.BundleRoot.TrimEnd('/'));
        settings.BuildRemoteCatalog = true;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, "Remote.BuildPath");
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, "Remote.LoadPath");
        foreach (var group in settings.groups) {
            if (group == null) {
                continue;
            }
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null) {
                schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
                schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
                EditorUtility.SetDirty(schema);
            }
        }
        foreach (string name in manifest.assemblies) {
            CopyAssembly(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(BuildTarget.WebGL), name, "code");
        }
        foreach (string name in manifest.aot) {
            if (copyAot) {
                CopyAssembly(SettingsUtil.GetAssembliesPostIl2CppStripDir(BuildTarget.WebGL), name, "aot");
            }
            if (!File.Exists(BytesRoot + "/aot/" + name + ".bytes")) {
                throw new FileNotFoundException("底座配套 AOT 元数据缺失", name);
            }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in manifest.assemblies) {
            Register(settings, name, "code", Path.GetFileNameWithoutExtension(name));
        }
        foreach (string name in manifest.aot) {
            Register(settings, name, "aot", "Foundation");
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    /**复制指定目标平台的程序集*/
    private static void CopyAssembly(string source, string name, string kind) {
        string directory = BytesRoot + "/" + kind;
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(source, name), directory + "/" + name + ".bytes", true);
    }

    /**注册程序集资源，独立打包以便先于界面资源加载*/
    private static void Register(AddressableAssetSettings settings, string name, string kind, string groupName) {
        var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
        schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(BytesRoot + "/" + kind + "/" + name + ".bytes"), group);
        entry.address = "hotupdate/" + kind + "/" + name;
        EditorUtility.SetDirty(schema);
    }

    /**读取本机当前底座构建记录*/
    public static HotUpdateManifest ReadState() {
        return JsonUtility.FromJson<HotUpdateManifest>(File.ReadAllText(StatePath));
    }

    /// <summary>
    /// 生成候选发布内容，不切换已生效清单。
    /// </summary>
    public static void BuildCandidate() {
        HotUpdateManifest manifest = ReadState();
        string root = Path.Combine(ServerDirectory, manifest.baseVersion);
        string release = Path.Combine(root, manifest.release);
        if (Directory.Exists(release)) {
            throw new InvalidOperationException("候选目录已存在，禁止覆盖：" + release);
        }
        Directory.CreateDirectory(release);
        string runtime = UnityEngine.AddressableAssets.Addressables.BuildPath;
        CopyFile(Path.Combine(runtime, "settings.json"), Path.Combine(release, "settings.json"));
        CopyFile(Path.Combine(runtime, "catalog.json"), Path.Combine(release, "catalog.json"));
        // 只复制当前 Catalog 引用的 Bundle，避免历史构建产物累积。
        string remote = "ServerData/WebGL";
        CatalogFiles catalog = JsonUtility.FromJson<CatalogFiles>(File.ReadAllText(Path.Combine(runtime, "catalog.json")));
        foreach (string id in catalog.m_InternalIds.Distinct()) {
            if (!id.StartsWith(HotUpdateBootstrap.BundleRoot, StringComparison.Ordinal) || !id.EndsWith(".bundle", StringComparison.Ordinal)) {
                continue;
            }
            string relative = id.Substring(HotUpdateBootstrap.BundleRoot.Length);
            CopyFile(Path.Combine(remote, relative), Path.Combine(release, relative));
        }
        foreach (string file in Directory.GetFiles(remote, "catalog*")) {
            if (Path.GetExtension(file) == ".json" || Path.GetExtension(file) == ".hash") {
                CopyFile(file, Path.Combine(release, Path.GetFileName(file)));
            }
        }
        File.WriteAllText(Path.Combine(release, "manifest.json"), JsonUtility.ToJson(manifest, true));
        Debug.Log("[HotUpdateBuild] 候选内容已生成，尚未切换生效版本：" + release);
    }

    /// <summary>
    /// 将候选版本设为本地待发布版本，保留其他底座映射。
    /// </summary>
    public static void Publish() {
        HotUpdateManifest manifest = ReadState();
        string root = Path.Combine(ServerDirectory, manifest.baseVersion);
        string candidate = Path.Combine(root, manifest.release, "manifest.json");
        if (!File.Exists(candidate)) {
            throw new FileNotFoundException("请先构建候选版本。", candidate);
        }
        WriteAtomic(Path.Combine(root, "current.json"), File.ReadAllText(candidate));
        string indexPath = Path.Combine(ServerDirectory, "bootstrap.json");
        HotUpdateIndex index = File.Exists(indexPath) ? JsonUtility.FromJson<HotUpdateIndex>(File.ReadAllText(indexPath)) : new HotUpdateIndex();
        var entries = (index.bases ?? Array.Empty<HotUpdateBaseEntry>()).Where(item => item.baseVersion != manifest.baseVersion).ToList();
        entries.Add(new HotUpdateBaseEntry { baseVersion = manifest.baseVersion, manifestUrl = manifest.baseVersion + "/current.json" });
        index.bases = entries.ToArray();
        WriteAtomic(indexPath, JsonUtility.ToJson(index, true));
        Debug.Log("[HotUpdateBuild] 本地清单已切换，推送资源仓库后远端才会生效。");
    }

    /// <summary>
    /// 读取 Catalog 中的原始资源地址。
    /// </summary>
    [Serializable]
    private class CatalogFiles {

        public string[] m_InternalIds;

    }

    /**复制单个发布文件*/
    private static void CopyFile(string source, string target) {
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(source, target, false);
    }

    /**原子切换清单文件*/
    private static void WriteAtomic(string path, string text) {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        if (File.Exists(path)) {
            File.Replace(temporary, path, null);
        } else {
            File.Move(temporary, path);
        }
    }

}
#endif
