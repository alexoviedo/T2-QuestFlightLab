using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuestFlightLab.Environment;
using QuestFlightLab.Flight;
using QuestFlightLab.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace QuestFlightLab.Editor
{
    public static class JSBSimLiveEditorDriverRunner
    {
        private const string EnvOutputDir = "QFL_JSBSIM_LIVE_DRIVER_DIR";
        private const int ScreenshotWidth = 1280;
        private const int ScreenshotHeight = 720;
        private const float DurationSeconds = 60f;
        private const float FrameDt = 1f / 30f;
        private static readonly Vector3 RunwayStartPosition = new Vector3(-560f, 1.25f, 0f);
        private static readonly Quaternion RunwayStartRotation = Quaternion.Euler(0f, 90f, 0f);

        [MenuItem("Quest Flight Lab/Run JSBSim Live Editor Driver")]
        public static void RunLiveDriver()
        {
            string outputDir = System.Environment.GetEnvironmentVariable(EnvOutputDir);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.GetFullPath(Path.Combine("..", "T2-QuestFlightLab-setup-artifacts", $"jsbsim_live_driver_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "screenshots"));

            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string sidecarPath = Path.Combine(repoRoot, "tools", "jsbsim_probe", "jsbsim_live_sidecar.py");
            if (!File.Exists(sidecarPath))
            {
                throw new FileNotFoundException("JSBSim live sidecar script missing.", sidecarPath);
            }

            LiveDriverScene scene = BuildScene();
            List<LiveDriverFrame> frames = new List<LiveDriverFrame>();
            List<string> screenshotPaths = new List<string>();
            string sidecarStderrPath = Path.Combine(outputDir, "jsbsim_live_sidecar_stderr.txt");

            using LiveSidecar sidecar = LiveSidecar.Start(sidecarPath, repoRoot, sidecarStderrPath);
            LiveSidecarResponse hello = sidecar.ReadInitial();
            if (hello == null || hello.status != "ready")
            {
                throw new Exception($"JSBSim live sidecar did not become ready. stderr={sidecar.Stderr}");
            }

            LiveSidecarResponse reset = sidecar.Send("{\"command\":\"reset\",\"heading_deg\":90.0}");
            if (reset == null || reset.status != "ok")
            {
                throw new Exception($"JSBSim live sidecar reset failed. stderr={sidecar.Stderr}");
            }

            float[] captureTimes = { 0f, 18f, 32f, 48f, 58f };
            int nextCaptureIndex = 0;
            Vector3 firstPosition = Vector3.zero;
            Vector3 lastPosition = Vector3.zero;
            float maxAglMeters = 0f;
            float maxAirspeedKts = 0f;
            float maxAbsBankDeg = 0f;
            LiveSample previousSample = null;

            int stepCount = Mathf.CeilToInt(DurationSeconds / FrameDt);
            for (int i = 0; i <= stepCount; i++)
            {
                float elapsed = i * FrameDt;
                AircraftControlState controls = LiveDriverControlsForElapsedSeconds(elapsed, previousSample, out string phase);
                LiveSidecarResponse response = sidecar.Step(FrameDt, controls);
                if (response == null || response.status != "ok" || response.sample == null)
                {
                    throw new Exception($"JSBSim live sidecar step failed at t={elapsed:0.00}s. stderr={sidecar.Stderr}");
                }

                LiveSample sample = response.sample;
                ApplySample(scene.aircraftRoot, scene.aircraftState, sample, controls);
                if (i == 0) firstPosition = scene.aircraftRoot.transform.position;
                lastPosition = scene.aircraftRoot.transform.position;
                maxAglMeters = Mathf.Max(maxAglMeters, sample.agl_ft * 0.3048f);
                maxAirspeedKts = Mathf.Max(maxAirspeedKts, sample.airspeed_kt);
                maxAbsBankDeg = Mathf.Max(maxAbsBankDeg, Mathf.Abs(sample.bank_deg));
                previousSample = sample;

                frames.Add(new LiveDriverFrame
                {
                    time_s = elapsed,
                    phase = phase,
                    sample = sample,
                    unityPosition = scene.aircraftRoot.transform.position,
                    unityEuler = scene.aircraftRoot.transform.eulerAngles
                });

                if (nextCaptureIndex < captureTimes.Length && elapsed + FrameDt * 0.5f >= captureTimes[nextCaptureIndex])
                {
                    string id = $"jsbsim_live_{nextCaptureIndex:00}_{SafePhaseName(phase)}";
                    string path = Path.Combine(outputDir, "screenshots", id + ".png");
                    PositionCameraForSample(scene, sample, elapsed, nextCaptureIndex == 0);
                    CaptureCamera(scene.camera, path, ScreenshotWidth, ScreenshotHeight);
                    screenshotPaths.Add(path);
                    nextCaptureIndex++;
                }
            }

            sidecar.Shutdown();

            string csvPath = Path.Combine(outputDir, "jsbsim_live_driver_frames.csv");
            WriteFrameCsv(csvPath, frames);

            LiveDriverReport report = new LiveDriverReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                status = "PASS",
                classification = "editor_interactive_sidecar_frame_loop",
                outputDirectory = outputDir,
                sidecarScriptPath = sidecarPath,
                sidecarReady = hello.status == "ready",
                sidecarJsbsimVersion = hello.jsbsim_version,
                aircraft = hello.aircraft,
                reset = hello.reset,
                durationSeconds = DurationSeconds,
                timestepSeconds = FrameDt,
                sampleCount = frames.Count,
                appliedPoseCount = frames.Count,
                firstUnityPosition = firstPosition,
                finalUnityPosition = lastPosition,
                groundTrackMeters = Vector3.Distance(
                    Vector3.ProjectOnPlane(firstPosition, Vector3.up),
                    Vector3.ProjectOnPlane(lastPosition, Vector3.up)),
                maxAglMeters = maxAglMeters,
                maxAirspeedKts = maxAirspeedKts,
                maxAbsBankDeg = maxAbsBankDeg,
                finalHeadingDeg = frames.Count > 0 ? frames[^1].sample.heading_deg : 0f,
                poseChanged = Vector3.Distance(firstPosition, lastPosition) > 10f,
                airborne = maxAglMeters > 3f,
                cockpitAssetStatus = scene.cockpitAssetStatus,
                worldStatus = scene.worldStatus,
                frameCsvPath = csvPath,
                screenshotPaths = screenshotPaths.ToArray(),
                limitations = new[]
                {
                    "Editor-only live sidecar bridge; Quest/Android runtime JSBSim integration is not attempted.",
                    "Unity sends per-frame controls and timesteps, then immediately applies returned JSBSim pose to an Editor aircraft root.",
                    "This proves interactive process/data/pose plumbing, not final C172 fidelity, FAA/training suitability, or Quest runtime performance.",
                    "Position is integrated from JSBSim heading and ground-speed output in the sidecar for Unity visualization."
                }
            };

            File.WriteAllText(Path.Combine(outputDir, "jsbsim_live_driver_report.json"), JsonUtility.ToJson(report, true));
            WriteMarkdown(Path.Combine(outputDir, "jsbsim_live_driver_summary.md"), report);
            Debug.Log($"[QuestFlightLab][JSBSimLiveDriver] Applied {report.appliedPoseCount} live JSBSim poses. Output: {outputDir}");
        }

        private static LiveDriverScene BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderQualityEvidence renderQuality = QuestRenderQualityConfigurator.ApplyProfile("jsbsim_live_editor_driver");

            GameObject sunObject = new GameObject("JSBSim Live Driver Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sunObject.transform.rotation = Quaternion.Euler(45f, -34f, 0f);

            GameObject airport = KbduApproxAirport.Build(null);
            AirportRuntimeEnhancer.EnhanceExistingScene();
            GameObject world = KbduInspiredWorldBuilder.AddWorld(airport != null ? airport.transform : null);
            string worldStatus = "world unavailable";
            if (world != null && world.TryGetComponent(out WorldPerformanceBudget budget))
            {
                worldStatus = $"profile={budget.profileName} chunks={budget.terrainChunkCount} lodGroups={budget.lodGroupCount} renderers={budget.rendererCount} tris~{budget.approxTriangleCount} draw={budget.farDrawRadiusMeters:0}m";
            }

            GameObject aircraft = new GameObject("JSBSim Live Driven C172 Root");
            aircraft.transform.SetPositionAndRotation(RunwayStartPosition, RunwayStartRotation);
            AircraftState state = aircraft.AddComponent<AircraftState>();
            state.config = Resources.Load<C172StyleAircraftConfig>("C172StyleAircraftConfig");
            state.ResetState(Vector3.zero);
            aircraft.AddComponent<FlightTelemetry>().aircraftState = state;

            string cockpitStatus = AddImportedC172(aircraft.transform);

            GameObject cameraObject = new GameObject("JSBSim Live Driver Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = Mathf.Max(12000f, renderQuality.cameraFarClipMeters);
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();

            return new LiveDriverScene
            {
                aircraftRoot = aircraft,
                aircraftState = state,
                camera = camera,
                cockpitAssetStatus = cockpitStatus,
                worldStatus = worldStatus
            };
        }

        private static string AddImportedC172(Transform aircraft)
        {
            GameObject prefab = Resources.Load<GameObject>(QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            if (prefab == null)
            {
                return "Imported C172 resource missing.";
            }

            GameObject instance = Object.Instantiate(prefab, aircraft);
            instance.name = "JSBSim Live Imported C172";
            ApplyImportedCockpitPose(instance.transform, Vector3.zero, 0f);
            instance.transform.localScale = Vector3.one;
            int renderers = instance.GetComponentsInChildren<Renderer>(true).Length;
            return $"Imported C172 resource loaded from Resources/{QuestFirstViewRuntimeRepair.ImportedC172ResourcePath}; renderers={renderers}.";
        }

        private static void ApplySample(GameObject aircraftRoot, AircraftState state, LiveSample sample, AircraftControlState controls)
        {
            Vector3 position = RunwayStartPosition + new Vector3(sample.east_m, sample.agl_ft * 0.3048f, sample.north_m);
            Quaternion rotation = Quaternion.Euler(-sample.pitch_deg, sample.heading_deg, -sample.bank_deg);
            aircraftRoot.transform.SetPositionAndRotation(position, rotation);

            float horizontalSpeedMps = sample.airspeed_kt * AircraftUnitConversions.KnotsToMetersPerSecond;
            float verticalSpeedMps = sample.vertical_speed_fpm / 196.8504f;
            Vector3 velocity = aircraftRoot.transform.forward * horizontalSpeedMps + Vector3.up * verticalSpeedMps;

            state.velocityWorld = velocity;
            state.angularVelocityDeg = Vector3.zero;
            state.angleOfAttackDeg = 0f;
            state.stallIntensity = 0f;
            state.slipSkid = 0f;
            state.referenceSpeedKts = state.config != null ? state.config.Speeds.bestRateClimbKts : 74f;
            state.targetSpeedErrorKts = sample.airspeed_kt - state.referenceSpeedKts;
            state.engineRpm = Mathf.Lerp(700f, 2700f, Mathf.Clamp01(controls.throttle));
            state.powerPercent = Mathf.Clamp01(controls.throttle) * 100f;
            state.flapDegrees = Mathf.Clamp01(controls.flaps) * 30f;
            state.trimPercent = controls.trim;
            state.loadFactorG = Mathf.Clamp(1f / Mathf.Max(0.2f, Mathf.Cos(sample.bank_deg * Mathf.Deg2Rad)), 0f, 4f);
            state.onGround = sample.agl_ft < 3f;
            state.runwayLateralOffsetMeters = sample.north_m;
            state.RefreshFromTransform(aircraftRoot.transform);
        }

        private static void PositionCameraForSample(LiveDriverScene scene, LiveSample sample, float elapsed, bool cockpit)
        {
            Transform aircraft = scene.aircraftRoot.transform;
            if (cockpit)
            {
                Vector3 eye = QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal;
                scene.camera.transform.SetPositionAndRotation(aircraft.TransformPoint(eye), aircraft.rotation);
                scene.camera.fieldOfView = 76f;
                return;
            }

            Vector3 target = aircraft.position + Vector3.up * 1.4f;
            Vector3 offset = -aircraft.forward * 18f + aircraft.right * 7.5f + Vector3.up * Mathf.Lerp(5f, 10f, Mathf.InverseLerp(30f, 90f, elapsed));
            Vector3 cameraPosition = target + offset;
            scene.camera.transform.SetPositionAndRotation(cameraPosition, LookAt(cameraPosition, target));
            scene.camera.fieldOfView = sample.agl_ft > 20f ? 48f : 55f;
        }

        private static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(texture);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static void WriteFrameCsv(string path, List<LiveDriverFrame> frames)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("time_s,phase,x_m,y_m,z_m,airspeed_kt,agl_ft,vertical_speed_fpm,pitch_deg,bank_deg,heading_deg,throttle,elevator,aileron,rudder,flaps,trim");
            foreach (LiveDriverFrame frame in frames)
            {
                LiveSample sample = frame.sample;
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0:0.000},{1},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.000},{8:0.000},{9:0.000},{10:0.000},{11:0.000},{12:0.000},{13:0.000},{14:0.000},{15:0.000},{16:0.000}\n",
                    frame.time_s,
                    EscapeCsv(frame.phase),
                    frame.unityPosition.x,
                    frame.unityPosition.y,
                    frame.unityPosition.z,
                    sample.airspeed_kt,
                    sample.agl_ft,
                    sample.vertical_speed_fpm,
                    sample.pitch_deg,
                    sample.bank_deg,
                    sample.heading_deg,
                    sample.throttle,
                    sample.elevator,
                    sample.aileron,
                    sample.rudder,
                    sample.flaps,
                    sample.trim);
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteMarkdown(string path, LiveDriverReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# JSBSim Live Editor Driver Summary");
            sb.AppendLine();
            sb.AppendLine($"- Status: `{report.status}`");
            sb.AppendLine($"- Classification: `{report.classification}`");
            sb.AppendLine($"- Unity: `{report.unityVersion}`");
            sb.AppendLine($"- JSBSim: `{report.sidecarJsbsimVersion}`");
            sb.AppendLine($"- Aircraft: `{report.aircraft}` reset `{report.reset}`");
            sb.AppendLine($"- Duration: {report.durationSeconds:0.0}s at dt {report.timestepSeconds:0.000}s");
            sb.AppendLine($"- Samples sent/applied: {report.sampleCount}/{report.appliedPoseCount}");
            sb.AppendLine($"- Pose changed: {report.poseChanged}");
            sb.AppendLine($"- Airborne: {report.airborne}");
            sb.AppendLine($"- Ground track: {report.groundTrackMeters:0.0} m");
            sb.AppendLine($"- Max AGL: {report.maxAglMeters:0.0} m");
            sb.AppendLine($"- Max airspeed: {report.maxAirspeedKts:0.0} kt");
            sb.AppendLine($"- Max abs bank: {report.maxAbsBankDeg:0.0} deg");
            sb.AppendLine($"- Final heading: {report.finalHeadingDeg:0.0} deg");
            sb.AppendLine($"- Cockpit asset: {report.cockpitAssetStatus}");
            sb.AppendLine($"- World: {report.worldStatus}");
            sb.AppendLine($"- Frame CSV: `{report.frameCsvPath}`");
            sb.AppendLine();
            sb.AppendLine("## Screenshots");
            sb.AppendLine();
            foreach (string screenshot in report.screenshotPaths ?? Array.Empty<string>())
            {
                sb.AppendLine($"- `{screenshot}`");
            }
            sb.AppendLine();
            sb.AppendLine("## Limitations");
            sb.AppendLine();
            foreach (string limitation in report.limitations ?? Array.Empty<string>())
            {
                sb.AppendLine($"- {limitation}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static string StepCommand(float dt, AircraftControlState c)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"command\":\"step\",\"dt\":{0:0.########},\"control\":{{\"throttle\":{1:0.######},\"elevator\":{2:0.######},\"aileron\":{3:0.######},\"rudder\":{4:0.######},\"flaps\":{5:0.######},\"trim\":{6:0.######},\"mixture\":{7:0.######},\"carb_heat\":{8:0.######},\"left_brake\":{9:0.######},\"right_brake\":{10:0.######}}}}}",
                dt,
                c.throttle,
                c.elevator,
                c.aileron,
                c.rudder,
                c.flaps,
                c.trim,
                c.mixture,
                c.carbHeat,
                c.leftToeBrake,
                c.rightToeBrake);
        }

        private static AircraftControlState LiveDriverControlsForElapsedSeconds(float elapsed, LiveSample previousSample, out string phase)
        {
            AircraftControlState c = AircraftControlState.Neutral(0.05f);
            c.trim = 0.10f;

            if (elapsed < 2f)
            {
                phase = "neutral";
                return c;
            }

            if (elapsed < 8f)
            {
                phase = "control sweep";
                float t = (elapsed - 2f) * 1.7f;
                c.throttle = 0.08f;
                c.aileron = Mathf.Sin(t) * 0.18f;
                c.elevator = Mathf.Sin(t * 0.8f) * 0.12f;
                c.rudder = Mathf.Sin(t * 0.65f) * 0.12f;
                return c;
            }

            if (elapsed < 20f)
            {
                phase = "takeoff roll";
                c.throttle = 1f;
                c.elevator = 0f;
                c.rudder = 0f;
                return c;
            }

            if (elapsed < 32f)
            {
                phase = "rotate";
                c.throttle = 1f;
                c.elevator = 0.16f;
                c.rudder = 0f;
                return c;
            }

            if (elapsed < 52f)
            {
                phase = "climb";
                c.throttle = 1f;
                c.elevator = 0.18f;
                c.rudder = 0f;
                return c;
            }

            phase = "stabilize";
            c.throttle = 0.82f;
            c.elevator = 0.04f;
            return c;
        }

        private static void ApplyImportedCockpitPose(Transform cockpit, Vector3 offset, float yawDegrees)
        {
            Quaternion modelRotation = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler);
            Quaternion cameraInModelRotation = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEyeEuler);
            Quaternion modelInCameraRotation = Quaternion.Inverse(cameraInModelRotation) * modelRotation;
            cockpit.localRotation = Quaternion.Euler(0f, yawDegrees, 0f) * modelInCameraRotation;
            Vector3 baseSeatTarget = QuestFirstViewRuntimeRepair.ImportedC172SeatReferenceLocal + offset;
            cockpit.localPosition = baseSeatTarget - cockpit.localRotation * QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye;
        }

        private static Quaternion LookAt(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static string SafePhaseName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "frame";
            char[] chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            return new string(chars).Trim('_');
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class LiveSidecar : IDisposable
        {
            private readonly Process _process;
            private readonly StringBuilder _stderr = new StringBuilder();
            private bool _disposed;

            private LiveSidecar(Process process)
            {
                _process = process;
                _process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) _stderr.AppendLine(e.Data);
                };
                _process.BeginErrorReadLine();
            }

            public string Stderr => _stderr.ToString();

            public static LiveSidecar Start(string scriptPath, string workingDirectory, string stderrPath)
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" --aircraft c172x --reset reset00 --dt 0.008333333 --heading-deg 90",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Process process = Process.Start(psi);
                if (process == null) throw new Exception("Could not start JSBSim live sidecar process.");
                LiveSidecar sidecar = new LiveSidecar(process);
                AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    try
                    {
                        File.WriteAllText(stderrPath, sidecar.Stderr);
                    }
                    catch
                    {
                        // Best-effort diagnostic on process shutdown.
                    }
                };
                return sidecar;
            }

            public LiveSidecarResponse ReadInitial()
            {
                return ReadResponse(30000);
            }

            public LiveSidecarResponse Step(float dt, AircraftControlState controls)
            {
                return Send(StepCommand(dt, controls));
            }

            public LiveSidecarResponse Send(string commandJson)
            {
                if (_process.HasExited)
                {
                    throw new Exception($"JSBSim live sidecar exited early with code {_process.ExitCode}. stderr={Stderr}");
                }

                _process.StandardInput.WriteLine(commandJson);
                _process.StandardInput.Flush();
                return ReadResponse(30000);
            }

            public void Shutdown()
            {
                if (_disposed || _process.HasExited) return;
                try
                {
                    _process.StandardInput.WriteLine("{\"command\":\"shutdown\"}");
                    _process.StandardInput.Flush();
                    ReadResponse(5000);
                }
                catch
                {
                    // The process may already be closing; Dispose will clean up.
                }
            }

            private LiveSidecarResponse ReadResponse(int timeoutMs)
            {
                while (true)
                {
                    Task<string> task = Task.Run(() => _process.StandardOutput.ReadLine());
                    if (!task.Wait(timeoutMs))
                    {
                        throw new TimeoutException($"Timed out waiting for JSBSim live sidecar response. stderr={Stderr}");
                    }

                    string line = task.Result;
                    if (line == null)
                    {
                        throw new EndOfStreamException($"JSBSim live sidecar stdout closed. stderr={Stderr}");
                    }

                    line = line.Trim();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("{", StringComparison.Ordinal)) continue;
                    LiveSidecarResponse response = JsonUtility.FromJson<LiveSidecarResponse>(line);
                    if (response == null)
                    {
                        throw new Exception($"Could not parse JSBSim live sidecar response: {line}");
                    }

                    if (response.status == "FAIL")
                    {
                        throw new Exception($"JSBSim live sidecar error: {response.message}");
                    }

                    return response;
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    Shutdown();
                }
                catch
                {
                    // Best effort.
                }

                if (!_process.HasExited)
                {
                    try
                    {
                        _process.Kill();
                    }
                    catch
                    {
                        // Best effort.
                    }
                }

                _process.Dispose();
            }
        }

        private class LiveDriverScene
        {
            public GameObject aircraftRoot;
            public AircraftState aircraftState;
            public Camera camera;
            public string cockpitAssetStatus;
            public string worldStatus;
        }

        [Serializable]
        private class LiveSidecarResponse
        {
            public string type;
            public string status;
            public string message;
            public string jsbsim_version;
            public string aircraft;
            public string reset;
            public float fixed_dt;
            public LiveSample sample;
        }

        [Serializable]
        private class LiveSample
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
            public float ground_speed_kt;
            public float throttle;
            public float elevator;
            public float aileron;
            public float rudder;
            public float flaps;
            public float trim;
            public float mixture;
            public float left_brake;
            public float right_brake;
        }

        [Serializable]
        private class LiveDriverFrame
        {
            public float time_s;
            public string phase;
            public LiveSample sample;
            public Vector3 unityPosition;
            public Vector3 unityEuler;
        }

        [Serializable]
        private class LiveDriverReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string status;
            public string classification;
            public string outputDirectory;
            public string sidecarScriptPath;
            public bool sidecarReady;
            public string sidecarJsbsimVersion;
            public string aircraft;
            public string reset;
            public float durationSeconds;
            public float timestepSeconds;
            public int sampleCount;
            public int appliedPoseCount;
            public Vector3 firstUnityPosition;
            public Vector3 finalUnityPosition;
            public float groundTrackMeters;
            public float maxAglMeters;
            public float maxAirspeedKts;
            public float maxAbsBankDeg;
            public float finalHeadingDeg;
            public bool poseChanged;
            public bool airborne;
            public string cockpitAssetStatus;
            public string worldStatus;
            public string frameCsvPath;
            public string[] screenshotPaths;
            public string[] limitations;
        }
    }
}
