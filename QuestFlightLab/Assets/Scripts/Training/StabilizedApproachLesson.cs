using System;
using System.Collections.Generic;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class ApproachGate
    {
        public string id = "";
        public string name = "";
        public float altitudeAglFt = 300f;
        public float maxGlidePathDeviationDeg = 1.2f;
        public float maxCenterlineDeviationMeters = 12f;
        public bool goAroundIfUnstable = true;
    }

    [Serializable]
    public class ApproachPhase
    {
        public string id = "";
        public string name = "";
        public string prompt = "";
        public string successHint = "";
        public string warningHint = "";
        public float startFraction;
        public float endFraction = 1f;
        public float targetHeadingDeg = 78f;
        public float headingToleranceDeg = 20f;
        public float minAirspeedKts = 60f;
        public float maxAirspeedKts = 75f;
        public float minAltitudeFt;
        public float maxAltitudeFt = 1200f;
        public float minVerticalSpeedFpm = -1000f;
        public float maxVerticalSpeedFpm = -250f;
        public float bankLimitDeg = 15f;
        public float targetFlapDeg = 30f;
        public float flapToleranceDeg = 12f;
        public float maxGlidePathDeviationDeg = 1.2f;
        public float maxCenterlineDeviationMeters = 12f;
        public bool checklistRequired;
        public bool stableGate;
        public bool goAroundDecisionPhase;
        public bool landingPhase;
        public bool resetPhase;
        public float scoringWeight = 1f;

        public string TargetSummary()
        {
            string speed = $"{minAirspeedKts:0}-{maxAirspeedKts:0} kt";
            string altitude = minAltitudeFt > 0f || maxAltitudeFt > 0f ? $"{minAltitudeFt:0}-{maxAltitudeFt:0} ft" : "altitude managed";
            string vsi = $"{minVerticalSpeedFpm:0} to {maxVerticalSpeedFpm:0} fpm";
            return $"HDG {targetHeadingDeg:000}+/-{headingToleranceDeg:0}, {speed}, {altitude}, VSI {vsi}, bank < {bankLimitDeg:0}";
        }
    }

    public static class StabilizedApproachLesson
    {
        public const string LessonId = "stabilized_approach_go_around_familiarization";
        public const string LessonTitle = "Stabilized Approach + Go-Around Familiarization";

        public static readonly ApproachGate StableGate300Agl = new ApproachGate
        {
            id = "stable_gate_300_agl",
            name = "300 ft AGL stabilized approach gate",
            altitudeAglFt = 300f,
            maxGlidePathDeviationDeg = 1.2f,
            maxCenterlineDeviationMeters = 12f,
            goAroundIfUnstable = true
        };

        public static readonly string[] RequiredPhaseIds =
        {
            "downwind_stabilized_setup",
            "abeam_touchdown_power_reduction",
            "base_turn",
            "final_intercept",
            "stabilized_approach_gate",
            "continue_landing_decision",
            "unstable_approach_warning",
            "go_around_decision",
            "go_around_power_pitch_config",
            "climbout_rejoin_upwind",
            "landing_touchdown_placeholder",
            "after_landing_or_reset"
        };

        public static LessonSequence BuildLessonSequence()
        {
            LessonSequence sequence = new LessonSequence
            {
                id = LessonId,
                title = LessonTitle
            };

            foreach (ApproachPhase phase in BasicPhases())
            {
                sequence.steps.Add(new LessonStep(phase.id, phase.prompt, phase.successHint, 1f, phase.warningHint)
                {
                    targetAirspeedKts = Midpoint(phase.minAirspeedKts, phase.maxAirspeedKts),
                    airspeedToleranceKts = Math.Max(5f, (phase.maxAirspeedKts - phase.minAirspeedKts) * 0.5f),
                    targetHeadingDeg = phase.targetHeadingDeg,
                    headingToleranceDeg = phase.headingToleranceDeg,
                    targetAltitudeFt = Midpoint(phase.minAltitudeFt, phase.maxAltitudeFt),
                    altitudeToleranceFt = Math.Max(75f, (phase.maxAltitudeFt - phase.minAltitudeFt) * 0.5f),
                    bankLimitDeg = phase.bankLimitDeg,
                    targetFlapDeg = phase.targetFlapDeg
                });
            }

            return sequence;
        }

        public static List<ApproachPhase> BasicPhases()
        {
            return new List<ApproachPhase>
            {
                new ApproachPhase
                {
                    id = "downwind_stabilized_setup",
                    name = "Downwind stabilized setup",
                    prompt = "Configure on downwind for a controlled descent",
                    successHint = "Speed, trim, and power are ready before descending",
                    warningHint = "Do not rush final configuration from an unstable downwind",
                    startFraction = 0f,
                    endFraction = 0.12f,
                    targetHeadingDeg = 258f,
                    headingToleranceDeg = 70f,
                    minAirspeedKts = 70f,
                    maxAirspeedKts = 95f,
                    minAltitudeFt = 700f,
                    maxAltitudeFt = 1250f,
                    minVerticalSpeedFpm = -400f,
                    maxVerticalSpeedFpm = 300f,
                    bankLimitDeg = 20f,
                    targetFlapDeg = 0f,
                    checklistRequired = true
                },
                new ApproachPhase
                {
                    id = "abeam_touchdown_power_reduction",
                    name = "Abeam touchdown / power reduction",
                    prompt = "Reduce power abeam the touchdown-zone placeholder",
                    successHint = "Descent begins with controlled speed",
                    warningHint = "Avoid getting slow before base",
                    startFraction = 0.12f,
                    endFraction = 0.22f,
                    targetHeadingDeg = 258f,
                    headingToleranceDeg = 90f,
                    minAirspeedKts = 65f,
                    maxAirspeedKts = 90f,
                    minAltitudeFt = 550f,
                    maxAltitudeFt = 1150f,
                    minVerticalSpeedFpm = -800f,
                    maxVerticalSpeedFpm = 100f,
                    bankLimitDeg = 20f,
                    targetFlapDeg = 10f
                },
                new ApproachPhase
                {
                    id = "base_turn",
                    name = "Base turn",
                    prompt = "Turn base with controlled bank and descent",
                    successHint = "Bank and speed remain within approach limits",
                    warningHint = "Steep, slow base turns are unstable",
                    startFraction = 0.22f,
                    endFraction = 0.34f,
                    targetHeadingDeg = 168f,
                    headingToleranceDeg = 100f,
                    minAirspeedKts = 62f,
                    maxAirspeedKts = 85f,
                    minAltitudeFt = 400f,
                    maxAltitudeFt = 1000f,
                    minVerticalSpeedFpm = -900f,
                    maxVerticalSpeedFpm = 100f,
                    bankLimitDeg = 28f,
                    targetFlapDeg = 20f
                },
                new ApproachPhase
                {
                    id = "final_intercept",
                    name = "Final intercept",
                    prompt = "Intercept final and align with extended centerline",
                    successHint = "Final heading, centerline, and descent are controlled",
                    warningHint = "Correct alignment early or go around",
                    startFraction = 0.34f,
                    endFraction = 0.48f,
                    targetHeadingDeg = 78f,
                    headingToleranceDeg = 30f,
                    minAirspeedKts = 60f,
                    maxAirspeedKts = 78f,
                    minAltitudeFt = 250f,
                    maxAltitudeFt = 850f,
                    minVerticalSpeedFpm = -1000f,
                    maxVerticalSpeedFpm = -200f,
                    targetFlapDeg = 20f,
                    maxCenterlineDeviationMeters = 24f
                },
                new ApproachPhase
                {
                    id = "stabilized_approach_gate",
                    name = "Stabilized approach gate",
                    prompt = "Meet the 300 ft AGL stable-approach gate or go around",
                    successHint = "Stable gate passed",
                    warningHint = "Go around if speed, path, configuration, or alignment are unstable",
                    startFraction = 0.48f,
                    endFraction = 0.58f,
                    minAirspeedKts = 60f,
                    maxAirspeedKts = 75f,
                    minAltitudeFt = 220f,
                    maxAltitudeFt = 430f,
                    minVerticalSpeedFpm = -950f,
                    maxVerticalSpeedFpm = -350f,
                    targetFlapDeg = 30f,
                    stableGate = true,
                    scoringWeight = 1.4f
                },
                new ApproachPhase
                {
                    id = "continue_landing_decision",
                    name = "Continue-to-landing decision",
                    prompt = "Continue only if the approach remains stable",
                    successHint = "Continue decision matches stable criteria",
                    warningHint = "Continuing unstable is scored as a decision error",
                    startFraction = 0.58f,
                    endFraction = 0.66f,
                    minAirspeedKts = 58f,
                    maxAirspeedKts = 75f,
                    minAltitudeFt = 120f,
                    maxAltitudeFt = 340f,
                    minVerticalSpeedFpm = -1000f,
                    maxVerticalSpeedFpm = -250f,
                    targetFlapDeg = 30f,
                    scoringWeight = 1.2f
                },
                new ApproachPhase
                {
                    id = "unstable_approach_warning",
                    name = "Unstable approach warning",
                    prompt = "Identify unstable criteria before descending lower",
                    successHint = "Unstable condition flagged",
                    warningHint = "Do not normalize unstable low-altitude approaches",
                    startFraction = 0.66f,
                    endFraction = 0.72f,
                    minAirspeedKts = 58f,
                    maxAirspeedKts = 78f,
                    minAltitudeFt = 80f,
                    maxAltitudeFt = 300f,
                    minVerticalSpeedFpm = -1000f,
                    maxVerticalSpeedFpm = -200f,
                    targetFlapDeg = 30f,
                    scoringWeight = 0.8f
                },
                new ApproachPhase
                {
                    id = "go_around_decision",
                    name = "Go-around decision",
                    prompt = "Initiate go-around when the gate or warning requires it",
                    successHint = "Decision matches the stable/unstable state",
                    warningHint = "Delayed go-around is a decision error in this prototype",
                    startFraction = 0.72f,
                    endFraction = 0.80f,
                    minAirspeedKts = 55f,
                    maxAirspeedKts = 85f,
                    minAltitudeFt = 60f,
                    maxAltitudeFt = 360f,
                    minVerticalSpeedFpm = -1200f,
                    maxVerticalSpeedFpm = 700f,
                    goAroundDecisionPhase = true,
                    scoringWeight = 1.3f
                },
                new ApproachPhase
                {
                    id = "go_around_power_pitch_config",
                    name = "Go-around power/pitch/configuration",
                    prompt = "Apply power, set climb attitude, and retract flaps in stages",
                    successHint = "Power and positive climb are established",
                    warningHint = "Avoid abrupt full-flap retraction while slow",
                    startFraction = 0.80f,
                    endFraction = 0.90f,
                    minAirspeedKts = 55f,
                    maxAirspeedKts = 90f,
                    minAltitudeFt = 80f,
                    maxAltitudeFt = 600f,
                    minVerticalSpeedFpm = 100f,
                    maxVerticalSpeedFpm = 1600f,
                    targetFlapDeg = 10f,
                    flapToleranceDeg = 18f,
                    goAroundDecisionPhase = true
                },
                new ApproachPhase
                {
                    id = "climbout_rejoin_upwind",
                    name = "Climb-out / rejoin upwind placeholder",
                    prompt = "Climb out and rejoin the upwind placeholder",
                    successHint = "Positive climb and runway heading recovered",
                    warningHint = "Pitch for safe airspeed during climb-out",
                    startFraction = 0.90f,
                    endFraction = 0.96f,
                    minAirspeedKts = 60f,
                    maxAirspeedKts = 95f,
                    minAltitudeFt = 100f,
                    maxAltitudeFt = 900f,
                    minVerticalSpeedFpm = 100f,
                    maxVerticalSpeedFpm = 1800f,
                    targetFlapDeg = 0f,
                    flapToleranceDeg = 20f
                },
                new ApproachPhase
                {
                    id = "landing_touchdown_placeholder",
                    name = "Landing flare/touchdown placeholder",
                    prompt = "If stable, transition to the landing placeholder",
                    successHint = "Low-altitude stable endpoint reached",
                    warningHint = "This is not a validated landing model",
                    startFraction = 0.80f,
                    endFraction = 0.96f,
                    minAirspeedKts = 45f,
                    maxAirspeedKts = 75f,
                    minAltitudeFt = 0f,
                    maxAltitudeFt = 220f,
                    minVerticalSpeedFpm = -700f,
                    maxVerticalSpeedFpm = 250f,
                    targetFlapDeg = 30f,
                    landingPhase = true,
                    scoringWeight = 0.8f
                },
                new ApproachPhase
                {
                    id = "after_landing_or_reset",
                    name = "After-landing or reset",
                    prompt = "Reset or prepare for another pattern repetition",
                    successHint = "State returns to a known endpoint",
                    warningHint = "Use the debrief before repeating",
                    startFraction = 0.96f,
                    endFraction = 1f,
                    minAirspeedKts = 0f,
                    maxAirspeedKts = 85f,
                    minAltitudeFt = 0f,
                    maxAltitudeFt = 500f,
                    minVerticalSpeedFpm = -1000f,
                    maxVerticalSpeedFpm = 1500f,
                    resetPhase = true,
                    scoringWeight = 0.4f
                }
            };
        }

        public static ApproachPhase FindPhase(string id)
        {
            foreach (ApproachPhase phase in BasicPhases())
            {
                if (phase.id == id) return phase;
            }

            return null;
        }

        public static ApproachPhase PhaseAt(float timestamp, float durationSeconds)
        {
            float fraction = durationSeconds <= 0.01f ? 0f : Clamp01(timestamp / durationSeconds);
            foreach (ApproachPhase phase in BasicPhases())
            {
                if (fraction >= phase.startFraction && fraction <= phase.endFraction)
                {
                    return phase;
                }
            }

            return BasicPhases()[^1];
        }

        private static float Midpoint(float min, float max)
        {
            if (min <= 0f && max <= 0f) return 0f;
            if (min <= 0f) return max;
            if (max <= 0f) return min;
            return (min + max) * 0.5f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
