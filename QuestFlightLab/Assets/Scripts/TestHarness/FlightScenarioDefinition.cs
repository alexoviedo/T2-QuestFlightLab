using System;
using System.Collections.Generic;
using QuestFlightLab.Flight;
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
        public float durationSeconds = 8f;
        public float timeStepSeconds = 1f / 72f;
        public float initialAirspeedKts;
        public float initialAltitudeFt;
        public bool startOnRunway = true;
        public bool markChecklistComplete;

        public static List<FlightScenarioDefinition> DefaultSuite()
        {
            return new List<FlightScenarioDefinition>
            {
                new FlightScenarioDefinition { id = "neutral_controls", name = "Neutral controls", purpose = "Baseline idle/taxi stability", durationSeconds = 4f, initialAirspeedKts = 0f, startOnRunway = true },
                new FlightScenarioDefinition { id = "control_surface_sweep", name = "Control surface sweep", purpose = "Full-scale mapped control deflections", durationSeconds = 8f, initialAirspeedKts = 70f, initialAltitudeFt = 800f, startOnRunway = false },
                new FlightScenarioDefinition { id = "taxi_throttle_brake", name = "Taxi throttle/brake check", purpose = "Ground acceleration and brake response", durationSeconds = 10f, initialAirspeedKts = 0f, startOnRunway = true },
                new FlightScenarioDefinition { id = "takeoff_roll", name = "Takeoff roll", purpose = "Runway acceleration toward rotation speed", durationSeconds = 18f, initialAirspeedKts = 0f, startOnRunway = true },
                new FlightScenarioDefinition { id = "rotation_climb", name = "Rotation and climb", purpose = "Rotate near placeholder Vr and establish climb", durationSeconds = 28f, initialAirspeedKts = 0f, startOnRunway = true, markChecklistComplete = true },
                new FlightScenarioDefinition { id = "shallow_turns", name = "Shallow left/right turns", purpose = "Roll response and coordinated-turn behavior", durationSeconds = 18f, initialAirspeedKts = 82f, initialAltitudeFt = 1000f, startOnRunway = false },
                new FlightScenarioDefinition { id = "rudder_yaw", name = "Rudder yaw response", purpose = "Yaw and heading response to rudder", durationSeconds = 12f, initialAirspeedKts = 75f, initialAltitudeFt = 900f, startOnRunway = false },
                new FlightScenarioDefinition { id = "flap_deployment", name = "Flap deployment effect", purpose = "Flap telemetry, lift/drag/pitch placeholders", durationSeconds = 12f, initialAirspeedKts = 78f, initialAltitudeFt = 900f, startOnRunway = false },
                new FlightScenarioDefinition { id = "trim_effect", name = "Trim effect placeholder", purpose = "Trim telemetry and pitch tendency", durationSeconds = 12f, initialAirspeedKts = 78f, initialAltitudeFt = 900f, startOnRunway = false },
                new FlightScenarioDefinition { id = "stall_approach", name = "Stall approach warning", purpose = "Low-speed/high-AoA warning placeholder", durationSeconds = 10f, initialAirspeedKts = 58f, initialAltitudeFt = 2500f, startOnRunway = false },
                new FlightScenarioDefinition { id = "runway_reset", name = "Runway reset", purpose = "Reset returns aircraft to runway start", durationSeconds = 5f, initialAirspeedKts = 45f, initialAltitudeFt = 300f, startOnRunway = false }
            };
        }

        public AircraftControlState EvaluateControls(float time)
        {
            AircraftControlState c = AircraftControlState.Neutral(0.2f);
            c.mixture = 1f;

            switch (id)
            {
                case "neutral_controls":
                    c.throttle = 0f;
                    c.leftToeBrake = 0.15f;
                    c.rightToeBrake = 0.15f;
                    break;
                case "control_surface_sweep":
                    c.throttle = 0.55f;
                    c.aileron = Mathf.Sin(time * Mathf.PI * 0.65f);
                    c.elevator = Mathf.Sin(time * Mathf.PI * 0.5f);
                    c.rudder = Mathf.Sin(time * Mathf.PI * 0.8f);
                    c.leftToeBrake = Mathf.Clamp01(Mathf.Sin(time * Mathf.PI * 0.7f));
                    c.rightToeBrake = Mathf.Clamp01(Mathf.Sin(time * Mathf.PI * 0.9f));
                    c.flaps = Mathf.PingPong(time * 0.35f, 1f);
                    c.trim = Mathf.Sin(time * Mathf.PI * 0.3f) * 0.6f;
                    break;
                case "taxi_throttle_brake":
                    c.throttle = time < 6f ? 0.38f : 0f;
                    c.rudder = time < 3f ? 0.15f : time < 6f ? -0.15f : 0f;
                    if (time > 6f)
                    {
                        c.leftToeBrake = 0.9f;
                        c.rightToeBrake = 0.9f;
                    }
                    break;
                case "takeoff_roll":
                    c.throttle = 1f;
                    c.rudder = Mathf.Sin(time * 0.6f) * 0.08f;
                    break;
                case "rotation_climb":
                    c.throttle = 1f;
                    c.rudder = Mathf.Sin(time * 0.45f) * 0.06f;
                    c.elevator = time > 11f ? 0.28f : time > 8f ? 0.16f : 0f;
                    c.trim = 0.08f;
                    break;
                case "shallow_turns":
                    c.throttle = 0.74f;
                    c.elevator = 0.12f;
                    c.aileron = time < 2.2f ? 0.16f : time < 7f ? 0.02f : time < 9.2f ? -0.2f : time < 14.5f ? -0.02f : 0.1f;
                    c.rudder = c.aileron * 0.22f;
                    break;
                case "rudder_yaw":
                    c.throttle = 0.68f;
                    c.elevator = 0.04f;
                    c.rudder = time < 5.5f ? 0.55f : -0.55f;
                    break;
                case "flap_deployment":
                    c.throttle = 0.58f;
                    c.elevator = 0.08f;
                    c.flaps = time < 3f ? 0f : time < 7f ? 0.5f : 1f;
                    break;
                case "trim_effect":
                    c.throttle = 0.62f;
                    c.trim = time < 5f ? 0.7f : -0.5f;
                    break;
                case "stall_approach":
                    c.throttle = time < 5f ? 0.22f : 0.08f;
                    c.elevator = time < 3f ? 0.28f : 0.58f;
                    c.flaps = 0.25f;
                    break;
                case "runway_reset":
                    c.throttle = 0.3f;
                    c.elevator = 0.15f;
                    break;
            }

            return c;
        }
    }
}
