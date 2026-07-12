using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using QuestFlightLab.Flight;
using QuestFlightLab.Flight.Backends;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Editor
{
    /// <summary>
    /// Production-v2 native gate. Scenarios use a deterministic, bounded and
    /// rate-limited scripted pilot so the gate exercises the stock c172x model
    /// with plausible corrections instead of unstable fixed deflections.
    /// Physical envelopes remain independent of the Unity comparison model.
    /// </summary>
    internal static class JSBSimNativeScenarioGateV2
    {
        private const double Dt = 1.0 / 120.0;
        private const double RunwayHeading = FlightDynamicsInitialConditions.KbduRunwayTrueHeadingDegrees;

        public static void Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            GameObject nativeRoot = new GameObject("JSBSimNativeValidationRootV2");
            GameObject unityRoot = new GameObject("UnityPrototypeValidationRootV2");
            AircraftState unityState = unityRoot.AddComponent<AircraftState>();
            SimpleAircraftPhysics unityPhysics = unityRoot.AddComponent<SimpleAircraftPhysics>();
            unityPhysics.state = unityState;
            C172StyleAircraftConfig runtimeConfig = C172StyleAircraftConfig.CreateRuntimeDefault();
            unityPhysics.config = runtimeConfig;
            unityState.config = runtimeConfig;

            using JSBSimNativeFlightBackend native = new JSBSimNativeFlightBackend();
            using UnityPrototypeFlightBackend unity = new UnityPrototypeFlightBackend();
            NativeValidationReport report = CreateReport();
            try
            {
                if (!native.Initialize(new FlightDynamicsBackendContext
                    {
                        simulationRoot = nativeRoot.transform,
                        localOrigin = GeodeticReference.Kbdu,
                        jsbsimAircraft = "c172x",
                        jsbsimDataRoot = JSBSimRuntimeDataPaths.BundledDataRoot,
                        fixedDeltaTimeSeconds = Dt
                    }))
                {
                    throw new InvalidOperationException(native.LastError);
                }

                if (!unity.Initialize(new FlightDynamicsBackendContext
                    {
                        simulationRoot = unityRoot.transform,
                        aircraftState = unityState,
                        unityPrototype = unityPhysics,
                        localOrigin = GeodeticReference.Kbdu,
                        fixedDeltaTimeSeconds = Dt
                    }))
                {
                    throw new InvalidOperationException(unity.LastError);
                }

                report.controlContracts.Add(ProbeControlContract(native, ControlAxis.Elevator));
                report.controlContracts.Add(ProbeControlContract(native, ControlAxis.Trim));
                report.controlContracts.Add(ProbeControlContract(native, ControlAxis.Aileron));
                report.controlContracts.Add(ProbeControlContract(native, ControlAxis.Rudder));
                report.controlContractPass = report.controlContracts.All(item => item.passed);

                foreach (ScenarioSpec spec in ScenarioSpecs())
                {
                    report.scenarios.Add(RunScenario(spec, native, unity));
                }

                FinalizeReport(report);
            }
            catch (Exception exception)
            {
                report.status = "FAIL";
                report.fatalError = exception.ToString();
                report.productionBackendDecision = "UNITY_PROTOTYPE_ONLY";
                report.nativePromotionReason = "Native validation could not complete: " + exception.Message;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(nativeRoot);
                UnityEngine.Object.DestroyImmediate(unityRoot);
                if (runtimeConfig != null) UnityEngine.Object.DestroyImmediate(runtimeConfig);
            }

            WriteReports(outputDirectory, report);
            if (report.status != "PASS")
            {
                throw new InvalidOperationException("Native JSBSim production-v2 gate failed. See the scenario report.");
            }
        }

        private static NativeValidationReport CreateReport()
        {
            return new NativeValidationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                status = "FAIL",
                classification = "native_jsbsim_11_scenario_production_v2_gate",
                unityVersion = Application.unityVersion,
                jsbsimVersion = JSBSimNativeFlightBackend.PinnedJsbsimVersion,
                jsbsimRevision = JSBSimNativeFlightBackend.PinnedJsbsimRevision,
                dataRoot = JSBSimRuntimeDataPaths.BundledDataRoot,
                fixedStepHz = 120.0,
                localOrigin = GeodeticReference.Kbdu,
                baselineResult = "1/10 passed before initialization/control correction",
                baselineEvidence = "production_vertical_slice_v2_20260712_020951/baseline_jsbsim",
                initializationAudit =
                    "Every reset clears AP/FCS/trim/flap/brake commands before RunIC; sets KBDU geodetic/terrain/runway ICs; direct-starts one engine with both magnetos, mixture rich and starter released; then applies fresh idle controls.",
                trimAudit =
                    "No opaque JSBSim DoTrim result is used as hidden acceptance input. The scripted pilot applies bounded pitch feedback and moves the actual pitch-trim command at <=0.12 normalized units/s.",
                scheduleAudit =
                    "Elevator <=0.45, aileron <=0.60, rudder <=0.75; surface rates <=0.8/1.0/1.2 units/s; throttle <=0.75 units/s; flap <=0.20 units/s.",
                limitations = new[]
                {
                    "The scripted pilot is deterministic acceptance equipment, not a production autopilot or a validated C172 control law.",
                    "Windows native timing is not Quest CPU timing.",
                    "Passing these integration envelopes does not establish training-grade C172 fidelity or human handling quality.",
                    "Unity comparison values are trend context only and never decide native physical acceptance."
                }
            };
        }

        private static void FinalizeReport(NativeValidationReport report)
        {
            report.scenarioCount = report.scenarios.Count;
            report.passedCount = report.scenarios.Count(item => item.passed);
            report.criticalScenarioCount = report.scenarios.Count(item => item.critical);
            report.criticalPassedCount = report.scenarios.Count(item => item.critical && item.passed);
            report.allCriticalScenariosPass = report.criticalScenarioCount > 0 &&
                                              report.criticalPassedCount == report.criticalScenarioCount;
            report.totalNativeSteps = report.scenarios.Sum(item => item.completedSteps);
            report.nativeAverageStepMilliseconds = report.scenarios.Sum(item => item.nativeAverageStepMilliseconds * item.completedSteps) /
                                                   Math.Max(1, report.totalNativeSteps);
            report.nativeWorstP95StepMilliseconds = report.scenarios.Count == 0
                ? double.PositiveInfinity
                : report.scenarios.Max(item => item.nativeP95StepMilliseconds);
            report.windowsPerformanceTargetPass = report.nativeAverageStepMilliseconds < 0.5 &&
                                                  report.nativeWorstP95StepMilliseconds < 1.0;
            report.zeroSteadyStateAllocationsPass = report.scenarios.All(item => item.managedAllocatedBytesDuringSteadySteps == 0);
            bool completePass = report.scenarioCount == 11 &&
                                report.passedCount == 11 &&
                                report.allCriticalScenariosPass &&
                                report.controlContractPass &&
                                report.windowsPerformanceTargetPass &&
                                report.zeroSteadyStateAllocationsPass &&
                                string.IsNullOrEmpty(report.fatalError);
            report.status = completePass ? "PASS" : "FAIL";
            report.productionBackendDecision = completePass ? "JSBSIM_NATIVE_QUALIFIED" : "UNITY_PROTOTYPE_ONLY";
            report.nativePromotionReason = completePass
                ? "All 11 scenarios, every critical scenario, direct sign contracts, 120 Hz timing, and zero-allocation gates passed."
                : BuildPromotionFailure(report);
        }

        private static string BuildPromotionFailure(NativeValidationReport report)
        {
            List<string> reasons = new List<string>();
            if (report.scenarioCount != 11) reasons.Add($"scenario count {report.scenarioCount}/11");
            if (report.passedCount != report.scenarioCount) reasons.Add($"physical scenarios {report.passedCount}/{report.scenarioCount}");
            if (!report.allCriticalScenariosPass) reasons.Add($"critical scenarios {report.criticalPassedCount}/{report.criticalScenarioCount}");
            if (!report.controlContractPass) reasons.Add("one or more direct control-sign contracts failed");
            if (!report.windowsPerformanceTargetPass) reasons.Add("native timing target failed");
            if (!report.zeroSteadyStateAllocationsPass) reasons.Add("steady-state allocation target failed");
            if (!string.IsNullOrEmpty(report.fatalError)) reasons.Add("fatal gate error");
            return "Native is not production-qualified: " + string.Join("; ", reasons);
        }

        private static NativeScenarioResult RunScenario(
            ScenarioSpec spec,
            JSBSimNativeFlightBackend native,
            UnityPrototypeFlightBackend unity)
        {
            FlightDynamicsInitialConditions initial = InitialConditions(spec);
            NativeScenarioResult result = CreateScenarioResult(spec);
            if (!native.Reset(initial))
            {
                result.runtimeFailure = "native reset: " + native.LastError;
                NormalizeUnobservedMetrics(result);
                EvaluatePhysicalAcceptance(spec, result, false);
                return result;
            }
            if (!unity.Reset(initial))
            {
                result.runtimeFailure = "Unity comparison reset: " + unity.LastError;
                NormalizeUnobservedMetrics(result);
                EvaluatePhysicalAcceptance(spec, result, false);
                return result;
            }

            int plannedSteps = (int)Math.Ceiling(spec.durationSeconds / Dt);
            double[] timings = new double[plannedSteps];
            ScriptedPilot pilot = new ScriptedPilot();
            FlightDynamicsState start = native.CurrentState;
            FlightDynamicsState unityStart = unity.CurrentState;
            result.initialAirspeedKnots = start.calibratedAirspeedKnots;
            result.initialAglFeet = start.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
            result.initialHeadingDegrees = start.headingDegrees;
            result.initialEngineRpm = start.engineRpm;
            double speedSquaredError = 0.0;
            double altitudeSquaredError = 0.0;
            double pitchSquaredError = 0.0;
            double bankSquaredError = 0.0;
            double headingSquaredError = 0.0;
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();

            for (int step = 0; step < plannedSteps; step++)
            {
                double time = step * Dt;
                pilot.Apply(spec.id, time, native.CurrentState);
                AircraftControlState controls = pilot.Controls;
                native.SetControls(controls);
                unity.SetControls(controls);
                long startTicks = Stopwatch.GetTimestamp();
                bool nativeAdvanced = native.Advance(Dt);
                long endTicks = Stopwatch.GetTimestamp();
                timings[result.completedSteps] = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
                if (!nativeAdvanced)
                {
                    result.runtimeFailure = $"native step {step}: {native.LastError}";
                    break;
                }
                if (!unity.Advance(Dt))
                {
                    result.runtimeFailure = $"Unity comparison step {step}: {unity.LastError}";
                    break;
                }

                FlightDynamicsState state = native.CurrentState;
                FlightDynamicsState unityState = unity.CurrentState;
                if (!state.IsFinite)
                {
                    result.runtimeFailure = $"non-finite native state at step {step}";
                    break;
                }

                result.completedSteps++;
                CaptureSample(spec, time, start, state, controls, result);
                double aglFeet = state.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
                double unityAglFeet = unityState.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
                speedSquaredError += Square(state.calibratedAirspeedKnots - unityState.calibratedAirspeedKnots);
                altitudeSquaredError += Square(aglFeet - unityAglFeet);
                pitchSquaredError += Square(state.pitchDegrees - unityState.pitchDegrees);
                bankSquaredError += Square(state.bankDegrees - unityState.bankDegrees);
                headingSquaredError += Square(Mathf.DeltaAngle((float)state.headingDegrees, (float)unityState.headingDegrees));
            }

            result.managedAllocatedBytesDuringSteadySteps = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            if (result.completedSteps > 0)
            {
                Array.Sort(timings, 0, result.completedSteps);
                double sum = 0.0;
                for (int i = 0; i < result.completedSteps; i++) sum += timings[i];
                result.nativeAverageStepMilliseconds = sum / result.completedSteps;
                result.nativeP95StepMilliseconds = timings[(int)Math.Floor((result.completedSteps - 1) * 0.95)];
                FlightDynamicsState final = native.CurrentState;
                FlightDynamicsState unityFinal = unity.CurrentState;
                result.finalAirspeedKnots = final.calibratedAirspeedKnots;
                result.finalAglFeet = final.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
                result.altitudeChangeFeet = result.finalAglFeet - result.initialAglFeet;
                result.finalVerticalSpeedFeetPerMinute = final.verticalSpeedFeetPerMinute;
                result.finalPitchDegrees = final.pitchDegrees;
                result.finalBankDegrees = final.bankDegrees;
                result.finalHeadingDegrees = final.headingDegrees;
                result.headingChangeDegrees = Mathf.DeltaAngle((float)start.headingDegrees, (float)final.headingDegrees);
                result.finalEngineRpm = final.engineRpm;
                result.finalWeightOnWheels = final.weightOnWheels;
                result.airspeedRmseVsUnityKnots = Math.Sqrt(speedSquaredError / result.completedSteps);
                result.aglRmseVsUnityFeet = Math.Sqrt(altitudeSquaredError / result.completedSteps);
                result.pitchRmseVsUnityDegrees = Math.Sqrt(pitchSquaredError / result.completedSteps);
                result.bankRmseVsUnityDegrees = Math.Sqrt(bankSquaredError / result.completedSteps);
                result.headingRmseVsUnityDegrees = Math.Sqrt(headingSquaredError / result.completedSteps);
                result.unityFinalAirspeedKnots = unityFinal.calibratedAirspeedKnots;
                result.unityFinalAglFeet = unityFinal.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
            }

            bool finiteAndTimePass = result.completedSteps == plannedSteps &&
                                     native.CurrentState.IsFinite &&
                                     Math.Abs(native.CurrentState.simulationTimeSeconds - spec.durationSeconds) < 0.02 &&
                                     string.IsNullOrEmpty(result.runtimeFailure);
            NormalizeUnobservedMetrics(result);
            EvaluatePhysicalAcceptance(spec, result, finiteAndTimePass);
            return result;
        }

        private static void NormalizeUnobservedMetrics(NativeScenarioResult result)
        {
            result.minAirspeedKnots = FiniteOrZero(result.minAirspeedKnots);
            result.maxAirspeedKnots = FiniteOrZero(result.maxAirspeedKnots);
            result.minAglFeet = FiniteOrZero(result.minAglFeet);
            result.maxAglFeet = FiniteOrZero(result.maxAglFeet);
            result.minVerticalSpeedFeetPerMinute = FiniteOrZero(result.minVerticalSpeedFeetPerMinute);
            result.maxVerticalSpeedFeetPerMinute = FiniteOrZero(result.maxVerticalSpeedFeetPerMinute);
            result.minPitchDegrees = FiniteOrZero(result.minPitchDegrees);
            result.maxPitchDegrees = FiniteOrZero(result.maxPitchDegrees);
            result.minBankDegrees = FiniteOrZero(result.minBankDegrees);
            result.maxBankDegrees = FiniteOrZero(result.maxBankDegrees);
            result.minEngineRpm = FiniteOrZero(result.minEngineRpm);
            result.maxEngineRpm = FiniteOrZero(result.maxEngineRpm);
            result.secondHalfMinAirspeedKnots = FiniteOrZero(result.secondHalfMinAirspeedKnots);
            result.secondHalfMaxAirspeedKnots = FiniteOrZero(result.secondHalfMaxAirspeedKnots);
            result.secondHalfMinAglFeet = FiniteOrZero(result.secondHalfMinAglFeet);
            result.secondHalfMaxAglFeet = FiniteOrZero(result.secondHalfMaxAglFeet);
            result.secondHalfMaxVerticalSpeedFeetPerMinute = FiniteOrZero(result.secondHalfMaxVerticalSpeedFeetPerMinute);
            result.slowPhaseMinAirspeedKnots = FiniteOrZero(result.slowPhaseMinAirspeedKnots);
            result.slowPhaseMaxAirspeedKnots = FiniteOrZero(result.slowPhaseMaxAirspeedKnots);
            result.preRecoveryMinAirspeedKnots = FiniteOrZero(result.preRecoveryMinAirspeedKnots);
            result.postRecoveryMaxAirspeedKnots = FiniteOrZero(result.postRecoveryMaxAirspeedKnots);
            result.recoveryMaxVerticalSpeedFeetPerMinute = FiniteOrZero(result.recoveryMaxVerticalSpeedFeetPerMinute);
        }

        private static NativeScenarioResult CreateScenarioResult(ScenarioSpec spec)
        {
            return new NativeScenarioResult
            {
                id = spec.id,
                name = spec.name,
                critical = spec.critical,
                durationSeconds = spec.durationSeconds,
                plannedSteps = (int)Math.Ceiling(spec.durationSeconds / Dt),
                minAirspeedKnots = double.PositiveInfinity,
                maxAirspeedKnots = double.NegativeInfinity,
                minAglFeet = double.PositiveInfinity,
                maxAglFeet = double.NegativeInfinity,
                minVerticalSpeedFeetPerMinute = double.PositiveInfinity,
                maxVerticalSpeedFeetPerMinute = double.NegativeInfinity,
                minPitchDegrees = double.PositiveInfinity,
                maxPitchDegrees = double.NegativeInfinity,
                minBankDegrees = double.PositiveInfinity,
                maxBankDegrees = double.NegativeInfinity,
                minEngineRpm = double.PositiveInfinity,
                maxEngineRpm = double.NegativeInfinity,
                secondHalfMinAirspeedKnots = double.PositiveInfinity,
                secondHalfMaxAirspeedKnots = double.NegativeInfinity,
                secondHalfMinAglFeet = double.PositiveInfinity,
                secondHalfMaxAglFeet = double.NegativeInfinity,
                secondHalfMaxVerticalSpeedFeetPerMinute = double.NegativeInfinity,
                slowPhaseMinAirspeedKnots = spec.id == "slow_flight_stall_recovery" ? double.PositiveInfinity : 0.0,
                slowPhaseMaxAirspeedKnots = spec.id == "slow_flight_stall_recovery" ? double.NegativeInfinity : 0.0,
                preRecoveryMinAirspeedKnots = spec.id == "slow_flight_stall_recovery" ? double.PositiveInfinity : 0.0,
                postRecoveryMaxAirspeedKnots = spec.id == "slow_flight_stall_recovery" ? double.NegativeInfinity : 0.0,
                recoveryMaxVerticalSpeedFeetPerMinute = spec.id == "slow_flight_stall_recovery" ? double.NegativeInfinity : 0.0,
                timeToRotationSeconds = -1.0
            };
        }

        private static void CaptureSample(
            ScenarioSpec spec,
            double time,
            FlightDynamicsState start,
            FlightDynamicsState state,
            AircraftControlState controls,
            NativeScenarioResult result)
        {
            double agl = state.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
            result.minAirspeedKnots = Math.Min(result.minAirspeedKnots, state.calibratedAirspeedKnots);
            result.maxAirspeedKnots = Math.Max(result.maxAirspeedKnots, state.calibratedAirspeedKnots);
            result.minAglFeet = Math.Min(result.minAglFeet, agl);
            result.maxAglFeet = Math.Max(result.maxAglFeet, agl);
            result.minVerticalSpeedFeetPerMinute = Math.Min(result.minVerticalSpeedFeetPerMinute, state.verticalSpeedFeetPerMinute);
            result.maxVerticalSpeedFeetPerMinute = Math.Max(result.maxVerticalSpeedFeetPerMinute, state.verticalSpeedFeetPerMinute);
            result.minPitchDegrees = Math.Min(result.minPitchDegrees, state.pitchDegrees);
            result.maxPitchDegrees = Math.Max(result.maxPitchDegrees, state.pitchDegrees);
            result.minBankDegrees = Math.Min(result.minBankDegrees, state.bankDegrees);
            result.maxBankDegrees = Math.Max(result.maxBankDegrees, state.bankDegrees);
            result.minEngineRpm = Math.Min(result.minEngineRpm, state.engineRpm);
            result.maxEngineRpm = Math.Max(result.maxEngineRpm, state.engineRpm);
            result.maxGroundDistanceMeters = Math.Max(result.maxGroundDistanceMeters,
                Vector3.Distance(start.positionUnityMeters, state.positionUnityMeters));
            if (result.timeToRotationSeconds < 0.0 && agl > result.initialAglFeet + 2.0)
                result.timeToRotationSeconds = time;
            result.maxThrottle = Math.Max(result.maxThrottle, controls.throttle);
            result.maxAbsAileron = Math.Max(result.maxAbsAileron, Math.Abs(controls.aileron));
            result.maxAbsElevator = Math.Max(result.maxAbsElevator, Math.Abs(controls.elevator));
            result.maxAbsRudder = Math.Max(result.maxAbsRudder, Math.Abs(controls.rudder));
            result.maxAbsTrim = Math.Max(result.maxAbsTrim, Math.Abs(controls.trim));
            result.maxFlaps = Math.Max(result.maxFlaps, controls.flaps);
            result.maxBrake = Math.Max(result.maxBrake, Math.Max(controls.leftToeBrake, controls.rightToeBrake));
            if (time >= spec.durationSeconds * 0.5)
            {
                result.secondHalfMinAirspeedKnots = Math.Min(result.secondHalfMinAirspeedKnots, state.calibratedAirspeedKnots);
                result.secondHalfMaxAirspeedKnots = Math.Max(result.secondHalfMaxAirspeedKnots, state.calibratedAirspeedKnots);
                result.secondHalfMinAglFeet = Math.Min(result.secondHalfMinAglFeet, agl);
                result.secondHalfMaxAglFeet = Math.Max(result.secondHalfMaxAglFeet, agl);
                result.secondHalfMaxVerticalSpeedFeetPerMinute = Math.Max(result.secondHalfMaxVerticalSpeedFeetPerMinute,
                    state.verticalSpeedFeetPerMinute);
            }
            if (spec.id == "slow_flight_stall_recovery")
            {
                if (time >= 5.0 && time < 10.0)
                {
                    result.slowPhaseMinAirspeedKnots = Math.Min(result.slowPhaseMinAirspeedKnots, state.calibratedAirspeedKnots);
                    result.slowPhaseMaxAirspeedKnots = Math.Max(result.slowPhaseMaxAirspeedKnots, state.calibratedAirspeedKnots);
                }
                if (time < 18.0)
                    result.preRecoveryMinAirspeedKnots = Math.Min(result.preRecoveryMinAirspeedKnots, state.calibratedAirspeedKnots);
                else
                {
                    result.postRecoveryMaxAirspeedKnots = Math.Max(result.postRecoveryMaxAirspeedKnots, state.calibratedAirspeedKnots);
                    result.recoveryMaxVerticalSpeedFeetPerMinute = Math.Max(result.recoveryMaxVerticalSpeedFeetPerMinute,
                        state.verticalSpeedFeetPerMinute);
                }
            }
        }

        private static void EvaluatePhysicalAcceptance(
            ScenarioSpec spec,
            NativeScenarioResult result,
            bool finiteAndTimePass)
        {
            List<string> failures = new List<string>();
            Require(finiteAndTimePass, string.IsNullOrEmpty(result.runtimeFailure)
                ? "non-finite state or fixed-step duration mismatch"
                : result.runtimeFailure, failures);
            Require(result.managedAllocatedBytesDuringSteadySteps == 0,
                $"steady stepping allocated {result.managedAllocatedBytesDuringSteadySteps} managed bytes", failures);
            Require(result.maxEngineRpm >= 500.0, "engine never reached 500 RPM", failures);

            switch (spec.id)
            {
                case "idle_brake_hold":
                    Require(result.maxAirspeedKnots <= 2.0, "brake hold exceeded 2 kt", failures);
                    Require(result.maxGroundDistanceMeters <= 5.0, "brake hold moved more than 5 m", failures);
                    Require(Math.Abs(result.altitudeChangeFeet) <= 5.0, "ground altitude drift exceeded 5 ft", failures);
                    Require(result.maxBrake >= 0.99, "full brake command was not applied", failures);
                    break;
                case "taxi_acceleration_braking":
                    Require(result.maxAirspeedKnots >= 3.0 && result.maxAirspeedKnots <= 20.0,
                        "taxi acceleration did not remain in 3-20 kt envelope", failures);
                    Require(result.finalAirspeedKnots <= 2.5, "taxi braking did not slow below 2.5 kt", failures);
                    Require(result.maxGroundDistanceMeters >= 5.0 && result.maxGroundDistanceMeters <= 150.0,
                        "taxi distance outside 5-150 m", failures);
                    Require(Math.Abs(result.headingChangeDegrees) <= 15.0, "taxi departed runway heading by more than 15 degrees", failures);
                    Require(result.maxBrake >= 0.99, "taxi scenario never reached full braking", failures);
                    break;
                case "takeoff_roll":
                    Require(result.maxAirspeedKnots >= 45.0, "takeoff roll never reached 45 kt", failures);
                    Require(result.maxGroundDistanceMeters >= 150.0, "ground roll was shorter than 150 m", failures);
                    Require(result.maxAglFeet <= result.initialAglFeet + 3.0, "uncommanded liftoff during roll-only case", failures);
                    Require(Math.Abs(result.headingChangeDegrees) <= 15.0, "departed runway heading by more than 15 degrees", failures);
                    break;
                case "rotation":
                    Require(result.maxAirspeedKnots >= 50.0, "never reached a credible rotation speed", failures);
                    Require(result.timeToRotationSeconds >= 15.0 && result.timeToRotationSeconds <= 34.0,
                        "liftoff timing outside 15-34 s", failures);
                    Require(result.maxAglFeet >= result.initialAglFeet + 5.0, "did not climb at least 5 ft after rotation", failures);
                    Require(result.minAglFeet >= 0.0, "gear penetrated terrain", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    Require(result.maxPitchDegrees <= 25.0 && result.minPitchDegrees >= -10.0,
                        "pitch departed -10/+25 degree envelope", failures);
                    Require(Math.Abs(result.headingChangeDegrees) <= 20.0, "rotation departed runway heading by more than 20 degrees", failures);
                    break;
                case "vy_climb":
                    Require(result.altitudeChangeFeet >= 100.0, "did not gain 100 ft", failures);
                    Require(result.secondHalfMaxVerticalSpeedFeetPerMinute >= 300.0,
                        "second half never exceeded +300 fpm", failures);
                    Require(result.finalAirspeedKnots >= 60.0 && result.finalAirspeedKnots <= 90.0,
                        "final airspeed outside 60-90 kt", failures);
                    Require(result.minAglFeet >= result.initialAglFeet - 100.0, "lost more than 100 ft in climb case", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    Require(Math.Abs(result.headingChangeDegrees) <= 20.0, "climb heading drift exceeded 20 degrees", failures);
                    break;
                case "trimmed_level_flight":
                    Require(Math.Abs(result.altitudeChangeFeet) <= 200.0, "altitude drift exceeded 200 ft", failures);
                    Require(result.finalVerticalSpeedFeetPerMinute >= -1000.0 && result.finalVerticalSpeedFeetPerMinute <= 1000.0,
                        "final vertical speed exceeded +/-1000 fpm", failures);
                    Require(result.finalAirspeedKnots >= 75.0 && result.finalAirspeedKnots <= 120.0,
                        "final airspeed outside 75-120 kt", failures);
                    Require(MaxAbs(result.minPitchDegrees, result.maxPitchDegrees) <= 20.0, "pitch exceeded 20 degrees", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 20.0, "bank exceeded 20 degrees", failures);
                    Require(Math.Abs(result.headingChangeDegrees) <= 20.0, "level-flight heading drift exceeded 20 degrees", failures);
                    break;
                case "shallow_turn_left":
                    Require(result.minBankDegrees <= -5.0, "left turn did not establish 5 degrees left bank", failures);
                    Require(result.headingChangeDegrees <= -5.0, "left turn heading did not decrease", failures);
                    RequireTurnEnvelope(result, failures);
                    break;
                case "shallow_turn_right":
                    Require(result.maxBankDegrees >= 5.0, "right turn did not establish 5 degrees right bank", failures);
                    Require(result.headingChangeDegrees >= 5.0, "right turn heading did not increase", failures);
                    RequireTurnEnvelope(result, failures);
                    break;
                case "slow_flight_stall_recovery":
                    Require(result.slowPhaseMinAirspeedKnots >= 40.0 && result.slowPhaseMaxAirspeedKnots <= 80.0,
                        "slow-flight phase left 40-80 kt envelope", failures);
                    Require(result.preRecoveryMinAirspeedKnots <= 50.0, "stall-warning phase did not decelerate to 50 kt", failures);
                    Require(result.postRecoveryMaxAirspeedKnots >= result.preRecoveryMinAirspeedKnots + 10.0,
                        "recovery did not regain 10 kt", failures);
                    Require(result.recoveryMaxVerticalSpeedFeetPerMinute > 0.0,
                        "recovery never established positive vertical speed", failures);
                    Require(result.minAglFeet >= result.initialAglFeet - 600.0, "recovery lost more than 600 ft", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 45.0, "bank exceeded 45 degrees", failures);
                    break;
                case "approach":
                    Require(result.altitudeChangeFeet <= -100.0 && result.altitudeChangeFeet >= -800.0,
                        "descent was outside 100-800 ft", failures);
                    Require(result.finalAglFeet >= 100.0, "descended below 100 ft AGL", failures);
                    Require(result.finalAirspeedKnots >= 55.0 && result.finalAirspeedKnots <= 85.0,
                        "final airspeed outside 55-85 kt", failures);
                    Require(result.finalVerticalSpeedFeetPerMinute >= -1200.0 && result.finalVerticalSpeedFeetPerMinute <= -200.0,
                        "final descent rate outside -1200/-200 fpm", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 25.0, "bank exceeded 25 degrees", failures);
                    break;
                case "go_around":
                    Require(result.secondHalfMaxAglFeet >= result.secondHalfMinAglFeet + 75.0,
                        "second half did not recover 75 ft", failures);
                    Require(result.secondHalfMaxVerticalSpeedFeetPerMinute >= 300.0,
                        "second half never exceeded +300 fpm", failures);
                    Require(result.finalAglFeet >= 100.0, "ended below 100 ft AGL", failures);
                    Require(result.finalAirspeedKnots >= 55.0 && result.finalAirspeedKnots <= 90.0,
                        "final airspeed outside 55-90 kt", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    break;
            }

            result.passed = failures.Count == 0;
            result.acceptanceSummary = result.passed
                ? "PASS: unchanged scenario-specific physical acceptance met"
                : "FAIL: " + string.Join("; ", failures);
        }

        private static void RequireTurnEnvelope(NativeScenarioResult result, List<string> failures)
        {
            Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 35.0,
                "bank exceeded shallow-turn envelope", failures);
            Require(Math.Abs(result.altitudeChangeFeet) <= 300.0, "turn altitude drift exceeded 300 ft", failures);
            Require(result.finalAirspeedKnots >= 70.0 && result.finalAirspeedKnots <= 120.0,
                "turn final airspeed outside 70-120 kt", failures);
        }

        private static ControlContractResult ProbeControlContract(JSBSimNativeFlightBackend native, ControlAxis axis)
        {
            double negative = RunControlPulse(native, axis, -0.20f);
            double positive = RunControlPulse(native, axis, 0.20f);
            double minimumDelta = axis == ControlAxis.Rudder ? 2.0 : axis == ControlAxis.Aileron ? 8.0 : 5.0;
            double delta = positive - negative;
            bool finite = IsFinite(negative) && IsFinite(positive) && IsFinite(delta);
            return new ControlContractResult
            {
                axis = axis.ToString().ToLowerInvariant(),
                projectPositiveMeaning = axis switch
                {
                    ControlAxis.Elevator => "nose up",
                    ControlAxis.Trim => "nose-up trim",
                    ControlAxis.Aileron => "right bank",
                    _ => "right yaw"
                },
                measuredQuantity = axis switch
                {
                    ControlAxis.Elevator => "pitch change deg",
                    ControlAxis.Trim => "pitch change deg",
                    ControlAxis.Aileron => "bank change deg",
                    _ => "heading change deg"
                },
                negativeCommandResponse = finite ? negative : 0.0,
                positiveCommandResponse = finite ? positive : 0.0,
                pairedResponseDelta = finite ? delta : 0.0,
                requiredDelta = minimumDelta,
                passed = finite && delta >= minimumDelta,
                beforeAdapterMapping = axis == ControlAxis.Elevator || axis == ControlAxis.Trim || axis == ControlAxis.Rudder
                    ? "pass-through (incorrect for stock c172x moment sign)"
                    : "pass-through (correct)",
                correctedAdapterMapping = axis == ControlAxis.Elevator || axis == ControlAxis.Trim || axis == ControlAxis.Rudder
                    ? "inverted at managed/native ABI boundary"
                    : "pass-through"
            };
        }

        private static double RunControlPulse(JSBSimNativeFlightBackend native, ControlAxis axis, float command)
        {
            FlightDynamicsInitialConditions initial = FlightDynamicsInitialConditions.KbduRunway();
            initial.calibratedAirspeedKnots = 90.0;
            initial.altitudeMslMeters = GeodeticReference.Kbdu.altitudeMslMeters + 2000.0 * FlightFrameConversions.FeetToMeters;
            initial.pitchDegrees = 2.0;
            initial.engineRunning = false;
            if (!native.Reset(initial)) return double.NaN;
            AircraftControlState controls = AircraftControlState.Neutral(0f);
            controls.mixture = 0f;
            for (int i = 0; i < 60; i++)
            {
                native.SetControls(controls);
                if (!native.Advance(Dt)) return double.NaN;
            }
            FlightDynamicsState before = native.CurrentState;
            switch (axis)
            {
                case ControlAxis.Elevator: controls.elevator = command; break;
                case ControlAxis.Trim: controls.trim = command; break;
                case ControlAxis.Aileron: controls.aileron = command; break;
                case ControlAxis.Rudder: controls.rudder = command; break;
            }
            for (int i = 0; i < 120; i++)
            {
                native.SetControls(controls);
                if (!native.Advance(Dt)) return double.NaN;
            }
            FlightDynamicsState after = native.CurrentState;
            return axis switch
            {
                ControlAxis.Elevator => after.pitchDegrees - before.pitchDegrees,
                ControlAxis.Trim => after.pitchDegrees - before.pitchDegrees,
                ControlAxis.Aileron => after.bankDegrees - before.bankDegrees,
                _ => Mathf.DeltaAngle((float)before.headingDegrees, (float)after.headingDegrees)
            };
        }

        private static FlightDynamicsInitialConditions InitialConditions(ScenarioSpec spec)
        {
            FlightDynamicsInitialConditions initial = FlightDynamicsInitialConditions.KbduRunway();
            initial.calibratedAirspeedKnots = spec.initialAirspeedKnots;
            initial.altitudeMslMeters = GeodeticReference.Kbdu.altitudeMslMeters +
                                        spec.initialAglFeet * FlightFrameConversions.FeetToMeters;
            initial.flightPathAngleDegrees = spec.flightPathAngleDegrees;
            initial.pitchDegrees = spec.initialPitchDegrees;
            return initial;
        }

        private static IEnumerable<ScenarioSpec> ScenarioSpecs()
        {
            yield return new ScenarioSpec("idle_brake_hold", "Idle and brake hold", true, 8.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("taxi_acceleration_braking", "Taxi acceleration and braking", true, 12.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("takeoff_roll", "Takeoff roll", true, 24.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("rotation", "Rotation", true, 36.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("vy_climb", "Vy climb", true, 20.0, 74.0, 1000.0, 4.0, 5.0);
            yield return new ScenarioSpec("trimmed_level_flight", "Trimmed level flight", true, 20.0, 100.0, 1200.0, 0.0, 2.0);
            yield return new ScenarioSpec("shallow_turn_left", "Shallow coordinated turn left", true, 18.0, 95.0, 1200.0, 0.0, 2.0);
            yield return new ScenarioSpec("shallow_turn_right", "Shallow coordinated turn right", true, 18.0, 95.0, 1200.0, 0.0, 2.0);
            yield return new ScenarioSpec("slow_flight_stall_recovery", "Slow flight, stall warning and recovery", false, 32.0, 58.0, 1800.0, 0.0, 5.0);
            yield return new ScenarioSpec("approach", "Approach", true, 20.0, 70.0, 900.0, -3.0, 1.0);
            yield return new ScenarioSpec("go_around", "Go-around", true, 20.0, 65.0, 300.0, -2.0, 2.0);
        }

        private static void WriteReports(string directory, NativeValidationReport report)
        {
            File.WriteAllText(Path.Combine(directory, "jsbsim_native_scenario_report.json"), JsonUtility.ToJson(report, true));
            WriteCsv(Path.Combine(directory, "jsbsim_native_scenario_summary.csv"), report.scenarios);
            WriteMarkdown(Path.Combine(directory, "jsbsim_native_scenario_summary.md"), report);
        }

        private static void WriteCsv(string path, IEnumerable<NativeScenarioResult> scenarios)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("id,critical,pass,completed_steps,planned_steps,native_avg_ms,native_p95_ms,allocated_bytes,initial_kt,final_kt,initial_agl_ft,final_agl_ft,altitude_change_ft,min_vsi_fpm,max_vsi_fpm,heading_change_deg,max_abs_bank_deg,max_abs_pitch_deg,max_aileron,max_elevator,max_rudder,max_trim,max_flaps,max_brake,acceptance");
            foreach (NativeScenarioResult item in scenarios)
            {
                csv.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5:0.000000},{6:0.000000},{7},{8:0.000},{9:0.000},{10:0.000},{11:0.000},{12:0.000},{13:0.000},{14:0.000},{15:0.000},{16:0.000},{17:0.000},{18:0.000},{19:0.000},{20:0.000},{21:0.000},{22:0.000},{23:0.000},{24}\n",
                    item.id, item.critical, item.passed, item.completedSteps, item.plannedSteps,
                    item.nativeAverageStepMilliseconds, item.nativeP95StepMilliseconds,
                    item.managedAllocatedBytesDuringSteadySteps, item.initialAirspeedKnots, item.finalAirspeedKnots,
                    item.initialAglFeet, item.finalAglFeet, item.altitudeChangeFeet,
                    item.minVerticalSpeedFeetPerMinute, item.maxVerticalSpeedFeetPerMinute,
                    item.headingChangeDegrees, MaxAbs(item.minBankDegrees, item.maxBankDegrees),
                    MaxAbs(item.minPitchDegrees, item.maxPitchDegrees), item.maxAbsAileron,
                    item.maxAbsElevator, item.maxAbsRudder, item.maxAbsTrim, item.maxFlaps, item.maxBrake,
                    CsvEscape(item.acceptanceSummary));
            }
            File.WriteAllText(path, csv.ToString());
        }

        private static void WriteMarkdown(string path, NativeValidationReport report)
        {
            StringBuilder markdown = new StringBuilder();
            markdown.AppendLine("# JSBSim Native Production V2 Gate");
            markdown.AppendLine();
            markdown.AppendLine($"- Status: `{report.status}` ({report.passedCount}/{report.scenarioCount}; critical {report.criticalPassedCount}/{report.criticalScenarioCount})");
            markdown.AppendLine($"- Backend decision: `{report.productionBackendDecision}`");
            markdown.AppendLine($"- Reason: {report.nativePromotionReason}");
            markdown.AppendLine($"- Revision: `{report.jsbsimRevision}`");
            markdown.AppendLine($"- Timing average / worst p95: {report.nativeAverageStepMilliseconds:0.0000} / {report.nativeWorstP95StepMilliseconds:0.0000} ms");
            markdown.AppendLine($"- Zero steady-state allocation pass: `{report.zeroSteadyStateAllocationsPass}`");
            markdown.AppendLine();
            markdown.AppendLine("## Direct control contracts");
            markdown.AppendLine();
            markdown.AppendLine("| Axis | Project positive | Negative response | Positive response | Delta / required | Pass |");
            markdown.AppendLine("| --- | --- | ---: | ---: | ---: | --- |");
            foreach (ControlContractResult item in report.controlContracts)
            {
                markdown.AppendLine($"| {item.axis} | {item.projectPositiveMeaning} | {item.negativeCommandResponse:0.00} | {item.positiveCommandResponse:0.00} | {item.pairedResponseDelta:0.00} / {item.requiredDelta:0.00} | {item.passed} |");
            }
            markdown.AppendLine();
            markdown.AppendLine("## Scenarios");
            markdown.AppendLine();
            markdown.AppendLine("| Scenario | Critical | Pass | Speed kt initial/final | AGL ft initial/final | VSI range fpm | Heading | Native avg/p95 ms | Acceptance |");
            markdown.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |");
            foreach (NativeScenarioResult item in report.scenarios)
            {
                markdown.AppendLine($"| {item.name} | {item.critical} | {item.passed} | {item.initialAirspeedKnots:0.0}/{item.finalAirspeedKnots:0.0} | {item.initialAglFeet:0}/{item.finalAglFeet:0} | {item.minVerticalSpeedFeetPerMinute:0}/{item.maxVerticalSpeedFeetPerMinute:0} | {item.headingChangeDegrees:0.0}° | {item.nativeAverageStepMilliseconds:0.0000}/{item.nativeP95StepMilliseconds:0.0000} | {item.acceptanceSummary.Replace("|", "/")} |");
            }
            markdown.AppendLine();
            markdown.AppendLine("## Initialization and schedule audit");
            markdown.AppendLine();
            markdown.AppendLine(report.initializationAudit);
            markdown.AppendLine();
            markdown.AppendLine(report.trimAudit);
            markdown.AppendLine();
            markdown.AppendLine(report.scheduleAudit);
            File.WriteAllText(path, markdown.ToString());
        }

        private static void Require(bool condition, string failure, List<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static double MaxAbs(double minimum, double maximum) => Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        private static double Square(double value) => value * value;
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double FiniteOrZero(double value) => IsFinite(value) ? value : 0.0;
        private static string CsvEscape(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private enum ControlAxis { Elevator, Trim, Aileron, Rudder }

        private readonly struct ScenarioSpec
        {
            public readonly string id;
            public readonly string name;
            public readonly bool critical;
            public readonly double durationSeconds;
            public readonly double initialAirspeedKnots;
            public readonly double initialAglFeet;
            public readonly double flightPathAngleDegrees;
            public readonly double initialPitchDegrees;

            public ScenarioSpec(string id, string name, bool critical, double durationSeconds,
                double initialAirspeedKnots, double initialAglFeet, double flightPathAngleDegrees,
                double initialPitchDegrees)
            {
                this.id = id;
                this.name = name;
                this.critical = critical;
                this.durationSeconds = durationSeconds;
                this.initialAirspeedKnots = initialAirspeedKnots;
                this.initialAglFeet = initialAglFeet;
                this.flightPathAngleDegrees = flightPathAngleDegrees;
                this.initialPitchDegrees = initialPitchDegrees;
            }
        }

        private sealed class ScriptedPilot
        {
            private const float ElevatorRate = 0.80f;
            private const float AileronRate = 1.00f;
            private const float RudderRate = 1.20f;
            private const float ThrottleRate = 0.75f;
            private const float TrimRate = 0.12f;
            private const float FlapRate = 0.20f;
            private double _pitchTrimIntegral;
            private double _bankIntegral;
            public AircraftControlState Controls { get; } = AircraftControlState.Neutral(0f);

            public void Apply(string id, double time, FlightDynamicsState state)
            {
                Controls.leftToeBrake = 0f;
                Controls.rightToeBrake = 0f;
                switch (id)
                {
                    case "idle_brake_hold":
                        ApplyAttitude(state, 0f, 0f, 0.12f, 0f, 0f, RunwayHeading, true);
                        Controls.leftToeBrake = Controls.rightToeBrake = 1f;
                        break;
                    case "taxi_acceleration_braking":
                        ApplyAttitude(state, 0f, 0f, time < 6.0 ? 0.55f : 0.10f, 0f, 0f, RunwayHeading, true);
                        if (time >= 6.0)
                            Controls.leftToeBrake = Controls.rightToeBrake = (float)Clamp((time - 6.0) / 2.0, 0.0, 1.0);
                        break;
                    case "takeoff_roll":
                        ApplyAttitude(state, 1.5f, 0f, (float)Clamp(time / 2.0, 0.0, 1.0), 0f, 0f, RunwayHeading, true);
                        break;
                    case "rotation":
                        float rotationPitch = time < 18.0 ? 0f : time < 28.0 ? 9f : 7f;
                        ApplyAttitude(state, rotationPitch, 0f, (float)Clamp(time / 2.0, 0.0, 1.0),
                            time > 18.0 ? 0.05f : 0f, 0f, RunwayHeading,
                            state.altitudeAglMeters * FlightFrameConversions.MetersToFeet < 8.0);
                        break;
                    case "vy_climb":
                        ApplyAttitude(state, 8f, 0f, 1f, 0.05f, 0f, RunwayHeading, false);
                        break;
                    case "trimmed_level_flight":
                        ApplyAttitude(state, 2f, 0f,
                            (float)Clamp(0.68 + 0.02 * (100.0 - state.calibratedAirspeedKnots), 0.40, 0.90),
                            0f, 0f, RunwayHeading, false);
                        break;
                    case "shallow_turn_left":
                        ApplyAttitude(state, 2f, -18f,
                            (float)Clamp(0.70 + 0.018 * (95.0 - state.calibratedAirspeedKnots), 0.45, 0.90),
                            0f, 0f, null, false);
                        break;
                    case "shallow_turn_right":
                        ApplyAttitude(state, 2f, 18f,
                            (float)Clamp(0.70 + 0.018 * (95.0 - state.calibratedAirspeedKnots), 0.45, 0.90),
                            0f, 0f, null, false);
                        break;
                    case "slow_flight_stall_recovery":
                        if (time < 10.0)
                        {
                            ApplyAttitude(state, 6f, 0f,
                                (float)Clamp(0.54 + 0.025 * (55.0 - state.calibratedAirspeedKnots), 0.35, 0.80),
                                0.06f, 0.33f, RunwayHeading, false);
                        }
                        else if (time < 18.0)
                        {
                            ApplyAttitude(state, 13f, 0f, 0.18f, 0.09f, 0.33f, RunwayHeading, false);
                        }
                        else
                        {
                            float recoveryFlaps = (float)Clamp(0.33 - (time - 18.0) * 0.08, 0.0, 0.33);
                            ApplyAttitude(state, 1f, 0f, 1f, 0f, recoveryFlaps, RunwayHeading, false);
                        }
                        break;
                    case "approach":
                        ApplyAttitude(state, -2f, 0f,
                            (float)Clamp(0.38 + 0.02 * (70.0 - state.calibratedAirspeedKnots), 0.20, 0.65),
                            0.01f, 1f, RunwayHeading, false);
                        break;
                    case "go_around":
                        float goAroundFlaps = time < 3.0 ? 0.66f : time < 7.0 ? 0.33f : 0f;
                        ApplyAttitude(state, 8f, 0f, 1f, 0.04f, goAroundFlaps, RunwayHeading, false);
                        break;
                }
            }

            private void ApplyAttitude(
                FlightDynamicsState state,
                float targetPitch,
                float targetBank,
                float targetThrottle,
                float feedForwardTrim,
                float targetFlaps,
                double? targetHeading,
                bool groundSteering)
            {
                double headingError = targetHeading.HasValue
                    ? Mathf.DeltaAngle((float)state.headingDegrees, (float)targetHeading.Value)
                    : 0.0;
                if (targetHeading.HasValue && !groundSteering)
                    targetBank = (float)Clamp(0.50 * headingError, -12.0, 12.0);
                double pitchError = Clamp(targetPitch - state.pitchDegrees, -15.0, 15.0);
                double bankError = Clamp(targetBank - state.bankDegrees, -30.0, 30.0);
                _pitchTrimIntegral = Clamp(_pitchTrimIntegral + pitchError * Dt * 0.004, -0.12, 0.12);
                _bankIntegral = Clamp(_bankIntegral + bankError * Dt * 0.002, -0.08, 0.08);
                // Body-rate conversion stores JSBSim q/p/r as -x/-z/+y.
                double pitchRate = -state.angularVelocityBodyDegreesPerSecond.x;
                double rollRate = -state.angularVelocityBodyDegreesPerSecond.z;
                double yawRate = state.angularVelocityBodyDegreesPerSecond.y;
                float elevatorTarget = (float)Clamp(0.035 * pitchError - 0.030 * pitchRate, -0.45, 0.45);
                float aileronTarget = (float)Clamp(0.035 * bankError - 0.045 * rollRate + _bankIntegral, -0.60, 0.60);
                float rudderTarget = (float)Clamp(0.018 * state.sideslipDegrees - 0.035 * yawRate, -0.30, 0.30);
                if (targetHeading.HasValue)
                    rudderTarget = (float)Clamp(0.045 * headingError - 0.050 * yawRate, -0.75, 0.75);
                float trimTarget = (float)Clamp(feedForwardTrim + _pitchTrimIntegral, -0.30, 0.30);

                Controls.elevator = Move(Controls.elevator, elevatorTarget, ElevatorRate);
                Controls.aileron = Move(Controls.aileron, aileronTarget, AileronRate);
                Controls.rudder = Move(Controls.rudder, rudderTarget, RudderRate);
                Controls.throttle = Move(Controls.throttle, targetThrottle, ThrottleRate);
                Controls.trim = Move(Controls.trim, trimTarget, TrimRate);
                Controls.flaps = Move(Controls.flaps, targetFlaps, FlapRate);
                Controls.mixture = 1f;
                Controls.carbHeat = 0f;

                if (groundSteering && state.calibratedAirspeedKnots < 35.0)
                {
                    double differentialBrake = Clamp(-headingError * 0.018, -0.28, 0.28);
                    if (differentialBrake > 0.0) Controls.leftToeBrake = (float)differentialBrake;
                    else Controls.rightToeBrake = (float)-differentialBrake;
                }
            }

            private static float Move(float current, float target, float ratePerSecond)
            {
                return Mathf.MoveTowards(current, target, ratePerSecond * (float)Dt);
            }
        }

        private static double Clamp(double value, double minimum, double maximum) => Math.Max(minimum, Math.Min(maximum, value));

        [Serializable]
        private sealed class NativeValidationReport
        {
            public string generatedUtc;
            public string status;
            public string classification;
            public string unityVersion;
            public string jsbsimVersion;
            public string jsbsimRevision;
            public string dataRoot;
            public double fixedStepHz;
            public GeodeticReference localOrigin;
            public string baselineResult;
            public string baselineEvidence;
            public string initializationAudit;
            public string trimAudit;
            public string scheduleAudit;
            public int scenarioCount;
            public int passedCount;
            public int criticalScenarioCount;
            public int criticalPassedCount;
            public bool allCriticalScenariosPass;
            public bool controlContractPass;
            public int totalNativeSteps;
            public double nativeAverageStepMilliseconds;
            public double nativeWorstP95StepMilliseconds;
            public bool windowsPerformanceTargetPass;
            public bool zeroSteadyStateAllocationsPass;
            public string productionBackendDecision;
            public string nativePromotionReason;
            public string fatalError;
            public List<ControlContractResult> controlContracts = new List<ControlContractResult>();
            public List<NativeScenarioResult> scenarios = new List<NativeScenarioResult>();
            public string[] limitations;
        }

        [Serializable]
        private sealed class ControlContractResult
        {
            public string axis;
            public string projectPositiveMeaning;
            public string measuredQuantity;
            public double negativeCommandResponse;
            public double positiveCommandResponse;
            public double pairedResponseDelta;
            public double requiredDelta;
            public bool passed;
            public string beforeAdapterMapping;
            public string correctedAdapterMapping;
        }

        [Serializable]
        private sealed class NativeScenarioResult
        {
            public string id;
            public string name;
            public bool critical;
            public bool passed;
            public string acceptanceSummary;
            public string runtimeFailure;
            public double durationSeconds;
            public int plannedSteps;
            public int completedSteps;
            public double nativeAverageStepMilliseconds;
            public double nativeP95StepMilliseconds;
            public long managedAllocatedBytesDuringSteadySteps;
            public double initialAirspeedKnots;
            public double finalAirspeedKnots;
            public double minAirspeedKnots;
            public double maxAirspeedKnots;
            public double initialAglFeet;
            public double finalAglFeet;
            public double altitudeChangeFeet;
            public double minAglFeet;
            public double maxAglFeet;
            public double finalVerticalSpeedFeetPerMinute;
            public double minVerticalSpeedFeetPerMinute;
            public double maxVerticalSpeedFeetPerMinute;
            public double secondHalfMinAirspeedKnots;
            public double secondHalfMaxAirspeedKnots;
            public double secondHalfMinAglFeet;
            public double secondHalfMaxAglFeet;
            public double secondHalfMaxVerticalSpeedFeetPerMinute;
            public double slowPhaseMinAirspeedKnots;
            public double slowPhaseMaxAirspeedKnots;
            public double preRecoveryMinAirspeedKnots;
            public double postRecoveryMaxAirspeedKnots;
            public double recoveryMaxVerticalSpeedFeetPerMinute;
            public double minPitchDegrees;
            public double maxPitchDegrees;
            public double finalPitchDegrees;
            public double minBankDegrees;
            public double maxBankDegrees;
            public double finalBankDegrees;
            public double initialHeadingDegrees;
            public double finalHeadingDegrees;
            public double headingChangeDegrees;
            public double initialEngineRpm;
            public double minEngineRpm;
            public double maxEngineRpm;
            public double finalEngineRpm;
            public bool finalWeightOnWheels;
            public double maxGroundDistanceMeters;
            public double timeToRotationSeconds;
            public double maxThrottle;
            public double maxAbsAileron;
            public double maxAbsElevator;
            public double maxAbsRudder;
            public double maxAbsTrim;
            public double maxFlaps;
            public double maxBrake;
            public double airspeedRmseVsUnityKnots;
            public double aglRmseVsUnityFeet;
            public double pitchRmseVsUnityDegrees;
            public double bankRmseVsUnityDegrees;
            public double headingRmseVsUnityDegrees;
            public double unityFinalAirspeedKnots;
            public double unityFinalAglFeet;
        }
    }
}
