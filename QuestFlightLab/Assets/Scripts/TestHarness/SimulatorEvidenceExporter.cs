using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using QuestFlightLab.Training;
using UnityEngine;

namespace QuestFlightLab.TestHarness
{
    public static class SimulatorEvidenceExporter
    {
        public static void ExportSuite(FlightScenarioSuiteResult suite, string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "scenario_results.json"), JsonUtility.ToJson(suite, true));
            File.WriteAllText(Path.Combine(directory, "scenario_results.csv"), BuildScenarioCsv(suite));
            string summary = BuildSummaryMarkdown(suite);
            File.WriteAllText(Path.Combine(directory, "flight_core_summary.md"), summary);
            File.WriteAllText(Path.Combine(directory, "flight_pattern_summary.md"), summary);
            File.WriteAllText(Path.Combine(directory, "flight_approach_summary.md"), summary);

            FlightScenarioResult debriefScenario = suite.scenarios.FirstOrDefault(s => s.id == "basic_traffic_pattern_full")
                                                  ?? suite.scenarios.FirstOrDefault(s => s.debriefReport != null && s.debriefReport.phaseScores.Count > 0);
            if (debriefScenario != null)
            {
                File.WriteAllText(Path.Combine(directory, "debrief_report.json"), JsonUtility.ToJson(debriefScenario.debriefReport, true));
                File.WriteAllText(Path.Combine(directory, "debrief_report.md"), LessonScoring.BuildMarkdown(debriefScenario.debriefReport));
            }

