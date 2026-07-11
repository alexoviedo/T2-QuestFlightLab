using System;
using System.IO;
using UnityEngine;

namespace QuestFlightLab.Flight.Backends
{
    /// <summary>
    /// Lightweight on-device evidence sink for an explicitly selected backend.
    /// Step recording is allocation-free; JSON/p95 work runs outside Advance.
    /// </summary>
    public sealed class FlightBackendRuntimeEvidenceReporter : MonoBehaviour
    {
        private const int TimingCapacity = 16384;
        private readonly double[] _stepMilliseconds = new double[TimingCapacity];
        private long _stepCount;
        private double _stepMillisecondsSum;
        private double _maxStepMilliseconds;
        private long _allocatedBytes;
        private int _timingWriteIndex;
        private int _timingCount;
        private float _nextWriteTime;
        private string _requestedBackend = string.Empty;
        private string _activeBackend = string.Empty;
        private string _fallbackReason = string.Empty;
        private string _lastError = string.Empty;
        private bool _allocationCounterAvailable = true;

        public string OutputDirectory => Path.Combine(Application.persistentDataPath, "QuestFlightLab", "flight_backend");
        public string OutputPath => Path.Combine(OutputDirectory, "flight_backend_runtime_report.json");

        public void Configure(FlightDynamicsBackendKind requested, FlightDynamicsBackendKind active, string fallbackReason, string lastError)
        {
            _requestedBackend = requested.ToString();
            _activeBackend = active.ToString();
            _fallbackReason = fallbackReason ?? string.Empty;
            _lastError = lastError ?? string.Empty;
            WriteReport();
        }

        public void RecordStep(double milliseconds, long allocatedBytes, bool allocationCounterAvailable)
        {
            _stepCount++;
            _stepMillisecondsSum += milliseconds;
            _maxStepMilliseconds = Math.Max(_maxStepMilliseconds, milliseconds);
            _stepMilliseconds[_timingWriteIndex] = milliseconds;
            _timingWriteIndex = (_timingWriteIndex + 1) % TimingCapacity;
            _timingCount = Math.Min(TimingCapacity, _timingCount + 1);
            if (allocationCounterAvailable) _allocatedBytes += Math.Max(0L, allocatedBytes);
            else _allocationCounterAvailable = false;
        }

        public void RecordError(string error)
        {
            _lastError = error ?? string.Empty;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextWriteTime) return;
            _nextWriteTime = Time.unscaledTime + 5f;
            WriteReport();
        }

        private void OnDisable()
        {
            WriteReport();
        }

        public void WriteReport()
        {
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                RuntimeEvidence report = new RuntimeEvidence
                {
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    requestedBackend = _requestedBackend,
                    activeBackend = _activeBackend,
                    fallbackReason = _fallbackReason,
                    lastError = _lastError,
                    jsbsimVersion = JSBSimNativeFlightBackend.PinnedJsbsimVersion,
                    jsbsimRevision = JSBSimNativeFlightBackend.PinnedJsbsimRevision,
                    targetSimulationHz = 120.0,
                    stepCount = _stepCount,
                    averageStepCpuMilliseconds = _stepCount > 0 ? _stepMillisecondsSum / _stepCount : 0.0,
                    p95StepCpuMilliseconds = P95(),
                    maxStepCpuMilliseconds = _maxStepMilliseconds,
                    steadyStateAllocatedBytes = _allocationCounterAvailable ? _allocatedBytes : -1,
                    steadyStateAllocationSignal = !_allocationCounterAvailable
                        ? "counter_unavailable"
                        : _allocatedBytes == 0 ? "zero_detected" : "allocations_detected",
                    platform = Application.platform.ToString(),
                    unityVersion = Application.unityVersion,
                    deviceModel = SystemInfo.deviceModel
                };
                File.WriteAllText(OutputPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[QuestFlightLab][FlightBackend] Could not write runtime evidence: {exception.Message}");
            }
        }

        private double P95()
        {
            if (_timingCount == 0) return 0.0;
            double[] sorted = new double[_timingCount];
            for (int i = 0; i < _timingCount; i++) sorted[i] = _stepMilliseconds[i];
            Array.Sort(sorted);
            return sorted[(int)Math.Floor((sorted.Length - 1) * 0.95)];
        }

        [Serializable]
        private sealed class RuntimeEvidence
        {
            public string generatedUtc;
            public string requestedBackend;
            public string activeBackend;
            public string fallbackReason;
            public string lastError;
            public string jsbsimVersion;
            public string jsbsimRevision;
            public double targetSimulationHz;
            public long stepCount;
            public double averageStepCpuMilliseconds;
            public double p95StepCpuMilliseconds;
            public double maxStepCpuMilliseconds;
            public long steadyStateAllocatedBytes;
            public string steadyStateAllocationSignal;
            public string platform;
            public string unityVersion;
            public string deviceModel;
        }
    }
}
