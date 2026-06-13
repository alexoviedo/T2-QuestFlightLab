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
                new FlightScenarioDefinition { id = "pattern_reset_retry", name = "Pattern reset/retry", purpose = "Pattern retry returns aircraft and scoring to a known state", durationSeconds = 10f, initialAirspeedKts = 62f, initialAltitudeFt = 450f, startOnRunway = false, markChecklistComplete = true, isTrafficPatternScenario = true, requiresDebrief = true }
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
            }

            return c;
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
