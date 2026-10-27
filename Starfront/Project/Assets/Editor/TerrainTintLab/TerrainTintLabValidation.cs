using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证实验Shader输出、透明区域和六边形画笔的GPU结果。
/// </summary>
/// <remarks>使用当前空闲编辑器，不进入Play、不构建Addressables。</remarks>
[InitializeOnLoad]
public static class TerrainTintLabValidation {

    [Serializable]
    private class Request {
        public string requestId;
        public int processId;
    }

    [Serializable]
    private class Result {
        public string requestId;
        public string status;
        public string error;
        public string checks;
    }

    static TerrainTintLabValidation() {
        EditorApplication.update += Poll;
    }

    /**无用户编辑器时执行GPU验证，完成后正常退出自有实例。*/
    public static void RunBatch() {
        var result = new Result { requestId = "terrain-tint-lab-batch-v1" };
        try {
            Run();
            result.status = "passed";
            result.checks = "shader compiled; grayscale; five blend equations; source alpha; hex paint; exported previews";
        } catch (Exception exception) {
            result.status = "failed";
            result.error = exception.ToString();
            Debug.LogException(exception);
        }
        File.WriteAllText("Library/terrain-tint-lab-result.json", JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.status == "passed" ? 0 : 1);
    }

    private static void Poll() {
        const string path = "Library/terrain-tint-lab-request.json";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var result = new Result();
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            result.requestId = request?.requestId;
            File.Delete(path);
            if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("Wrong editor identity.");
            }
            Run();
            TerrainTintLabWindow.Open();
            result.status = "passed";
            result.checks = "shader compiled; 720x1280 output; grayscale channels; alpha retained for all modes; five blend equations checked against GPU; per-cell paint matches CPU hex selection; material and RT cleanup; editor window opened";
        } catch (Exception exception) {
            result.status = "failed";
            result.error = exception.ToString();
            Debug.LogException(exception);
        }
        File.WriteAllText("Library/terrain-tint-lab-result.json", JsonUtility.ToJson(result, true));
    }

    private static void Run() {
        var importer = (TextureImporter)AssetImporter.GetAtPath(TerrainTintLabRenderer.SourcePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTintLabRenderer.SourcePath);
        Require(source.width == 720 && source.height == 1280, "source dimensions");
        var settings = new TerrainTintLabSettings { brightness = 1, contrast = 1, strength = 1, layout = 1 };
        string output = "E:/ChatGPT/星域前线/outputs/TerrainTintLab-v1";
        Directory.CreateDirectory(output);
        using (var renderer = new TerrainTintLabRenderer()) {
            var original = Pixels(renderer, source, settings, 0, null);
            var gray = Pixels(renderer, source, settings, 1, output + "/gray.png");
            int sample = -1;
            int holes = 0;
            int land = 0;
            for (int i = 0; i < gray.Length; i++) {
                Require(Math.Abs(gray[i].a - original[i].a) <= 1, "grayscale alpha");
                if (gray[i].a == 0) {
                    holes++;
                }
                if (gray[i].a == 255) {
                    land++;
                    Require(Math.Abs(gray[i].r - gray[i].g) <= 1 && Math.Abs(gray[i].r - gray[i].b) <= 1, "grayscale RGB equality");
                    if (sample < 0 && gray[i].r > 40 && gray[i].r < 180) {
                        sample = i;
                    }
                }
            }
            Require(holes > 100000 && land > 700000 && sample >= 0, "real transparent pools and opaque land");
            for (int mode = 2; mode <= 6; mode++) {
                var pixels = Pixels(renderer, source, settings, mode, null);
                for (int i = 0; i < pixels.Length; i++) {
                    Require(Math.Abs(pixels[i].a - original[i].a) <= 1, "blend alpha mode=" + mode);
                }
                float g = gray[sample].r / 255f;
                var actual = (Color)pixels[sample];
                Require(Mathf.Abs(actual.r - Blend(g, settings.red.r, mode)) < .025f && Mathf.Abs(actual.g - Blend(g, settings.red.g, mode)) < .025f && Mathf.Abs(actual.b - Blend(g, settings.red.b, mode)) < .025f, "GPU blend mode=" + mode + ", actual=" + actual);
            }
            int x = sample % source.width;
            int y = source.height - 1 - sample / source.width;
            var cell = TerrainTintLabRenderer.CellAt(new Vector2(x + .5f, y + .5f), settings);
            renderer.SetPaint(new[] { new Vector3Int(cell.x, cell.y, 2) });
            var painted = Pixels(renderer, source, settings, 2, null);
            float value = gray[sample].r / 255f;
            Require(Math.Abs(painted[sample].b / 255f - value * settings.blue.b) < .025f, "hex paint changes selected cell to blue");
            renderer.SetPaint(new List<Vector3Int>());
            settings = new TerrainTintLabSettings();
            Pixels(renderer, source, settings, 3, output + "/overlay.png");
            settings.mode = 4;
            Pixels(renderer, source, settings, 4, output + "/softlight.png");
        }
    }

    private static Color32[] Pixels(TerrainTintLabRenderer renderer, Texture2D source, TerrainTintLabSettings settings, int mode, string path) {
        var target = renderer.Render(source, settings, mode);
        Texture2D readback = null;
        try {
            readback = TerrainTintLabRenderer.Readback(target);
            Require(readback.width == source.width && readback.height == source.height, "render dimensions");
            if (path != null) {
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            return readback.GetPixels32();
        } finally {
            if (readback != null) {
                UnityEngine.Object.DestroyImmediate(readback);
            }
            TerrainTintLabRenderer.Release(target);
        }
    }

    private static float Blend(float gray, float tint, int mode) {
        switch (mode) {
            case 2:
                return gray * tint;
            case 3:
                return gray < .5f ? 2 * gray * tint : 1 - 2 * (1 - gray) * (1 - tint);
            case 4:
                float d = gray <= .25f ? ((16 * gray - 12) * gray + 4) * gray : Mathf.Sqrt(gray);
                return tint <= .5f ? gray - (1 - 2 * tint) * gray * (1 - gray) : gray + (2 * tint - 1) * (d - gray);
            case 5:
                return 1 - (1 - gray) * (1 - tint);
            default:
                return Mathf.Clamp01(gray + tint);
        }
    }

    private static void Require(bool value, string message) {
        if (!value) {
            throw new InvalidOperationException("Terrain tint lab: " + message);
        }
    }

}
