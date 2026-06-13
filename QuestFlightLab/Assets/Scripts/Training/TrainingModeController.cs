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
        public LessonSequence lesson = LessonSequence.BasicTakeoffFamiliarization();

        public int CurrentStepIndex { get; private set; }
        public string CurrentLessonTitle => lesson != null ? lesson.title : "No lesson";
        public string CurrentPrompt => CurrentStep != null ? CurrentStep.prompt : "Lesson complete";
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
            lesson ??= LessonSequence.BasicTakeoffFamiliarization();
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
                    detail = $"power {f.powerPercent:F0}%";
                    return f.powerPercent > 85f;
                case "maintain_centerline":
                    detail = $"offset {f.runwayLateralOffsetMeters:F1} m rudder {c.rudder:F2}";
                    return f.onGround && f.airspeedKts > 20f && Mathf.Abs(f.runwayLateralOffsetMeters) < 12f;
                case "rotate":
                    detail = $"airspeed {f.airspeedKts:F0} kt pitch {f.pitchDeg:F1}";
                    return f.airspeedKts > 52f && f.pitchDeg > 3f;
                case "climb_vy":
                    detail = $"airspeed {f.airspeedKts:F0} kt vsi {f.verticalSpeedFpm:F0} fpm";
                    return !f.onGround && f.verticalSpeedFpm > 150f && f.airspeedKts > 60f;
                case "maintain_heading":
                case "maintain_runway_heading":
                    detail = $"heading {f.headingDeg:F0} vsi {f.verticalSpeedFpm:F0} fpm";
                    return !f.onGround && f.verticalSpeedFpm > 100f && Mathf.Abs(Mathf.DeltaAngle(f.headingDeg, 78f)) < 15f;
                case "after_takeoff_cleanup":
                    detail = $"flaps {f.flapDegrees:F0} stall {(f.stallWarning ? "warn" : "clear")}";
                    return !f.onGround && f.verticalSpeedFpm > 100f && f.flapDegrees < 12f && !f.stallWarning;
                default:
                    detail = "no evaluator";
                    return false;
            }
        }
    }
}
