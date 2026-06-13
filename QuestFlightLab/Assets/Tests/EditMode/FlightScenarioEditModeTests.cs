#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
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
    }
}
#endif
