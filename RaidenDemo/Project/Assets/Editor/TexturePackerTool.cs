#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TexturePackerImporter {
    /// <summary>通过 TexturePacker 生成图集，并以 Unity Sprite Data Provider 写入切片信息。</summary>
    public class TexturePackerTool : Editor {
        const string FingerprintPrefix = "TexturePackerToolFingerprintV4=";
        const string ToolVersion = "4";
        const int MaxAtlasSize = 2048;
        const int LargeBackgroundMinWidth = 720;
        const int LargeBackgroundMinHeight = 720;
        const int ProcessTimeoutMs = 120000;

        static readonly string AtlasSourceRoot = ResourceConst.PATH_ATLAS_SOURCE;
        static readonly string AtlasOutputRoot = ResourceConst.PATH_ATLAS;
        static string TexturePackerExe => EditorPrefs.GetString("TexturePackerExePath",
            "D:/DingWork/U3Dproj/TexturePakcer/bin/TexturePacker.exe");

        public sealed class AtlasPackResult {
            public string atlasAssetPath;
            public bool skipped;
            public int spriteCount;
            public long durationMs;
        }

        sealed class SourceSpriteInfo {
            public string name;
            public string assetPath;
            public string contentHash;
            public Vector4 border;
            public Vector2 pivot;
        }

        [MenuItem("Assets/UI工具/合并图集")]
        [MenuItem("Tools/合并图集")]
        public static void GenerateAtlasByTexturePacker() {
            PackSelection(trimTrans: true);
        }

        [MenuItem("Assets/UI工具/合并图集（保留透明）")]
        [MenuItem("Tools/合并图集（保留透明）")]
        public static void GenerateAtlasByTexturePackerKeepTrans() {
            PackSelection(trimTrans: false);
        }

        static void PackSelection(bool trimTrans) {
            if (Selection.assetGUIDs.Length == 0)
                throw new InvalidOperationException("请先选中 " + AtlasSourceRoot + " 路径下的图集源目录");

            foreach (string guid in Selection.assetGUIDs) {
                string sourceDir = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(sourceDir))
                    throw new InvalidOperationException("无法解析选中的资源目录：" + guid);
                AtlasPackResult result = PackAtlasSourceAssetPath(sourceDir, trimTrans);
                Debug.Log($"[TexturePackerTool] {(result.skipped ? "跳过未变化图集" : "图集生成成功")}: " +
                          $"{result.atlasAssetPath}, Sprites={result.spriteCount}, {result.durationMs}ms");
            }
        }

        /// <summary>按 atlasSource 资产路径打图集；大背景需明确允许后才可进入图集。</summary>
        public static AtlasPackResult PackAtlasSourceAssetPath(string sourceAssetDir, bool trimTrans = true,
            bool allowLargeSprites = false) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ValidateSourceAssetPath(sourceAssetDir);

            string relativePath = GetRelativePath(sourceAssetDir);
            int separatorIndex = relativePath.LastIndexOf("/", StringComparison.Ordinal);
            if (separatorIndex <= 0)
                throw new InvalidOperationException("图集源目录必须包含 Group/模块 两级路径：" + sourceAssetDir);

            string sourceDir = Path.Combine(Application.dataPath, sourceAssetDir.Substring("Assets/".Length))
                .Replace("\\", "/");
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException("图集源目录不存在：" + sourceDir);
            if (!File.Exists(TexturePackerExe))
                throw new FileNotFoundException("TexturePacker 不存在，请配置 EditorPrefs.TexturePackerExePath", TexturePackerExe);

            string finalFolder = relativePath.Substring(0, separatorIndex);
            string outputDir = (Application.dataPath + "/" + AtlasOutputRoot.Replace("Assets/", "") + finalFolder + "/")
                .Replace("\\", "/");
            Directory.CreateDirectory(outputDir);

            List<SourceSpriteInfo> sources = CollectAndValidateSources(sourceDir, allowLargeSprites);
            string trimMode = trimTrans ? "Trim" : "None";
            string atlasName = Path.GetFileName(sourceDir);
            string atlasPath = outputDir + atlasName + ".png";
            string sheetPath = outputDir + atlasName + ".tpsheet";
            string atlasAssetPath = "Assets" + atlasPath.Replace(Application.dataPath.Replace("\\", "/"), "");
            string fingerprint = ComputeFingerprint(sources, trimMode);

            if (CanSkipUnchangedAtlas(atlasAssetPath, sources, fingerprint)) {
                return new AtlasPackResult {
                    atlasAssetPath = atlasAssetPath,
                    skipped = true,
                    spriteCount = sources.Count,
                    durationMs = stopwatch.ElapsedMilliseconds
                };
            }

            try {
                RunTexturePackerProcess(sourceDir, atlasPath, sheetPath, trimMode);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(atlasAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                TextureImporter importer = AssetImporter.GetAtPath(atlasAssetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("图集生成后无法取得 TextureImporter：" + atlasAssetPath);

                SheetInfo sheet = TexturePackerImporter.getSheetInfo(importer);
                SpriteMetaData[] metadata = sheet.metadata;
                ValidateSheetMetadata(metadata, sources, sheetPath);
                ApplySourceGeometry(metadata, sources);
                ApplySpriteRects(importer, metadata, fingerprint);
                ValidateImportedAtlas(atlasAssetPath, sources);

                DeleteIfExists(sheetPath);
                DeleteIfExists(sheetPath + ".meta");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                return new AtlasPackResult {
                    atlasAssetPath = atlasAssetPath,
                    skipped = false,
                    spriteCount = sources.Count,
                    durationMs = stopwatch.ElapsedMilliseconds
                };
            } catch (Exception exception) {
                throw new InvalidOperationException(
                    $"TexturePacker 图集事务失败：{sourceAssetDir}。诊断 Sheet 已保留：{sheetPath}\n{exception.Message}",
                    exception);
            }
        }

        public static void BatchPackTitleTitleAtlasKeepTrans() {
            PackAtlasSourceAssetPath(AtlasSourceRoot + "title/title", trimTrans: false);
            Debug.Log("BatchPackTitleTitleAtlasKeepTrans completed -> " + AtlasOutputRoot + "title/title.png");
        }

        static void ValidateSourceAssetPath(string sourceAssetDir) {
            if (string.IsNullOrEmpty(sourceAssetDir) ||
                !sourceAssetDir.Replace("\\", "/").StartsWith(AtlasSourceRoot, StringComparison.Ordinal))
                throw new InvalidOperationException("只支持 " + AtlasSourceRoot + " 下的目录：" + sourceAssetDir);
        }

        static string GetRelativePath(string sourceDir) {
            return sourceDir.Replace("\\", "/").Substring(AtlasSourceRoot.Length);
        }

        static List<SourceSpriteInfo> CollectAndValidateSources(string sourceDir, bool allowLargeSprites) {
            FileInfo[] pngFiles = new DirectoryInfo(sourceDir).GetFiles("*.png", SearchOption.AllDirectories);
            Array.Sort(pngFiles, (left, right) => string.CompareOrdinal(left.FullName, right.FullName));
            if (pngFiles.Length == 0) throw new InvalidOperationException("图集源目录没有 PNG：" + sourceDir);

            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> contentHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            List<SourceSpriteInfo> result = new List<SourceSpriteInfo>(pngFiles.Length);
            string appPath = Application.dataPath.Replace("\\", "/");

            foreach (FileInfo file in pngFiles) {
                string name = Path.GetFileNameWithoutExtension(file.Name);
                if (names.TryGetValue(name, out string duplicateNamePath))
                    throw new InvalidOperationException($"图集 Sprite 重名：{name}\n{duplicateNamePath}\n{file.FullName}");
                names[name] = file.FullName;

                string contentHash = ComputeFileSha256(file.FullName);
                if (contentHashes.TryGetValue(contentHash, out string duplicateContentPath))
                    throw new InvalidOperationException($"图集源图内容重复，请复用同一 Sprite：\n{duplicateContentPath}\n{file.FullName}");
                contentHashes[contentHash] = file.FullName;

                string absolutePath = file.FullName.Replace("\\", "/");
                string assetPath = "Assets" + absolutePath.Replace(appPath, "");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null) {
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                }
                if (sprite == null)
                    throw new InvalidOperationException("图集源图未按 Sprite 导入：" + assetPath);
                if (!allowLargeSprites && sprite.rect.width >= LargeBackgroundMinWidth &&
                    sprite.rect.height >= LargeBackgroundMinHeight)
                    throw new InvalidOperationException(
                        $"检测到疑似全屏/超大背景 {name} ({sprite.rect.width}x{sprite.rect.height})。" +
                        "请迁移到 Assets/Art/unpack；确需入图集时显式传 allowLargeSprites: true。");

                result.Add(new SourceSpriteInfo {
                    name = name,
                    assetPath = assetPath,
                    contentHash = contentHash,
                    border = sprite.border,
                    pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height)
                });
            }
            return result;
        }

        static void RunTexturePackerProcess(string sourceDir, string atlasPath, string sheetPath, string trimMode) {
            string arguments =
                $"\"{sourceDir}\" --sheet \"{atlasPath}\" --data \"{sheetPath}\" " +
                $"--trim-mode {trimMode} --texture-format png --format unity-texture2d --opt RGBA8888 " +
                $"--size-constraints AnySize --disable-rotation --algorithm MaxRects --extrude 0 " +
                $"--shape-padding 1 --max-size {MaxAtlasSize}";

            using (Process process = new Process()) {
                process.StartInfo = new ProcessStartInfo {
                    FileName = TexturePackerExe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.Start();
                Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
                Task<string> standardError = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ProcessTimeoutMs)) {
                    process.Kill();
                    throw new TimeoutException($"TexturePacker 超过 {ProcessTimeoutMs / 1000} 秒未完成");
                }
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"TexturePacker ExitCode={process.ExitCode}\n{standardError.Result}\n{standardOutput.Result}");
            }
            if (!File.Exists(atlasPath) || !File.Exists(sheetPath))
                throw new InvalidOperationException("TexturePacker 未生成完整的 PNG/.tpsheet 产物");
        }

        static void ValidateSheetMetadata(SpriteMetaData[] metadata, List<SourceSpriteInfo> sources, string sheetPath) {
            if (metadata == null || metadata.Length != sources.Count)
                throw new InvalidOperationException(
                    $"Sheet Sprite 数量不一致：expected={sources.Count}, actual={metadata?.Length ?? 0}, {sheetPath}");
            HashSet<string> expected = BuildNameSet(sources);
            foreach (SpriteMetaData spriteMeta in metadata) {
                if (!expected.Remove(spriteMeta.name))
                    throw new InvalidOperationException("Sheet 包含重复或未知 Sprite：" + spriteMeta.name);
            }
            if (expected.Count > 0)
                throw new InvalidOperationException("Sheet 缺少 Sprite：" + string.Join(", ", expected));
        }

        static void ApplySourceGeometry(SpriteMetaData[] metadata, List<SourceSpriteInfo> sources) {
            Dictionary<string, SourceSpriteInfo> sourceMap = new Dictionary<string, SourceSpriteInfo>(StringComparer.Ordinal);
            foreach (SourceSpriteInfo source in sources) sourceMap[source.name] = source;
            for (int i = 0; i < metadata.Length; i++) {
                SpriteMetaData spriteMeta = metadata[i];
                SourceSpriteInfo source = sourceMap[spriteMeta.name];
                spriteMeta.border = source.border;
                spriteMeta.alignment = (int)SpriteAlignment.Custom;
                spriteMeta.pivot = source.pivot;
                metadata[i] = spriteMeta;
            }
        }

        static void ApplySpriteRects(TextureImporter importer, SpriteMetaData[] metadata, string fingerprint) {
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("无法创建 Sprite Data Provider：" + importer.assetPath);
            provider.InitSpriteEditorDataProvider();

            Dictionary<string, GUID> spriteIdMap = new Dictionary<string, GUID>(StringComparer.Ordinal);
            foreach (SpriteRect existingRect in provider.GetSpriteRects())
                spriteIdMap[existingRect.name] = existingRect.spriteID;

            SpriteRect[] spriteRects = new SpriteRect[metadata.Length];
            for (int i = 0; i < metadata.Length; i++) {
                SpriteMetaData spriteMeta = metadata[i];
                spriteRects[i] = new SpriteRect {
                    name = spriteMeta.name,
                    rect = spriteMeta.rect,
                    border = spriteMeta.border,
                    pivot = spriteMeta.pivot,
                    alignment = (SpriteAlignment)spriteMeta.alignment,
                    spriteID = spriteIdMap.TryGetValue(spriteMeta.name, out GUID spriteId) ? spriteId : GUID.Generate()
                };
            }
            provider.SetSpriteRects(spriteRects);
            provider.Apply();
            importer.userData = SetFingerprint(importer.userData, fingerprint);
            importer.SaveAndReimport();
            ValidatePreservedSpriteIds(importer.assetPath, spriteIdMap);
        }

        static void ValidateImportedAtlas(string atlasAssetPath, List<SourceSpriteInfo> sources) {
            HashSet<string> expected = BuildNameSet(sources);
            Dictionary<string, SourceSpriteInfo> sourceMap = new Dictionary<string, SourceSpriteInfo>(StringComparer.Ordinal);
            foreach (SourceSpriteInfo source in sources) sourceMap[source.name] = source;
            int spriteCount = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(atlasAssetPath)) {
                if (!(asset is Sprite sprite)) continue;
                spriteCount++;
                if (!expected.Remove(sprite.name))
                    throw new InvalidOperationException("导入后的图集包含重复或未知 Sprite：" + sprite.name);
                SourceSpriteInfo source = sourceMap[sprite.name];
                Vector2 normalizedPivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                if (sprite.border != source.border || Vector2.Distance(normalizedPivot, source.pivot) > 0.0001f)
                    throw new InvalidOperationException("导入后的 Sprite border/pivot 与源图不一致：" + sprite.name);
            }
            if (spriteCount != sources.Count || expected.Count > 0)
                throw new InvalidOperationException(
                    $"导入后 Sprite 集合不一致：expected={sources.Count}, actual={spriteCount}, missing={string.Join(", ", expected)}");
        }

        static void ValidatePreservedSpriteIds(string atlasAssetPath, Dictionary<string, GUID> previousIds) {
            TextureImporter importer = AssetImporter.GetAtPath(atlasAssetPath) as TextureImporter;
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            foreach (SpriteRect spriteRect in provider.GetSpriteRects()) {
                if (previousIds.TryGetValue(spriteRect.name, out GUID previousId) && spriteRect.spriteID != previousId)
                    throw new InvalidOperationException("重打图集后 Sprite ID 发生变化：" + spriteRect.name);
            }
        }

        static bool CanSkipUnchangedAtlas(string atlasAssetPath, List<SourceSpriteInfo> sources, string fingerprint) {
            TextureImporter importer = AssetImporter.GetAtPath(atlasAssetPath) as TextureImporter;
            if (importer == null || GetFingerprint(importer.userData) != fingerprint) return false;
            try {
                ValidateImportedAtlas(atlasAssetPath, sources);
                return true;
            } catch {
                return false;
            }
        }

        static HashSet<string> BuildNameSet(List<SourceSpriteInfo> sources) {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceSpriteInfo source in sources) names.Add(source.name);
            return names;
        }

        static string ComputeFingerprint(List<SourceSpriteInfo> sources, string trimMode) {
            StringBuilder builder = new StringBuilder();
            builder.Append(ToolVersion).Append('|').Append(trimMode).Append('|').Append(MaxAtlasSize).Append('\n');
            foreach (SourceSpriteInfo source in sources) {
                builder.Append(source.assetPath).Append('|').Append(source.contentHash).Append('|')
                    .Append(source.border).Append('|').Append(source.pivot).Append('\n');
            }
            using (SHA256 sha256 = SHA256.Create()) {
                return BytesToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
            }
        }

        static string ComputeFileSha256(string path) {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return BytesToHex(sha256.ComputeHash(stream));
        }

        static string BytesToHex(byte[] bytes) {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        static string GetFingerprint(string userData) {
            if (string.IsNullOrEmpty(userData)) return string.Empty;
            foreach (string line in userData.Split('\n')) {
                if (line.StartsWith(FingerprintPrefix, StringComparison.Ordinal))
                    return line.Substring(FingerprintPrefix.Length).Trim();
            }
            return string.Empty;
        }

        static string SetFingerprint(string userData, string fingerprint) {
            List<string> lines = new List<string>();
            if (!string.IsNullOrEmpty(userData)) {
                foreach (string line in userData.Split('\n')) {
                    if (!string.IsNullOrWhiteSpace(line) &&
                        !line.StartsWith("TexturePackerToolFingerprint", StringComparison.Ordinal))
                        lines.Add(line.TrimEnd('\r'));
                }
            }
            lines.Add(FingerprintPrefix + fingerprint);
            return string.Join("\n", lines);
        }

        static void DeleteIfExists(string path) {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
#endif
