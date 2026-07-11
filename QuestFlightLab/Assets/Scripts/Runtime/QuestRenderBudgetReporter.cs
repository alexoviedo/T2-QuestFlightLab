using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using QuestFlightLab.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class QuestRuntimePerformanceReport
    {
        public string generatedUtc;
        public RenderQualityEvidence renderQuality;
        public EnvironmentRenderOptimizationReport environmentOptimization;
        public RenderBudgetSnapshot renderBudget;
        public SceneryPerformanceSnapshot steadyStateFrameTiming;
        public bool frameTimingManagerAvailable;
        public int frameTimingManagerSamples;
        public float averageCpuFrameMs;
        public float p95CpuFrameMs;
        public float averageGpuFrameMs;
        public float p95GpuFrameMs;
        public bool readinessPrerequisitesMet;
        public string readinessStatus;
        public bool timingSampleComplete;
        public bool quest72HzFrameBudgetPlausible;
        public string evidencePath;
        public string limitations;
    }

    /// <summary>
    /// Writes a post-load Quest render budget and steady-state frame timing report.
    /// The report deliberately labels scene-derived draw-call counts as estimates.
    /// </summary>
    public class QuestRenderBudgetReporter : MonoBehaviour
    {
        private const string LogPrefix = "[QuestFlightLab][RenderBudget]";

        public float readinessTimeoutSeconds = 35f;
        public float postReadinessQuietSeconds = 0.5f;
        public float sampleTimeoutSeconds = 15f;
        public int frameSamples = 180;
        public int warmupFrames = 90;

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + readinessTimeoutSeconds;
            bool readinessMet = SceneReady(out string readinessStatus);
            while (!readinessMet && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                readinessMet = SceneReady(out readinessStatus);
            }

            if (readinessMet && postReadinessQuietSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(postReadinessQuietSeconds);
                readinessMet = SceneReady(out readinessStatus);
            }

            if (!readinessMet)
            {
                Debug.LogWarning($"{LogPrefix} Steady-state readiness timed out: {readinessStatus}");
            }

            yield return null;
            Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            EnvironmentRenderOptimizationReport optimization = QuestRenderQualityConfigurator.ApplyEnvironmentOptimization();
            GameObject probeObject = new GameObject("Quest Render Frame Timing Probe");
            probeObject.transform.SetParent(transform, false);
            SceneryPerformanceProbe probe = probeObject.AddComponent<SceneryPerformanceProbe>();
            int requestedSamples = Mathf.Max(30, frameSamples);
            probe.maxSamples = requestedSamples;
            probe.warmupSamples = Mathf.Max(0, warmupFrames);

            float[] cpuSamples = new float[requestedSamples];
            float[] gpuSamples = new float[requestedSamples];
            int frameTimingCount = 0;
            FrameTiming[] latestTiming = new FrameTiming[1];
            float sampleDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, sampleTimeoutSeconds);
            while (probe.SampleCount < probe.maxSamples && Time.realtimeSinceStartup < sampleDeadline)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return new WaitForEndOfFrame();
                // SceneryPerformanceProbe discards these same warmup frames. Do not
                // let FrameTimingManager's CPU/GPU percentiles include them either.
                if (probe.SampleCount <= 0) continue;

                uint count = FrameTimingManager.GetLatestTimings(1, latestTiming);
                if (count == 0 || frameTimingCount >= cpuSamples.Length) continue;
                if (latestTiming[0].cpuFrameTime <= 0.01d && latestTiming[0].gpuFrameTime <= 0.01d) continue;
                cpuSamples[frameTimingCount] = (float)latestTiming[0].cpuFrameTime;
                gpuSamples[frameTimingCount] = (float)latestTiming[0].gpuFrameTime;
                frameTimingCount++;
            }

            SceneryPerformanceSnapshot steadyState = probe.Capture();
            bool timingSampleComplete = steadyState.sampleCount >= requestedSamples;
            string limitations = "Unity frame timing and scene-audit estimates do not replace OVR Metrics Tool/Meta GPU counters or comfort testing on a representative busy flight.";
            if (!readinessMet)
            {
                limitations += $" Steady-state readiness timed out ({readinessStatus}); the 72 Hz gate is forced to FAIL.";
            }
            if (!timingSampleComplete)
            {
                limitations += $" Timing capture ended with {steadyState.sampleCount}/{requestedSamples} requested samples; the 72 Hz gate is forced to FAIL.";
            }

            QuestRuntimePerformanceReport report = new QuestRuntimePerformanceReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                renderQuality = QuestRenderQualityConfigurator.CaptureEvidence("quest_runtime_post_load"),
                environmentOptimization = optimization,
                renderBudget = QuestRenderBudgetAudit.Capture(camera),
                steadyStateFrameTiming = steadyState,
                frameTimingManagerAvailable = frameTimingCount > 0,
                frameTimingManagerSamples = frameTimingCount,
                averageCpuFrameMs = Average(cpuSamples, frameTimingCount),
                p95CpuFrameMs = SceneryPerformanceProbe.CalculatePercentile(cpuSamples, frameTimingCount, 0.95f),
                averageGpuFrameMs = Average(gpuSamples, frameTimingCount),
                p95GpuFrameMs = SceneryPerformanceProbe.CalculatePercentile(gpuSamples, frameTimingCount, 0.95f),
                readinessPrerequisitesMet = readinessMet,
                readinessStatus = readinessStatus,
                timingSampleComplete = timingSampleComplete,
                limitations = limitations
            };
            report.quest72HzFrameBudgetPlausible = report.readinessPrerequisitesMet &&
                                                   report.timingSampleComplete &&
                                                   report.steadyStateFrameTiming.p95FrameMs <= 1000f / 72f &&
                                                   (!report.frameTimingManagerAvailable || report.p95GpuFrameMs <= 1000f / 72f) &&
                                                   report.renderBudget.drawCallBudgetPlausible &&
                                                   report.renderBudget.visibleTriangleBudgetPlausible;
            WriteReport(report);
        }

        private static bool SceneReady(out string status)
        {
            if (QuestEnvironmentRenderOptimizer.FindPrimaryEnvironmentRoot() == null)
            {
                status = "waiting for environment root";
                return false;
            }

            QuestFirstViewRuntimeRepair repair = FindFirstObjectByType<QuestFirstViewRuntimeRepair>();
            if (repair != null)
            {
                if (!repair.ImportedC172Loaded)
                {
                    status = "waiting for imported C172";
                    return false;
                }

                if (!repair.StartupSeatAlignmentCompleted)
                {
                    status = $"waiting for startup seat alignment ({repair.StartupSeatAlignmentStatus})";
                    return false;
                }
            }

            FirstViewPlaytestDiagnostics diagnostics = FirstViewPlaytestDiagnostics.Instance != null
                ? FirstViewPlaytestDiagnostics.Instance
                : FindFirstObjectByType<FirstViewPlaytestDiagnostics>();
            if (diagnostics != null && !diagnostics.InitialDiagnosticScreenshotsComplete)
            {
                status = "waiting for initial diagnostic screenshots";
                return false;
            }

            status = "ready: environment, cockpit, seat alignment, and diagnostic captures settled";
            return true;
        }

        private static float Average(float[] values, int count)
        {
            if (values == null || count <= 0) return 0f;
            count = Mathf.Min(count, values.Length);
            double total = 0d;
            for (int i = 0; i < count; i++) total += values[i];
            return (float)(total / count);
        }

        private static void WriteReport(QuestRuntimePerformanceReport report)
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "QuestFlightLab", "render_performance");
                Directory.CreateDirectory(directory);
                string stem = $"render_performance_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                string jsonPath = Path.Combine(directory, stem + ".json");
                report.evidencePath = jsonPath;
                File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(directory, stem + ".md"), BuildMarkdown(report));
                Debug.Log(
                    $"{LogPrefix} Evidence written: {jsonPath} p95={report.steadyStateFrameTiming.p95FrameMs:0.00}ms " +
                    $"drawEstimate={report.renderBudget.estimatedInstancedDrawCalls} visibleTris~{report.renderBudget.estimatedFrustumTriangles} " +
                    $"72HzPlausible={report.quest72HzFrameBudgetPlausible}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Evidence write failed: {ex.Message}");
            }
        }

        private static string BuildMarkdown(QuestRuntimePerformanceReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Quest Runtime Render Performance");
            sb.AppendLine();
            sb.AppendLine($"- Generated UTC: {report.generatedUtc}");
            sb.AppendLine($"- 72 Hz plausibility gate: {(report.quest72HzFrameBudgetPlausible ? "PASS" : "FAIL")}");
            sb.AppendLine($"- Steady-state readiness: {(report.readinessPrerequisitesMet ? "ready" : "timed out")}; {report.readinessStatus}");
            sb.AppendLine($"- Timing sample complete: {report.timingSampleComplete}; {report.steadyStateFrameTiming.sampleCount} samples after {report.steadyStateFrameTiming.warmupSamplesDiscarded} warmup frames");
            sb.AppendLine($"- Steady frame time: average {report.steadyStateFrameTiming.averageFrameMs.ToString("0.00", CultureInfo.InvariantCulture)} ms; p95 {report.steadyStateFrameTiming.p95FrameMs.ToString("0.00", CultureInfo.InvariantCulture)} ms; p99 {report.steadyStateFrameTiming.p99FrameMs.ToString("0.00", CultureInfo.InvariantCulture)} ms");
            sb.AppendLine($"- FrameTimingManager: {(report.frameTimingManagerAvailable ? report.frameTimingManagerSamples + " samples" : "unavailable")}; CPU p95 {report.p95CpuFrameMs.ToString("0.00", CultureInfo.InvariantCulture)} ms; GPU p95 {report.p95GpuFrameMs.ToString("0.00", CultureInfo.InvariantCulture)} ms");
            sb.AppendLine($"- Render scale/MSAA/foveation: {report.renderQuality.eyeTextureResolutionScale:0.00} / {report.renderQuality.antiAliasing}x / {report.renderQuality.foveatedRenderingLevel:0.00}");
            sb.AppendLine($"- Draw calls: {report.renderBudget.estimatedInstancedDrawCalls} instancing estimate against target {report.renderBudget.drawCallTarget}");
            sb.AppendLine($"- Visible triangles: ~{report.renderBudget.estimatedFrustumTriangles:N0} against target {report.renderBudget.visibleTriangleTarget:N0}");
            sb.AppendLine($"- LOD coverage: {report.renderBudget.renderersManagedByLod}/{report.renderBudget.rendererCount} renderers");
            sb.AppendLine($"- Materials/shader variants: {report.renderBudget.uniqueMaterialCount}/{report.renderBudget.materialVariantSignatureCount}");
            sb.AppendLine();
            sb.AppendLine($"Limitations: {report.limitations}");
            return sb.ToString();
        }
    }
}
