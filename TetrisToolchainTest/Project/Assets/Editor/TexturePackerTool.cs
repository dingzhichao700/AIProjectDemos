#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TexturePackerImporter {

    ///
    //用TexturePacker生成图集，流程是：
    //1.遍历选择的文件夹下的所有png图片，把每张图的border信息存好
    //2.通过指令使用TexturePacker工具打图集，并把多余的sheet文件删掉
    //3.对生成出的图集图片中的每张成员图片，基于第1步中的信息，设置其border
    //打图集完成
    public class TexturePackerTool : Editor {

        /**图集原图路径*/
        private static string ATLAS_SOURCE_ROOT = ResourceConst.PATH_ATLAS_SOURCE;
        /**图集输出路径*/
        private static string ATLAS_OUTPUT_ROOT = ResourceConst.PATH_ATLAS;
        /**TexturePacker 工具路径（可在 EditorPrefs 覆盖：TexturePackerExePath）*/
        private static string TexturePackerExe =>
            EditorPrefs.GetString("TexturePackerExePath",
                "D:/DingWork/U3Dproj/TexturePakcer/bin/TexturePacker.exe");

        [MenuItem("Assets/UI工具/合并图集")]
        [MenuItem("Tools/合并图集")]

        public static void GenerateAtlasByTexturePacker() {
            DoGenerateAtlasByTexturePacker();
        }

        [MenuItem("Assets/UI工具/合并图集（保留透明）")]
        [MenuItem("Tools/合并图集（保留透明）")]
        public static void GenerateAtlasByTexturePackerKeepTrans() {
            DoGenerateAtlasByTexturePacker(false);
        }

        /// <summary>
        /// 打图集
        /// </summary>
        /// <param name="trimTrans">是否裁剪透明区域</param>
        private static void DoGenerateAtlasByTexturePacker(bool trimTrans = true) {
            //这里格式是列表，因为支持选中多个图集源目录，一起打图集
            string[] strs = Selection.assetGUIDs;
            if (strs.Length == 0) {
                Debug.LogError("请先选中" + ATLAS_SOURCE_ROOT + "路径下的文件");
                return;
            }

            foreach (string str in strs) {
                string sourceDir = AssetDatabase.GUIDToAssetPath(str);
                if (string.IsNullOrEmpty(sourceDir))
                    continue;
                PackAtlasSourceAssetPath(sourceDir, trimTrans);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>按 atlasSource 资产路径打图集（供 batchmode / 脚本调用）。</summary>
        public static void PackAtlasSourceAssetPath(string sourceAssetDir, bool trimTrans = true) {
            if (string.IsNullOrEmpty(sourceAssetDir) || !sourceAssetDir.StartsWith(ATLAS_SOURCE_ROOT)) {
                Debug.LogError("只支持 " + ATLAS_SOURCE_ROOT + " 下的目录：" + sourceAssetDir);
                return;
            }

            string relativePath = GetRelativePath(sourceAssetDir);
            string finalFolder = relativePath.Substring(0, relativePath.LastIndexOf("/"));
            string outputDir = Application.dataPath + "/" + ATLAS_OUTPUT_ROOT.Replace("Assets/", "") + finalFolder + "/";

            string sourceDir = Path.Combine(Application.dataPath, sourceAssetDir.Replace("Assets/", "")).Replace("\\", "/");
            if (!Directory.Exists(sourceDir)) {
                Debug.LogError("不是有效的散图目录：" + sourceDir);
                return;
            }

            string trimMode = trimTrans ? "Trim" : "None";
            RunTexturePacker(sourceDir, outputDir, trimMode);
        }

        /// <summary>batchmode: -executeMethod TexturePackerImporter.TexturePackerTool.BatchPackTitleTitleAtlasKeepTrans</summary>
        public static void BatchPackTitleTitleAtlasKeepTrans() {
            PackAtlasSourceAssetPath(ATLAS_SOURCE_ROOT + "title/title", trimTrans: false);
            AssetDatabase.Refresh();
            Debug.Log("BatchPackTitleTitleAtlasKeepTrans completed -> " + ATLAS_OUTPUT_ROOT + "title/title.png");
        }

        /**根据源路径，获取输出目录下的相对路径*/
        private static string GetRelativePath(string sourceDir) {
            return sourceDir.Replace("\\", "/").Substring(ATLAS_SOURCE_ROOT.Length);
        }

        /// <summary>
        /// 执行图集打包
        /// </summary>
        /// <param name="sourceDir">原图目录路径</param>
        /// <param name="outputDir">目标路径</param>
        /// <param name="trimTrans">透明区域裁剪模式</param>
        private static void RunTexturePacker(string sourceDir, string outputDir, string trimMode) {
            DirectoryInfo directoryInfo = new DirectoryInfo(sourceDir);
            FileInfo[] fileInfos = directoryInfo.GetFiles(".", SearchOption.AllDirectories);
            //原图border字典<图片路径，border信息>
            Dictionary<string, Vector4> sourceSpriteBorderMap = new Dictionary<string, Vector4>();
            //原图pivot字典<图片路径，pivot信息>
            Dictionary<string, Vector2> sourceSpritePivotMap = new Dictionary<string, Vector2>();
            for (int i = 0; i < fileInfos.Length; i++) {
                FileInfo fileInfo = fileInfos[i];
                string pngPath = fileInfo.FullName.Replace("\\", "/");
                string appPath = Application.dataPath.Replace("\\", "/");
                pngPath = "Assets" + pngPath.Replace(appPath, "");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                if (sprite) {
                    sourceSpriteBorderMap[fileInfo.Name.Replace(".png", "")] = sprite.border;
                    sourceSpritePivotMap[fileInfo.Name.Replace(".png", "")] = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                }
            }

            string atlasName = Path.GetFileName(sourceDir);
            string atlasPath = outputDir + atlasName + ".png";
            string sheetPath = outputDir + atlasName + ".tpsheet";

            string commandStr =
                $"\"{sourceDir}\" " +
                $"--sheet {atlasPath} " +
                $"--data {sheetPath} " +
                $"--trim-mode {trimMode} " +
                "--texture-format png " +
                "--format unity-texture2d " +
                "--opt RGBA8888 " +
                "--size-constraints AnySize " +
                "--disable-rotation " +
                "--algorithm MaxRects " +
                "--extrude 0 " +
                "--shape-padding 1 " +
                "--max-size 2048 ";

            //执行命令行
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = TexturePackerExe,
                    Arguments = commandStr,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                Debug.LogError($"❌ TexturePacker failed:\n{sourceDir}");
                return;
            }
            AssetDatabase.Refresh();

            string assetPath = "Assets" + atlasPath.Replace(Application.dataPath, "");
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) {
                Debug.LogError($"找不到资源：{assetPath}");
                return;
            }

            SheetInfo sheet = TexturePackerImporter.getSheetInfo(ti);
            SpriteMetaData[] data = sheet.metadata;

            for (int i = 0; i < data.Length; i++) {
                SpriteMetaData spriteMeta = data[i];
                //原图的border信息
                Vector4 sourceSpriteBorder;
                sourceSpriteBorderMap.TryGetValue(spriteMeta.name, out sourceSpriteBorder);
                //原图的pivot信息
                Vector2 sourceSpritePivot;
                sourceSpritePivotMap.TryGetValue(spriteMeta.name, out sourceSpritePivot);
                if (sourceSpriteBorder != null) {
                    spriteMeta.border = sourceSpriteBorder;
                    spriteMeta.alignment = 9;
                    spriteMeta.pivot = sourceSpritePivot;
                }
                data[i] = spriteMeta;
            }
            ti.spritesheet = data;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (File.Exists(sheetPath)) {
                File.Delete(sheetPath);
                File.Delete(sheetPath + ".meta");
            }
        }

    }
#endif
}
