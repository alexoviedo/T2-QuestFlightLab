using System.Collections.Generic;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.Training;
using UnityEngine;

namespace QuestFlightLab.UI
{
    public class CockpitInstrumentPanel : MonoBehaviour
    {
        public static readonly string[] RequiredInstrumentNames =
        {
            "Instrument_Airspeed",
            "Instrument_Altitude",
            "Instrument_VSI",
            "Instrument_Heading",
            "Instrument_Attitude",
            "Instrument_TurnSlip",
            "Instrument_RPMPower",
            "Instrument_Throttle",
            "Instrument_Flaps",
            "Instrument_Trim",
            "Instrument_StallWarning",
            "Instrument_Targets",
            "Instrument_LessonFeedback",
            "Instrument_LessonChecklist",
            "Instrument_DebriefScore",
            "Instrument_DebriefWarnings",
            "Instrument_ControlInput",
            "Instrument_YokeIndicator",
            "Instrument_RudderPedals",
            "Instrument_ToeBrakes",
            "Instrument_ApproachPhase",
            "Instrument_ApproachTargets",
            "Instrument_GlidePath",
            "Instrument_Centerline",
            "Instrument_ApproachStability",
            "Instrument_GoAround",
            "Instrument_ApproachScore"
        };

        public FlightTelemetry flightTelemetry;
        public Usb2BleInputMapper mapper;
        public TrainingModeController trainingMode;

        private readonly Dictionary<string, TextMesh> _lines = new Dictionary<string, TextMesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimePanel()
        {
            CreateOrFindPanel();
        }

        public static CockpitInstrumentPanel CreateOrFindPanel()
        {
            CockpitInstrumentPanel existing = FindFirstObjectByType<CockpitInstrumentPanel>();
            if (existing != null) return existing;

            Transform parent = Camera.main != null ? Camera.main.transform : null;
            GameObject panel = new GameObject("Cockpit Instrument Panel v0.5");
            if (parent != null)
            {
                panel.transform.SetParent(parent, false);
                panel.transform.localPosition = new Vector3(-1.05f, -0.58f, 2.05f);
                panel.transform.localRotation = Quaternion.identity;
            }
            else
            {
                panel.transform.position = new Vector3(0f, 1.6f, 2f);
            }

            CockpitInstrumentPanel instrumentPanel = panel.AddComponent<CockpitInstrumentPanel>();
            instrumentPanel.BuildTextLines();
            return instrumentPanel;
        }

        public static bool HasRequiredInstrumentObjects(out List<string> missing)
        {
            CreateOrFindPanel();
            missing = new List<string>();
            foreach (string name in RequiredInstrumentNames)
            {
                if (GameObject.Find(name) == null)
                {
                    missing.Add(name);
                }
            }
            return missing.Count == 0;
        }

        private void Awake()
        {
            EnsureBindings();
        }

        private void Update()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            EnsureBindings();
            FlightTelemetrySnapshot f = flightTelemetry != null ? flightTelemetry.Current : new FlightTelemetrySnapshot();
            AircraftControlState c = mapper != null ? mapper.Current : AircraftControlState.Neutral();
            string lesson = trainingMode != null ? trainingMode.CurrentPrompt : "lesson scaffold idle";
            string targets = trainingMode != null ? trainingMode.CurrentTargetSummary : "targets n/a";
            string feedback = trainingMode != null ? trainingMode.LastEvaluation : "feedback n/a";
            string checklist = trainingMode != null && trainingMode.checklist != null
                ? trainingMode.checklist.StatusSummary
                : "checklist n/a";
            ApproachEvaluationSnapshot approach = ApproachScoring.EvaluateTelemetrySample("stabilized_approach_gate", Time.unscaledTime, f, c);

