using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

namespace QuestFlightLab.Experimental.Splats.Editor
{
    public static class RealSplatRendererBatchRunner
    {
        private const string GeneratedAssetFolder = "Assets/Experimental/Splats/Generated";

        [Serializable]
        public class RealSplatBudgetResult
        {
            public int splatCount;
            public string inputPlyPath;
            public long inputBytes;
            public string generatedAssetPath;
            public long generatedAssetBytes;
            public bool assetCreated;
            public bool rendererInstantiated;
            public bool hasValidAsset;
            public bool hasValidRenderSetup;
            public bool screenshotWritten;
            public int nonTransparentPixels;
            public float averageRenderMs;
            public string result;
            public List<string> warnings = new List<string>();
        }

        [Serializable]
        public class RealSplatSpikeReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string graphicsDeviceType;
            public string rendererPackage;
            public string rendererVersion;
            public string rendererCommit;
            public string sampleSchema;
            public string classification;
            public List<RealSplatBudgetResult> budgets = new List<RealSplatBudgetResult>();
            public List<string> limitations = new List<string>();
        }

        [MenuItem("Quest Flight Lab/Run Real Gaussian Splat Renderer Spike")]
        public static void Run()
        {
            string artifactDir = System.Environment.GetEnvironmentVariable("QFL_ARTIFACT_DIR");
            if (string.IsNullOrWhiteSpace(artifactDir))
            {
                artifactDir = Path.GetFullPath(Path.Combine("..", "T2-QuestFlightLab-setup-artifacts", $"real_splat_renderer_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            string sampleDir = System.Environment.GetEnvironmentVariable("QFL_REAL_SPLAT_SAMPLE_DIR");
            Directory.CreateDirectory(artifactDir);
            Directory.CreateDirectory(GeneratedAssetFolder);

            RealSplatSpikeReport report = new RealSplatSpikeReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                rendererPackage = "aras-p/UnityGaussianSplatting",
                rendererVersion = PackageVersion(),
                rendererCommit = PackageCommit(),
                sampleSchema = "unity_3dgs_binary_little_endian"
            };

            foreach (int count in new[] { 5000, 50000, 100000 })
            {
                string plyPath = string.IsNullOrWhiteSpace(sampleDir) ? string.Empty : Path.Combine(sampleDir, $"synthetic_splats_{count}.ply");
                RealSplatBudgetResult result = RunBudget(count, plyPath, artifactDir);
                report.budgets.Add(result);
                if (result.result != "editor_render_pass")
                {
                    break;
                }
            }

            report.classification = Classify(report);
            report.limitations.Add("Editor render smoke is not Quest runtime proof.");
            report.limitations.Add("Synthetic samples are not photogrammetry captures and do not prove scenic visual quality.");
            report.limitations.Add("Mesh/terrain fallback remains the default simulator scenery path.");

            WriteReport(report, artifactDir);
            UnityEngine.Debug.Log($"[QuestFlightLab][RealSplats] Classification {report.classification}. Evidence: {artifactDir}");
        }

        private static RealSplatBudgetResult RunBudget(int count, string plyPath, string artifactDir)
        {
            RealSplatBudgetResult result = new RealSplatBudgetResult
            {
                splatCount = count,
                inputPlyPath = plyPath ?? string.Empty,
                inputBytes = File.Exists(plyPath) ? new FileInfo(plyPath).Length : 0L,
                result = "editor_render_fail"
            };

            if (!File.Exists(plyPath))
            {
                result.warnings.Add($"Missing input PLY for budget {count}: {plyPath}");
                return result;
            }

            string baseName = Path.GetFileNameWithoutExtension(plyPath);
            string assetPath = $"{GeneratedAssetFolder}/{baseName}.asset";
            try
            {
                CreateGaussianSplatAsset(plyPath, GeneratedAssetFolder);
                result.generatedAssetPath = assetPath;
                result.generatedAssetBytes = SumGeneratedAssetBytes(baseName);
                GaussianSplatAsset asset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(assetPath);
                result.assetCreated = asset != null && asset.splatCount == count;
                if (!result.assetCreated)
                {
                    result.warnings.Add($"GaussianSplatAsset was not created or splat count mismatched at {assetPath}");
                    return result;
                }

                RenderSmoke(asset, count, artifactDir, result);
            }
            catch (Exception ex)
            {
                result.warnings.Add(ex.GetType().Name + ": " + ex.Message);
                result.result = "editor_render_fail";
            }

            return result;
        }

        private static void CreateGaussianSplatAsset(string inputFile, string outputFolder)
        {
            Type creatorType = Type.GetType("GaussianSplatting.Editor.GaussianSplatAssetCreator, GaussianSplattingEditor", true);
            ScriptableObject creator = ScriptableObject.CreateInstance(creatorType);
            try
            {
                SetField(creatorType, creator, "m_InputFile", inputFile);
                SetField(creatorType, creator, "m_OutputFolder", outputFolder);
                SetField(creatorType, creator, "m_ImportCameras", false);
                Type qualityType = creatorType.GetNestedType("DataQuality", BindingFlags.NonPublic);
                SetField(creatorType, creator, "m_Quality", Enum.Parse(qualityType, "Medium"));
                creatorType.GetMethod("ApplyQualityLevel", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(creator, null);
                creatorType.GetMethod("CreateAsset", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(creator, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creator);
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
        }

        private static void SetField(Type type, object instance, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            field.SetValue(instance, value);
        }

        private static void RenderSmoke(GaussianSplatAsset asset, int count, string artifactDir, RealSplatBudgetResult result)
        {
            GameObject root = new GameObject($"RealSplatSmoke_{count}");
            GameObject cameraObject = new GameObject($"RealSplatSmokeCamera_{count}");
            RenderTexture rt = null;
            Texture2D capture = null;
            try
            {
                GaussianSplatRenderer renderer = root.AddComponent<GaussianSplatRenderer>();
                renderer.m_Asset = asset;
                renderer.m_ShaderSplats = LoadShader("RenderGaussianSplats.shader");
                renderer.m_ShaderComposite = LoadShader("GaussianComposite.shader");
                renderer.m_ShaderDebugPoints = LoadShader("GaussianDebugRenderPoints.shader");
                renderer.m_ShaderDebugBoxes = LoadShader("GaussianDebugRenderBoxes.shader");
                renderer.m_CSSplatUtilities = LoadCompute("SplatUtilities.compute");
                renderer.m_SplatScale = 1.35f;
                renderer.m_OpacityScale = 1.0f;
                renderer.m_SHOrder = 0;
                renderer.m_SortNthFrame = 1;
                renderer.OnEnable();

                result.rendererInstantiated = true;
                result.hasValidAsset = renderer.HasValidAsset;
                result.hasValidRenderSetup = renderer.HasValidRenderSetup;

                Camera cam = cameraObject.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 2.5f, -18f);
                cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 120f;
                cam.fieldOfView = 55f;
                rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < 6; i++)
                {
                    cam.Render();
                }
                sw.Stop();
                result.averageRenderMs = (float)(sw.Elapsed.TotalMilliseconds / 6.0);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                capture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                capture.Apply();
                RenderTexture.active = previous;

                Color32[] pixels = capture.GetPixels32();
                int nonTransparent = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 0 && (pixels[i].r > 8 || pixels[i].g > 8 || pixels[i].b > 8))
                    {
                        nonTransparent++;
                    }
                }

                result.nonTransparentPixels = nonTransparent;
                string screenshotPath = Path.Combine(artifactDir, $"real_splat_{count}_editor.png");
                File.WriteAllBytes(screenshotPath, capture.EncodeToPNG());
                result.screenshotWritten = File.Exists(screenshotPath);

                if (result.hasValidAsset && result.hasValidRenderSetup && result.nonTransparentPixels > 100)
                {
                    result.result = "editor_render_pass";
                }
                else
                {
                    result.result = "editor_render_partial";
                    result.warnings.Add("Renderer instantiated but screenshot/pixel validation did not prove visible splats.");
                }
            }
            finally
            {
                if (rt != null) rt.Release();
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static Shader LoadShader(string name)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"Packages/org.nesnausk.gaussian-splatting/Shaders/{name}");
            if (shader == null) throw new FileNotFoundException($"Missing Gaussian splat shader {name}");
            return shader;
        }

        private static ComputeShader LoadCompute(string name)
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>($"Packages/org.nesnausk.gaussian-splatting/Shaders/{name}");
            if (shader == null) throw new FileNotFoundException($"Missing Gaussian splat compute shader {name}");
            return shader;
        }

