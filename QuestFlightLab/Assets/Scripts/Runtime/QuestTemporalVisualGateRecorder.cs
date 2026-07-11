using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using QuestFlightLab.Environment;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public struct QuestTemporalFrameSample
    {
        public int sequence;
        public int unityFrameCount;
        public float elapsedSeconds;
        public float frameMs;
        public bool frameTimingAvailable;
        public float cpuFrameMs;
        public float gpuFrameMs;
        public long gcAllocatedBytes;
        public long runtimeDrawCalls;
        public long runtimeTriangles;
    }

    [Serializable]
    public sealed class QuestTemporalVisualGateReport
    {
        public int schemaVersion = 1;
        public string generatedUtc;
        public string unityVersion;
        public string platform;
        public string deviceModel;
        public string graphicsDeviceType;
        public string graphicsDeviceName;
        public string sceneryMode;
        public string runtimeEnvironmentRoot;
        public bool realDataEnvironmentActive;
        public bool proceduralFallbackActive;
        public bool readinessPrerequisitesMet;
        public string readinessStatus;
        public float readinessWaitSeconds;
        public float warmupSeconds;
        public float requestedMeasurementSeconds;
        public float measuredSeconds;
        public int sampleCount;
        public float averageFrameMs;
        public float minFrameMs;
        public float maxFrameMs;
        public float p95FrameMs;
        public float p99FrameMs;
        public float estimatedFps;
        public int framesOver72HzBudget;
        public float framesOver72HzBudgetRatio;
        public string deliveredIntervalSemantics;
        public float averageDeliveredIntervalMs;
        public float p95DeliveredIntervalMs;
        public string workloadTimingSemantics;
        public int workloadTimingSamples;
        public float averageWorkloadMs;
        public float p95WorkloadMs;
        public int workloadSamplesOver72HzBudget;
        public float workloadSamplesOver72HzBudgetRatio;
        public bool frameTimingManagerAvailable;
        public int frameTimingManagerSamples;
        public int gpuTimingSamples;
        public float averageCpuFrameMs;
        public float p95CpuFrameMs;
        public float averageGpuFrameMs;
        public float p95GpuFrameMs;
        public bool allocationRecorderAvailable;
        public long averageGcAllocatedBytesPerFrame;
        public long maxGcAllocatedBytesPerFrame;
        public bool runtimeDrawCallRecorderAvailable;
        public long maximumRuntimeDrawCalls;
        public bool runtimeTriangleRecorderAvailable;
        public long maximumRuntimeTriangles;
        public RenderBudgetSnapshot renderBudget;
        public int activeRendererCount;
        public int activeLodGroupCount;
        public int crossFadeLodGroupCount;
        public int waterRendererCount;
        public int transparentWaterRendererCount;
        public int uniqueWaterMaterialCount;
        public long waterTriangles;
        public bool importedC172Loaded;
        public bool startupSeatAlignmentCompleted;
        public int startupSeatStableFrameCount;
        public int startupSeatRecenterCount;
        public float startupSeatPositionErrorMeters;
        public float startupSeatYawErrorDegrees;
        public float defaultPilotEyeAftMeters;
        public float eyeToPanelDistanceMeters;
        public bool cockpitLightingPolicyAvailable;
        public string cockpitLightingStrategy;
        public float cockpitStaticDepthStrength;
        public int cockpitRealtimeShadowCasters;
        public int cockpitRealtimeShadowReceivers;
        public bool hardPerformanceGatePassed;
        public string hardPerformanceGateSemantics;
        public bool deliveredCadencePerformanceGatePassed;
        public bool workloadPerformanceGatePassed;
        public bool observerManagedAllocationsExpected;
        public string evidencePath;
        public List<QuestTemporalFrameSample> frameSamples = new List<QuestTemporalFrameSample>();
        public List<string> limitations = new List<string>();
    }

    /// <summary>
    /// Captures the exact 90-second warmup and 60-second Quest measurement window used by the
    /// temporal visual gate. This recorder observes the scene and writes evidence only; it never
    /// changes the camera, seat, aircraft, environment, quality settings, or frame-rate policy.
    /// Host-side VrApi parsing supplements this report because Unity/Android commonly returns
    /// zero GPU milliseconds through FrameTimingManager.
    /// </summary>
    public sealed class QuestTemporalVisualGateRecorder : MonoBehaviour
    {
        public const string EvidenceDirectoryName = "temporal_visual_gate";
        public const float DefaultReadinessTimeoutSeconds = 90f;
        public const float DefaultWarmupSeconds = 90f;
        public const float DefaultMeasurementSeconds = 60f;
        public const float Quest72HzFrameBudgetMs = 1000f / 72f;
        public const float QuestAverageFrameGateMs = 13.2f;
        public const float QuestOverBudgetGateRatio = 0.05f;
        public const int QuestDrawCallGate = 150;
        public const long QuestVisibleTriangleGate = 500000L;
        public const string DeliveredIntervalSemantics = "Time.unscaledDeltaTime delivered-frame cadence; synchronized 72 Hz presentation naturally centers near 13.889 ms and is not app execution headroom.";
        public const string WorkloadTimingSemantics = "Per-frame max of Unity FrameTimingManager CPU and nonzero GPU durations; Quest CPU timing may include XR synchronization wait, so host app-PID VrApi App and CPU&GPU metrics remain separate.";

        private const string LogPrefix = "[QuestFlightLab][TemporalVisualGate]";

        public float readinessTimeoutSeconds = DefaultReadinessTimeoutSeconds;
        public float warmupSeconds = DefaultWarmupSeconds;
        public float measurementSeconds = DefaultMeasurementSeconds;
        public QuestTemporalVisualGateReport LastReport { get; private set; }

        private ProfilerRecorder _allocationRecorder;
        private ProfilerRecorder _drawCallRecorder;
        private ProfilerRecorder _triangleRecorder;
        private bool _allocationRecorderAvailable;
        private bool _drawCallRecorderAvailable;
        private bool _triangleRecorderAvailable;
        private readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor || !QuestLaunchOptions.VisualFidelityDemoRequested()) return;
            if (FindFirstObjectByType<QuestTemporalVisualGateRecorder>() != null) return;

            GameObject host = new GameObject("Quest Temporal Visual Gate Recorder");
            DontDestroyOnLoad(host);
            host.AddComponent<QuestTemporalVisualGateRecorder>();
        }

        private IEnumerator Start()
        {
            float readinessStarted = Time.realtimeSinceStartup;
            float deadline = readinessStarted + Mathf.Max(1f, readinessTimeoutSeconds);
            bool ready = SceneReady(out string readinessStatus);
            while (!ready && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                ready = SceneReady(out readinessStatus);
            }

            float readinessWait = Time.realtimeSinceStartup - readinessStarted;
            Debug.Log($"{LogPrefix} Readiness {(ready ? "PASS" : "TIMEOUT")} after {readinessWait:0.00}s: {readinessStatus}");

            float warmupTarget = Mathf.Max(0f, warmupSeconds);
            float warmed = 0f;
            Debug.Log($"{LogPrefix} Warmup started duration={warmupTarget:0.00}s");
            while (warmed < warmupTarget)
            {
                yield return null;
                warmed += Time.unscaledDeltaTime;
            }

            Debug.Log($"{LogPrefix} Measurement started duration={Mathf.Max(1f, measurementSeconds):0.00}s");
            StartRecorders();
            QuestTemporalVisualGateReport report = CreateReport(ready, readinessStatus, readinessWait, warmed);
            LastReport = report;

            float measured = 0f;
            FrameTiming[] latestTiming = new FrameTiming[1];
            while (measured < Mathf.Max(1f, measurementSeconds))
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return _waitForEndOfFrame;

                float frameMs = Mathf.Max(0f, Time.unscaledDeltaTime * 1000f);
                measured += Time.unscaledDeltaTime;
                uint timingCount = FrameTimingManager.GetLatestTimings(1, latestTiming);
                bool timingAvailable = timingCount > 0 &&
                                       (latestTiming[0].cpuFrameTime > 0.01d || latestTiming[0].gpuFrameTime > 0.01d);

                QuestTemporalFrameSample sample = new QuestTemporalFrameSample
                {
                    sequence = report.frameSamples.Count + 1,
                    unityFrameCount = Time.frameCount,
                    elapsedSeconds = measured,
                    frameMs = frameMs,
                    frameTimingAvailable = timingAvailable,
                    cpuFrameMs = timingAvailable ? (float)latestTiming[0].cpuFrameTime : 0f,
                    gpuFrameMs = timingAvailable ? (float)latestTiming[0].gpuFrameTime : 0f,
                    gcAllocatedBytes = ReadRecorder(_allocationRecorder, _allocationRecorderAvailable),
                    runtimeDrawCalls = ReadRecorder(_drawCallRecorder, _drawCallRecorderAvailable),
                    runtimeTriangles = ReadRecorder(_triangleRecorder, _triangleRecorderAvailable)
                };
                report.frameSamples.Add(sample);
            }

            StopRecorders();
            report.measuredSeconds = measured;
            PopulateSummary(report);
            WriteEvidence(report);
        }

        private void OnDestroy()
        {
            StopRecorders();
        }

        private static bool SceneReady(out string status)
        {
            GameObject realRoot = GameObject.Find(RealKbduEnvironmentBuilder.RootName);
            if (realRoot == null || !realRoot.activeInHierarchy)
            {
                status = "waiting for active real-data environment root";
                return false;
            }

            QuestFirstViewRuntimeRepair repair = QuestFirstViewRuntimeRepair.Instance != null
                ? QuestFirstViewRuntimeRepair.Instance
                : FindFirstObjectByType<QuestFirstViewRuntimeRepair>();
            if (repair == null)
            {
                status = "waiting for first-view runtime repair";
                return false;
            }
            if (!repair.ImportedC172Loaded)
            {
                status = "waiting for imported C172 cockpit";
                return false;
            }
            if (!repair.StartupSeatAlignmentCompleted)
            {
                status = "waiting for startup seat alignment: " + repair.StartupSeatAlignmentStatus;
                return false;
            }
            if (repair.ImportedC172Lighting == null)
            {
                status = "waiting for cockpit lighting policy";
                return false;
            }

            FirstViewPlaytestDiagnostics diagnostics = FirstViewPlaytestDiagnostics.Instance != null
                ? FirstViewPlaytestDiagnostics.Instance
                : FindFirstObjectByType<FirstViewPlaytestDiagnostics>();
            if (diagnostics == null || !diagnostics.InitialDiagnosticScreenshotsComplete)
            {
                status = "waiting for first-view diagnostic captures";
                return false;
            }

            status = "ready: real-data environment, cockpit, seat alignment, lighting, and first-view evidence settled";
            return true;
        }

        private QuestTemporalVisualGateReport CreateReport(
            bool ready,
            string readinessStatus,
            float readinessWait,
            float actualWarmup)
        {
            GameObject realRoot = GameObject.Find(RealKbduEnvironmentBuilder.RootName);
            GameObject fallbackRoot = GameObject.Find(RealKbduEnvironmentBuilder.ProceduralFallbackRootName);
            QuestFirstViewRuntimeRepair repair = QuestFirstViewRuntimeRepair.Instance != null
                ? QuestFirstViewRuntimeRepair.Instance
                : FindFirstObjectByType<QuestFirstViewRuntimeRepair>();
            CockpitLightingReport lighting = repair != null ? repair.ImportedC172Lighting : null;

            QuestTemporalVisualGateReport report = new QuestTemporalVisualGateReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                sceneryMode = QuestLaunchOptions.SceneryMode(),
                runtimeEnvironmentRoot = realRoot != null ? HierarchyPath(realRoot.transform) : string.Empty,
                realDataEnvironmentActive = realRoot != null && realRoot.activeInHierarchy,
                proceduralFallbackActive = fallbackRoot != null && fallbackRoot.activeInHierarchy,
                readinessPrerequisitesMet = ready,
                readinessStatus = readinessStatus,
                readinessWaitSeconds = readinessWait,
                warmupSeconds = actualWarmup,
                requestedMeasurementSeconds = Mathf.Max(1f, measurementSeconds),
                importedC172Loaded = repair != null && repair.ImportedC172Loaded,
                startupSeatAlignmentCompleted = repair != null && repair.StartupSeatAlignmentCompleted,
                startupSeatStableFrameCount = repair != null ? repair.StartupSeatStableFrameCount : 0,
                startupSeatRecenterCount = repair != null ? repair.StartupSeatRecenterCount : 0,
                startupSeatPositionErrorMeters = repair != null ? repair.StartupSeatPositionErrorMeters : -1f,
                startupSeatYawErrorDegrees = repair != null ? repair.StartupSeatYawErrorDegrees : -1f,
                defaultPilotEyeAftMeters = repair != null ? repair.DefaultPilotEyeAftMetersUsed : 0f,
                eyeToPanelDistanceMeters = repair != null ? repair.DefaultEyeToPanelDistanceMeters : -1f,
                cockpitLightingPolicyAvailable = lighting != null,
                cockpitLightingStrategy = lighting != null ? lighting.strategy : string.Empty,
                cockpitStaticDepthStrength = lighting != null ? lighting.staticDepthStrength : 0f,
                cockpitRealtimeShadowCasters = lighting != null ? lighting.remainingRealtimeShadowCasterCount : -1,
                cockpitRealtimeShadowReceivers = lighting != null ? lighting.remainingRealtimeShadowReceiverCount : -1,
                allocationRecorderAvailable = _allocationRecorderAvailable,
                runtimeDrawCallRecorderAvailable = _drawCallRecorderAvailable,
                runtimeTriangleRecorderAvailable = _triangleRecorderAvailable
            };
            report.frameSamples = new List<QuestTemporalFrameSample>(
                Mathf.CeilToInt(Mathf.Max(1f, measurementSeconds) * 90f) + 16);
            report.observerManagedAllocationsExpected = false;
            report.limitations.Add("Unity FrameTimingManager may return zero GPU milliseconds on Quest; use the host summary's app-PID-filtered VrApi GPU utilization and combined timing for bottleneck evidence.");
            report.limitations.Add("Scene-audit draw calls and visible triangles are conservative estimates when Unity runtime profiler counters are unavailable.");
            report.limitations.Add("Screen recording is started by the host script and can add overhead; the host summary reports whether it ran during this measurement.");
            return report;
        }

        private static void PopulateSummary(QuestTemporalVisualGateReport report)
        {
            report.sampleCount = report.frameSamples.Count;
            float[] frameValues = report.frameSamples.Select(sample => sample.frameMs).ToArray();
            float[] cpuValues = report.frameSamples
                .Where(sample => sample.frameTimingAvailable && sample.cpuFrameMs > 0.01f)
                .Select(sample => sample.cpuFrameMs)
                .ToArray();
            float[] gpuValues = report.frameSamples
                .Where(sample => sample.frameTimingAvailable && sample.gpuFrameMs > 0.01f)
                .Select(sample => sample.gpuFrameMs)
                .ToArray();
            float[] workloadValues = report.frameSamples
                .Where(sample => sample.frameTimingAvailable && (sample.cpuFrameMs > 0.01f || sample.gpuFrameMs > 0.01f))
                .Select(sample => Mathf.Max(sample.cpuFrameMs, sample.gpuFrameMs))
                .ToArray();

            report.averageFrameMs = Average(frameValues);
            report.minFrameMs = frameValues.Length > 0 ? frameValues.Min() : 0f;
            report.maxFrameMs = frameValues.Length > 0 ? frameValues.Max() : 0f;
            report.p95FrameMs = CalculatePercentile(frameValues, 0.95f);
            report.p99FrameMs = CalculatePercentile(frameValues, 0.99f);
            report.estimatedFps = report.averageFrameMs > 0.001f ? 1000f / report.averageFrameMs : 0f;
            report.framesOver72HzBudget = frameValues.Count(value => value > Quest72HzFrameBudgetMs);
            report.framesOver72HzBudgetRatio = frameValues.Length > 0
                ? report.framesOver72HzBudget / (float)frameValues.Length
                : 0f;
            report.deliveredIntervalSemantics = DeliveredIntervalSemantics;
            report.averageDeliveredIntervalMs = report.averageFrameMs;
            report.p95DeliveredIntervalMs = report.p95FrameMs;
            report.workloadTimingSemantics = WorkloadTimingSemantics;
            report.workloadTimingSamples = workloadValues.Length;
            report.averageWorkloadMs = Average(workloadValues);
            report.p95WorkloadMs = CalculatePercentile(workloadValues, 0.95f);
            report.workloadSamplesOver72HzBudget = workloadValues.Count(value => value > Quest72HzFrameBudgetMs);
            report.workloadSamplesOver72HzBudgetRatio = workloadValues.Length > 0
                ? report.workloadSamplesOver72HzBudget / (float)workloadValues.Length
                : 0f;
            report.frameTimingManagerSamples = cpuValues.Length;
            report.gpuTimingSamples = gpuValues.Length;
            report.frameTimingManagerAvailable = cpuValues.Length > 0 || gpuValues.Length > 0;
            report.averageCpuFrameMs = Average(cpuValues);
            report.p95CpuFrameMs = CalculatePercentile(cpuValues, 0.95f);
            report.averageGpuFrameMs = Average(gpuValues);
            report.p95GpuFrameMs = CalculatePercentile(gpuValues, 0.95f);
            report.averageGcAllocatedBytesPerFrame = report.frameSamples.Count > 0
                ? (long)report.frameSamples.Average(sample => (double)Math.Max(0L, sample.gcAllocatedBytes))
                : 0L;
            report.maxGcAllocatedBytesPerFrame = report.frameSamples.Count > 0
                ? report.frameSamples.Max(sample => Math.Max(0L, sample.gcAllocatedBytes))
                : 0L;
            report.maximumRuntimeDrawCalls = report.frameSamples.Count > 0
                ? report.frameSamples.Max(sample => Math.Max(0L, sample.runtimeDrawCalls))
                : 0L;
            report.maximumRuntimeTriangles = report.frameSamples.Count > 0
                ? report.frameSamples.Max(sample => Math.Max(0L, sample.runtimeTriangles))
                : 0L;

            Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            report.renderBudget = QuestRenderBudgetAudit.Capture(camera);
            report.activeRendererCount = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(renderer => renderer.enabled);
            LODGroup[] lodGroups = FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            report.activeLodGroupCount = lodGroups.Count(group => group.enabled);
            report.crossFadeLodGroupCount = lodGroups.Count(group => group.enabled && group.fadeMode != LODFadeMode.None);
            PopulateWaterSummary(report);

            int drawCalls = report.runtimeDrawCallRecorderAvailable && report.maximumRuntimeDrawCalls > 0
                ? (int)Math.Min(int.MaxValue, report.maximumRuntimeDrawCalls)
                : report.renderBudget.estimatedInstancedDrawCalls;
            long triangles = report.runtimeTriangleRecorderAvailable && report.maximumRuntimeTriangles > 0
                ? report.maximumRuntimeTriangles
                : report.renderBudget.estimatedFrustumTriangles;
            bool commonBudgetGate = report.readinessPrerequisitesMet &&
                                    report.measuredSeconds >= report.requestedMeasurementSeconds &&
                                    drawCalls <= QuestDrawCallGate &&
                                    triangles <= QuestVisibleTriangleGate;
            report.deliveredCadencePerformanceGatePassed = commonBudgetGate && EvaluateTimingGate(
                report.averageFrameMs,
                report.p95FrameMs,
                report.framesOver72HzBudgetRatio);
            report.workloadPerformanceGatePassed = commonBudgetGate && report.workloadTimingSamples > 0 && EvaluateTimingGate(
                report.averageWorkloadMs,
                report.p95WorkloadMs,
                report.workloadSamplesOver72HzBudgetRatio);
            report.hardPerformanceGatePassed = report.deliveredCadencePerformanceGatePassed;
            report.hardPerformanceGateSemantics = "Legacy compatibility field: identical to deliveredCadencePerformanceGatePassed. Workload timing has its own gate and is never substituted silently.";
        }

        private static void PopulateWaterSummary(QuestTemporalVisualGateReport report)
        {
            Renderer[] waterRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && renderer.sharedMaterials.Any(QuestEnvironmentMaterialFactory.IsStableWaterMaterial))
                .ToArray();
            HashSet<int> materials = new HashSet<int>();
            long triangles = 0L;
            int transparent = 0;
            foreach (Renderer renderer in waterRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3L;
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    materials.Add(material.GetInstanceID());
                    if (material.renderQueue >= (int)RenderQueue.Transparent ||
                        (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f))
                        transparent++;
                }
            }

            report.waterRendererCount = waterRenderers.Length;
            report.transparentWaterRendererCount = transparent;
            report.uniqueWaterMaterialCount = materials.Count;
            report.waterTriangles = triangles;
        }

        public static float CalculatePercentile(IReadOnlyList<float> values, float percentile)
        {
            if (values == null || values.Count == 0) return 0f;
            float[] sorted = values.ToArray();
            Array.Sort(sorted);
            float position = Mathf.Clamp01(percentile) * (sorted.Length - 1);
            int lower = Mathf.FloorToInt(position);
            int upper = Mathf.CeilToInt(position);
            if (lower == upper) return sorted[lower];
            float blend = position - lower;
            return Mathf.Lerp(sorted[lower], sorted[upper], blend);
        }

        public static bool EvaluateTimingGate(float averageMs, float p95Ms, float overBudgetRatio)
        {
            return averageMs <= QuestAverageFrameGateMs &&
                   p95Ms <= Quest72HzFrameBudgetMs &&
                   overBudgetRatio <= QuestOverBudgetGateRatio;
        }

        private static float Average(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            double total = 0d;
            for (int index = 0; index < values.Count; index++) total += values[index];
            return (float)(total / values.Count);
        }

        private void StartRecorders()
        {
            _allocationRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", out _allocationRecorderAvailable);
            _drawCallRecorder = StartRecorder(ProfilerCategory.Render, "Draw Calls Count", out _drawCallRecorderAvailable);
            _triangleRecorder = StartRecorder(ProfilerCategory.Render, "Triangles Count", out _triangleRecorderAvailable);
        }

        private static ProfilerRecorder StartRecorder(ProfilerCategory category, string statName, out bool available)
        {
            try
            {
                ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, statName, 1);
                available = recorder.Valid;
                return recorder;
            }
            catch (Exception)
            {
                available = false;
                return default;
            }
        }

        private void StopRecorders()
        {
            DisposeRecorder(ref _allocationRecorder);
            DisposeRecorder(ref _drawCallRecorder);
            DisposeRecorder(ref _triangleRecorder);
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid) recorder.Dispose();
            recorder = default;
        }

        private static long ReadRecorder(ProfilerRecorder recorder, bool available)
        {
            return available && recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0L;
        }

        private static string HierarchyPath(Transform candidate)
        {
            if (candidate == null) return string.Empty;
            Stack<string> names = new Stack<string>();
            Transform current = candidate;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names.ToArray());
        }

        private static void WriteEvidence(QuestTemporalVisualGateReport report)
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "QuestFlightLab", EvidenceDirectoryName);
                Directory.CreateDirectory(directory);
                string stem = "quest_temporal_visual_gate_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string jsonPath = Path.Combine(directory, stem + ".json");
                report.generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                report.evidencePath = jsonPath;
                File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(directory, stem + ".md"), BuildMarkdown(report));
                Debug.Log(
                    $"{LogPrefix} Evidence written: {jsonPath} samples={report.sampleCount} " +
                    $"avg={report.averageFrameMs:0.000}ms p95={report.p95FrameMs:0.000}ms " +
                    $"over72Hz={report.framesOver72HzBudgetRatio:P1} hardPerformanceGate={report.hardPerformanceGatePassed}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{LogPrefix} Evidence write failed: {exception.Message}");
            }
        }

        private static string BuildMarkdown(QuestTemporalVisualGateReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Quest Temporal Visual Gate Runtime Evidence");
            builder.AppendLine();
            builder.AppendLine($"- Generated UTC: {report.generatedUtc}");
            builder.AppendLine($"- Readiness: {(report.readinessPrerequisitesMet ? "PASS" : "FAIL")}; {report.readinessStatus}");
            builder.AppendLine($"- Warmup/measurement: {report.warmupSeconds:0.00}s / {report.measuredSeconds:0.00}s");
            builder.AppendLine($"- Frames: {report.sampleCount}; average {report.averageFrameMs:0.000}ms; p95 {report.p95FrameMs:0.000}ms; p99 {report.p99FrameMs:0.000}ms");
            builder.AppendLine($"- Delivered interval semantics: {report.deliveredIntervalSemantics}");
            builder.AppendLine($"- Workload timing: average {report.averageWorkloadMs:0.000}ms; p95 {report.p95WorkloadMs:0.000}ms; samples={report.workloadTimingSamples}");
            builder.AppendLine($"- Delivered cadence/workload timing gates: {report.deliveredCadencePerformanceGatePassed}/{report.workloadPerformanceGatePassed}");
            builder.AppendLine($"- Over 13.889ms: {report.framesOver72HzBudget}/{report.sampleCount} ({report.framesOver72HzBudgetRatio:P2})");
            builder.AppendLine($"- Unity CPU/GPU p95: {report.p95CpuFrameMs:0.000}ms / {report.p95GpuFrameMs:0.000}ms ({report.gpuTimingSamples} nonzero GPU samples)");
            builder.AppendLine($"- Runtime/audit draw calls: {report.maximumRuntimeDrawCalls} / {report.renderBudget?.estimatedInstancedDrawCalls ?? 0}");
            builder.AppendLine($"- Runtime/audit visible triangles: {report.maximumRuntimeTriangles} / {report.renderBudget?.estimatedFrustumTriangles ?? 0}");
            builder.AppendLine($"- Active renderers/LOD groups/crossfade LOD groups: {report.activeRendererCount}/{report.activeLodGroupCount}/{report.crossFadeLodGroupCount}");
            builder.AppendLine($"- Water renderers/transparent slots/materials/triangles: {report.waterRendererCount}/{report.transparentWaterRendererCount}/{report.uniqueWaterMaterialCount}/{report.waterTriangles}");
            builder.AppendLine($"- Seat alignment: {report.startupSeatAlignmentCompleted}; recenter={report.startupSeatRecenterCount}; error={report.startupSeatPositionErrorMeters:0.0000}m/{report.startupSeatYawErrorDegrees:0.00}deg");
            builder.AppendLine($"- Default eye aft/panel distance: {report.defaultPilotEyeAftMeters:0.000}m / {report.eyeToPanelDistanceMeters:0.000}m");
            builder.AppendLine($"- Cockpit depth/shadow map: strength={report.cockpitStaticDepthStrength:0.00}; realtime casters/receivers={report.cockpitRealtimeShadowCasters}/{report.cockpitRealtimeShadowReceivers}");
            builder.AppendLine($"- Requested delivered-cadence performance gate (host crash/thermal checks excluded): {(report.deliveredCadencePerformanceGatePassed ? "PASS" : "FAIL")}");
            builder.AppendLine();
            builder.AppendLine("## Limitations");
            builder.AppendLine();
            foreach (string limitation in report.limitations) builder.AppendLine("- " + limitation);
            return builder.ToString();
        }
    }
}
