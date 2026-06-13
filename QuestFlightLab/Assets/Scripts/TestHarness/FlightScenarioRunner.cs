using System;
using System.Collections.Generic;
using QuestFlightLab.Flight;
using QuestFlightLab.Runtime;
using QuestFlightLab.Training;
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
                suiteName = "Flight Sim Core v0.5 Stabilized Approach + Go-Around Scenario Suite",
                limitations =
                {
                    "Editor scenario runner does not prove Quest Bluetooth, USB2BLE hardware, or headset runtime behavior.",
                    "Flight dynamics are a prototype C172-style approximation, not validated C172 fidelity.",
                    "Traffic-pattern, stabilized-approach, go-around, replay, and scoring features are prototype scaffolds and do not provide real pilot-training credit.",
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
            result.instrumentVerification = InstrumentVerification.Capture();
            result.trainingVerification = TrainingVerification.Capture(definition.id);
            result.airportPatternVerification = definition.requiresAirportPatternVerification
                ? AirportPatternVerification.Capture()
                : new AirportPatternVerificationSnapshot
                {
                    airportRootPresent = true,
                    allRequiredReferencesPresent = true,
                    requiredCount = 0,
                    presentCount = 0,
                    summary = "not required for this scenario"
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
                bool resetWindow = (definition.id == "runway_reset" || definition.id == "pattern_reset_retry" || definition.id == "reset_after_goaround")
                                   && t >= definition.durationSeconds * 0.55f
                                   && t < definition.durationSeconds * 0.55f + definition.timeStepSeconds;
                if (resetWindow)
                {
                    physics.ResetToRunway();
                }

                AircraftControlState controls = definition.EvaluateControls(t);
                physics.StepSimulation(controls, definition.timeStepSeconds);

                if (i % sampleStride == 0 || i == steps)
                {
                    recorder.AddSample(BuildSample(definition, t, controls, state));
                }
            }

            if (definition.requiresDebrief || definition.isTrafficPatternScenario)
            {
                result.debriefReport = LessonScoring.ScoreTrafficPattern(
                    definition.id,
                    BuildScoringSamples(result),
                    definition.markChecklistComplete);
            }

            if (definition.requiresApproachDebrief || definition.isApproachScenario)
            {
                result.approachDebrief = ApproachScoring.ScoreApproach(
                    definition.id,
                    BuildApproachScoringSamples(result),
                    definition.markChecklistComplete);
            }

            if (definition.requiresTimeline || definition.isApproachScenario)
            {
                result.timeline = FlightTimelineRecorder.BuildTimeline(result);
                if (result.approachDebrief != null)
                {
                    result.approachDebrief.timelineSampleCount = result.timeline.sampleCount;
                    result.approachDebrief.replayMarkerCount = result.timeline.markerCount;
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
            Vector3 runwayForward = Vector3.ProjectOnPlane(Quaternion.Euler(physics.runwayResetEuler) * Vector3.forward, Vector3.up).normalized;
            float alongCenterlineMeters = definition.isApproachScenario ? -720f : -260f;
            Vector3 position = physics.runwayResetPosition + runwayForward * alongCenterlineMeters + Vector3.up * (altitudeM - physics.groundHeightMeters);
            Vector3 euler = new Vector3(-2f, 78f, 0f);
            transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            Vector3 velocity = transform.forward * (definition.initialAirspeedKts * AircraftUnitConversions.KnotsToMetersPerSecond);
            physics.SetStateForTest(position, euler, velocity);
        }

        private static FlightScenarioSample BuildSample(FlightScenarioDefinition definition, float timestamp, AircraftControlState controls, AircraftState state)
        {
            FlightTelemetrySnapshot flight = new FlightTelemetrySnapshot
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
                stallIntensity = state.stallIntensity,
                slipSkid = state.slipSkid,
                referenceSpeedKts = state.referenceSpeedKts,
                targetSpeedErrorKts = state.targetSpeedErrorKts,
                groundRollMeters = state.groundRollMeters,
                runwayLateralOffsetMeters = state.runwayLateralOffsetMeters
            };
            string phaseId = definition.isApproachScenario
                ? StabilizedApproachLesson.PhaseAt(timestamp, definition.durationSeconds).id
                : "";
            ApproachEvaluationSnapshot approach = definition.isApproachScenario
                ? ApproachScoring.EvaluateTelemetrySample(phaseId, timestamp, flight, controls)
                : new ApproachEvaluationSnapshot();

            return new FlightScenarioSample
            {
                timestamp = timestamp,
                controls = CloneControls(controls),
                flight = flight,
                leftAileronDeg = controls.aileron * 20f,
                rightAileronDeg = -controls.aileron * 20f,
                elevatorDeg = controls.elevator * 24f,
                rudderDeg = controls.rudder * 28f,
                flapDeg = state.flapDegrees,
                positionX = state.transform.position.x,
                positionY = state.transform.position.y,
                positionZ = state.transform.position.z,
                approachPhase = approach.phaseId,
                stableApproach = approach.stable,
                goAroundRequired = approach.goAroundRequired,
                goAroundInitiated = approach.goAroundInitiated,
                gateId = approach.gateId,
                glidePathDeviationDeg = approach.glidePathDeviationDeg,
                centerlineDeviationMeters = approach.centerlineDeviationMeters,
                targetDescentRateFpm = approach.targetDescentRateFpm,
                scoreDelta = approach.score,
                approachWarningSummary = approach.warningSummary
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
                "preflight_neutral_initialization" => s.maxAirspeedKts < 8f && s.maxRunwayOffsetAbsMeters < 3f,
                "before_takeoff_checklist" => result.trainingVerification.allRequiredStepsPresent && s.maxAirspeedKts < 5f,
                "taxi_brake_check" => s.maxAirspeedKts > 5f && s.maxLeftToeBrake > 0.8f && s.maxRightToeBrake > 0.8f && s.maxAirspeedKts < 35f,
                "takeoff_roll_to_vr" => s.maxAirspeedKts > 52f && s.maxAirspeedKts < 85f && s.maxGroundRollMeters > 120f && s.maxRunwayOffsetAbsMeters < 15f,
                "rotation_climb_to_altitude" => s.maxAirspeedKts > 52f && s.altitudeDeltaFt > 80f && s.maxVerticalSpeedFpm > 200f && s.maxPitchDeg < 18f,
                "vy_climb_stabilization" => s.altitudeDeltaFt > 80f && s.maxVerticalSpeedFpm > 200f && s.minAirspeedKts > 55f && s.maxReferenceSpeedErrorAbsKts < 24f && !s.stallWarningObserved,
                "shallow_left_right_turns" => HeadingSpan(s) > 6f && s.maxBankDeg > 5f && s.minBankDeg < -5f && Mathf.Max(Mathf.Abs(s.minBankDeg), Mathf.Abs(s.maxBankDeg)) < 35f && s.minAltitudeFt > 850f,
                "rudder_yaw_response" => HeadingSpan(s) > 4f && s.maxRudder > 0.5f && s.minRudder < -0.5f,
                "flap_deployment_effect" => s.maxFlapDegrees >= 25f && s.maxPitchDeg - s.minPitchDeg > 2f,
                "trim_nose_up_down" => s.maxTrim > 0.5f && s.minTrim < -0.3f && (s.maxPitchDeg - s.minPitchDeg) > 4f,
                "slow_flight_stall_warning_onset" => s.stallWarningObserved && s.stallWarningOnsetSeconds >= 0f && s.minAltitudeFt > 1500f && Mathf.Max(Mathf.Abs(s.minPitchDeg), Mathf.Abs(s.maxPitchDeg)) < 38f,
                "stall_recovery" => s.stallWarningObserved && result.samples.Count > 0 && !result.samples[^1].flight.stallWarning && result.samples[^1].flight.airspeedKts > 45f && s.minAltitudeFt > 1200f,
                "pattern_leg_heading_change" => HeadingSpan(s) > 20f && Mathf.Max(Mathf.Abs(s.minBankDeg), Mathf.Abs(s.maxBankDeg)) < 35f && s.minAltitudeFt > 850f,
                "runway_reset" => s.maxGroundRollMeters > 0f && result.samples.Count > 0 && result.samples[^1].flight.onGround,
                "basic_traffic_pattern_full" => result.debriefReport.passed && result.debriefReport.totalScore >= 70f && s.maxAirspeedKts > 55f && s.maxGroundRollMeters > 120f && result.airportPatternVerification.allRequiredReferencesPresent,
                "traffic_pattern_phase_progression" => result.debriefReport.phaseScores.Count >= 12 && result.debriefReport.gateHitCount >= 7 && result.debriefReport.totalScore >= 62f,
                "traffic_pattern_scoring_debrief" => result.debriefReport.phaseScores.Count >= 12 && result.debriefReport.totalScore >= 60f && !string.IsNullOrEmpty(result.debriefReport.summary),
                "instrument_ui_verification" => result.instrumentVerification.allRequiredPresent && result.instrumentVerification.valuesUpdated,
                "lesson_panel_prompt_update" => result.trainingVerification.allRequiredStepsPresent && result.instrumentVerification.valuesUpdated,
                "airport_gate_checkpoint_verification" => result.airportPatternVerification.allRequiredReferencesPresent,
                "pattern_reset_retry" => result.samples.Count > 0 && result.samples[^1].flight.onGround && result.debriefReport.phaseScores.Count >= 12,
                "stabilized_final_approach" => result.approachDebrief.phaseScores.Count >= 12 && !result.approachDebrief.goAroundRequired && result.approachDebrief.totalScore >= 52f && s.maxCenterlineDeviationAbsMeters < 55f,
                "high_unstable_approach_goaround" => result.approachDebrief.goAroundRequired && result.approachDebrief.goAroundInitiated && result.approachDebrief.goAroundDecisionCorrect,
                "low_unstable_approach_goaround" => result.approachDebrief.goAroundRequired && result.approachDebrief.goAroundInitiated && result.approachDebrief.goAroundDecisionCorrect,
                "excessive_sink_rate_goaround" => result.approachDebrief.goAroundRequired && result.approachDebrief.goAroundInitiated && result.approachDebrief.maxDescentDeviationFpm > 100f,
                "final_speed_deviation" => result.approachDebrief.maxSpeedErrorKts > 6f && result.approachDebrief.phaseScores.Count >= 12 && result.approachDebrief.totalScore < 92f,
                "go_around_sequence" => result.approachDebrief.goAroundRequired && result.approachDebrief.goAroundInitiated && s.maxThrottle > 0.95f && s.maxVerticalSpeedFpm > 50f,
                "approach_debrief_generation" => result.approachDebrief.phaseScores.Count >= 12 && !string.IsNullOrEmpty(result.approachDebrief.summary),
                "timeline_export_replay_markers" => result.timeline.sampleCount > 20 && result.timeline.markerCount >= 1 && result.approachDebrief.timelineSampleCount == result.timeline.sampleCount,
                "instrument_approach_status_verification" => result.instrumentVerification.allRequiredPresent && result.instrumentVerification.valuesUpdated,
                "pattern_to_final_transition" => result.debriefReport.phaseScores.Count >= 12 && result.approachDebrief.phaseScores.Count >= 12 && result.airportPatternVerification.allRequiredReferencesPresent,
                "stable_touchdown_placeholder" => result.approachDebrief.touchdownPlaceholderObserved || s.minAltitudeFt < 90f,
                "reset_after_goaround" => result.samples.Count > 0 && result.samples[^1].flight.onGround && result.timeline.sampleCount > 0,
                _ => false
            };

            pass = pass
                   && result.instrumentVerification.allRequiredPresent
                   && result.instrumentVerification.valuesUpdated
                   && result.trainingVerification.allRequiredStepsPresent
                   && result.airportPatternVerification.allRequiredReferencesPresent;

            reasons.Add($"initial/final speed {s.initialAirspeedKts:0.0}/{s.finalAirspeedKts:0.0} kt");
            reasons.Add($"speed {s.minAirspeedKts:0.0}-{s.maxAirspeedKts:0.0} kt");
            reasons.Add($"alt {s.minAltitudeFt:0}-{s.maxAltitudeFt:0} ft delta {s.altitudeDeltaFt:0}");
            reasons.Add($"vsi {s.minVerticalSpeedFpm:0}-{s.maxVerticalSpeedFpm:0} fpm");
            reasons.Add($"heading change {HeadingSpan(s):0.0} deg");
            reasons.Add($"pitch {s.minPitchDeg:0.0}/{s.maxPitchDeg:0.0}");
            reasons.Add($"bank {s.minBankDeg:0.0}/{s.maxBankDeg:0.0}");
            reasons.Add($"flaps max {s.maxFlapDegrees:0} deg");
            reasons.Add($"stall {(s.stallWarningObserved ? $"observed x{s.stallWarningSamples} at {s.stallWarningOnsetSeconds:0.0}s" : "not observed")}");
            reasons.Add($"instruments {result.instrumentVerification.summary}");
            reasons.Add($"training {result.trainingVerification.summary}");
            if (definition.requiresAirportPatternVerification)
            {
                reasons.Add($"airport {result.airportPatternVerification.summary}");
            }
            if (definition.requiresDebrief || definition.isTrafficPatternScenario)
            {
                reasons.Add($"debrief {result.debriefReport.summary}");
            }
            if (definition.requiresApproachDebrief || definition.isApproachScenario)
            {
                reasons.Add($"approach {result.approachDebrief.summary}");
                reasons.Add($"timeline samples {result.timeline.sampleCount} markers {result.timeline.markerCount}");
                reasons.Add($"approach path dev {s.maxGlidePathDeviationAbsDeg:0.0} deg centerline {s.maxCenterlineDeviationAbsMeters:0.0} m");
            }

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

        private static List<PatternScoringSample> BuildScoringSamples(FlightScenarioResult result)
        {
            List<PatternScoringSample> samples = new List<PatternScoringSample>();
            foreach (FlightScenarioSample sample in result.samples)
            {
                samples.Add(new PatternScoringSample
                {
                    timestamp = sample.timestamp,
                    controls = sample.controls,
                    flight = sample.flight
                });
            }

            return samples;
        }

        private static List<ApproachScoringSample> BuildApproachScoringSamples(FlightScenarioResult result)
        {
            List<ApproachScoringSample> samples = new List<ApproachScoringSample>();
            foreach (FlightScenarioSample sample in result.samples)
            {
                samples.Add(new ApproachScoringSample
                {
                    timestamp = sample.timestamp,
                    controls = sample.controls,
                    flight = sample.flight,
                    phaseId = sample.approachPhase
                });
            }

            return samples;
        }
    }
}
