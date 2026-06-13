using System;
using System.Collections.Generic;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class TrafficPatternPhase
    {
        public string id = "";
        public string name = "";
        public string prompt = "";
        public string successHint = "";
        public string warningHint = "";
        public float startFraction;
        public float endFraction = 1f;
        public float targetHeadingDeg = -1f;
        public float headingToleranceDeg = 30f;
        public float minAirspeedKts;
        public float maxAirspeedKts;
        public float minAltitudeFt;
        public float maxAltitudeFt;
        public float bankLimitDeg = 30f;
        public float minPitchDeg = -10f;
        public float maxPitchDeg = 18f;
        public float minVerticalSpeedFpm = -900f;
        public float maxVerticalSpeedFpm = 900f;
        public float targetFlapDeg = -1f;
        public float flapToleranceDeg = 12f;
        public bool checklistRequired;
        public float scoringWeight = 1f;

        public string TargetSummary()
        {
            string heading = targetHeadingDeg >= 0f ? $"HDG {targetHeadingDeg:000}+/-{headingToleranceDeg:0}" : "HDG managed";
            string speed = minAirspeedKts > 0f || maxAirspeedKts > 0f ? $"{minAirspeedKts:0}-{maxAirspeedKts:0} kt" : "speed managed";
            string altitude = minAltitudeFt > 0f || maxAltitudeFt > 0f ? $"{minAltitudeFt:0}-{maxAltitudeFt:0} ft" : "altitude managed";
            return $"{heading}, {speed}, {altitude}, bank < {bankLimitDeg:0}";
        }
    }

    public static class TrafficPatternLesson
    {
        public const string LessonId = "basic_traffic_pattern_familiarization";
        public const string LessonTitle = "Basic Traffic Pattern Familiarization";

        public static readonly string[] RequiredPhaseIds =
        {
            "before_takeoff",
            "line_up",
            "takeoff_roll",
            "rotate_vr",
            "upwind_climb",
            "crosswind_turn",
            "downwind_level_configure",
            "abeam_power_reduction",
            "base_turn",
            "final_alignment",
            "flare_or_go_around",
            "after_landing_reset"
        };

        public static LessonSequence BuildLessonSequence()
        {
            LessonSequence sequence = new LessonSequence
            {
                id = LessonId,
                title = LessonTitle
            };

            foreach (TrafficPatternPhase phase in BasicPhases())
            {
                sequence.steps.Add(new LessonStep(phase.id, phase.prompt, phase.successHint, 1f, phase.warningHint)
                {
                    targetAirspeedKts = Midpoint(phase.minAirspeedKts, phase.maxAirspeedKts),
                    airspeedToleranceKts = Math.Max(5f, (phase.maxAirspeedKts - phase.minAirspeedKts) * 0.5f),
                    targetHeadingDeg = phase.targetHeadingDeg,
                    headingToleranceDeg = phase.headingToleranceDeg,
                    targetAltitudeFt = Midpoint(phase.minAltitudeFt, phase.maxAltitudeFt),
                    altitudeToleranceFt = Math.Max(50f, (phase.maxAltitudeFt - phase.minAltitudeFt) * 0.5f),
                    bankLimitDeg = phase.bankLimitDeg,
                    targetFlapDeg = phase.targetFlapDeg
                });
            }

            return sequence;
        }

        public static List<TrafficPatternPhase> BasicPhases()
        {
            return new List<TrafficPatternPhase>
            {
                new TrafficPatternPhase
                {
                    id = "before_takeoff",
                    name = "Pre-takeoff checklist",
                    prompt = "Complete before-takeoff checklist placeholders",
                    successHint = "Checklist acknowledged",
                    warningHint = "Do not begin the pattern with incomplete setup",
                    startFraction = 0f,
                    endFraction = 0.06f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 20f,
                    maxAirspeedKts = 10f,
                    maxAltitudeFt = 80f,
                    checklistRequired = true
                },
                new TrafficPatternPhase
                {
                    id = "line_up",
                    name = "Line up",
                    prompt = "Line up on the approximate runway centerline",
                    successHint = "Runway heading and centerline established",
                    warningHint = "Correct drift before adding full power",
                    startFraction = 0.06f,
                    endFraction = 0.12f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 18f,
                    maxAirspeedKts = 20f,
                    maxAltitudeFt = 90f
                },
                new TrafficPatternPhase
                {
                    id = "takeoff_roll",
                    name = "Takeoff roll",
                    prompt = "Smoothly apply throttle and maintain centerline",
                    successHint = "Accelerating with controlled runway offset",
                    warningHint = "Use rudder for centerline control",
                    startFraction = 0.12f,
                    endFraction = 0.24f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 20f,
                    minAirspeedKts = 25f,
                    maxAirspeedKts = 85f,
                    maxAltitudeFt = 120f
                },
                new TrafficPatternPhase
                {
                    id = "rotate_vr",
                    name = "Rotate near Vr",
                    prompt = "Rotate near the placeholder Vr target",
                    successHint = "Positive pitch and liftoff",
                    warningHint = "Avoid over-rotation and stall warning",
                    startFraction = 0.24f,
                    endFraction = 0.34f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 25f,
                    minAirspeedKts = 52f,
                    maxAirspeedKts = 88f,
                    minAltitudeFt = 0f,
                    maxAltitudeFt = 350f,
                    maxPitchDeg = 16f
                },
                new TrafficPatternPhase
                {
                    id = "upwind_climb",
                    name = "Upwind climb",
                    prompt = "Climb out while holding runway heading",
                    successHint = "Positive climb with controlled heading",
                    warningHint = "Pitch for airspeed, not just vertical speed",
                    startFraction = 0.34f,
                    endFraction = 0.44f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 30f,
                    minAirspeedKts = 60f,
                    maxAirspeedKts = 92f,
                    minAltitudeFt = 80f,
                    maxAltitudeFt = 900f,
                    minVerticalSpeedFpm = 100f
                },
                new TrafficPatternPhase
                {
                    id = "crosswind_turn",
                    name = "Crosswind turn",
                    prompt = "Begin a shallow left crosswind turn",
                    successHint = "Bank and heading change remain controlled",
                    warningHint = "Keep bank shallow in the climb",
                    startFraction = 0.44f,
                    endFraction = 0.54f,
                    targetHeadingDeg = 350f,
                    headingToleranceDeg = 80f,
                    minAirspeedKts = 60f,
                    maxAirspeedKts = 95f,
                    minAltitudeFt = 150f,
                    maxAltitudeFt = 1100f,
                    bankLimitDeg = 28f
                },
                new TrafficPatternPhase
                {
                    id = "downwind_level_configure",
                    name = "Downwind level-off / configure",
                    prompt = "Level the downwind placeholder and configure",
                    successHint = "Power and trim reduce climb tendency",
                    warningHint = "Avoid excessive speed while configuring",
                    startFraction = 0.54f,
                    endFraction = 0.66f,
                    targetHeadingDeg = 258f,
                    headingToleranceDeg = 100f,
                    minAirspeedKts = 65f,
                    maxAirspeedKts = 105f,
                    minAltitudeFt = 300f,
                    maxAltitudeFt = 1300f,
                    targetFlapDeg = 0f
                },
                new TrafficPatternPhase
                {
                    id = "abeam_power_reduction",
                    name = "Abeam touchdown point",
                    prompt = "Reduce power abeam the touchdown-zone placeholder",
                    successHint = "Power reduction and first flap placeholder recorded",
                    warningHint = "Do not get slow before turning base",
                    startFraction = 0.66f,
                    endFraction = 0.74f,
                    targetHeadingDeg = 258f,
                    headingToleranceDeg = 110f,
                    minAirspeedKts = 60f,
                    maxAirspeedKts = 100f,
                    minAltitudeFt = 250f,
                    maxAltitudeFt = 1300f,
                    targetFlapDeg = 10f,
                    flapToleranceDeg = 12f
                },
                new TrafficPatternPhase
                {
                    id = "base_turn",
                    name = "Base turn",
                    prompt = "Turn base with controlled bank",
                    successHint = "Heading changing toward final",
                    warningHint = "Avoid steepening the turn",
                    startFraction = 0.74f,
                    endFraction = 0.84f,
                    targetHeadingDeg = 168f,
                    headingToleranceDeg = 110f,
                    minAirspeedKts = 55f,
                    maxAirspeedKts = 95f,
                    minAltitudeFt = 180f,
                    maxAltitudeFt = 1200f,
                    bankLimitDeg = 30f,
                    targetFlapDeg = 20f,
                    flapToleranceDeg = 15f
                },
                new TrafficPatternPhase
                {
                    id = "final_alignment",
                    name = "Final approach alignment",
                    prompt = "Align with the runway/final gate placeholder",
                    successHint = "Final heading and approach speed are controlled",
                    warningHint = "Go around if alignment or speed is unstable",
                    startFraction = 0.84f,
                    endFraction = 0.93f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 120f,
                    minAirspeedKts = 55f,
                    maxAirspeedKts = 90f,
                    minAltitudeFt = 80f,
                    maxAltitudeFt = 900f,
                    targetFlapDeg = 30f,
                    flapToleranceDeg = 18f
                },
                new TrafficPatternPhase
                {
                    id = "flare_or_go_around",
                    name = "Flare/touchdown or go-around placeholder",
                    prompt = "Stabilize the flare/touchdown placeholder or go around",
                    successHint = "Low-energy endpoint without stall warning",
                    warningHint = "This is not a landing lesson yet",
                    startFraction = 0.93f,
                    endFraction = 0.985f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 120f,
                    minAirspeedKts = 45f,
                    maxAirspeedKts = 85f,
                    maxAltitudeFt = 650f,
                    targetFlapDeg = 30f,
                    flapToleranceDeg = 18f,
                    scoringWeight = 0.7f
                },
                new TrafficPatternPhase
                {
                    id = "after_landing_reset",
                    name = "After-landing / reset",
                    prompt = "Reset and prepare for the next repetition",
                    successHint = "Aircraft and lesson return to a known state",
                    warningHint = "Debrief before repeating",
                    startFraction = 0.985f,
                    endFraction = 1f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 120f,
                    maxAirspeedKts = 90f,
                    maxAltitudeFt = 650f,
                    scoringWeight = 0.4f
                }
            };
        }

        public static TrafficPatternPhase FindPhase(string id)
        {
            foreach (TrafficPatternPhase phase in BasicPhases())
            {
                if (phase.id == id) return phase;
            }

            return null;
        }

        private static float Midpoint(float min, float max)
        {
            if (min <= 0f && max <= 0f) return 0f;
            if (min <= 0f) return max;
            if (max <= 0f) return min;
            return (min + max) * 0.5f;
        }
    }
}
