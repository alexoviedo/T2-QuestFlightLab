using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace QuestFlightLab.Editor
{
    public static class JSBSimEditorBridgeRunner
    {
        private const string EnvOutputDir = "QFL_JSBSIM_BRIDGE_DIR";

        [MenuItem("Quest Flight Lab/Run JSBSim Editor Bridge")]
        public static void RunBridge()
        {
            string outputDir = System.Environment.GetEnvironmentVariable(EnvOutputDir);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.GetFullPath(Path.Combine("..", "T2-QuestFlightLab-setup-artifacts", $"jsbsim_bridge_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            Directory.CreateDirectory(outputDir);
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string scriptPath = Path.Combine(repoRoot, "tools", "jsbsim_probe", "jsbsim_editor_bridge.py");
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("JSBSim editor bridge script missing.", scriptPath);
            }

            string stdoutPath = Path.Combine(outputDir, "jsbsim_editor_bridge_stdout.txt");
            string stderrPath = Path.Combine(outputDir, "jsbsim_editor_bridge_stderr.txt");
            int exitCode = RunPython(scriptPath, outputDir, stdoutPath, stderrPath);
            if (exitCode != 0)
            {
                throw new Exception($"JSBSim editor bridge sidecar failed with exit code {exitCode}. See {stderrPath}");
            }

            string reportPath = Path.Combine(outputDir, "jsbsim_editor_bridge_report.json");
            if (!File.Exists(reportPath))
            {
                throw new FileNotFoundException("JSBSim editor bridge report was not written.", reportPath);
            }

            BridgePythonReport pythonReport = JsonUtility.FromJson<BridgePythonReport>(File.ReadAllText(reportPath));
            if (pythonReport == null || pythonReport.samples == null || pythonReport.samples.Length == 0)
            {
                throw new Exception("JSBSim editor bridge produced no samples.");
            }

            BridgeUnityReport unityReport = ApplySamplesToUnityProxy(pythonReport, reportPath, outputDir);
            string unityReportPath = Path.Combine(outputDir, "jsbsim_editor_bridge_unity_report.json");
            File.WriteAllText(unityReportPath, JsonUtility.ToJson(unityReport, true));
            WriteMarkdown(Path.Combine(outputDir, "jsbsim_editor_bridge_unity_summary.md"), unityReport);
            Debug.Log($"[QuestFlightLab][JSBSimBridge] Imported {unityReport.sampleCount} JSBSim samples and applied {unityReport.appliedPoseCount} Unity proxy poses. Output: {outputDir}");
        }

        private static int RunPython(string scriptPath, string outputDir, string stdoutPath, string stderrPath)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" --output-dir \"{outputDir}\" --duration 45",
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi);
            if (process == null) return -1;
            if (!process.WaitForExit(90000))
            {
                process.Kill();
                File.WriteAllText(stdoutPath, process.StandardOutput.ReadToEnd());
                File.WriteAllText(stderrPath, process.StandardError.ReadToEnd() + "\nTimed out after 90 seconds.");
                return -2;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            File.WriteAllText(stdoutPath, stdout);
            File.WriteAllText(stderrPath, stderr);
            return process.ExitCode;
        }

        private static BridgeUnityReport ApplySamplesToUnityProxy(BridgePythonReport pythonReport, string pythonReportPath, string outputDir)
        {
            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.name = "JSBSim Editor Bridge Aircraft Proxy";
            proxy.transform.localScale = new Vector3(10f, 2.5f, 8f);
            Vector3 runwayStart = new Vector3(-560f, 1.25f, 0f);
            Vector3 firstPosition = Vector3.zero;
            Vector3 finalPosition = Vector3.zero;
            float maxAglMeters = 0f;
            float maxAirspeed = 0f;

            for (int i = 0; i < pythonReport.samples.Length; i++)
            {
                BridgeSample sample = pythonReport.samples[i];
                Vector3 position = runwayStart + new Vector3(sample.east_m, sample.agl_ft * 0.3048f, sample.north_m);
                Quaternion rotation = Quaternion.Euler(-sample.pitch_deg, sample.heading_deg, -sample.bank_deg);
                proxy.transform.SetPositionAndRotation(position, rotation);
                if (i == 0) firstPosition = position;
                finalPosition = position;
                maxAglMeters = Mathf.Max(maxAglMeters, sample.agl_ft * 0.3048f);
                maxAirspeed = Mathf.Max(maxAirspeed, sample.airspeed_kt);
            }

            string poseCsvPath = Path.Combine(outputDir, "jsbsim_editor_bridge_unity_poses.csv");
            WritePoseCsv(poseCsvPath, pythonReport.samples, runwayStart);

            return new BridgeUnityReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                status = "PASS",
                classification = "editor_sidecar_bridge_unity_applied",
                pythonReportPath = pythonReportPath,
                unityPoseCsvPath = poseCsvPath,
                sampleCount = pythonReport.samples.Length,
                appliedPoseCount = pythonReport.samples.Length,
                firstUnityPosition = firstPosition,
                finalUnityPosition = finalPosition,
                maxAglMeters = maxAglMeters,
                maxAirspeedKts = maxAirspeed,
                finalHeadingDeg = pythonReport.samples[^1].heading_deg,
                limitations = new[]
                {
                    "Editor-only sidecar bridge; Quest/Android runtime JSBSim integration is not attempted.",
                    "Unity applies imported JSBSim samples to a proxy object after the sidecar run; this is not interactive runtime flight yet.",
                    "Coordinate conversion is a first-pass east/north/AGL mapping for visualization, not a final simulator state pipeline."
                }
            };
        }

        private static void WritePoseCsv(string path, BridgeSample[] samples, Vector3 runwayStart)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("time_s,x_m,y_m,z_m,pitch_deg,bank_deg,heading_deg,airspeed_kt");
            foreach (BridgeSample sample in samples)
            {
                Vector3 position = runwayStart + new Vector3(sample.east_m, sample.agl_ft * 0.3048f, sample.north_m);
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0:0.000},{1:0.000},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000}\n",
                    sample.time_s,
                    position.x,
                    position.y,
                    position.z,
                    sample.pitch_deg,
                    sample.bank_deg,
                    sample.heading_deg,
                    sample.airspeed_kt);
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteMarkdown(string path, BridgeUnityReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# JSBSim Editor Bridge Unity Summary");
            sb.AppendLine();
            sb.AppendLine($"- Status: `{report.status}`");
            sb.AppendLine($"- Classification: `{report.classification}`");
            sb.AppendLine($"- Unity: `{report.unityVersion}`");
            sb.AppendLine($"- Samples imported: {report.sampleCount}");
            sb.AppendLine($"- Proxy poses applied: {report.appliedPoseCount}");
            sb.AppendLine($"- Max AGL: {report.maxAglMeters:0.0} m");
            sb.AppendLine($"- Max airspeed: {report.maxAirspeedKts:0.0} kt");
            sb.AppendLine($"- Final heading: {report.finalHeadingDeg:0.0} deg");
            sb.AppendLine();
            sb.AppendLine("## Limitations");
            sb.AppendLine();
            foreach (string limitation in report.limitations)
            {
                sb.AppendLine($"- {limitation}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        [Serializable]
        private class BridgePythonReport
        {
            public string tool;
            public string classification;
            public string jsbsim_version;
            public string aircraft;
            public string reset;
            public string profile;
            public BridgeSample[] samples;
            public BridgeAggregate aggregate;
            public string[] limitations;
        }

        [Serializable]
        private class BridgeAggregate
        {
            public int sample_count;
            public float duration_s;
            public float max_airspeed_kt;
            public float max_agl_ft;
            public float max_abs_bank_deg;
            public float final_airspeed_kt;
            public float final_agl_ft;
            public float final_heading_deg;
            public float ground_track_m;
        }

        [Serializable]
        private class BridgeSample
        {
            public float time_s;
            public float east_m;
            public float north_m;
            public float agl_ft;
            public float altitude_delta_ft;
            public float airspeed_kt;
            public float vertical_speed_fpm;
            public float pitch_deg;
            public float bank_deg;
            public float heading_deg;
            public float throttle;
            public float elevator;
            public float aileron;
            public float rudder;
            public float flaps;
            public float trim;
        }

        [Serializable]
        private class BridgeUnityReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string status;
            public string classification;
            public string pythonReportPath;
            public string unityPoseCsvPath;
            public int sampleCount;
            public int appliedPoseCount;
            public Vector3 firstUnityPosition;
            public Vector3 finalUnityPosition;
            public float maxAglMeters;
            public float maxAirspeedKts;
            public float finalHeadingDeg;
            public string[] limitations;
        }
    }
}
