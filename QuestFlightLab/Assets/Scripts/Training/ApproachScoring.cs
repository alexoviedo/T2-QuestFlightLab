using System;
using System.Collections.Generic;
using System.Text;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class ApproachScoringSample
    {
        public float timestamp;
        public AircraftControlState controls;
        public FlightTelemetrySnapshot flight;
        public string phaseId = "";
    }

    [Serializable]
    public class ApproachEvaluationSnapshot
    {
        public string phaseId = "";
        public string gateId = "";
        public bool stable;
        public bool goAroundRequired;
        public bool goAroundInitiated;
        public float score;
        public float targetAirspeedKts = 65f;
        public float targetDescentRateFpm = -650f;
        public float glidePathDeviationDeg;
        public float centerlineDeviationMeters;
        public string warningSummary = "";
    }

    [Serializable]
    public class ApproachPhaseScore
    {
        public string phaseId = "";
        public string phaseName = "";
        public float score;
        public bool passed;
        public bool stable;
        public bool goAroundRequired;
        public bool goAroundInitiated;
        public string targetSummary = "";
        public string observedSummary = "";
        public float maxSpeedErrorKts;
        public float maxAltitudeErrorFt;
        public float maxDescentDeviationFpm;
        public float maxGlidePathDeviationDeg;
        public float maxCenterlineDeviationMeters;
        public float maxBankDeg;
        public int stallWarningSamples;
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public class ApproachDebrief
    {
        public string lessonId = "";
        public string lessonTitle = "";
        public string scenarioId = "";
        public string generatedUtc = "";
        public float totalScore;
        public bool passed;
        public bool stableAtGate;
        public bool goAroundRequired;
        public bool goAroundInitiated;
        public bool goAroundDecisionCorrect;
        public bool touchdownPlaceholderObserved;
        public int completedPhases;
        public int totalPhases;
        public int unstableWarningCount;
        public int stallWarningCount;
        public int timelineSampleCount;
        public int replayMarkerCount;
        public float maxSpeedErrorKts;
        public float maxAltitudeErrorFt;
        public float maxDescentDeviationFpm;
        public float maxGlidePathDeviationDeg;
        public float maxCenterlineDeviationMeters;
        public string summary = "";
        public List<string> warnings = new List<string>();
        public List<string> recommendations = new List<string>();
        public List<ApproachPhaseScore> phaseScores = new List<ApproachPhaseScore>();
    }

    public static class GoAroundDecisionModel
    {
        public static bool ShouldGoAround(ApproachEvaluationSnapshot evaluation, FlightTelemetrySnapshot flight)
        {
            if (evaluation == null || flight == null) return false;
            float altitudeAglFt = Mathf.Max(0f, flight.altitudeFt - 4f);
            return evaluation.goAroundRequired
                   || (!evaluation.stable && altitudeAglFt <= StabilizedApproachLesson.StableGate300Agl.altitudeAglFt)
                   || flight.stallWarning
                   || flight.verticalSpeedFpm < -1100f;
        }
    }

    public static class ApproachScoring
    {
        public const float TargetFinalApproachSpeedKts = 65f;
        public const float TargetDescentRateFpm = -650f;
        public const float TargetGlidePathDeg = 3f;

        public static ApproachDebrief ScoreApproach(
            string scenarioId,
            IList<ApproachScoringSample> samples,
            bool checklistComplete)
        {
            ApproachDebrief report = new ApproachDebrief
            {
                lessonId = StabilizedApproachLesson.LessonId,
                lessonTitle = StabilizedApproachLesson.LessonTitle,
                scenarioId = scenarioId,
                generatedUtc = DateTime.UtcNow.ToString("o"),
                totalPhases = StabilizedApproachLesson.RequiredPhaseIds.Length
            };

            if (samples == null || samples.Count == 0)
            {
                report.summary = "No samples were available for approach scoring.";
                report.warnings.Add(report.summary);
                return report;
            }

            float duration = Mathf.Max(0.01f, samples[^1].timestamp);
            float weighted = 0f;
            float weightSum = 0f;

            foreach (ApproachPhase phase in StabilizedApproachLesson.BasicPhases())
            {
                ApproachPhaseScore score = ScorePhaseWindow(phase, samples, duration, checklistComplete);
                report.phaseScores.Add(score);
                weighted += score.score * Mathf.Max(0.01f, phase.scoringWeight);
                weightSum += Mathf.Max(0.01f, phase.scoringWeight);
                if (score.passed) report.completedPhases++;
                if (score.goAroundRequired) report.goAroundRequired = true;
                if (score.goAroundInitiated) report.goAroundInitiated = true;
                if (score.warnings.Count > 0) report.unstableWarningCount += score.warnings.Count;
                report.stallWarningCount += score.stallWarningSamples;
                report.maxSpeedErrorKts = Mathf.Max(report.maxSpeedErrorKts, score.maxSpeedErrorKts);
                report.maxAltitudeErrorFt = Mathf.Max(report.maxAltitudeErrorFt, score.maxAltitudeErrorFt);
                report.maxDescentDeviationFpm = Mathf.Max(report.maxDescentDeviationFpm, score.maxDescentDeviationFpm);
                report.maxGlidePathDeviationDeg = Mathf.Max(report.maxGlidePathDeviationDeg, score.maxGlidePathDeviationDeg);
                report.maxCenterlineDeviationMeters = Mathf.Max(report.maxCenterlineDeviationMeters, score.maxCenterlineDeviationMeters);

                foreach (string warning in score.warnings)
                {
                    string text = $"{phase.name}: {warning}";
                    if (!report.warnings.Contains(text) && report.warnings.Count < 24)
                    {
                        report.warnings.Add(text);
                    }
                }

                if (phase.stableGate && score.stable)
                {
                    report.stableAtGate = true;
                }
            }

            foreach (ApproachScoringSample sample in samples)
            {
                if (sample?.flight == null) continue;
                ApproachEvaluationSnapshot eval = EvaluateTelemetrySample(sample.phaseId, sample.timestamp, sample.flight, sample.controls);
                ApproachPhase phase = StabilizedApproachLesson.FindPhase(sample.phaseId);
                bool decisionPhase = phase != null && (phase.stableGate || phase.goAroundDecisionPhase || phase.id == "continue_landing_decision" || phase.id == "unstable_approach_warning");
                bool ignoreNoisyPassedDecisionSample = decisionPhase && PhaseWasStable(report, sample.phaseId);
                if (!ignoreNoisyPassedDecisionSample)
                {
                    report.goAroundRequired |= eval.goAroundRequired;
                }
                report.goAroundInitiated |= eval.goAroundInitiated;
                report.touchdownPlaceholderObserved |= sample.flight.onGround && sample.timestamp > duration * 0.7f;
            }

            if (report.stableAtGate && !ScenarioRequiresGoAroundProfile(scenarioId))
            {
                report.goAroundRequired = false;
            }

            report.goAroundDecisionCorrect = report.goAroundRequired ? report.goAroundInitiated : !report.goAroundInitiated;
            if (!checklistComplete)
            {
                report.warnings.Add("Approach checklist placeholder was not complete.");
            }

            report.totalScore = weightSum > 0f ? Mathf.Clamp(weighted / weightSum, 0f, 100f) : 0f;
            if (report.goAroundRequired && !report.goAroundInitiated) report.totalScore = Mathf.Min(report.totalScore, 55f);
            if (!report.goAroundRequired && report.goAroundInitiated) report.totalScore = Mathf.Min(report.totalScore, 72f);

            report.passed = report.totalScore >= 70f
                            && report.completedPhases >= 8
                            && report.goAroundDecisionCorrect
                            && report.stallWarningCount < 45;
            report.summary = $"score {report.totalScore:0}, phases {report.completedPhases}/{report.totalPhases}, stable gate {(report.stableAtGate ? "pass" : "not met")}, go-around {(report.goAroundRequired ? "required" : "not required")}/{(report.goAroundInitiated ? "initiated" : "not initiated")}";
            AddRecommendations(report);
            return report;
        }

        public static ApproachEvaluationSnapshot EvaluateTelemetrySample(
            string phaseId,
            float timestamp,
            FlightTelemetrySnapshot flight,
            AircraftControlState controls)
        {
            ApproachPhase phase = StabilizedApproachLesson.FindPhase(phaseId) ?? StabilizedApproachLesson.FindPhase("stabilized_approach_gate");
            float score = ScoreInstant(phase, flight, controls, out List<string> warnings);
            bool stable = warnings.Count == 0 || (score >= 78f && !flight.stallWarning);
            float altitudeAglFt = Mathf.Max(0f, flight.altitudeFt - 4f);
            float glideDeviation = ComputeGlidePathDeviationDeg(flight);
            bool decisionWindow = phase.stableGate
                                  || phase.goAroundDecisionPhase
                                  || phase.id == "continue_landing_decision"
                                  || phase.id == "unstable_approach_warning";
            bool gateFailure = phase.stableGate && !stable;
            bool goAroundRequired = gateFailure
                                    || (decisionWindow && !stable && altitudeAglFt <= StabilizedApproachLesson.StableGate300Agl.altitudeAglFt)
                                    || flight.stallWarning
                                    || flight.verticalSpeedFpm < -1100f;
            bool goAroundInitiated = controls != null
                                     && controls.throttle > 0.85f
                                     && (flight.verticalSpeedFpm > 50f || flight.pitchDeg > 3f);

            return new ApproachEvaluationSnapshot
            {
                phaseId = phase.id,
                gateId = phase.stableGate ? StabilizedApproachLesson.StableGate300Agl.id : "",
                stable = stable,
                goAroundRequired = goAroundRequired,
                goAroundInitiated = goAroundInitiated,
                score = score,
                targetAirspeedKts = TargetFinalApproachSpeedKts,
                targetDescentRateFpm = TargetDescentRateFpm,
                glidePathDeviationDeg = glideDeviation,
                centerlineDeviationMeters = flight != null ? flight.runwayLateralOffsetMeters : 0f,
                warningSummary = warnings.Count == 0 ? "none" : string.Join("; ", warnings)
            };
        }

        public static float ScoreInstant(ApproachPhase phase, FlightTelemetrySnapshot flight, AircraftControlState controls, out List<string> warnings)
        {
            warnings = new List<string>();
            if (phase == null || flight == null) return 0f;

            float score = 100f;
            float headingError = Mathf.Abs(Mathf.DeltaAngle(flight.headingDeg, phase.targetHeadingDeg));
            score -= Mathf.Max(0f, headingError - phase.headingToleranceDeg) * 0.65f;
            if (headingError > phase.headingToleranceDeg) warnings.Add($"heading error {headingError:0} deg");

            if (flight.airspeedKts < phase.minAirspeedKts)
            {
                float error = phase.minAirspeedKts - flight.airspeedKts;
                score -= error * 1.4f;
                if (error > 1f) warnings.Add($"airspeed low by {error:0} kt");
            }

            if (flight.airspeedKts > phase.maxAirspeedKts)
            {
                float error = flight.airspeedKts - phase.maxAirspeedKts;
                score -= error * 1.1f;
                if (error > 1f) warnings.Add($"airspeed high by {error:0} kt");
            }

            if (phase.minAltitudeFt > 0f && flight.altitudeFt < phase.minAltitudeFt)
            {
                float error = phase.minAltitudeFt - flight.altitudeFt;
                score -= error * 0.035f;
                if (error > 60f) warnings.Add($"altitude low by {error:0} ft");
            }

            if (phase.maxAltitudeFt > 0f && flight.altitudeFt > phase.maxAltitudeFt)
            {
                float error = flight.altitudeFt - phase.maxAltitudeFt;
                score -= error * 0.03f;
                if (error > 80f) warnings.Add($"altitude high by {error:0} ft");
            }

            if (flight.verticalSpeedFpm < phase.minVerticalSpeedFpm)
            {
                float error = phase.minVerticalSpeedFpm - flight.verticalSpeedFpm;
                score -= error * 0.018f;
                if (error > 80f) warnings.Add($"sink rate high by {error:0} fpm");
            }

            if (flight.verticalSpeedFpm > phase.maxVerticalSpeedFpm)
            {
                float error = flight.verticalSpeedFpm - phase.maxVerticalSpeedFpm;
                score -= error * 0.012f;
                if (error > 100f) warnings.Add($"descent/climb outside target by {error:0} fpm");
            }

            float bank = Mathf.Abs(flight.bankDeg);
            score -= Mathf.Max(0f, bank - phase.bankLimitDeg) * 2.2f;
            if (bank > phase.bankLimitDeg) warnings.Add($"bank exceeded {phase.bankLimitDeg:0} deg");

            float flapError = Mathf.Abs(flight.flapDegrees - phase.targetFlapDeg);
            score -= Mathf.Max(0f, flapError - phase.flapToleranceDeg) * 0.5f;
            if (flapError > phase.flapToleranceDeg) warnings.Add($"flaps differ by {flapError:0} deg");

            float centerline = Mathf.Abs(flight.runwayLateralOffsetMeters);
            score -= Mathf.Max(0f, centerline - phase.maxCenterlineDeviationMeters) * 0.5f;
            if (centerline > phase.maxCenterlineDeviationMeters) warnings.Add($"centerline offset {centerline:0} m");

            float glide = Mathf.Abs(ComputeGlidePathDeviationDeg(flight));
            score -= Mathf.Max(0f, glide - phase.maxGlidePathDeviationDeg) * 4.5f;
            if (glide > phase.maxGlidePathDeviationDeg) warnings.Add($"glide path deviation {glide:0.0} deg");

            if (flight.stallWarning)
            {
                score -= 18f;
                warnings.Add("stall warning observed");
            }

            return Mathf.Clamp(score, 0f, 100f);
        }

        public static float ComputeGlidePathDeviationDeg(FlightTelemetrySnapshot flight)
        {
            if (flight == null || flight.airspeedKts < 15f) return 0f;
            float groundSpeedFpm = Mathf.Max(1f, flight.airspeedKts * 101.269f);
            float descentFpm = Mathf.Max(0f, -flight.verticalSpeedFpm);
            float pathDeg = Mathf.Atan2(descentFpm, groundSpeedFpm) * Mathf.Rad2Deg;
            return pathDeg - TargetGlidePathDeg;
        }

        public static string BuildMarkdown(ApproachDebrief report)
        {
            if (report == null) return "# Approach Debrief Report\n\nNo approach debrief was generated.\n";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Stabilized Approach + Go-Around Debrief Report");
            sb.AppendLine();
            sb.AppendLine($"Lesson: {report.lessonTitle}");
            sb.AppendLine($"Scenario: {report.scenarioId}");
            sb.AppendLine($"Generated UTC: {report.generatedUtc}");
            sb.AppendLine($"Score: {report.totalScore:0} / 100");
            sb.AppendLine($"Result: {(report.passed ? "PASS" : "NEEDS WORK")}");
            sb.AppendLine($"Summary: {report.summary}");
            sb.AppendLine($"Stable gate: {(report.stableAtGate ? "PASS" : "NOT MET")}");
            sb.AppendLine($"Go-around: {(report.goAroundRequired ? "required" : "not required")}, {(report.goAroundInitiated ? "initiated" : "not initiated")}, decision {(report.goAroundDecisionCorrect ? "correct" : "incorrect")}");
            sb.AppendLine();
            sb.AppendLine("| Phase | Score | Stable | Go-Around | Warnings | Observed |");
            sb.AppendLine("| --- | ---: | --- | --- | ---: | --- |");
            foreach (ApproachPhaseScore phase in report.phaseScores)
            {
                sb.Append("| ")
                    .Append(phase.phaseName)
                    .Append(" | ")
                    .Append($"{phase.score:0}")
                    .Append(" | ")
                    .Append(phase.stable ? "yes" : "no")
                    .Append(" | ")
                    .Append(phase.goAroundRequired ? "required" : phase.goAroundInitiated ? "initiated" : "no")
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

            if (report.recommendations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Prototype recommendations:");
                foreach (string recommendation in report.recommendations)
                {
                    sb.AppendLine($"- {recommendation}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Limitations: This debrief is deterministic prototype evidence only. It is not FAA-approved training, a POH substitute, legal training credit, or validated C172 approach/landing performance.");
            return sb.ToString();
        }

        private static ApproachPhaseScore ScorePhaseWindow(
            ApproachPhase phase,
            IList<ApproachScoringSample> samples,
            float duration,
            bool checklistComplete)
        {
            float start = phase.startFraction * duration;
            float end = Mathf.Max(start + 0.01f, phase.endFraction * duration);
            ApproachPhaseScore result = new ApproachPhaseScore
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
            float minVsi = float.MaxValue;
            float maxVsi = float.MinValue;

            foreach (ApproachScoringSample sample in samples)
            {
                if (sample == null || sample.flight == null || sample.timestamp < start || sample.timestamp > end) continue;
                float instant = ScoreInstant(phase, sample.flight, sample.controls, out List<string> warnings);
                ApproachEvaluationSnapshot eval = EvaluateTelemetrySample(phase.id, sample.timestamp, sample.flight, sample.controls);
                scoreTotal += instant;
                count++;

                result.stable |= eval.stable;
                if (!phase.stableGate)
                {
                    result.goAroundRequired |= eval.goAroundRequired;
                }
                result.goAroundInitiated |= eval.goAroundInitiated;
                minSpeed = Mathf.Min(minSpeed, sample.flight.airspeedKts);
                maxSpeed = Mathf.Max(maxSpeed, sample.flight.airspeedKts);
                minAlt = Mathf.Min(minAlt, sample.flight.altitudeFt);
                maxAlt = Mathf.Max(maxAlt, sample.flight.altitudeFt);
                minVsi = Mathf.Min(minVsi, sample.flight.verticalSpeedFpm);
                maxVsi = Mathf.Max(maxVsi, sample.flight.verticalSpeedFpm);
                result.maxBankDeg = Mathf.Max(result.maxBankDeg, Mathf.Abs(sample.flight.bankDeg));
                result.maxGlidePathDeviationDeg = Mathf.Max(result.maxGlidePathDeviationDeg, Mathf.Abs(eval.glidePathDeviationDeg));
                result.maxCenterlineDeviationMeters = Mathf.Max(result.maxCenterlineDeviationMeters, Mathf.Abs(eval.centerlineDeviationMeters));
                result.maxSpeedErrorKts = Mathf.Max(result.maxSpeedErrorKts, SpeedError(sample.flight.airspeedKts, phase.minAirspeedKts, phase.maxAirspeedKts));
                result.maxAltitudeErrorFt = Mathf.Max(result.maxAltitudeErrorFt, AltitudeError(sample.flight.altitudeFt, phase.minAltitudeFt, phase.maxAltitudeFt));
                result.maxDescentDeviationFpm = Mathf.Max(result.maxDescentDeviationFpm, DescentError(sample.flight.verticalSpeedFpm, phase.minVerticalSpeedFpm, phase.maxVerticalSpeedFpm));
                if (sample.flight.stallWarning) result.stallWarningSamples++;

                foreach (string warning in warnings)
                {
                    if (!result.warnings.Contains(warning) && result.warnings.Count < 6)
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
            if (phase.stableGate && !result.stable)
            {
                result.goAroundRequired = true;
            }
            if ((phase.goAroundDecisionPhase || phase.id == "continue_landing_decision" || phase.id == "unstable_approach_warning") && result.stable)
            {
                result.goAroundRequired = false;
            }
            result.passed = result.score >= 68f && result.stallWarningSamples < Mathf.Max(1, count / 3);
            result.observedSummary = $"{minSpeed:0}-{maxSpeed:0} kt, {minAlt:0}-{maxAlt:0} ft, VSI {minVsi:0}/{maxVsi:0}, bank {result.maxBankDeg:0}, centerline {result.maxCenterlineDeviationMeters:0} m";
            return result;
        }

        private static void AddRecommendations(ApproachDebrief report)
        {
            if (!report.stableAtGate && !report.goAroundInitiated)
            {
                report.recommendations.Add("At the prototype stable gate, initiate a go-around when speed, path, alignment, or configuration is outside criteria.");
            }
            if (report.maxSpeedErrorKts > 8f)
            {
                report.recommendations.Add("Use pitch and power earlier to reduce final-approach speed deviation.");
            }
            if (report.maxGlidePathDeviationDeg > 1.5f || report.maxDescentDeviationFpm > 250f)
            {
                report.recommendations.Add("Stabilize descent rate and glide-path trend before descending below the gate altitude.");
            }
            if (report.maxCenterlineDeviationMeters > 18f)
            {
                report.recommendations.Add("Correct centerline alignment earlier on final or discontinue the approach.");
            }
            if (report.recommendations.Count == 0)
            {
                report.recommendations.Add("Prototype criteria were met; review timeline markers for consistency before repeating.");
            }
        }

        private static bool PhaseWasStable(ApproachDebrief report, string phaseId)
        {
            if (report == null || string.IsNullOrEmpty(phaseId)) return false;
            foreach (ApproachPhaseScore phase in report.phaseScores)
            {
                if (phase.phaseId == phaseId) return phase.stable;
            }

            return false;
        }

        private static bool ScenarioRequiresGoAroundProfile(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId)) return false;
            string id = scenarioId.ToLowerInvariant();
            return id.Contains("unstable")
                   || id.Contains("goaround")
                   || id.Contains("go_around")
                   || id.Contains("sink");
        }

        private static float SpeedError(float speed, float min, float max)
        {
            if (speed < min) return min - speed;
            return speed > max ? speed - max : 0f;
        }

        private static float AltitudeError(float altitude, float min, float max)
        {
            if (min > 0f && altitude < min) return min - altitude;
            return max > 0f && altitude > max ? altitude - max : 0f;
        }

        private static float DescentError(float vsi, float min, float max)
        {
            if (vsi < min) return min - vsi;
            return vsi > max ? vsi - max : 0f;
        }
    }
}
