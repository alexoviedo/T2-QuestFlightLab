using System;
using System.Collections.Generic;
using QuestFlightLab.Flight;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.TestHarness
{
    public static class FlightScenarioRunner
    {
        public static FlightScenarioSuiteResult RunDefaultSuite(string outputDirectory = null, string metaXrSimulatorStatus = "Not installed/detected; editor scenario fallback used")
        {
            FlightScenarioSuiteResult suite = new FlightScenarioSuiteResult
            {
                startedUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                metaXrSimulatorStatus = metaXrSimulatorStatus,
                fixedTimeStepSeconds = 1f / 72f,
                limitations =
                {
                    "Editor scenario runner does not prove Quest Bluetooth, USB2BLE hardware, or headset runtime behavior.",
                    "Flight dynamics are a prototype C172-style approximation, not validated C172 fidelity.",
                    "Training prompts are a scaffold and do not provide real pilot-training credit.",
                    "Gaussian splat rendering is not part of this simulator evidence."
                }
            };

            foreach (FlightScenarioDefinition definition in FlightScenarioDefinition.DefaultSuite())
            {
                FlightScenarioResult result = RunScenario(definition);
                suite.scenarios.Add(result);
                suite.simulatedSeconds += definition.durationSeconds;
                if (result.passed) suite.passedCount++;
            }

            suite.scenarioCount = suite.scenarios.Count;
            suite.failedCount = suite.scenarioCount - suite.passedCount;

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                SimulatorEvidenceExporter.ExportSuite(suite, outputDirectory);
            }

            return suite;
        }

        public static FlightScenarioResult RunScenario(FlightScenarioDefinition definition)
        {
            FlightScenarioResult result = new FlightScenarioResult
            {
                id = definition.id,
                name = definition.name,
                purpose = definition.purpose,
                durationSeconds = definition.durationSeconds,
                timeStepSeconds = definition.timeStepSeconds
            };

            GameObject aircraft = new GameObject($"ScenarioAircraft_{definition.id}");
            AircraftState state = aircraft.AddComponent<AircraftState>();
            SimpleAircraftPhysics physics = aircraft.AddComponent<SimpleAircraftPhysics>();
            C172StyleAircraftConfig config = C172StyleAircraftConfig.CreateRuntimeDefault();
            physics.state = state;
            physics.config = config;
            state.config = config;

            InitializeScenarioState(definition, physics, aircraft.transform);
            FlightTestRecorder recorder = new FlightTestRecorder(result);

            int steps = Mathf.CeilToInt(definition.durationSeconds / definition.timeStepSeconds);
            int sampleStride = Mathf.Max(1, Mathf.RoundToInt(0.1f / definition.timeStepSeconds));
            for (int i = 0; i <= steps; i++)
            {
                float t = i * definition.timeStepSeconds;
                if (definition.id == "runway_reset" && t >= definition.durationSeconds * 0.55f && t < definition.durationSeconds * 0.55f + definition.timeStepSeconds)
                {
                    physics.ResetToRunway();
                }

                AircraftControlState controls = definition.EvaluateControls(t);
                physics.StepSimulation(controls, definition.timeStepSeconds);

                if (i % sampleStride == 0 || i == steps)
                {
                    recorder.AddSample(BuildSample(t, controls, state));
                }
            }

            EvaluatePass(definition, result);
            UnityEngine.Object.DestroyImmediate(aircraft);
            UnityEngine.Object.DestroyImmediate(config);
            return result;
        }

        private static void InitializeScenarioState(FlightScenarioDefinition definition, SimpleAircraftPhysics physics, Transform transform)
        {
            if (definition.startOnRunway)
            {
                physics.initialForwardSpeedKts = definition.initialAirspeedKts;
                physics.ResetToRunway();
                return;
            }

            float altitudeM = Mathf.Max(100f, definition.initialAltitudeFt / AircraftUnitConversions.MetersToFeet);
            Vector3 position = new Vector3(0f, altitudeM, -260f);
            Vector3 euler = new Vector3(-2f, 78f, 0f);
            transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            Vector3 velocity = transform.forward * (definition.initialAirspeedKts * AircraftUnitConversions.KnotsToMetersPerSecond);
            physics.SetStateForTest(position, euler, velocity);
        }

        private static FlightScenarioSample BuildSample(float timestamp, AircraftControlState controls, AircraftState state)
        {
            return new FlightScenarioSample
            {
                timestamp = timestamp,
                controls = CloneControls(controls),
                flight = new FlightTelemetrySnapshot
                {
                    timestamp = timestamp,
                    airspeedKts = state.airspeedKts,
                    altitudeFt = state.altitudeFt,
                    verticalSpeedFpm = state.verticalSpeedFpm,
                    headingDeg = state.headingDeg,
                    pitchDeg = state.pitchDeg,
                    bankDeg = state.bankDeg,
                    angleOfAttackDeg = state.angleOfAttackDeg,
                    stallWarning = state.stallWarning,
                    onGround = state.onGround,
                    engineRpm = state.engineRpm,
                    powerPercent = state.powerPercent,
                    flapDegrees = state.flapDegrees,
                    trimPercent = state.trimPercent,
                    loadFactorG = state.loadFactorG,
                    groundRollMeters = state.groundRollMeters,
                    runwayLateralOffsetMeters = state.runwayLateralOffsetMeters
                },
                leftAileronDeg = controls.aileron * 20f,
                rightAileronDeg = -controls.aileron * 20f,
                elevatorDeg = controls.elevator * 24f,
                rudderDeg = controls.rudder * 28f,
                flapDeg = state.flapDegrees
            };
        }

        private static AircraftControlState CloneControls(AircraftControlState c)
        {
            return new AircraftControlState
            {
                aileron = c.aileron,
                elevator = c.elevator,
                rudder = c.rudder,
                throttle = c.throttle,
                mixture = c.mixture,
                carbHeat = c.carbHeat,
                trim = c.trim,
                flaps = c.flaps,
                leftToeBrake = c.leftToeBrake,
                rightToeBrake = c.rightToeBrake,
                markerPressed = c.markerPressed,
                resetPressed = c.resetPressed,
                pausePressed = c.pausePressed,
                telemetryTogglePressed = c.telemetryTogglePressed
            };
        }

        private static void EvaluatePass(FlightScenarioDefinition definition, FlightScenarioResult result)
        {
            FlightScenarioStats s = result.stats;
            List<string> reasons = new List<string>();

            bool pass = definition.id switch
            {
                "neutral_controls" => s.maxAirspeedKts < 8f && s.maxRunwayOffsetAbsMeters < 3f,
                "control_surface_sweep" => s.minAileron < -0.85f && s.maxAileron > 0.85f && s.minElevator < -0.85f && s.maxElevator > 0.85f && s.minRudder < -0.85f && s.maxRudder > 0.85f,
                "taxi_throttle_brake" => s.maxAirspeedKts > 5f && s.maxLeftToeBrake > 0.8f && s.maxRightToeBrake > 0.8f,
                "takeoff_roll" => s.maxAirspeedKts > 38f && s.maxGroundRollMeters > 80f,
                "rotation_climb" => s.maxAirspeedKts > 52f && s.maxAltitudeFt > 15f && s.maxVerticalSpeedFpm > 100f && s.maxPitchDeg < 24f,
                "shallow_turns" => HeadingSpan(s) > 6f && s.maxBankDeg > 5f && s.minBankDeg < -5f && Mathf.Max(Mathf.Abs(s.minBankDeg), Mathf.Abs(s.maxBankDeg)) < 42f && s.minAltitudeFt > 850f,
                "rudder_yaw" => HeadingSpan(s) > 4f && s.maxRudder > 0.5f && s.minRudder < -0.5f,
                "flap_deployment" => s.maxFlapDegrees >= 25f,
                "trim_effect" => s.maxTrim > 0.5f && s.minTrim < -0.3f && (s.maxPitchDeg - s.minPitchDeg) > 2f,
                "stall_approach" => s.stallWarningObserved && s.minAltitudeFt > 1200f && Mathf.Max(Mathf.Abs(s.minPitchDeg), Mathf.Abs(s.maxPitchDeg)) < 45f,
                "runway_reset" => s.maxGroundRollMeters > 0f && result.samples.Count > 0 && result.samples[^1].flight.onGround,
                _ => false
            };

            reasons.Add($"speed {s.minAirspeedKts:0.0}-{s.maxAirspeedKts:0.0} kt");
            reasons.Add($"alt {s.minAltitudeFt:0}-{s.maxAltitudeFt:0} ft");
            reasons.Add($"vsi {s.minVerticalSpeedFpm:0}-{s.maxVerticalSpeedFpm:0} fpm");
            reasons.Add($"pitch {s.minPitchDeg:0.0}/{s.maxPitchDeg:0.0}");
            reasons.Add($"bank {s.minBankDeg:0.0}/{s.maxBankDeg:0.0}");
            reasons.Add($"flaps max {s.maxFlapDegrees:0} deg");
            reasons.Add($"stall {(s.stallWarningObserved ? "observed" : "not observed")}");

            result.passed = pass;
            result.passReason = string.Join("; ", reasons);
            if (!pass)
            {
                result.errors.Add($"Scenario acceptance failed: {definition.id}");
            }
        }

        private static float HeadingSpan(FlightScenarioStats stats)
        {
            return Mathf.Abs(Mathf.DeltaAngle(stats.minHeadingDeg, stats.maxHeadingDeg));
        }
    }
}
