using System;
using System.Collections.Generic;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class PatternScoringSample
    {
        public float timestamp;
        public AircraftControlState controls;
        public FlightTelemetrySnapshot flight;
    }

    [Serializable]
    public class DebriefPhaseScore
    {
        public string phaseId = "";
        public string phaseName = "";
        public float score;
        public bool passed;
        public string targetSummary = "";
        public string observedSummary = "";
        public float maxHeadingErrorDeg;
        public float maxAirspeedErrorKts;
        public float maxAltitudeErrorFt;
        public float maxBankDeg;
        public int stallWarningSamples;
        public bool gateHit;
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public class DebriefReport
    {
        public string lessonId = "";
        public string lessonTitle = "";
        public string scenarioId = "";
        public string generatedUtc = "";
        public float totalScore;
        public bool passed;
        public int completedPhases;
        public int totalPhases;
        public int checklistMisses;
        public int majorDeviationCount;
        public int stallWarningCount;
        public int gateHitCount;
        public int gateCount;
        public float maxBankDeg;
        public float maxRunwayOffsetMeters;
        public float rotationSpeedErrorKts;
        public float finalAlignmentErrorDeg;
        public string summary = "";
        public List<string> warnings = new List<string>();
        public List<DebriefPhaseScore> phaseScores = new List<DebriefPhaseScore>();
    }

    public static class LessonScoring
    {
        public static DebriefReport ScoreTrafficPattern(
            string scenarioId,
            IList<PatternScoringSample> samples,
            bool checklistComplete)
        {
            DebriefReport report = new DebriefReport
            {
                lessonId = TrafficPatternLesson.LessonId,
                lessonTitle = TrafficPatternLesson.LessonTitle,
                scenarioId = scenarioId,
                generatedUtc = DateTime.UtcNow.ToString("o"),
                totalPhases = TrafficPatternLesson.RequiredPhaseIds.Length,
                gateCount = TrafficPatternLesson.RequiredPhaseIds.Length
            };

            if (samples == null || samples.Count == 0)
            {
                report.summary = "No samples were available for scoring.";
                report.warnings.Add(report.summary);
                return report;
            }

            float duration = Mathf.Max(0.01f, samples[^1].timestamp);
            float weighted = 0f;
            float weightSum = 0f;

            foreach (TrafficPatternPhase phase in TrafficPatternLesson.BasicPhases())
            {
                DebriefPhaseScore phaseScore = ScorePhaseWindow(phase, samples, duration, checklistComplete);
                report.phaseScores.Add(phaseScore);
                weighted += phaseScore.score * Mathf.Max(0.01f, phase.scoringWeight);
                weightSum += Mathf.Max(0.01f, phase.scoringWeight);
                if (phaseScore.passed) report.completedPhases++;
                if (phaseScore.gateHit) report.gateHitCount++;
                report.stallWarningCount += phaseScore.stallWarningSamples;
                report.maxBankDeg = Mathf.Max(report.maxBankDeg, phaseScore.maxBankDeg);
                if (phaseScore.warnings.Count > 0)
                {
                    report.majorDeviationCount += phaseScore.warnings.Count;
                    foreach (string warning in phaseScore.warnings)
                    {
                        report.warnings.Add($"{phase.name}: {warning}");
                    }
                }
            }

            if (!checklistComplete)
            {
                report.checklistMisses = 1;
                report.warnings.Add("Before-takeoff checklist placeholder was not complete.");
            }

            foreach (PatternScoringSample sample in samples)
            {
                if (sample?.flight == null) continue;
                report.maxRunwayOffsetMeters = Mathf.Max(report.maxRunwayOffsetMeters, Mathf.Abs(sample.flight.runwayLateralOffsetMeters));
                if (sample.flight.airspeedKts > 48f && report.rotationSpeedErrorKts == 0f)
                {
                    report.rotationSpeedErrorKts = Mathf.Abs(sample.flight.airspeedKts - 55f);
                }
            }

            PatternScoringSample finalSample = samples[^1];
            report.finalAlignmentErrorDeg = finalSample?.flight != null ? Mathf.Abs(Mathf.DeltaAngle(finalSample.flight.headingDeg, 78f)) : 999f;
            report.totalScore = weightSum > 0f ? Mathf.Clamp(weighted / weightSum, 0f, 100f) : 0f;
            report.passed = report.totalScore >= 70f && report.completedPhases >= 9 && report.stallWarningCount < 45;
            report.summary = $"{report.completedPhases}/{report.totalPhases} phases passed, score {report.totalScore:0}, gates {report.gateHitCount}/{report.gateCount}, warnings {report.warnings.Count}";
            return report;
        }

        public static float ScoreInstant(TrafficPatternPhase phase, FlightTelemetrySnapshot flight, AircraftControlState controls, out List<string> warnings)
        {
            warnings = new List<string>();
            if (phase == null || flight == null) return 0f;

            float score = 100f;
            if (phase.targetHeadingDeg >= 0f)
            {
                float error = Mathf.Abs(Mathf.DeltaAngle(flight.headingDeg, phase.targetHeadingDeg));
                score -= Mathf.Max(0f, error - phase.headingToleranceDeg) * 0.6f;
                if (error > phase.headingToleranceDeg) warnings.Add($"heading error {error:0} deg");
            }

            if (phase.minAirspeedKts > 0f && flight.airspeedKts < phase.minAirspeedKts)
            {
                float error = phase.minAirspeedKts - flight.airspeedKts;
                score -= error * 1.1f;
                if (error > 1f) warnings.Add($"airspeed low by {error:0} kt");
            }

            if (phase.maxAirspeedKts > 0f && flight.airspeedKts > phase.maxAirspeedKts)
            {
                float error = flight.airspeedKts - phase.maxAirspeedKts;
                score -= error * 0.9f;
                if (error > 1f) warnings.Add($"airspeed high by {error:0} kt");
            }

            if (phase.minAltitudeFt > 0f && flight.altitudeFt < phase.minAltitudeFt)
            {
                float error = phase.minAltitudeFt - flight.altitudeFt;
                score -= error * 0.025f;
                if (error > 80f) warnings.Add($"altitude low by {error:0} ft");
            }

            if (phase.maxAltitudeFt > 0f && flight.altitudeFt > phase.maxAltitudeFt)
            {
                float error = flight.altitudeFt - phase.maxAltitudeFt;
                score -= error * 0.02f;
                if (error > 100f) warnings.Add($"altitude high by {error:0} ft");
            }

            float bank = Mathf.Abs(flight.bankDeg);
            score -= Mathf.Max(0f, bank - phase.bankLimitDeg) * 1.8f;
            if (bank > phase.bankLimitDeg) warnings.Add($"bank exceeded {phase.bankLimitDeg:0} deg");

            if (flight.pitchDeg < phase.minPitchDeg || flight.pitchDeg > phase.maxPitchDeg)
            {
                score -= 5f;
                warnings.Add($"pitch outside {phase.minPitchDeg:0}/{phase.maxPitchDeg:0} deg");
            }

            if (phase.targetFlapDeg >= 0f)
            {
                float flapError = Mathf.Abs(flight.flapDegrees - phase.targetFlapDeg);
                score -= Mathf.Max(0f, flapError - phase.flapToleranceDeg) * 0.4f;
                if (flapError > phase.flapToleranceDeg) warnings.Add($"flaps differ by {flapError:0} deg");
            }

            if (flight.stallWarning)
            {
                score -= 12f;
                warnings.Add("stall warning observed");
            }

            return Mathf.Clamp(score, 0f, 100f);
        }

        public static string BuildMarkdown(DebriefReport report)
        {
            if (report == null) return "# Debrief Report\n\nNo debrief report was generated.\n";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("# Traffic Pattern Debrief Report");
            sb.AppendLine();
            sb.AppendLine($"Lesson: {report.lessonTitle}");
            sb.AppendLine($"Scenario: {report.scenarioId}");
            sb.AppendLine($"Generated UTC: {report.generatedUtc}");
            sb.AppendLine($"Score: {report.totalScore:0} / 100");
            sb.AppendLine($"Result: {(report.passed ? "PASS" : "NEEDS WORK")}");
            sb.AppendLine($"Summary: {report.summary}");
            sb.AppendLine();
            sb.AppendLine("| Phase | Score | Gate | Warnings | Observed |");
            sb.AppendLine("| --- | ---: | --- | --- | --- |");
            foreach (DebriefPhaseScore phase in report.phaseScores)
            {
                sb.Append("| ")
                    .Append(phase.phaseName)
                    .Append(" | ")
                    .Append($"{phase.score:0}")
                    .Append(" | ")
                    .Append(phase.gateHit ? "HIT" : "MISS")
                    .Append(" | ")
                    .Append(phase.warnings.Count)
                    .Append(" | ")
                    .Append(phase.observedSummary.Replace("|", "/"))
                    .AppendLine(" |");
            }

            if (report.warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings:");
                foreach (string warning in report.warnings)
                {
                    sb.AppendLine($"- {warning}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Limitations: This debrief is deterministic prototype evidence only. It is not real-world pilot instruction, a certified procedure, or validated C172 performance scoring.");
            return sb.ToString();
        }

        private static DebriefPhaseScore ScorePhaseWindow(
            TrafficPatternPhase phase,
            IList<PatternScoringSample> samples,
            float duration,
            bool checklistComplete)
        {
            float start = phase.startFraction * duration;
            float end = Mathf.Max(start + 0.01f, phase.endFraction * duration);
            DebriefPhaseScore result = new DebriefPhaseScore
            {
                phaseId = phase.id,
                phaseName = phase.name,
                targetSummary = phase.TargetSummary()
            };

            float scoreTotal = 0f;
            int count = 0;
            float minSpeed = float.MaxValue;
            float maxSpeed = float.MinValue;
            float minAlt = float.MaxValue;
            float maxAlt = float.MinValue;
            float lastHeading = 0f;

            foreach (PatternScoringSample sample in samples)
            {
                if (sample == null || sample.flight == null || sample.timestamp < start || sample.timestamp > end) continue;
                float instant = ScoreInstant(phase, sample.flight, sample.controls, out List<string> warnings);
                scoreTotal += instant;
                count++;
                minSpeed = Mathf.Min(minSpeed, sample.flight.airspeedKts);
                maxSpeed = Mathf.Max(maxSpeed, sample.flight.airspeedKts);
                minAlt = Mathf.Min(minAlt, sample.flight.altitudeFt);
                maxAlt = Mathf.Max(maxAlt, sample.flight.altitudeFt);
                lastHeading = sample.flight.headingDeg;
                result.maxBankDeg = Mathf.Max(result.maxBankDeg, Mathf.Abs(sample.flight.bankDeg));
                if (phase.targetHeadingDeg >= 0f)
                {
                    result.maxHeadingErrorDeg = Mathf.Max(result.maxHeadingErrorDeg, Mathf.Abs(Mathf.DeltaAngle(sample.flight.headingDeg, phase.targetHeadingDeg)));
                }
                if (phase.minAirspeedKts > 0f && sample.flight.airspeedKts < phase.minAirspeedKts)
                {
                    result.maxAirspeedErrorKts = Mathf.Max(result.maxAirspeedErrorKts, phase.minAirspeedKts - sample.flight.airspeedKts);
                }
                if (phase.maxAirspeedKts > 0f && sample.flight.airspeedKts > phase.maxAirspeedKts)
                {
                    result.maxAirspeedErrorKts = Mathf.Max(result.maxAirspeedErrorKts, sample.flight.airspeedKts - phase.maxAirspeedKts);
                }
                if (phase.minAltitudeFt > 0f && sample.flight.altitudeFt < phase.minAltitudeFt)
                {
                    result.maxAltitudeErrorFt = Mathf.Max(result.maxAltitudeErrorFt, phase.minAltitudeFt - sample.flight.altitudeFt);
                }
                if (phase.maxAltitudeFt > 0f && sample.flight.altitudeFt > phase.maxAltitudeFt)
                {
                    result.maxAltitudeErrorFt = Mathf.Max(result.maxAltitudeErrorFt, sample.flight.altitudeFt - phase.maxAltitudeFt);
                }
                if (sample.flight.stallWarning) result.stallWarningSamples++;

                foreach (string warning in warnings)
                {
                    if (!result.warnings.Contains(warning) && result.warnings.Count < 5)
                    {
                        result.warnings.Add(warning);
                    }
                }
            }

            if (count == 0)
            {
                result.score = 0f;
                result.warnings.Add("no samples in phase window");
                result.observedSummary = "no samples";
                return result;
            }

            if (phase.checklistRequired && !checklistComplete)
            {
                result.warnings.Add("checklist incomplete");
                scoreTotal -= 20f * count;
            }

            result.score = Mathf.Clamp(scoreTotal / count, 0f, 100f);
            result.passed = result.score >= 68f && result.stallWarningSamples < Mathf.Max(1, count / 3);
            result.gateHit = result.score >= 62f;
            result.observedSummary = $"{minSpeed:0}-{maxSpeed:0} kt, {minAlt:0}-{maxAlt:0} ft, final HDG {lastHeading:000}, bank {result.maxBankDeg:0}";
            return result;
        }
    }
}
