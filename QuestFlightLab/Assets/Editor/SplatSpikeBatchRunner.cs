using System;
using System.IO;
using QuestFlightLab.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Editor
{
    public static class SplatSpikeBatchRunner
    {
        [MenuItem("Quest Flight Lab/Run Gaussian Splat Spike")]
        public static void RunSplatSpike()
        {
            string artifactDir = System.Environment.GetEnvironmentVariable("QFL_ARTIFACT_DIR");
            if (string.IsNullOrWhiteSpace(artifactDir))
            {
                artifactDir = Path.GetFullPath(Path.Combine("..", "T2-QuestFlightLab-setup-artifacts", $"splat_spike_editor_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            string sampleDir = System.Environment.GetEnvironmentVariable("QFL_SPLAT_SAMPLE_DIR");
            Directory.CreateDirectory(artifactDir);

            ScenerySpikeReport report = new ScenerySpikeReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                renderPipeline = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.name : "Built-in/Core or project default",
                rendererPackage = SplatSceneryProvider.IsGaussianSplatRendererAvailable()
                    ? "Gaussian splat renderer-like Unity types detected"
                    : "No Gaussian splat renderer package detected"
            };

            GameObject probeRoot = new GameObject("GaussianSplatSpikeBatchProbe");
            try
            {
                MeshSceneryProvider meshProvider = probeRoot.AddComponent<MeshSceneryProvider>();
                report.providerStatuses.Add(meshProvider.ActivateProvider(null));

                SplatSceneryProvider disabledSplatProvider = probeRoot.AddComponent<SplatSceneryProvider>();
                disabledSplatProvider.enableExperimentalProxy = false;
                disabledSplatProvider.syntheticSplatCount = 5000;
                disabledSplatProvider.sampleAssetPath = FirstSamplePath(sampleDir, 5000);
                report.providerStatuses.Add(disabledSplatProvider.ActivateProvider(probeRoot.transform));

                GameObject proxyRoot = new GameObject("GaussianSplatProxyProbe");
                proxyRoot.transform.SetParent(probeRoot.transform, false);
                SplatSceneryProvider proxyProvider = proxyRoot.AddComponent<SplatSceneryProvider>();
                proxyProvider.enableExperimentalProxy = true;
                proxyProvider.syntheticSplatCount = 5000;
                proxyProvider.maxProxyPointCount = 5000;
                proxyProvider.sampleAssetPath = FirstSamplePath(sampleDir, 5000);
                report.providerStatuses.Add(proxyProvider.ActivateProvider(proxyRoot.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probeRoot);
            }

            report.budgets = SceneryEvidenceLogger.BuildBudgetEstimates(sampleDir);
            report.limitations.Add("The editor proxy is not a true Gaussian renderer and does not prove Quest visual quality or frame rate.");
            report.limitations.Add("No full-airport splat capture, LOD streaming, SPZ runtime path, or production renderer binding is included in this spike.");
            report.limitations.Add("Mesh/terrain fallback remains the default and the simulator/scenario evidence must not depend on splats.");
            report.viabilityClassification = SceneryEvidenceLogger.Classify(report);

            SceneryEvidenceLogger.WriteReport(report, artifactDir);
            Debug.Log($"[QuestFlightLab][Splats] Spike classified as {report.viabilityClassification}. Evidence: {artifactDir}");
        }

        private static string FirstSamplePath(string sampleDir, int splatCount)
        {
            if (string.IsNullOrWhiteSpace(sampleDir)) return string.Empty;
            string path = Path.Combine(sampleDir, $"synthetic_splats_{splatCount}.ply");
            return File.Exists(path) ? path : string.Empty;
        }
    }
}
