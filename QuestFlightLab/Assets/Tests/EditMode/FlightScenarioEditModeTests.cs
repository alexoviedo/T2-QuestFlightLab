#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Environment;
using QuestFlightLab.TestHarness;
using QuestFlightLab.Training;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Tests.EditMode
{
    public class FlightScenarioEditModeTests
    {
        [Test]
        public void DefaultScenarioSuitePassesAcceptanceChecks()
        {
            FlightScenarioSuiteResult suite = FlightScenarioRunner.RunDefaultSuite();
            string failures = string.Join(", ", suite.scenarios.FindAll(s => !s.passed).ConvertAll(s => $"{s.id}: {s.passReason}"));
            Assert.AreEqual(0, suite.failedCount, $"All deterministic flight-core scenarios should pass. Failures: {failures}");
        }

        [Test]
        public void TrafficPatternLessonContainsRequiredPhases()
        {
            LessonSequence lesson = LessonSequence.BasicTrafficPatternFamiliarization();
            foreach (string phaseId in TrafficPatternLesson.RequiredPhaseIds)
            {
                Assert.That(lesson.steps.Exists(step => step.id == phaseId), Is.True, $"Missing phase {phaseId}");
            }
        }

        [Test]
        public void LessonScoringDistinguishesStableAndUnstableSamples()
        {
            TrafficPatternPhase phase = TrafficPatternLesson.FindPhase("final_alignment");
            FlightTelemetrySnapshot stable = new FlightTelemetrySnapshot
            {
                headingDeg = 80f,
                airspeedKts = 68f,
                altitudeFt = 400f,
                bankDeg = 5f,
                pitchDeg = 3f,
                flapDegrees = 30f
            };
            FlightTelemetrySnapshot unstable = new FlightTelemetrySnapshot
            {
                headingDeg = 240f,
                airspeedKts = 38f,
                altitudeFt = 1400f,
                bankDeg = 48f,
                pitchDeg = 24f,
                flapDegrees = 0f,
                stallWarning = true
            };

            float stableScore = LessonScoring.ScoreInstant(phase, stable, AircraftControlState.Neutral(), out _);
            float unstableScore = LessonScoring.ScoreInstant(phase, unstable, AircraftControlState.Neutral(), out _);
            Assert.That(stableScore, Is.GreaterThan(unstableScore + 25f));
        }

        [Test]
        public void AirportPatternReferencesAreVerifiable()
        {
            AirportPatternVerificationSnapshot snapshot = AirportPatternVerification.Capture();
            Assert.That(snapshot.allRequiredReferencesPresent, Is.True, snapshot.summary);
        }

        [Test]
        public void DebriefReportSerializes()
        {
            DebriefReport report = LessonScoring.ScoreTrafficPattern(
                "serialization_probe",
                new[]
                {
                    new PatternScoringSample
                    {
                        timestamp = 0f,
                        controls = AircraftControlState.Neutral(),
                        flight = new FlightTelemetrySnapshot { airspeedKts = 70f, altitudeFt = 500f, headingDeg = 78f, bankDeg = 5f, pitchDeg = 3f }
                    },
                    new PatternScoringSample
                    {
                        timestamp = 8f,
                        controls = AircraftControlState.Neutral(),
                        flight = new FlightTelemetrySnapshot { airspeedKts = 72f, altitudeFt = 520f, headingDeg = 82f, bankDeg = 6f, pitchDeg = 2f }
                    }
                },
                true);

            string json = JsonUtility.ToJson(report, true);
            Assert.That(json, Does.Contain("Traffic Pattern"));
            Assert.That(report.phaseScores.Count, Is.EqualTo(TrafficPatternLesson.RequiredPhaseIds.Length));
        }

        [Test]
        public void StabilizedApproachLessonContainsRequiredPhases()
        {
            LessonSequence lesson = LessonSequence.StabilizedApproachGoAroundFamiliarization();
            foreach (string phaseId in StabilizedApproachLesson.RequiredPhaseIds)
            {
                Assert.That(lesson.steps.Exists(step => step.id == phaseId), Is.True, $"Missing phase {phaseId}");
            }
        }

        [Test]
        public void ApproachScoringDistinguishesStableAndUnstableSamples()
        {
            ApproachPhase phase = StabilizedApproachLesson.FindPhase("stabilized_approach_gate");
            FlightTelemetrySnapshot stable = new FlightTelemetrySnapshot
            {
                headingDeg = 78f,
                airspeedKts = 66f,
                altitudeFt = 310f,
                verticalSpeedFpm = -620f,
                bankDeg = 4f,
                pitchDeg = 2f,
                flapDegrees = 30f,
                runwayLateralOffsetMeters = 3f
            };
            FlightTelemetrySnapshot unstable = new FlightTelemetrySnapshot
            {
                headingDeg = 115f,
                airspeedKts = 49f,
                altitudeFt = 260f,
                verticalSpeedFpm = -1350f,
                bankDeg = 31f,
                pitchDeg = 12f,
                flapDegrees = 0f,
                runwayLateralOffsetMeters = 38f,
                stallWarning = true
            };

            float stableScore = ApproachScoring.ScoreInstant(phase, stable, AircraftControlState.Neutral(0.4f), out _);
            float unstableScore = ApproachScoring.ScoreInstant(phase, unstable, AircraftControlState.Neutral(0.4f), out _);
            ApproachEvaluationSnapshot unstableEval = ApproachScoring.EvaluateTelemetrySample(phase.id, 0f, unstable, AircraftControlState.Neutral(0.4f));

            Assert.That(stableScore, Is.GreaterThan(unstableScore + 30f));
            Assert.That(unstableEval.goAroundRequired, Is.True);
        }

        [Test]
        public void GoAroundDecisionRecognizesInitiatedRecovery()
        {
            FlightTelemetrySnapshot unstable = new FlightTelemetrySnapshot
            {
                headingDeg = 78f,
                airspeedKts = 61f,
                altitudeFt = 240f,
                verticalSpeedFpm = 180f,
                bankDeg = 4f,
                flapDegrees = 20f,
                runwayLateralOffsetMeters = 2f
            };
            AircraftControlState controls = AircraftControlState.Neutral(1f);
            controls.elevator = 0.15f;

            ApproachEvaluationSnapshot eval = ApproachScoring.EvaluateTelemetrySample("go_around_power_pitch_config", 0f, unstable, controls);
            Assert.That(eval.goAroundInitiated, Is.True);
        }

        [Test]
        public void ApproachDebriefAndTimelineSerializeRequiredFields()
        {
            FlightScenarioResult result = FlightScenarioRunner.RunScenario(new FlightScenarioDefinition
            {
                id = "timeline_export_replay_markers",
                name = "Timeline serialization probe",
                durationSeconds = 8f,
                initialAirspeedKts = 62f,
                initialAltitudeFt = 240f,
                startOnRunway = false,
                markChecklistComplete = true,
                isApproachScenario = true,
                requiresApproachDebrief = true,
                requiresTimeline = true
            });

            string approachJson = JsonUtility.ToJson(result.approachDebrief, true);
            string timelineJson = JsonUtility.ToJson(result.timeline, true);
            Assert.That(approachJson, Does.Contain("Stabilized Approach"));
            Assert.That(timelineJson, Does.Contain("samples"));
            Assert.That(result.timeline.sampleCount, Is.GreaterThan(0));
        }

        [Test]
        public void MeshSceneryProviderKeepsFallbackActive()
        {
            GameObject existingAirport = GameObject.Find(MeshSceneryProvider.AirportRootName);
            GameObject go = new GameObject("MeshSceneryProviderTest");
            MeshSceneryProvider provider = go.AddComponent<MeshSceneryProvider>();

            SceneryProviderStatus status = provider.ActivateProvider(null);

            Assert.That(status.activeMode, Is.EqualTo(SceneryMode.MeshFallback.ToString()));
            Assert.That(status.fallbackUsed, Is.False);
            Assert.That(GameObject.Find(MeshSceneryProvider.AirportRootName), Is.Not.Null);

            Object.DestroyImmediate(go);
            if (existingAirport == null)
            {
                Object.DestroyImmediate(GameObject.Find(MeshSceneryProvider.AirportRootName));
            }
        }

        [Test]
        public void SplatSceneryProviderFailsSafeWithoutRendererDependency()
        {
            GameObject go = new GameObject("SplatSceneryProviderTest");
            SplatSceneryProvider provider = go.AddComponent<SplatSceneryProvider>();
            provider.enableExperimentalProxy = false;
            provider.syntheticSplatCount = 5000;
            provider.runtimeSampleKey = QuestSplatRuntimeConfig.SyntheticProfile;

            SceneryProviderStatus status = provider.ActivateProvider(go.transform);

            if (SplatSceneryProvider.IsGaussianSplatRendererAvailable())
            {
                Assert.That(status.rendererAvailable, Is.True);
            }
            else
            {
                Assert.That(status.fallbackUsed, Is.True);
                Assert.That(status.activeMode, Is.EqualTo(SceneryMode.MeshFallback.ToString()));
                Assert.That(status.warnings.Exists(w => w.Contains("No Unity Gaussian splat renderer package")), Is.True);
            }

            Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestSplatRuntimeConfigSelectsScenicProfiles()
        {
            QuestSplatRuntimeConfig config = ScriptableObject.CreateInstance<QuestSplatRuntimeConfig>();
            Object synthetic = ScriptableObject.CreateInstance<QuestSplatRuntimeConfig>();
            Object scenicLow = ScriptableObject.CreateInstance<QuestSplatRuntimeConfig>();
            Object scenicMedium = ScriptableObject.CreateInstance<QuestSplatRuntimeConfig>();
            Object scenicHigh = ScriptableObject.CreateInstance<QuestSplatRuntimeConfig>();

            try
            {
                config.sample5k = synthetic;
                config.scenicLowSample = scenicLow;
                config.scenicMediumSample = scenicMedium;
                config.scenicHighSample = scenicHigh;

                Assert.That(config.AssetForProfile("synthetic", 5000), Is.SameAs(synthetic));
                Assert.That(config.AssetForProfile("scenic", 25000), Is.SameAs(scenicLow));
                Assert.That(config.AssetForProfile("scenic", 50000), Is.SameAs(scenicMedium));
                Assert.That(config.AssetForProfile("scenic", 100000), Is.SameAs(scenicHigh));
                Assert.That(QuestSplatRuntimeConfig.NormalizeProfile("scenic"), Is.EqualTo(QuestSplatRuntimeConfig.ScenicProfile));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(synthetic);
                Object.DestroyImmediate(scenicLow);
                Object.DestroyImmediate(scenicMedium);
                Object.DestroyImmediate(scenicHigh);
            }
        }

        [Test]
        public void SplatBudgetEstimatesIncludeQuestSpikeBudgets()
        {
            var estimates = SceneryEvidenceLogger.BuildBudgetEstimates(string.Empty);
            Assert.That(estimates.Count, Is.EqualTo(5));
            Assert.That(estimates.Exists(e => e.splatCount == 50000), Is.True);
            Assert.That(estimates.Exists(e => e.splatCount == 400000), Is.True);
            Assert.That(estimates.Find(e => e.splatCount == 400000).estimatedGpuMegabytes, Is.GreaterThan(18f));
        }
    }
}
#endif