            Set("Instrument_Airspeed", $"ASI {f.airspeedKts:000} kt / ref {f.referenceSpeedKts:000}");
            Set("Instrument_Altitude", $"ALT {f.altitudeFt:00000} ft");
            Set("Instrument_VSI", $"VSI {f.verticalSpeedFpm,5:0} fpm");
            Set("Instrument_Heading", $"HDG {f.headingDeg:000}");
            Set("Instrument_Attitude", $"ATT P {f.pitchDeg,5:0.0} B {f.bankDeg,5:0.0}");
            Set("Instrument_TurnSlip", $"TURN/SLIP {f.slipSkid,5:0.00}");
            Set("Instrument_RPMPower", $"RPM {f.engineRpm:0000} PWR {f.powerPercent:00}%");
            Set("Instrument_Throttle", $"THR {c.throttle:0.00} MIX {c.mixture:0.00} CARB {c.carbHeat:0.00}");
            Set("Instrument_Flaps", $"FLAPS {f.flapDegrees:00} deg");
            Set("Instrument_Trim", $"TRIM {c.trim,5:0.00}");
            Set("Instrument_StallWarning", $"STALL {(f.stallWarning ? "WARN" : "clear")} {f.stallIntensity:0.00}");
            Set("Instrument_Targets", $"TARGET {targets}");
            Set("Instrument_LessonFeedback", $"FEEDBACK {feedback}");
            Set("Instrument_LessonChecklist", $"TASK {lesson} / {checklist}");
            Set("Instrument_DebriefScore", "DEBRIEF score pending scenario run");
            Set("Instrument_DebriefWarnings", "WARNINGS none in live panel");
            Set("Instrument_ControlInput", $"CTL A {c.aileron:0.00} E {c.elevator:0.00} R {c.rudder:0.00}");
            Set("Instrument_YokeIndicator", $"YOKE roll {c.aileron:0.00} pitch {c.elevator:0.00}");
            Set("Instrument_RudderPedals", $"PEDALS rudder {c.rudder:0.00}");
            Set("Instrument_ToeBrakes", $"BRAKES L {c.leftToeBrake:0.00} R {c.rightToeBrake:0.00}");
            Set("Instrument_ApproachPhase", $"APP PHASE {approach.phaseId}");
            Set("Instrument_ApproachTargets", $"APP TGT {approach.targetAirspeedKts:0} kt / {approach.targetDescentRateFpm:0} fpm");
            Set("Instrument_GlidePath", $"GLIDE DEV {approach.glidePathDeviationDeg,5:0.0} deg");
            Set("Instrument_Centerline", $"CENTERLINE {approach.centerlineDeviationMeters,6:0.0} m");
            Set("Instrument_ApproachStability", $"STABLE {(approach.stable ? "YES" : "NO")} {approach.warningSummary}");
            Set("Instrument_GoAround", $"GO AROUND {(approach.goAroundRequired ? "REQ" : "no")} / {(approach.goAroundInitiated ? "active" : "standby")}");
            Set("Instrument_ApproachScore", $"APP SCORE {approach.score:000}");
        }

        private void EnsureBindings()
        {
            if (_lines.Count == 0) BuildTextLines();
            if (flightTelemetry == null) flightTelemetry = FindFirstObjectByType<FlightTelemetry>();
            if (mapper == null) mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (trainingMode == null) trainingMode = FindFirstObjectByType<TrainingModeController>();
        }

        private void BuildTextLines()
        {
            _lines.Clear();
            for (int i = 0; i < RequiredInstrumentNames.Length; i++)
            {
                TextMesh text = GetOrCreateLine(RequiredInstrumentNames[i], i);
                _lines[RequiredInstrumentNames[i]] = text;
            }
        }

        private TextMesh GetOrCreateLine(string name, int index)
        {
            Transform child = transform.Find(name);
            if (child != null && child.TryGetComponent(out TextMesh existing))
            {
                child.localPosition = new Vector3(0f, -index * 0.055f, 0f);
                return existing;
            }

            return CreateLine(name, index);
        }

        private TextMesh CreateLine(string name, int index)
        {
            GameObject line = new GameObject(name);
            line.transform.SetParent(transform, false);
            line.transform.localPosition = new Vector3(0f, -index * 0.055f, 0f);
            TextMesh text = line.AddComponent<TextMesh>();
            text.text = name.Replace("Instrument_", "");
            text.anchor = TextAnchor.UpperLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 24;
            text.characterSize = 0.016f;
            text.lineSpacing = 0.9f;
            text.color = new Color(0.78f, 0.95f, 0.78f);
            return text;
        }

        private void Set(string key, string value)
        {
            if (_lines.TryGetValue(key, out TextMesh text) && text != null)
            {
                text.text = value;
            }
        }
    }
}