        private static long SumGeneratedAssetBytes(string baseName)
        {
            long total = 0;
            foreach (string path in Directory.GetFiles(GeneratedAssetFolder, $"{baseName}*.*", SearchOption.TopDirectoryOnly))
            {
                string extension = Path.GetExtension(path);
                if (extension == ".meta") continue;
                total += new FileInfo(path).Length;
            }
            return total;
        }

        private static string Classify(RealSplatSpikeReport report)
        {
            if (report.budgets.Count == 0) return "blocked_package_integration";
            bool anyPass = report.budgets.Exists(b => b.result == "editor_render_pass");
            bool fiftyPass = report.budgets.Exists(b => b.splatCount == 50000 && b.result == "editor_render_pass");
            if (fiftyPass) return "partial_editor_only";
            if (anyPass) return "partial_editor_tiny_only";
            return "editor_render_fail";
        }

        private static void WriteReport(RealSplatSpikeReport report, string artifactDir)
        {
            Directory.CreateDirectory(artifactDir);
            File.WriteAllText(Path.Combine(artifactDir, "real_splat_editor_results.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(artifactDir, "real_splat_editor_results.csv"), BuildCsv(report));
            File.WriteAllText(Path.Combine(artifactDir, "real_splat_editor_summary.md"), BuildMarkdown(report));
        }

        private static string BuildCsv(RealSplatSpikeReport report)
        {
            List<string> lines = new List<string> { "splat_count,result,input_mb,generated_mb,valid_asset,valid_render_setup,nontransparent_pixels,avg_render_ms,warnings" };
            foreach (RealSplatBudgetResult budget in report.budgets)
            {
                lines.Add(string.Join(",",
                    budget.splatCount,
                    budget.result,
                    (budget.inputBytes / (1024f * 1024f)).ToString("0.###"),
                    (budget.generatedAssetBytes / (1024f * 1024f)).ToString("0.###"),
                    budget.hasValidAsset ? "true" : "false",
                    budget.hasValidRenderSetup ? "true" : "false",
                    budget.nonTransparentPixels,
                    budget.averageRenderMs.ToString("0.###"),
                    Escape(string.Join("; ", budget.warnings))));
            }
            return string.Join("\n", lines) + "\n";
        }

        private static string BuildMarkdown(RealSplatSpikeReport report)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("# Real Gaussian Splat Renderer Spike Summary");
            sb.AppendLine();
            sb.AppendLine($"Generated UTC: {report.generatedUtc}");
            sb.AppendLine($"Unity: {report.unityVersion}");
            sb.AppendLine($"Graphics device: {report.graphicsDeviceType}");
            sb.AppendLine($"Renderer: {report.rendererPackage} {report.rendererVersion} ({report.rendererCommit})");
            sb.AppendLine($"Classification: `{report.classification}`");
            sb.AppendLine();
            sb.AppendLine("| Splats | Result | Input MB | Generated MB | Valid Renderer | Pixels | Avg Render ms |");
            sb.AppendLine("| ---: | --- | ---: | ---: | --- | ---: | ---: |");
            foreach (RealSplatBudgetResult budget in report.budgets)
            {
                sb.AppendLine($"| {budget.splatCount} | {budget.result} | {budget.inputBytes / (1024f * 1024f):0.###} | {budget.generatedAssetBytes / (1024f * 1024f):0.###} | {budget.hasValidAsset && budget.hasValidRenderSetup} | {budget.nonTransparentPixels} | {budget.averageRenderMs:0.###} |");
                foreach (string warning in budget.warnings)
                {
                    sb.AppendLine($"  - {warning}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("## Limitations");
            foreach (string limitation in report.limitations)
            {
                sb.AppendLine($"- {limitation}");
            }
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string escaped = value.Replace("\"", "\"\"");
            if (escaped.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return $"\"{escaped}\"";
            }
            return escaped;
        }

        private static string PackageVersion() => "v1.1.1";

        private static string PackageCommit() => "9310dce438da726244ace17eaf6f768826435fa4";
    }
}
