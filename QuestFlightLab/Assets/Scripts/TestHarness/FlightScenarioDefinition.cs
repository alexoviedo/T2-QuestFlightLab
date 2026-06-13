using System;
using System.Collections.Generic;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class FlightScenarioDefinition
    {
        public string id = "";
        public string name = "";
        public string purpose = "";
        public string expectedLessonStepId = "";
        public float durationSeconds = 8f;
        public float timeStepSeconds = 1f / 72f;
        public float initialAirspeedKts;
        public float initialAltitudeFt;
        public bool startOnRunway = true;
        public bool markChecklistComplete;
        public bool isTrafficPatternScenario;
        public bool requiresAirportPatternVerification;
        public bool requiresDebrief;
        public bool isApproachScenario;
        public bool requiresApproachDebrief;
        public bool requiresTimeline;
        public string approachProfile = "";
        public bool structuralOnlyScenario;

        public static List<FlightScenarioDefinition> DefaultSuite()
        {
            return new List<FlightScenarioDefinition>
            {
                new FlightScenarioDefinition { id = "preflight_neutral_initialization", name = "Preflight neutral initialization", purpose = "Baseline idle stability and instrument availability", durationSeconds = 4f, initialAirspeedKts = 0f, startOnRunway = true },
                new FlightScenarioDefinition { id = "before_takeoff_checklist", name = "Before-takeoff checklist state", purpose = "Checklist model and lesson scaffold verification", durationSeconds = 3f, initialAirspeedKts = 0f, startOnRunway = true, markChecklistComplete = true, expectedLessonStepId = "before_takeoff" },
                new FlightScenarioDefinition { id = "taxi_brake_check", name = "Taxi/brake check", purpose = "Ground acceleration, rudder steering, and toe-brake response", durationSeconds = 10f, initialAirspeedKts = 0f, startOnRunway = true },
                new FlightScenarioDefinition { id = "takeoff_roll_to_vr", name = "Takeoff roll to Vr", purpose = "Accelerate toward configured rotation speed while holding centerline", durationSeconds = 17f, initialAirspeedKts = 0f, startOnRunway = true, expectedLessonStepId = "smooth_throttle" },
                new FlightScenarioDefinition { id = "rotation_climb_to_altitude", name = "Rotation and climb", purpose = "Rotate near placeholder Vr and establish positive climb", durationSeconds = 28f, initialAirspeedKts = 0f, startOnRunway = true, expectedLessonStepId = "rotate" },
                new FlightScenarioDefinition { id = "vy_climb_stabilization", name = "Vy climb stabilization", purpose = "Hold a positive climb near the configured Vy placeholder", durationSeconds = 18f, initialAirspeedKts = 74f, initialAltitudeFt = 600f, startOnRunway = false, expectedLessonStepId = "climb_vy" },
                new FlightScenarioDefinition { id = "shallow_left_right_turns", name = "Shallow left/right turns", purpose = "Bank, heading change, and coordinated-turn behavior", durationSeconds = 18f, initialAirspeedKts = 82f, initialAltitudeFt = 1000f, startOnRunway = false },
                new FlightScenarioDefinition { id = "rudder_yaw_response", name = "Rudder yaw response", purpose = "Yaw and slip/skid response to rudder", durationSeconds = 12f, initialAirspeedKts = 75f, initialAltitudeFt = 900f, startOnRunway = false },
                new FlightScenarioDefinition { id = "flap_deployment_effect", name = "Flap deployment effect", purpose = "Flap telemetry plus lift/drag/pitch effects", durationSeconds = 12f, initialAirspeedKts = 78f, initialAltitudeFt = 900f, startOnRunway = false },
                new FlightScenarioDefinition { id = "trim_nose_up_down", name = "Trim effect: nose-up/nose-down", purpose = "Trim changes pitch tendency and target-speed error", durationSeconds = 14f, initialAirspeedKts = 78f, initialAltitudeFt = 1000f, startOnRunway = false },
                new FlightScenarioDefinition { id = "slow_flight_stall_warning_onset", name = "Slow flight / stall warning onset", purpose = "Slow flight produces stall warning before deep stall", durationSeconds = 12f, initialAirspeedKts = 58f, initialAltitudeFt = 2600f, startOnRunway = false },
                new FlightScenarioDefinition { id = "stall_recovery", name = "Stall recovery", purpose = "Recover from warning/onset with power and reduced AoA", durationSeconds = 18f, initialAirspeedKts = 54f, initialAltitudeFt = 3000f, startOnRunway = false },
                new FlightScenarioDefinition { id = "pattern_leg_heading_change", name = "Pattern leg heading-change placeholder", purpose = "Upwind-to-crosswind style heading change without airport procedure claims", durationSeconds = 20f, initialAirspeedKts = 82f, initialAltitudeFt = 1000f, startOnRunway = false, expectedLessonStepId = "maintain_runway_heading" },
                new FlightScenarioDefinition { id = "runway_reset", name = "Runway reset", purpose = "Reset returns aircraft to runway start", durationSeconds = 5f, initialAirspeedKts = 45f, initialAltitudeFt = 300f, startOnRunway = false },
                new FlightScenarioDefinition { id = "basic_traffic_pattern_full", name = "Basic Traffic Pattern Familiarization", purpose = "Runs a deterministic prototype pattern from pre-takeoff through reset/debrief", durationSeconds = 120f, initialAirspeedKts = 0f, startOnRunway = true, markChecklistComplete = true, isTrafficPatternScenario = true, requiresAirportPatternVerification = true, requiresDebrief = true, expectedLessonStepId = "basic_traffic_pattern" },
                new FlightScenarioDefinition { id = "traffic_pattern_phase_progression", name = "Traffic pattern phase progression", purpose = "Verifies the pattern phase model and time-window progression", durationSeconds = 52f, initialAirspeedKts = 74f, initialAltitudeFt = 650f, startOnRunway = false, markChecklistComplete = true, isTrafficPatternScenario = true, requiresDebrief = true, expectedLessonStepId = "crosswind_turn" },
                new FlightScenarioDefinition { id = "traffic_pattern_scoring_debrief", name = "Traffic pattern scoring/debrief", purpose = "Generates a scored debrief with phase warnings and score breakdown", durationSeconds = 72f, initialAirspeedKts = 70f, initialAltitudeFt = 800f, startOnRunway = false, markChecklistComplete = true, isTrafficPatternScenario = true, requiresDebrief = true, expectedLessonStepId = "final_alignment" },
                new FlightScenarioDefinition { id = "instrument_ui_verification", name = "Instrument/UI verification", purpose = "Verifies cockpit/instrument and training/debrief panel object coverage", durationSeconds = 3f, initialAirspeedKts = 0f, startOnRunway = true, structuralOnlyScenario = true },
                new FlightScenarioDefinition { id = "lesson_panel_prompt_update", name = "Lesson panel prompt update", purpose = "Checks traffic-pattern lesson prompts and target summaries are available to cockpit UI", durationSeconds = 4f, initialAirspeedKts = 0f, startOnRunway = true, markChecklistComplete = true, structuralOnlyScenario = true, expectedLessonStepId = "downwind_level_configure" },
                new FlightScenarioDefinition { id = "airport_gate_checkpoint_verification", name = "Airport gate/checkpoint verification", purpose = "Verifies approximate KBDU training gates, checkpoints, and visual references", durationSeconds = 3f, initialAirspeedKts = 0f, startOnRunway = true, requiresAirportPatternVerification = true, structuralOnlyScenario = true },
                new FlightScenarioDefinition { id = "pattern_reset_retry", name = "Pattern reset/retry", purpose = "Pattern retry returns aircraft and scoring to a known state", durationSeconds = 10f, initialAirspeedKts = 62f, initialAltitudeFt = 450f, startOnRunway = false, markChecklistComplete = true, isTrafficPatternScenario = true, requiresDebrief = true },
                new FlightScenarioDefinition { id = "stabilized_final_approach", name = "Stabilized final approach - pass case", purpose = "Meets prototype final-approach stability criteria without requiring go-around", durationSeconds = 34f, initialAirspeedKts = 68f, initialAltitudeFt = 480f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, expectedLessonStepId = "stabilized_approach_gate", approachProfile = "stable" },
                new FlightScenarioDefinition { id = "high_unstable_approach_goaround", name = "High/unstable approach - go-around required", purpose = "Detects high glide-path/altitude state and scores go-around decision", durationSeconds = 36f, initialAirspeedKts = 76f, initialAltitudeFt = 780f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, expectedLessonStepId = "go_around_decision", approachProfile = "high_goaround" },
                new FlightScenarioDefinition { id = "low_unstable_approach_goaround", name = "Low/unstable approach - go-around required", purpose = "Detects low/slow final and scores go-around decision", durationSeconds = 34f, initialAirspeedKts = 58f, initialAltitudeFt = 190f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, expectedLessonStepId = "go_around_decision", approachProfile = "low_goaround" },
                new FlightScenarioDefinition { id = "excessive_sink_rate_goaround", name = "Excessive sink rate warning/go-around", purpose = "Triggers unstable descent warning and go-around from excessive sink", durationSeconds = 34f, initialAirspeedKts = 72f, initialAltitudeFt = 430f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "sink_goaround" },
                new FlightScenarioDefinition { id = "final_speed_deviation", name = "Final speed high/low scoring deviation", purpose = "Records approach score penalty for final-approach speed errors", durationSeconds = 24f, initialAirspeedKts = 88f, initialAltitudeFt = 420f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "speed_deviation" },
                new FlightScenarioDefinition { id = "go_around_sequence", name = "Go-around sequence - power/pitch/config/climb", purpose = "Verifies power, pitch, flap sequencing placeholder, and positive climb", durationSeconds = 32f, initialAirspeedKts = 62f, initialAltitudeFt = 240f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "goaround_sequence" },
                new FlightScenarioDefinition { id = "approach_debrief_generation", name = "Approach debrief generation", purpose = "Generates approach debrief JSON/Markdown summary data", durationSeconds = 30f, initialAirspeedKts = 70f, initialAltitudeFt = 460f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "stable" },
                new FlightScenarioDefinition { id = "timeline_export_replay_markers", name = "Timeline export and replay markers", purpose = "Records replay timeline samples and approach decision markers", durationSeconds = 32f, initialAirspeedKts = 72f, initialAltitudeFt = 390f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "goaround_sequence" },
                new FlightScenarioDefinition { id = "instrument_approach_status_verification", name = "Instrument approach-status verification", purpose = "Verifies approach status, glide path, centerline, and go-around fields are present", durationSeconds = 4f, initialAirspeedKts = 0f, startOnRunway = true, structuralOnlyScenario = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "stable" },
                new FlightScenarioDefinition { id = "pattern_to_final_transition", name = "Pattern-to-final transition", purpose = "Runs base-to-final transition evidence with approach scoring", durationSeconds = 42f, initialAirspeedKts = 74f, initialAltitudeFt = 720f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, isTrafficPatternScenario = true, requiresApproachDebrief = true, requiresDebrief = true, requiresAirportPatternVerification = true, requiresTimeline = true, approachProfile = "transition" },
                new FlightScenarioDefinition { id = "stable_touchdown_placeholder", name = "Touchdown/landing placeholder if stable", purpose = "Reaches a low-altitude stable endpoint without claiming landing fidelity", durationSeconds = 36f, initialAirspeedKts = 65f, initialAltitudeFt = 260f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "touchdown" },
                new FlightScenarioDefinition { id = "reset_after_goaround", name = "Reset after go-around", purpose = "Go-around/reset returns aircraft and approach state to known endpoint", durationSeconds = 18f, initialAirspeedKts = 62f, initialAltitudeFt = 220f, startOnRunway = false, markChecklistComplete = true, isApproachScenario = true, requiresApproachDebrief = true, requiresTimeline = true, approachProfile = "reset_after_goaround" }
            };
        }

        public AircraftControlState EvaluateControls(float time)
        {
            AircraftControlState c = AircraftControlState.Neutral(0.2f);
            c.mixture = 1f;

            switch (id)
            {
                case "preflight_neutral_initialization":
                    c.throttle = 0f;
                    c.leftToeBrake = 0.2f;
                    c.rightToeBrake = 0.2f;
                    break;
                case "before_takeoff_checklist":
                    c.throttle = 0f;
                    c.leftToeBrake = 0.25f;
                    c.rightToeBrake = 0.25f;
                    c.markerPressed = time > 1f;
                    break;
                case "taxi_brake_check":
                    c.throttle = time < 6f ? 0.34f : 0f;
                    c.rudder = time < 3f ? 0.16f : time < 6f ? -0.16f : 0f;
                    if (time > 6f)
                    {
                        c.leftToeBrake = 0.9f;
                        c.rightToeBrake = 0.9f;
                    }
                    break;
                case "takeoff_roll_to_vr":
                    c.throttle = 1f;
                    c.rudder = Mathf.Sin(time * 0.55f) * 0.06f;
                    break;
                case "rotation_climb_to_altitude":
                    c.throttle = 1f;
                    c.rudder = Mathf.Sin(time * 0.42f) * 0.05f;
                    c.elevator = time > 11.5f ? 0.27f : time > 8.5f ? 0.16f : 0f;
                    c.trim = 0.1f;
                    break;
                case "vy_climb_stabilization":
                    c.throttle = 0.92f;
                    c.elevator = 0.04f;
                    c.trim = 0.16f;
                    break;
                case "shallow_left_right_turns":
                    c.throttle = 0.72f;
                    c.elevator = 0.1f;
                    c.aileron = time < 2.2f ? 0.16f : time < 7f ? 0.02f : time < 9.2f ? -0.2f : time < 14.5f ? -0.02f : 0.1f;
                    c.rudder = c.aileron * 0.25f;
                    break;
                case "rudder_yaw_response":
                    c.throttle = 0.68f;
                    c.elevator = 0.04f;
                    c.rudder = time < 5.5f ? 0.55f : -0.55f;
                    break;
                case "flap_deployment_effect":
                    c.throttle = 0.56f;
                    c.elevator = 0.07f;
                    c.flaps = time < 3f ? 0f : time < 7f ? 0.5f : 1f;
                    break;
                case "trim_nose_up_down":
                    c.throttle = 0.62f;
                    c.trim = time < 5.5f ? 0.65f : time < 10f ? -0.55f : 0.05f;
                    break;
                case "slow_flight_stall_warning_onset":
                    c.throttle = time < 5f ? 0.2f : 0.1f;
                    c.elevator = time < 4f ? 0.25f : 0.56f;
                    c.flaps = 0.25f;
                    break;
                case "stall_recovery":
                    c.throttle = time < 3.5f ? 0.12f : 1f;
                    c.elevator = time < 3.5f ? 0.62f : time < 8f ? -0.42f : 0.04f;
                    c.flaps = time < 6.5f ? 0.25f : 0f;
                    break;
                case "pattern_leg_heading_change":
                    c.throttle = 0.74f;
                    c.elevator = 0.08f;
                    c.aileron = time < 4.5f ? 0.18f : time < 12f ? 0.03f : -0.08f;
                    c.rudder = c.aileron * 0.24f;
                    break;
                case "runway_reset":
                    c.throttle = 0.3f;
                    c.elevator = 0.12f;
                    break;
                case "basic_traffic_pattern_full":
                    ApplyTrafficPatternControls(c, time, durationSeconds, false);
                    break;
                case "traffic_pattern_phase_progression":
                    ApplyTrafficPatternControls(c, time + 28f, durationSeconds + 28f, true);
                    break;
                case "traffic_pattern_scoring_debrief":
                    ApplyTrafficPatternControls(c, time + 18f, durationSeconds + 18f, true);
                    break;
                case "instrument_ui_verification":
                case "lesson_panel_prompt_update":
                case "airport_gate_checkpoint_verification":
                    c.throttle = 0f;
                    c.leftToeBrake = 0.2f;
                    c.rightToeBrake = 0.2f;
                    break;
                case "pattern_reset_retry":
                    c.throttle = time < 5f ? 0.55f : 0f;
                    c.elevator = time < 3f ? 0.05f : 0f;
                    c.aileron = time < 4f ? 0.12f : 0f;
                    c.rudder = c.aileron * 0.2f;
                    c.leftToeBrake = time > 6f ? 0.6f : 0f;
                    c.rightToeBrake = time > 6f ? 0.6f : 0f;
                    break;
                case "stabilized_final_approach":
                case "approach_debrief_generation":
                    ApplyApproachControls(c, time, "stable");
                    break;
                case "high_unstable_approach_goaround":
                    ApplyApproachControls(c, time, "high_goaround");
                    break;
                case "low_unstable_approach_goaround":
                    ApplyApproachControls(c, time, "low_goaround");
                    break;
                case "excessive_sink_rate_goaround":
                    ApplyApproachControls(c, time, "sink_goaround");
                    break;
                case "final_speed_deviation":
                    ApplyApproachControls(c, time, "speed_deviation");
                    break;
                case "go_around_sequence":
                case "timeline_export_replay_markers":
                    ApplyApproachControls(c, time, "goaround_sequence");
                    break;
                case "instrument_approach_status_verification":
                    c.throttle = 0f;
                    c.flaps = 1f;
                    c.leftToeBrake = 0.2f;
                    c.rightToeBrake = 0.2f;
                    break;
                case "pattern_to_final_transition":
                    ApplyApproachControls(c, time, "transition");
                    break;
                case "stable_touchdown_placeholder":
                    ApplyApproachControls(c, time, "touchdown");
                    break;
                case "reset_after_goaround":
                    ApplyApproachControls(c, time, "reset_after_goaround");
                    break;
            }

            return c;
        }

        private static void ApplyApproachControls(AircraftControlState c, float time, string profile)
        {
            c.mixture = 1f;
            c.carbHeat = 0f;
            c.flaps = 1f;
            c.trim = 0.08f;
            c.rudder = 0.02f;

            bool goAroundNow = profile.Contains("goaround") && time > 6f;
            if (profile == "high_goaround") goAroundNow = time > 8f;
            if (profile == "low_goaround") goAroundNow = time > 4f;
            if (profile == "sink_goaround") goAroundNow = time > 5f;
            if (profile == "reset_after_goaround") goAroundNow = time > 3f && time < 10f;

            if (goAroundNow)
            {
                c.throttle = 1f;
                c.elevator = time < 10f ? 0.16f : 0.08f;
                c.trim = 0.12f;
                c.flaps = time < 10f ? 0.66f : time < 16f ? 0.33f : 0f;
                c.rudder = 0.05f;
                return;
            }

            switch (profile)
            {
                case "stable":
                    c.throttle = 0.42f;
                    c.elevator = 0.03f;
                    c.trim = 0.07f;
                    c.flaps = 1f;
                    break;
                case "high_goaround":
                    c.throttle = 0.34f;
                    c.elevator = -0.04f;
                    c.trim = 0.03f;
                    c.flaps = 1f;
                    break;
                case "low_goaround":
                    c.throttle = 0.22f;
                    c.elevator = 0.16f;
                    c.trim = 0.1f;
                    c.flaps = 1f;
                    break;
                case "sink_goaround":
                    c.throttle = 0.12f;
                    c.elevator = -0.18f;
                    c.trim = -0.04f;
                    c.flaps = 1f;
                    break;
                case "speed_deviation":
                    c.throttle = time < 10f ? 0.8f : 0.22f;
                    c.elevator = time < 10f ? -0.03f : 0.18f;
                    c.trim = time < 10f ? 0.01f : 0.12f;
                    c.flaps = time < 12f ? 0.33f : 1f;
                    break;
                case "transition":
                    if (time < 13f)
                    {
                        c.throttle = 0.52f;
                        c.elevator = 0.02f;
                        c.aileron = -0.22f;
                        c.rudder = -0.1f;
                        c.flaps = 0.66f;
                    }
                    else
                    {
                        c.throttle = 0.4f;
                        c.elevator = 0.04f;
                        c.aileron = -0.04f;
                        c.rudder = -0.02f;
                        c.flaps = 1f;
                    }
                    break;
                case "touchdown":
                    c.throttle = time < 22f ? 0.34f : 0.12f;
                    c.elevator = time < 24f ? 0.05f : 0.16f;
                    c.trim = 0.09f;
                    c.flaps = 1f;
                    break;
                case "reset_after_goaround":
                    c.throttle = time > 10f ? 0f : 0.35f;
                    c.elevator = 0.02f;
                    c.flaps = time > 10f ? 0f : 1f;
                    c.leftToeBrake = time > 12f ? 0.8f : 0f;
                    c.rightToeBrake = time > 12f ? 0.8f : 0f;
                    break;
                default:
                    c.throttle = 0.42f;
                    c.elevator = 0.03f;
                    c.flaps = 1f;
                    break;
            }
        }

        private static void ApplyTrafficPatternControls(AircraftControlState c, float time, float duration, bool airborneStart)
        {
            float t = Mathf.Clamp(time, 0f, Mathf.Max(1f, duration));
            c.mixture = 1f;

            if (!airborneStart && t < 12f)
            {
                c.throttle = t < 4f ? 0f : 1f;
                c.leftToeBrake = t < 3f ? 0.2f : 0f;
                c.rightToeBrake = t < 3f ? 0.2f : 0f;
                c.rudder = Mathf.Sin(t * 0.55f) * 0.05f;
                return;
            }

            if (!airborneStart && t < 40f)
            {
                c.throttle = 1f;
                c.elevator = t > 17f ? 0.18f : 0.04f;
                c.rudder = Mathf.Sin(t * 0.44f) * 0.05f;
                c.trim = 0.1f;
                return;
            }

            if (t < 54f)
            {
                c.throttle = 0.9f;
                c.elevator = 0.06f;
                c.trim = 0.15f;
                c.aileron = -0.12f;
                c.rudder = -0.06f;
                return;
            }

            if (t < 70f)
            {
                c.throttle = 0.72f;
                c.elevator = 0.02f;
                c.trim = 0.05f;
                c.aileron = -0.26f;
                c.rudder = -0.12f;
                return;
            }

            if (t < 84f)
            {
                c.throttle = 0.62f;
                c.elevator = -0.01f;
                c.trim = -0.02f;
                c.aileron = -0.04f;
                c.rudder = -0.02f;
                c.flaps = 0f;
                return;
            }

            if (t < 96f)
            {
                c.throttle = 0.48f;
                c.elevator = 0.02f;
                c.trim = 0.02f;
                c.aileron = -0.22f;
                c.rudder = -0.1f;
                c.flaps = 0.33f;
                return;
            }

            if (t < 110f)
            {
                c.throttle = 0.42f;
                c.elevator = 0.05f;
                c.trim = 0.08f;
                c.aileron = -0.24f;
                c.rudder = -0.1f;
                c.flaps = 0.66f;
                return;
            }

            c.throttle = 0.46f;
            c.elevator = 0.04f;
            c.trim = 0.08f;
            c.aileron = 0.08f;
            c.rudder = 0.03f;
            c.flaps = 1f;
        }
    }
}
