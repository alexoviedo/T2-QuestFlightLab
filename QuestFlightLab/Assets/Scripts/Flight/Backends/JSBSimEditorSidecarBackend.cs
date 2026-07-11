using System;
using System.IO;
using QuestFlightLab.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using System.Diagnostics;
using System.Threading.Tasks;
#endif

namespace QuestFlightLab.Flight.Backends
{
    /// <summary>
    /// Backend adapter for the repository's existing Python JSON-lines sidecar.
    /// It remains Editor-only and intentionally reuses jsbsim_live_sidecar.py.
    /// </summary>
    public sealed class JSBSimEditorSidecarBackend : IFlightDynamicsBackend
    {
        private FlightDynamicsBackendContext _context;
        private AircraftControlState _controls = AircraftControlState.Neutral();
#if UNITY_EDITOR
        private Process _process;
#endif

        public FlightDynamicsBackendKind Kind => FlightDynamicsBackendKind.JSBSimEditorSidecar;
        public string DisplayName => "JSBSim 1.3.1 Editor sidecar";
#if UNITY_EDITOR
        public bool IsAvailable => true;
        public bool IsInitialized => _process != null && !_process.HasExited;
#else
        public bool IsAvailable => false;
        public bool IsInitialized => false;
#endif
        public string LastError { get; private set; } = string.Empty;
        public FlightDynamicsState CurrentState { get; private set; }

        public bool Initialize(FlightDynamicsBackendContext context)
        {
#if !UNITY_EDITOR
            LastError = "The JSBSim process sidecar is available only in Unity Editor.";
            return false;
#else
            if (context == null || context.simulationRoot == null)
            {
                LastError = "Editor sidecar backend requires an aircraft simulation root.";
                return false;
            }

            string script = context.sidecarScriptPath;
            if (string.IsNullOrWhiteSpace(script))
            {
                string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                script = Path.Combine(repositoryRoot, "tools", "jsbsim_probe", "jsbsim_live_sidecar.py");
            }

            if (!File.Exists(script))
            {
                LastError = $"JSBSim live sidecar script was not found: {script}";
                return false;
            }

            Dispose();
            _context = context;
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{script}\" --aircraft {context.jsbsimAircraft} --reset reset00 --dt {context.fixedDeltaTimeSeconds:R} --heading-deg {FlightDynamicsInitialConditions.KbduRunwayTrueHeadingDegrees:R}",
                WorkingDirectory = Path.GetDirectoryName(script) ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            _process = Process.Start(startInfo);
            if (_process == null)
            {
                LastError = "Could not start the existing JSBSim Python sidecar.";
                return false;
            }

            SidecarResponse response = ReadResponse(30000);
            if (!IsSuccessful(response) || response.sample == null)
            {
                LastError = response?.message ?? "JSBSim sidecar did not become ready.";
                Dispose();
                return false;
            }

            CaptureState(response.sample);
            LastError = string.Empty;
            return true;
#endif
        }

        public bool Reset(FlightDynamicsInitialConditions initialConditions)
        {
#if !UNITY_EDITOR
            LastError = "The JSBSim process sidecar is available only in Unity Editor.";
            return false;
#else
            if (!RequireInitialized()) return false;
            SidecarCommand command = new SidecarCommand
            {
                command = "reset",
                heading_deg = initialConditions.headingDegrees
            };
            SidecarResponse response = Send(command);
            if (!IsSuccessful(response) || response.sample == null)
            {
                LastError = response?.message ?? "JSBSim sidecar reset failed.";
                return false;
            }

            CaptureState(response.sample);
            return true;
#endif
        }

        public void SetControls(AircraftControlState controls)
        {
            _controls = controls ?? AircraftControlState.Neutral();
        }

        public void SetAtmosphere(FlightDynamicsAtmosphere atmosphere)
        {
            // Existing sidecar protocol has no wind command. Native integration
            // supplies this capability; the Editor bridge behavior is preserved.
        }

        public bool Advance(double fixedDeltaTimeSeconds)
        {
#if !UNITY_EDITOR
            LastError = "The JSBSim process sidecar is available only in Unity Editor.";
            return false;
#else
            if (!RequireInitialized()) return false;
            SidecarCommand command = new SidecarCommand
            {
                command = "step",
                dt = fixedDeltaTimeSeconds,
                control = SidecarControl.From(_controls)
            };
            SidecarResponse response = Send(command);
            if (!IsSuccessful(response) || response.sample == null)
            {
                LastError = response?.message ?? "JSBSim sidecar fixed step failed.";
                return false;
            }

            CaptureState(response.sample);
            return CurrentState.IsFinite;
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.StandardInput.WriteLine("{\"command\":\"shutdown\"}");
                        _process.StandardInput.Flush();
                        _process.WaitForExit(2000);
                    }
                }
                catch
                {
                    // Best-effort cleanup for an Editor-only child process.
                }

