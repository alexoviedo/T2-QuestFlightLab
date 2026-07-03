using System;
using System.Collections.Generic;
using System.IO;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using UnityEngine;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class DemoPilotSample
    {
        public float timestamp;
        public string phase;
        public float aileron;
        public float elevator;
        public float rudder;
        public float throttle;
        public float airspeedKts;
        public float altitudeFt;
        public float headingDeg;
        public float pitchDeg;
        public float bankDeg;
        public bool onGround;
    }

    [Serializable]
    public class DemoPilotEvidence
    {
        public string generatedUtc;
        public string platform;
        public string unityVersion;
        public string deviceModel;
        public string sceneryMode;
        public string demoMode;
        public string evidencePath;
        public bool deterministicInputEnabled;
        public bool playtestRunwayAlignmentApplied;
        public bool visualFlightEnvelopeApplied;
        public bool aircraftMoved;
        public bool aircraftAirborne;
        public Vector3 startPosition;
        public Vector3 latestPosition;
        public float distanceFromStartMeters;
        public string phase;
        public List<DemoPilotSample> samples = new List<DemoPilotSample>();
    }

    public class ShortPlaytestDemoPilot : MonoBehaviour
    {
        private const string LogPrefix = "[QuestFlightLab][DemoPilot]";
        private static readonly Vector3 PlaytestRunwayStartPosition = new Vector3(-560f, 1.25f, 0f);
        private static readonly Vector3 PlaytestRunwayStartEuler = new Vector3(0f, 90f, 0f);

        public static ShortPlaytestDemoPilot Instance { get; private set; }

        public string PhaseName { get; private set; } = "initializing";
        public string EvidencePath => _evidence != null ? _evidence.evidencePath : string.Empty;

        private DeterministicGamepadInputSource _deterministicInput;
        private Usb2BleInputMapper _mapper;
        private FlightTelemetry _flightTelemetry;
        private SimpleAircraftPhysics _aircraftPhysics;
        private AircraftState _aircraftState;
        private DemoPilotEvidence _evidence;
        private Vector3 _startPosition;
        private Quaternion _startRotation = Quaternion.identity;
        private bool _visualFlightEnvelopeApplied;
        private bool _playtestRunwayAlignmentApplied;
        private float _elapsed;
        private float _nextSampleTime;
        private float _nextWriteTime;
        private bool _resetIssued;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!QuestLaunchOptions.ShortPlaytestDemoRequested()) return;
            if (FindFirstObjectByType<ShortPlaytestDemoPilot>() != null) return;

            GameObject go = new GameObject("Short Playtest Demo Pilot");
            DontDestroyOnLoad(go);
            go.AddComponent<ShortPlaytestDemoPilot>();
        }

        private void Start()
        {
            Instance = this;
            ResolveReferences();
            EnableDeterministicInput();

            if (_aircraftPhysics != null)
            {
                _aircraftPhysics.runwayResetPosition = PlaytestRunwayStartPosition;
                _aircraftPhysics.runwayResetEuler = PlaytestRunwayStartEuler;
                _playtestRunwayAlignmentApplied = true;
                _aircraftPhysics.ResetToRunway();
            }

            _startPosition = _aircraftState != null ? _aircraftState.transform.position : Vector3.zero;
            _startRotation = _aircraftState != null ? _aircraftState.transform.rotation : Quaternion.identity;
            _evidence = CreateEvidence();
            WriteEvidence();
            Debug.Log($"{LogPrefix} Short playtest demo active. evidence={_evidence.evidencePath}");
        }

        private void Update()
        {
            if (_deterministicInput == null || _evidence == null)
            {
                ResolveReferences();
                EnableDeterministicInput();
            }

            _elapsed += Time.unscaledDeltaTime;
            AircraftControlState controls = ControlsForElapsedSeconds(_elapsed, out string phase);
            PhaseName = phase;
            _deterministicInput?.SetControls(controls);
            ApplyVisualFlightEnvelope();

            if (_elapsed > 150f && !_resetIssued)
            {
                _aircraftPhysics?.ResetToRunway();
                _resetIssued = true;
                PhaseName = "reset/hold";
            }

            if (Time.unscaledTime >= _nextSampleTime)
            {
                AddSample(controls);
                _nextSampleTime = Time.unscaledTime + 0.5f;
            }

            if (Time.unscaledTime >= _nextWriteTime)
            {
                WriteEvidence();
                _nextWriteTime = Time.unscaledTime + 5f;
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) WriteEvidence();
        }

        private void OnApplicationQuit()
        {
            WriteEvidence();
        }

        public static AircraftControlState ControlsForElapsedSeconds(float elapsed, out string phase)
        {
            AircraftControlState c = AircraftControlState.Neutral(0.05f);

            if (elapsed < 2f)
            {
                phase = "neutral";
                return c;
            }

            if (elapsed < 10f)
            {
                phase = "control surface sweep";
                float t = (elapsed - 2f) * 1.7f;
                c.throttle = 0.15f;
                c.aileron = Mathf.Sin(t) * 0.75f;
                c.elevator = Mathf.Sin(t * 0.8f) * 0.55f;
                c.rudder = Mathf.Sin(t * 0.65f) * 0.65f;
                c.leftToeBrake = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(t));
                c.rightToeBrake = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(t + Mathf.PI));
                return c;
            }

            if (elapsed < 22f)
            {
                phase = "taxi power";
                c.throttle = Mathf.Lerp(0.25f, 0.55f, Mathf.InverseLerp(10f, 22f, elapsed));
                c.rudder = Mathf.Sin(elapsed * 0.8f) * 0.15f;
                return c;
            }

            if (elapsed < 45f)
            {
                phase = "takeoff roll";
                c.throttle = 1f;
                c.rudder = Mathf.Sin(elapsed * 0.5f) * 0.08f;
                return c;
            }

            if (elapsed < 65f)
            {
                phase = "rotate/climb";
                c.throttle = 1f;
                c.elevator = Mathf.Lerp(0.25f, 0.58f, Mathf.InverseLerp(45f, 52f, elapsed));
                return c;
            }

            if (elapsed < 90f)
            {
                phase = "shallow left bank";
                c.throttle = 0.95f;
                c.elevator = 0.22f;
                c.aileron = -0.25f;
                c.rudder = -0.08f;
                return c;
            }

            if (elapsed < 115f)
            {
                phase = "shallow right bank";
                c.throttle = 0.95f;
                c.elevator = 0.18f;
                c.aileron = 0.24f;
                c.rudder = 0.08f;
                return c;
            }

            if (elapsed < 150f)
            {
                phase = "stabilize/hold";
                c.throttle = 0.75f;
                c.elevator = 0.08f;
                return c;
            }

            phase = "reset/hold";
            c.throttle = 0.2f;
            return c;
        }

        public static bool TryGetVisualFlightPoseForElapsedSeconds(
            float elapsed,
            Vector3 startPosition,
            Quaternion runwayRotation,
            out Vector3 position,
            out Vector3 euler,
            out Vector3 velocityWorld)
        {
            position = startPosition;
            euler = runwayRotation.eulerAngles;
            velocityWorld = Vector3.zero;

            if (elapsed < 10f || elapsed >= 150f)
            {
                return false;
            }

            float runwayYaw = runwayRotation.eulerAngles.y;
            Vector3 runwayForward = Vector3.ProjectOnPlane(runwayRotation * Vector3.forward, Vector3.up).normalized;
            if (runwayForward.sqrMagnitude < 0.001f) runwayForward = Vector3.forward;
            Vector3 runwayRight = Vector3.ProjectOnPlane(runwayRotation * Vector3.right, Vector3.up).normalized;
            if (runwayRight.sqrMagnitude < 0.001f) runwayRight = Vector3.right;

            float distanceMeters;
            float lateralMeters = 0f;
            float altitudeMeters = 0f;
            float pitchUpDeg = 0f;
            float bankDeg = 0f;
            float yawOffsetDeg = 0f;
            float speedKts;
            float verticalMps = 0f;

            if (elapsed < 22f)
            {
                float t = Smooth01(Mathf.InverseLerp(10f, 22f, elapsed));
                distanceMeters = Mathf.Lerp(0f, 35f, t);
                speedKts = Mathf.Lerp(3f, 24f, t);
            }
            else if (elapsed < 42f)
            {
                float t = Smooth01(Mathf.InverseLerp(22f, 42f, elapsed));
                distanceMeters = 35f + Mathf.Lerp(0f, 240f, t);
                speedKts = Mathf.Lerp(24f, 64f, t);
            }
            else if (elapsed < 62f)
            {
                float t = Smooth01(Mathf.InverseLerp(42f, 62f, elapsed));
                distanceMeters = 275f + Mathf.Lerp(0f, 360f, t);
                altitudeMeters = Mathf.Lerp(0.8f, 38f, t);
                pitchUpDeg = Mathf.Lerp(4f, 8f, t);
                speedKts = Mathf.Lerp(64f, 76f, t);
                verticalMps = 3.4f;
            }
            else if (elapsed < 90f)
            {
                float t = Smooth01(Mathf.InverseLerp(62f, 90f, elapsed));
                distanceMeters = 635f + Mathf.Lerp(0f, 520f, t);
                lateralMeters = Mathf.Lerp(0f, -85f, t);
                altitudeMeters = Mathf.Lerp(38f, 85f, t);
                pitchUpDeg = Mathf.Lerp(7f, 4f, t);
                bankDeg = Mathf.Lerp(0f, -13f, t);
                yawOffsetDeg = Mathf.Lerp(0f, -12f, t);
                speedKts = 78f;
                verticalMps = 1.7f;
            }
            else if (elapsed < 115f)
            {
                float t = Smooth01(Mathf.InverseLerp(90f, 115f, elapsed));
                distanceMeters = 1155f + Mathf.Lerp(0f, 460f, t);
                lateralMeters = Mathf.Lerp(-85f, 25f, t);
                altitudeMeters = Mathf.Lerp(85f, 105f, t);
                pitchUpDeg = Mathf.Lerp(4f, 2f, t);
                bankDeg = Mathf.Lerp(-13f, 12f, t);
                yawOffsetDeg = Mathf.Lerp(-12f, 8f, t);
                speedKts = 80f;
                verticalMps = 0.8f;
            }
            else
            {
                float t = Smooth01(Mathf.InverseLerp(115f, 150f, elapsed));
                distanceMeters = 1615f + Mathf.Lerp(0f, 600f, t);
                lateralMeters = Mathf.Lerp(25f, 45f, t);
                altitudeMeters = Mathf.Lerp(105f, 115f, t);
                pitchUpDeg = Mathf.Lerp(2f, 1f, t);
                bankDeg = Mathf.Lerp(12f, 0f, t);
                yawOffsetDeg = Mathf.Lerp(8f, 10f, t);
                speedKts = 78f;
                verticalMps = 0.3f;
            }

            float yawDeg = runwayYaw + yawOffsetDeg;
            Quaternion heading = Quaternion.Euler(0f, yawDeg, 0f);
            Vector3 headingForward = Vector3.ProjectOnPlane(heading * Vector3.forward, Vector3.up).normalized;
            position = startPosition + runwayForward * distanceMeters + runwayRight * lateralMeters + Vector3.up * altitudeMeters;
            euler = new Vector3(-pitchUpDeg, yawDeg, bankDeg);
            velocityWorld = headingForward * (speedKts * AircraftUnitConversions.KnotsToMetersPerSecond) + Vector3.up * verticalMps;
            return true;
        }

        private void ResolveReferences()
        {
            if (_deterministicInput == null) _deterministicInput = FindFirstObjectByType<DeterministicGamepadInputSource>();
            if (_mapper == null) _mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (_flightTelemetry == null) _flightTelemetry = FindFirstObjectByType<FlightTelemetry>();
            if (_aircraftPhysics == null) _aircraftPhysics = FindFirstObjectByType<SimpleAircraftPhysics>();
            if (_aircraftState == null) _aircraftState = FindFirstObjectByType<AircraftState>();

            if (_deterministicInput == null)
            {
                GameObject systems = GameObject.Find("Runtime Systems") ?? new GameObject("Runtime Systems");
                _deterministicInput = systems.AddComponent<DeterministicGamepadInputSource>();
            }
        }

        private void EnableDeterministicInput()
        {
            if (_deterministicInput != null)
            {
                _deterministicInput.OverrideLiveInput = true;
            }

            if (_mapper != null)
            {
                _mapper.deterministicInput = _deterministicInput;
                _mapper.preferDeterministicInput = true;
            }
        }

        private void ApplyVisualFlightEnvelope()
        {
            if (_aircraftPhysics == null) return;
            if (!TryGetVisualFlightPoseForElapsedSeconds(_elapsed, _startPosition, _startRotation, out Vector3 position, out Vector3 euler, out Vector3 velocityWorld))
            {
                return;
            }

            _aircraftPhysics.SetStateForTest(position, euler, velocityWorld);
            _visualFlightEnvelopeApplied = true;
        }

        private DemoPilotEvidence CreateEvidence()
        {
            string dir = Path.Combine(Application.persistentDataPath, "QuestFlightLab", "demo_pilot");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"quest_demo_pilot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            return new DemoPilotEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel,
                sceneryMode = QuestLaunchOptions.SceneryMode(),
                demoMode = QuestLaunchOptions.DemoMode(),
                evidencePath = path,
                deterministicInputEnabled = _deterministicInput != null && _mapper != null && _mapper.preferDeterministicInput,
                playtestRunwayAlignmentApplied = _playtestRunwayAlignmentApplied,
                visualFlightEnvelopeApplied = _visualFlightEnvelopeApplied,
                startPosition = _startPosition,
                latestPosition = _startPosition
            };
        }

        private void AddSample(AircraftControlState controls)
        {
            if (_evidence == null) return;

            FlightTelemetrySnapshot f = _flightTelemetry != null ? _flightTelemetry.Current : new FlightTelemetrySnapshot();
            Vector3 latest = _aircraftState != null ? _aircraftState.transform.position : _evidence.latestPosition;
            _evidence.latestPosition = latest;
            _evidence.distanceFromStartMeters = Vector3.Distance(_startPosition, latest);
            _evidence.aircraftMoved = _evidence.distanceFromStartMeters > 3f;
            _evidence.aircraftAirborne = _aircraftState != null && (!_aircraftState.onGround || _aircraftState.transform.position.y > 1.8f);
            _evidence.phase = PhaseName;

            _evidence.samples.Add(new DemoPilotSample
            {
                timestamp = _elapsed,
                phase = PhaseName,
                aileron = controls.aileron,
                elevator = controls.elevator,
                rudder = controls.rudder,
                throttle = controls.throttle,
                airspeedKts = f.airspeedKts,
                altitudeFt = f.altitudeFt,
                headingDeg = f.headingDeg,
                pitchDeg = f.pitchDeg,
                bankDeg = f.bankDeg,
                onGround = f.onGround
            });
        }

        private void WriteEvidence()
        {
            if (_evidence == null) return;
            _evidence.generatedUtc = DateTime.UtcNow.ToString("O");
            _evidence.sceneryMode = QuestLaunchOptions.SceneryMode();
            _evidence.demoMode = QuestLaunchOptions.DemoMode();
            _evidence.deterministicInputEnabled = _deterministicInput != null && _mapper != null && _mapper.preferDeterministicInput;
            _evidence.playtestRunwayAlignmentApplied = _playtestRunwayAlignmentApplied;
            _evidence.visualFlightEnvelopeApplied = _visualFlightEnvelopeApplied;

            try
            {
                File.WriteAllText(_evidence.evidencePath, JsonUtility.ToJson(_evidence, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Evidence write failed: {ex.Message}");
            }
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
