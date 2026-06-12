using System;
using System.IO;
using QuestFlightLab.Flight;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Input
{
    public class InputEvidenceLogger : MonoBehaviour
    {
        public GamepadInputReader reader;
        public Usb2BleInputMapper mapper;
        public FlightTelemetry flightTelemetry;
        public float sampleIntervalSeconds = 0.1f;
        public int maxSamples = 6000;

        public string EvidencePath { get; private set; }
        public InputEvidenceSession Session { get; private set; }

        private float _nextSampleTime;
        private float _nextFlushTime;
        private GamepadInputSnapshot _lastGamepad;

        private void Awake()
        {
            if (reader == null) reader = FindFirstObjectByType<GamepadInputReader>();
            if (mapper == null) mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (flightTelemetry == null) flightTelemetry = FindFirstObjectByType<FlightTelemetry>();
        }

        private void Start()
        {
            string dir = Path.Combine(Application.persistentDataPath, "QuestFlightLab", "evidence");
            Directory.CreateDirectory(dir);
            EvidencePath = Path.Combine(dir, $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            Session = new InputEvidenceSession
            {
                startedUtc = DateTime.UtcNow.ToString("o"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                evidencePath = EvidencePath
            };

            AddEvent("session_started", "Quest Flight Input Lab evidence logging started");
            Flush();
            Debug.Log($"[QuestFlightLab][Evidence] Writing input session to {EvidencePath}");
        }

        private void Update()
        {
            if (Session == null) return;

            GamepadInputSnapshot gamepad = reader != null ? reader.Current : null;
            AircraftControlState controls = mapper != null ? mapper.Current : null;
            FlightTelemetrySnapshot flight = flightTelemetry != null ? flightTelemetry.Current : null;

            UpdateObservedControls(gamepad);
            DetectEvents(gamepad, controls);

            if (Time.unscaledTime >= _nextSampleTime)
            {
                _nextSampleTime = Time.unscaledTime + Mathf.Max(0.02f, sampleIntervalSeconds);
                AddSample(gamepad, controls, flight);
            }

            if (Time.unscaledTime >= _nextFlushTime)
            {
                _nextFlushTime = Time.unscaledTime + 2f;
                Flush();
            }

            _lastGamepad = gamepad;
        }

        private void OnApplicationPause(bool pause)
        {
            AddEvent(pause ? "app_pause" : "app_resume", "");
            Flush();
        }

        private void OnApplicationQuit()
        {
            AddEvent("session_ended", "");
            Flush();
        }

        public void AddEvent(string kind, string detail)
        {
            if (Session == null) return;
            Session.events.Add(new InputEvidenceEvent
            {
                timestamp = Time.unscaledTime,
                kind = kind,
                detail = detail ?? ""
            });
            Debug.Log($"[QuestFlightLab][Evidence] {kind}: {detail}");
        }

        private void AddSample(GamepadInputSnapshot gamepad, AircraftControlState controls, FlightTelemetrySnapshot flight)
        {
            if (Session.samples.Count >= maxSamples)
            {
                Session.samples.RemoveAt(0);
            }

            Session.samples.Add(new InputEvidenceSample
            {
                timestamp = Time.unscaledTime,
                gamepad = gamepad,
                controls = controls,
                flight = flight
            });
        }

        private void DetectEvents(GamepadInputSnapshot gamepad, AircraftControlState controls)
        {
            if (gamepad == null) return;

            if (_lastGamepad == null || gamepad.deviceId != _lastGamepad.deviceId || gamepad.connected != _lastGamepad.connected)
            {
                string description = $"{gamepad.displayName} layout={gamepad.layout} manufacturer={gamepad.manufacturer} product={gamepad.product} interface={gamepad.interfaceName}";
                Session.activeGamepadName = gamepad.displayName;
                Session.activeGamepadLayout = gamepad.layout;
                Session.activeGamepadDescription = description;
                AddEvent(gamepad.connected ? "gamepad_connected" : "gamepad_disconnected", description);
            }

            if (controls == null) return;
            if (controls.markerPressed) AddEvent("marker", $"marker_count={mapper?.MarkerCount ?? 0}");
            if (controls.resetPressed) AddEvent("reset_requested", "B button or keyboard reset");
            if (controls.pausePressed) AddEvent("pause_toggle", $"paused={mapper?.Paused ?? false}");
            if (controls.telemetryTogglePressed) AddEvent("telemetry_toggle", $"visible={mapper?.TelemetryVisible ?? true}");
        }

        private void UpdateObservedControls(GamepadInputSnapshot gamepad)
        {
            if (gamepad == null || !gamepad.connected) return;

            AddObservedAxis("leftStickX", gamepad.leftStickX);
            AddObservedAxis("leftStickY", gamepad.leftStickY);
            AddObservedAxis("rightStickX", gamepad.rightStickX);
            AddObservedAxis("rightStickY", gamepad.rightStickY);
            AddObservedAxis("leftTrigger", gamepad.leftTrigger);
            AddObservedAxis("rightTrigger", gamepad.rightTrigger);
            AddObservedAxis("dpadX", gamepad.dpadX);
            AddObservedAxis("dpadY", gamepad.dpadY);

            AddObservedButton("A/buttonSouth", gamepad.buttonSouth);
            AddObservedButton("B/buttonEast", gamepad.buttonEast);
            AddObservedButton("X/buttonWest", gamepad.buttonWest);
            AddObservedButton("Y/buttonNorth", gamepad.buttonNorth);
            AddObservedButton("leftShoulder", gamepad.leftShoulder);
            AddObservedButton("rightShoulder", gamepad.rightShoulder);
            AddObservedButton("start", gamepad.startButton);
            AddObservedButton("select", gamepad.selectButton);
        }

        private void AddObservedAxis(string label, float value)
        {
            if (Mathf.Abs(value) <= 0.02f || Session.axesObserved.Contains(label)) return;
            Session.axesObserved.Add(label);
        }

        private void AddObservedButton(string label, bool value)
        {
            if (!value || Session.buttonsObserved.Contains(label)) return;
            Session.buttonsObserved.Add(label);
        }

        private void Flush()
        {
            if (Session == null || string.IsNullOrEmpty(EvidencePath)) return;

            try
            {
                File.WriteAllText(EvidencePath, JsonUtility.ToJson(Session, true));
            }
            catch (Exception ex)
            {
                Session.errors.Add($"{Time.unscaledTime:F2} flush failed: {ex.GetType().Name}: {ex.Message}");
                Debug.LogError($"[QuestFlightLab][Evidence] Flush failed: {ex}");
            }
        }
    }
}

