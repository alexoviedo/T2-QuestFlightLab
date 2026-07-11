#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Runtime;

namespace QuestFlightLab.Tests.PlayMode
{
    public sealed class QuestTemporalVisualGateRecorderPlayModeTests
    {
        [Test]
        public void PerformanceBudgetMetadata_MatchesVisualStabilityV2Gate()
        {
            Assert.That(QuestTemporalVisualGateRecorder.QuestAverageFrameGateMs, Is.EqualTo(13.2f));
            Assert.That(QuestTemporalVisualGateRecorder.Quest72HzFrameBudgetMs, Is.EqualTo(1000f / 72f).Within(0.0001f));
            Assert.That(QuestTemporalVisualGateRecorder.QuestOverBudgetGateRatio, Is.EqualTo(0.05f));
            Assert.That(QuestTemporalVisualGateRecorder.QuestDrawCallGate, Is.EqualTo(150));
            Assert.That(QuestTemporalVisualGateRecorder.QuestVisibleTriangleGate, Is.EqualTo(500000L));
        }

        [Test]
        public void TimingGate_IncludesAverageP95AndOverBudgetRatioBoundaries()
        {
            Assert.That(QuestTemporalVisualGateRecorder.EvaluateTimingGate(13.2f, 1000f / 72f, 0.05f), Is.True);
            Assert.That(QuestTemporalVisualGateRecorder.EvaluateTimingGate(13.201f, 13f, 0.01f), Is.False);
            Assert.That(QuestTemporalVisualGateRecorder.EvaluateTimingGate(13f, (1000f / 72f) + 0.001f, 0.01f), Is.False);
            Assert.That(QuestTemporalVisualGateRecorder.EvaluateTimingGate(13f, 13f, 0.051f), Is.False);
        }

        [Test]
        public void Percentile_UsesInterpolatedSortedSamples()
        {
            float value = QuestTemporalVisualGateRecorder.CalculatePercentile(
                new[] { 30f, 10f, 20f, 40f },
                0.95f);
            Assert.That(value, Is.EqualTo(38.5f).Within(0.0001f));
        }

        [Test]
        public void EvidenceSemantics_KeepDeliveredCadenceAndWorkloadDistinct()
        {
            Assert.That(QuestTemporalVisualGateRecorder.DeliveredIntervalSemantics, Does.Contain("cadence"));
            Assert.That(QuestTemporalVisualGateRecorder.WorkloadTimingSemantics, Does.Contain("FrameTimingManager"));
            Assert.That(QuestTemporalVisualGateRecorder.DeliveredIntervalSemantics,
                Is.Not.EqualTo(QuestTemporalVisualGateRecorder.WorkloadTimingSemantics));
            Assert.That(typeof(QuestTemporalFrameSample).IsValueType, Is.True,
                "Per-frame samples must remain structs so the observer does not allocate one object per frame.");
        }
    }
}
#endif
