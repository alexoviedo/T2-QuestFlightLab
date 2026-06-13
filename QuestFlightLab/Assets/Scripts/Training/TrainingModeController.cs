using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Training
{
    public class TrainingModeController : MonoBehaviour
    {
        public Usb2BleInputMapper mapper;
        public FlightTelemetry flightTelemetry;
        public ChecklistController checklist;
        public LessonSequence lesson = LessonSequence.BasicTrafficPatternFamiliarization();

        public int CurrentStepIndex { get; private set; }
        public string CurrentLessonTitle => lesson != null ? lesson.title : "No lesson";
        public string CurrentPrompt => CurrentStep != null ? CurrentStep.prompt : "Lesson complete";
        public string CurrentStepId => CurrentStep != null ? CurrentStep.id : "complete";
        public string CurrentTargetSummary => CurrentStep != null ? BuildTargetSummary(CurrentStep) : "No active target";
        public string LastEvaluation { get; private set; } = "waiting";
        public bool IsLessonComplete => lesson == null || CurrentStepIndex >= lesson.steps.Count;

        private float _stepStartTime;

        private LessonStep CurrentStep => lesson != null && CurrentStepIndex >= 0 && CurrentStepIndex < lesson.steps.Count
            ? lesson.steps[CurrentStepIndex]
            : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureTrainingController()
        {
            if (FindFirstObjectByType<TrainingModeController>() != null) return;

            GameObject go = new GameObject("Training Mode Controller");
            go.AddComponent<ChecklistController>();
            go.AddComponent<TrainingModeController>();
        }

        private void Awake()
        {
            if (mapper == null) mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (flightTelemetry == null) flightTelemetry = FindFirstObjectByType<FlightTelemetry>();
            if (checklist == null) checklist = GetComponent<ChecklistController>() ?? FindFirstObjectByType<ChecklistController>();
            lesson ??= LessonSequence.BasicTrafficPatternFamiliarization();
            _stepStartTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (IsLessonComplete) return;

            AircraftControlState controls = mapper != null ? mapper.Current : AircraftControlState.Neutral();
            FlightTelemetrySnapshot flight = flightTelemetry != null ? flightTelemetry.Current : new FlightTelemetrySnapshot();
            LessonStep step = CurrentStep;
            bool passed = EvaluateStep(step.id, controls, flight, out string detail);
            LastEvaluation = detail;

            if (passed && Time.unscaledTime - _stepStartTime >= Mathf.Max(0.5f, step.minimumSeconds))
            {
                CurrentStepIndex++;
                _stepStartTime = Time.unscaledTime;
            }
        }

        public void ResetLesson()
        {
            CurrentStepIndex = 0;
            _stepStartTime = Time.unscaledTime;
            LastEvaluation = "reset";
        }

        private bool EvaluateStep(string id, AircraftControlState c, FlightTelemetrySnapshot f, out string detail)
        {
            switch (id)
            {
                case "before_takeoff":
                    bool checklistDone = checklist != null && checklist.IsComplete;
                    detail = checklistDone ? "checklist placeholder complete" : "waiting for checklist placeholder";
                    return checklistDone;
                case "align_runway":
                case "line_up":
                    detail = $"runway offset {f.runwayLateralOffsetMeters:F1} m";
                    return f.onGround && Mathf.Abs(f.runwayLateralOffsetMeters) < 8f;
                case "smooth_throttle":
                case "takeoff_roll":
                    detail = $"power {f.powerPercent:F0}%";
                    return f.powerPercent > 85f && (id == "smooth_throttle" || f.airspeedKts > 25f);
                case "maintain_centerline":
                    detail = $"offset {f.runwayLateralOffsetMeters:F1} m rudder {c.rudder:F2}";
                    return f.onGround && f.airspeedKts > 20f && Mathf.Abs(f.runwayLateralOffsetMeters) < 12f;
                case "rotate":
                case "rotate_vr":
                    detail = $"airspeed {f.airspeedKts:F0} kt pitch {f.pitchDeg:F1}";
                    return f.airspeedKts > 52f && f.pitchDeg > 3f;
                case "climb_vy":
                case "upwind_climb":
                    detail = $"airspeed {f.airspeedKts:F0} kt vsi {f.verticalSpeedFpm:F0} fpm";
                    return !f.onGround && f.verticalSpeedFpm > 150f && f.airspeedKts > 60f;
                case "maintain_heading":
                case "maintain_runway_heading":
                    detail = $"heading {f.headingDeg:F0} vsi {f.verticalSpeedFpm:F0} fpm";
                    return !f.onGround && f.verticalSpeedFpm > 100f && Mathf.Abs(Mathf.DeltaAngle(f.headingDeg, 78f)) < 15f;
                case "crosswind_turn":
                    detail = $"heading {f.headingDeg:F0} bank {f.bankDeg:F1}";
                    return !f.onGround && Mathf.Abs(f.bankDeg) > 4f && Mathf.Abs(f.bankDeg) < 32f;
                case "downwind_level_configure":
                    detail = $"airspeed {f.airspeedKts:F0} kt altitude {f.altitudeFt:F0} ft";
                    return !f.onGround && f.airspeedKts > 60f && f.airspeedKts < 110f;
                case "abeam_power_reduction":
                    detail = $"power {f.powerPercent:F0}% flaps {f.flapDegrees:F0}";
                    return !f.onGround && f.powerPercent < 75f && f.flapDegrees >= 0f;
                case "base_turn":
                    detail = $"bank {f.bankDeg:F1} flaps {f.flapDegrees:F0}";
                    return !f.onGround && Mathf.Abs(f.bankDeg) < 35f && f.flapDegrees >= 8f;
                case "final_alignment":
                    detail = $"heading {f.headingDeg:F0} speed {f.airspeedKts:F0} kt";
                    return !f.onGround && f.airspeedKts > 50f && f.airspeedKts < 95f;
                case "flare_or_go_around":
                    detail = $"altitude {f.altitudeFt:F0} stall {(f.stallWarning ? "warn" : "clear")}";
                    return f.airspeedKts > 40f && !f.stallWarning;
                case "after_takeoff_cleanup":
                case "after_landing_reset":
                    detail = $"flaps {f.flapDegrees:F0} stall {(f.stallWarning ? "warn" : "clear")}";
                    return id == "after_landing_reset" || (!f.onGround && f.verticalSpeedFpm > 100f && f.flapDegrees < 12f && !f.stallWarning);
                default:
                    detail = "no evaluator";
                    return false;
            }
        }

        private static string BuildTargetSummary(LessonStep step)
        {
            string speed = step.targetAirspeedKts > 0f ? $"SPD {step.targetAirspeedKts:0}+/-{step.airspeedToleranceKts:0}" : "SPD managed";
            string heading = step.targetHeadingDeg >= 0f ? $"HDG {step.targetHeadingDeg:000}+/-{step.headingToleranceDeg:0}" : "HDG managed";
            string altitude = step.targetAltitudeFt > 0f ? $"ALT {step.targetAltitudeFt:0}+/-{step.altitudeToleranceFt:0}" : "ALT managed";
            string bank = step.bankLimitDeg > 0f ? $"BANK < {step.bankLimitDeg:0}" : "BANK managed";
            return $"{speed} {heading} {altitude} {bank}";
        }
    }
}
