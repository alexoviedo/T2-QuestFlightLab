using System.Globalization;
using System.IO;
using System.Text;
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
            File.WriteAllText(Path.Combine(directory, "flight_core_summary.md"), BuildSummaryMarkdown(suite));
        }

        private static string BuildScenarioCsv(FlightScenarioSuiteResult suite)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("id,name,passed,max_airspeed_kts,max_altitude_ft,max_vsi_fpm,min_pitch_deg,max_pitch_deg,min_bank_deg,max_bank_deg,max_flap_deg,stall_warning,max_ground_roll_m,max_runway_offset_abs_m,reason");
            foreach (FlightScenarioResult r in suite.scenarios)
            {
                FlightScenarioStats s = r.stats;
                sb.Append(r.id).Append(',')
                    .Append(Escape(r.name)).Append(',')
                    .Append(r.passed).Append(',')
                    .Append(F(s.maxAirspeedKts)).Append(',')
                    .Append(F(s.maxAltitudeFt)).Append(',')
                    .Append(F(s.maxVerticalSpeedFpm)).Append(',')
                    .Append(F(s.minPitchDeg)).Append(',')
                    .Append(F(s.maxPitchDeg)).Append(',')
                    .Append(F(s.minBankDeg)).Append(',')
                    .Append(F(s.maxBankDeg)).Append(',')
                    .Append(F(s.maxFlapDegrees)).Append(',')
                    .Append(s.stallWarningObserved).Append(',')
                    .Append(F(s.maxGroundRollMeters)).Append(',')
                    .Append(F(s.maxRunwayOffsetAbsMeters)).Append(',')
                    .Append(Escape(r.passReason)).AppendLine();
            }
            return sb.ToString();
        }

        private static string BuildSummaryMarkdown(FlightScenarioSuiteResult suite)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Flight Core Autonomous Scenario Summary");
            sb.AppendLine();
            sb.AppendLine($"Started UTC: {suite.startedUtc}");
            sb.AppendLine($"Unity: {suite.unityVersion}");
            sb.AppendLine($"Mode: {suite.testMode}");
            sb.AppendLine($"Meta XR Simulator: {suite.metaXrSimulatorStatus}");
            sb.AppendLine($"Result: {suite.passedCount}/{suite.scenarioCount} scenarios passed");
            sb.AppendLine();
            sb.AppendLine("| Scenario | Pass | Key Result |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (FlightScenarioResult r in suite.scenarios)
            {
                sb.Append("| ")
                    .Append(r.name)
                    .Append(" | ")
                    .Append(r.passed ? "PASS" : "FAIL")
                    .Append(" | ")
                    .Append(r.passReason.Replace("|", "/"))
                    .AppendLine(" |");
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