            FlightScenarioResult approachScenario = suite.scenarios.FirstOrDefault(s => s.id == "stabilized_final_approach")
                                                  ?? suite.scenarios.FirstOrDefault(s => s.approachDebrief != null && s.approachDebrief.phaseScores.Count > 0);
            if (approachScenario != null)
            {
                File.WriteAllText(Path.Combine(directory, "approach_debrief_report.json"), JsonUtility.ToJson(approachScenario.approachDebrief, true));
                File.WriteAllText(Path.Combine(directory, "approach_debrief_report.md"), ApproachScoring.BuildMarkdown(approachScenario.approachDebrief));
                FlightTimelineRecorder.ExportTimeline(approachScenario.timeline, directory);
            }
        }

        private static string BuildScenarioCsv(FlightScenarioSuiteResult suite)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("id,name,passed,initial_airspeed_kts,final_airspeed_kts,max_airspeed_kts,altitude_delta_ft,max_altitude_ft,heading_change_deg,max_vsi_fpm,min_pitch_deg,max_pitch_deg,min_bank_deg,max_bank_deg,max_flap_deg,stall_warning_count,stall_onset_s,max_stall_intensity,max_reference_speed_error_abs_kts,max_glide_path_dev_deg,max_centerline_dev_m,instruments_present,approach_fields_present,training_steps_present,airport_refs_present,debrief_score,debrief_phases,debrief_warnings,approach_score,stable_gate,goaround_required,goaround_initiated,goaround_correct,timeline_samples,replay_markers,max_ground_roll_m,max_runway_offset_abs_m,reason");
            foreach (FlightScenarioResult r in suite.scenarios)
            {
                FlightScenarioStats s = r.stats;
                sb.Append(r.id).Append(',')
                    .Append(Escape(r.name)).Append(',')
                    .Append(r.passed).Append(',')
                    .Append(F(s.initialAirspeedKts)).Append(',')
                    .Append(F(s.finalAirspeedKts)).Append(',')
                    .Append(F(s.maxAirspeedKts)).Append(',')
                    .Append(F(s.altitudeDeltaFt)).Append(',')
                    .Append(F(s.maxAltitudeFt)).Append(',')
                    .Append(F(s.headingChangeDeg)).Append(',')
                    .Append(F(s.maxVerticalSpeedFpm)).Append(',')
                    .Append(F(s.minPitchDeg)).Append(',')
                    .Append(F(s.maxPitchDeg)).Append(',')
                    .Append(F(s.minBankDeg)).Append(',')
                    .Append(F(s.maxBankDeg)).Append(',')
                    .Append(F(s.maxFlapDegrees)).Append(',')
                    .Append(s.stallWarningSamples).Append(',')
                    .Append(F(s.stallWarningOnsetSeconds)).Append(',')
                    .Append(F(s.maxStallIntensity)).Append(',')
                    .Append(F(s.maxReferenceSpeedErrorAbsKts)).Append(',')
                    .Append(F(s.maxGlidePathDeviationAbsDeg)).Append(',')
                    .Append(F(s.maxCenterlineDeviationAbsMeters)).Append(',')
                    .Append(r.instrumentVerification.allRequiredPresent).Append(',')
                    .Append(r.instrumentVerification.approachFieldsPresent).Append(',')
                    .Append(r.trainingVerification.allRequiredStepsPresent).Append(',')
                    .Append(r.airportPatternVerification.allRequiredReferencesPresent).Append(',')
                    .Append(F(r.debriefReport != null ? r.debriefReport.totalScore : 0f)).Append(',')
                    .Append(r.debriefReport != null ? r.debriefReport.completedPhases : 0).Append(',')
                    .Append(r.debriefReport != null ? r.debriefReport.warnings.Count : 0).Append(',')
                    .Append(F(r.approachDebrief != null ? r.approachDebrief.totalScore : 0f)).Append(',')
                    .Append(r.approachDebrief != null && r.approachDebrief.stableAtGate).Append(',')
                    .Append(r.approachDebrief != null && r.approachDebrief.goAroundRequired).Append(',')
                    .Append(r.approachDebrief != null && r.approachDebrief.goAroundInitiated).Append(',')
                    .Append(r.approachDebrief != null && r.approachDebrief.goAroundDecisionCorrect).Append(',')
                    .Append(r.timeline != null ? r.timeline.sampleCount : 0).Append(',')
                    .Append(r.timeline != null ? r.timeline.markerCount : 0).Append(',')
                    .Append(F(s.maxGroundRollMeters)).Append(',')
                    .Append(F(s.maxRunwayOffsetAbsMeters)).Append(',')
                    .Append(Escape(r.passReason)).AppendLine();
            }
            return sb.ToString();
        }

        private static string BuildSummaryMarkdown(FlightScenarioSuiteResult suite)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Stabilized Approach + Traffic Pattern Autonomous Scenario Summary");
            sb.AppendLine();
            sb.AppendLine($"Started UTC: {suite.startedUtc}");
            sb.AppendLine($"Unity: {suite.unityVersion}");
            sb.AppendLine($"Mode: {suite.testMode}");
            sb.AppendLine($"Meta XR Simulator: {suite.metaXrSimulatorStatus}");
            sb.AppendLine($"Result: {suite.passedCount}/{suite.scenarioCount} scenarios passed");
            sb.AppendLine();
            sb.AppendLine("| Scenario | Pass | Speed | Alt Delta | Heading | Instruments | Training | Airport | Pattern Debrief | Approach | Timeline |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (FlightScenarioResult r in suite.scenarios)
            {
                FlightScenarioStats s = r.stats;
                sb.Append("| ")
                    .Append(r.name)
                    .Append(" | ")
                    .Append(r.passed ? "PASS" : "FAIL")
                    .Append(" | ")
                    .Append($"{s.initialAirspeedKts:0}->{s.finalAirspeedKts:0} kt")
                    .Append(" | ")
                    .Append($"{s.altitudeDeltaFt:0} ft")
                    .Append(" | ")
                    .Append($"{s.headingChangeDeg:0} deg")
                    .Append(" | ")
                    .Append(r.instrumentVerification.allRequiredPresent ? "PASS" : "FAIL")
                    .Append(" | ")
                    .Append(r.trainingVerification.allRequiredStepsPresent ? "PASS" : "FAIL")
                    .Append(" | ")
                    .Append(r.airportPatternVerification.allRequiredReferencesPresent ? "PASS" : "FAIL")
                    .Append(" | ")
                    .Append(r.debriefReport != null && r.debriefReport.phaseScores.Count > 0 ? $"{r.debriefReport.totalScore:0}" : "n/a")
                    .Append(" | ")
                    .Append(r.approachDebrief != null && r.approachDebrief.phaseScores.Count > 0
                        ? $"{r.approachDebrief.totalScore:0} / {(r.approachDebrief.goAroundRequired ? "GA req" : "stable")}"
                        : "n/a")
                    .Append(" | ")
                    .Append(r.timeline != null && r.timeline.sampleCount > 0 ? $"{r.timeline.sampleCount}/{r.timeline.markerCount}" : "n/a")
                    .AppendLine(" |");
            }
            sb.AppendLine();
            sb.AppendLine("Scenario details:");
            foreach (FlightScenarioResult r in suite.scenarios)
            {
                sb.Append("- ")
                    .Append(r.name)
                    .Append(": ")
                    .Append(r.passReason.Replace("|", "/"))
                    .AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Limitations:");
            foreach (string limitation in suite.limitations)
            {
                sb.AppendLine($"- {limitation}");
            }
            return sb.ToString();
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Escape(string value)
        {
            value ??= "";
            return value.Contains(",") || value.Contains("\"")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
