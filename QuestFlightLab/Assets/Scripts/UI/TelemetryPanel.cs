using System.Text;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.Training;
using UnityEngine;

namespace QuestFlightLab.UI
{
    public class TelemetryPanel : MonoBehaviour
    {
        public GamepadInputReader reader;
        public Usb2BleInputMapper mapper;
        public FlightTelemetry flightTelemetry;
        public InputEvidenceLogger evidenceLogger;
        public TrainingModeController trainingMode;
        public TextMesh text;
        public GameObject panelRoot;

        private readonly StringBuilder _buffer = new StringBuilder(2048);

        private void Awake()
        {
            if (reader == null) reader = FindFirstObjectByType<GamepadInputReader>();
            if (mapper == null) mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (flightTelemetry == null) flightTelemetry = FindFirstObjectByType<FlightTelemetry>();
            if (evidenceLogger == null) evidenceLogger = FindFirstObjectByType<InputEvidenceLogger>();
            if (trainingMode == null) trainingMode = FindFirstObjectByType<TrainingModeController>();
            if (text == null) text = GetComponentInChildren<TextMesh>();
        }

        private void Update()
        {
            bool visible = mapper == null || mapper.TelemetryVisible;
            if (panelRoot != null && panelRoot.activeSelf != visible) panelRoot.SetActive(visible);
            if (!visible || text == null) return;

            GamepadInputSnapshot g = reader != null ? reader.Current : GamepadInputSnapshot.Disconnected(Time.unscaledTime, 0f);
            AircraftControlState c = mapper != null ? mapper.Current : AircraftControlState.Neutral();
            FlightTelemetrySnapshot f = flightTelemetry != null ? flightTelemetry.Current : new FlightTelemetrySnapshot();

            _buffer.Clear();
            _buffer.AppendLine("QUEST FLIGHT INPUT LAB v0.3 FIDELITY");
            _buffer.AppendLine("Gamepad input + deterministic simulator harness");
            _buffer.AppendLine();
            _buffer.AppendLine(g.connected
                ? $"GAMEPAD: {g.displayName}  layout={g.layout}  id={g.deviceId}"
                : "GAMEPAD: not connected");
            _buffer.AppendLine($"Product: {g.product}  Manufacturer: {g.manufacturer}");
            _buffer.AppendLine($"Sample rate: {g.sampleRateHz:F0} Hz  Last input: {g.secondsSinceLastInput:F1}s");
            _buffer.AppendLine($"Device change: {(reader != null ? reader.LastDeviceChange : "n/a")}");
            _buffer.AppendLine();
            _buffer.AppendLine($"Left stick  X {g.leftStickX,6:F2}  Y {g.leftStickY,6:F2}");
            _buffer.AppendLine($"Right stick X {g.rightStickX,6:F2}  Y {g.rightStickY,6:F2}");
            _buffer.AppendLine($"Triggers    L {g.leftTrigger,6:F2}  R {g.rightTrigger,6:F2}");
            _buffer.AppendLine($"D-pad       X {g.dpadX,6:F2}  Y {g.dpadY,6:F2}");
            _buffer.AppendLine($"Buttons     A:{OnOff(g.buttonSouth)} B:{OnOff(g.buttonEast)} X:{OnOff(g.buttonWest)} Y:{OnOff(g.buttonNorth)}");
            _buffer.AppendLine();
            _buffer.AppendLine($"Aircraft controls  ail {c.aileron,6:F2} elev {c.elevator,6:F2} rud {c.rudder,6:F2}");
            _buffer.AppendLine($"Throttle {c.throttle,5:F2}  Mix {c.mixture,4:F2}  Carb {c.carbHeat,4:F2}  brakes L/R {c.leftToeBrake,4:F2}/{c.rightToeBrake,4:F2}");
            _buffer.AppendLine($"Flaps {f.flapDegrees,4:F0} deg  trim {c.trim,5:F2}  RPM {f.engineRpm,5:F0}  Power {f.powerPercent,5:F0}%");
            _buffer.AppendLine($"Markers: {(mapper != null ? mapper.MarkerCount : 0)}  Paused: {(mapper != null && mapper.Paused ? "YES" : "NO")}");
            _buffer.AppendLine();
            _buffer.AppendLine($"Airspeed {f.airspeedKts,6:F1} kt  Alt {f.altitudeFt,7:F0} ft  VSI {f.verticalSpeedFpm,7:F0} fpm");
            _buffer.AppendLine($"HDG {f.headingDeg,6:F0}  Pitch {f.pitchDeg,6:F1}  Bank {f.bankDeg,6:F1}  AoA {f.angleOfAttackDeg,6:F1}");
            _buffer.AppendLine($"Ref {f.referenceSpeedKts,5:F0} kt  Err {f.targetSpeedErrorKts,6:F1} kt  Slip {f.slipSkid,5:F2}  Stall {f.stallIntensity,4:F2}");
            _buffer.AppendLine($"FPS {f.fps,5:F0}  G {f.loadFactorG,4:F2}  Stall warning: {(f.stallWarning ? "ON" : "off")}  Ground: {(f.onGround ? "yes" : "no")}");
            _buffer.AppendLine($"Ground roll {f.groundRollMeters,6:F0} m  Runway offset {f.runwayLateralOffsetMeters,6:F1} m");
            if (trainingMode != null)
            {
                _buffer.AppendLine();
                _buffer.AppendLine($"Lesson: {trainingMode.CurrentLessonTitle}");
                _buffer.AppendLine($"Task: {trainingMode.CurrentPrompt}");
                _buffer.AppendLine($"Eval: {trainingMode.LastEvaluation}");
            }
            _buffer.AppendLine();
            _buffer.AppendLine($"Evidence: {(evidenceLogger != null ? evidenceLogger.EvidencePath : "not started")}");

            text.text = _buffer.ToString();
        }

        private static string OnOff(bool value)
        {
            return value ? "1" : "0";
        }
    }
}