                if (!_process.HasExited)
                {
                    try { _process.Kill(); } catch { }
                }
                _process.Dispose();
                _process = null;
            }
#endif
            _context = null;
        }

        private bool RequireInitialized()
        {
            if (IsInitialized) return true;
            LastError = "JSBSim Editor sidecar backend is not initialized.";
            return false;
        }

        private void CaptureState(SidecarSample sample)
        {
            double altitudeMsl = _context.localOrigin.altitudeMslMeters + sample.agl_ft * FlightFrameConversions.FeetToMeters;
            Vector3 position = new Vector3(
                (float)sample.east_m,
                (float)(sample.agl_ft * FlightFrameConversions.FeetToMeters),
                (float)sample.north_m);
            GeodeticReference geodetic = FlightFrameConversions.UnityToGeodetic(position, _context.localOrigin);
            double headingRadians = sample.heading_deg * FlightFrameConversions.DegreesToRadians;
            double horizontalMetersPerSecond = sample.ground_speed_kt * FlightFrameConversions.KnotsToMetersPerSecond;
            CurrentState = new FlightDynamicsState
            {
                simulationTimeSeconds = sample.time_s,
                latitudeDegrees = geodetic.latitudeDegrees,
                longitudeDegrees = geodetic.longitudeDegrees,
                altitudeMslMeters = altitudeMsl,
                altitudeAglMeters = sample.agl_ft * FlightFrameConversions.FeetToMeters,
                calibratedAirspeedKnots = sample.airspeed_kt,
                groundSpeedKnots = sample.ground_speed_kt,
                verticalSpeedFeetPerMinute = sample.vertical_speed_fpm,
                headingDegrees = sample.heading_deg,
                pitchDegrees = sample.pitch_deg,
                bankDegrees = sample.bank_deg,
                positionUnityMeters = position,
                rotationUnity = FlightFrameConversions.JsbsimAttitudeToUnity(sample.heading_deg, sample.pitch_deg, sample.bank_deg),
                velocityUnityMetersPerSecond = new Vector3(
                    (float)(Math.Sin(headingRadians) * horizontalMetersPerSecond),
                    (float)(sample.vertical_speed_fpm / FlightFrameConversions.FeetPerSecondToFeetPerMinute * FlightFrameConversions.FeetToMeters),
                    (float)(Math.Cos(headingRadians) * horizontalMetersPerSecond))
            };
            LastError = string.Empty;
        }

#if UNITY_EDITOR
        private SidecarResponse Send(SidecarCommand command)
        {
            try
            {
                _process.StandardInput.WriteLine(JsonUtility.ToJson(command));
                _process.StandardInput.Flush();
                return ReadResponse(30000);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return null;
            }
        }

        private SidecarResponse ReadResponse(int timeoutMilliseconds)
        {
            while (true)
            {
                Task<string> read = Task.Run(() => _process.StandardOutput.ReadLine());
                if (!read.Wait(timeoutMilliseconds))
                {
                    throw new TimeoutException("Timed out waiting for the JSBSim Editor sidecar.");
                }

                string line = read.Result;
                if (line == null)
                {
                    string stderr = _process.StandardError.ReadToEnd();
                    throw new EndOfStreamException($"JSBSim Editor sidecar closed stdout. {stderr}");
                }

                line = line.Trim();
                if (!line.StartsWith("{", StringComparison.Ordinal)) continue;
                return JsonUtility.FromJson<SidecarResponse>(line);
            }
        }
#endif

        private static bool IsSuccessful(SidecarResponse response)
        {
            return response != null && response.status != "FAIL";
        }

        [Serializable]
        private sealed class SidecarCommand
        {
            public string command;
            public double dt;
            public double heading_deg;
            public SidecarControl control;
        }

        [Serializable]
        private sealed class SidecarControl
        {
            public float aileron;
            public float elevator;
            public float rudder;
            public float throttle;
            public float mixture;
            public float carb_heat;
            public float trim;
            public float flaps;
            public float left_brake;
            public float right_brake;

            public static SidecarControl From(AircraftControlState controls)
            {
                return new SidecarControl
                {
                    aileron = controls.aileron,
                    elevator = controls.elevator,
                    rudder = controls.rudder,
                    throttle = controls.throttle,
                    mixture = controls.mixture,
                    carb_heat = controls.carbHeat,
                    trim = controls.trim,
                    flaps = controls.flaps,
                    left_brake = controls.leftToeBrake,
                    right_brake = controls.rightToeBrake
                };
            }
        }

        [Serializable]
        private sealed class SidecarResponse
        {
            public string status;
            public string message;
            public SidecarSample sample;
        }

        [Serializable]
        private sealed class SidecarSample
        {
            public double time_s;
            public double east_m;
            public double north_m;
            public double agl_ft;
            public double airspeed_kt;
            public double vertical_speed_fpm;
            public double pitch_deg;
            public double bank_deg;
            public double heading_deg;
            public double ground_speed_kt;
        }
    }
}
