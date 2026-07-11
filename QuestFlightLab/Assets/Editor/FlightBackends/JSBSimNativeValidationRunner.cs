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
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace QuestFlightLab.Editor
{
    public static class JSBSimNativeValidationRunner
    {
        private const string OutputEnvironmentVariable = "QFL_JSBSIM_NATIVE_VALIDATION_DIR";
        private const double FixedStepSeconds = 1.0 / 120.0;

        [MenuItem("Quest Flight Lab/Run JSBSim Native Validation")]
        public static void Run()
        {
            string outputDirectory = System.Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.GetFullPath(Path.Combine("..", "T2-QuestFlightLab-setup-artifacts", $"jsbsim_native_validation_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }
            Directory.CreateDirectory(outputDirectory);

            JSBSimPluginImporterConfigurator.Configure();
            if (!JSBSimNativeFlightBackend.ProbeLibrary(out string probeError))
            {
                throw new InvalidOperationException($"Native JSBSim plugin probe failed: {probeError}");
            }

            GameObject nativeRoot = new GameObject("JSBSimNativeValidationRoot");
            GameObject unityRoot = new GameObject("UnityPrototypeValidationRoot");
            AircraftState unityState = unityRoot.AddComponent<AircraftState>();
            SimpleAircraftPhysics unityPhysics = unityRoot.AddComponent<SimpleAircraftPhysics>();
            unityPhysics.state = unityState;
            C172StyleAircraftConfig runtimeConfig = C172StyleAircraftConfig.CreateRuntimeDefault();
            unityPhysics.config = runtimeConfig;
            unityState.config = runtimeConfig;

            JSBSimNativeFlightBackend native = new JSBSimNativeFlightBackend();
            UnityPrototypeFlightBackend unity = new UnityPrototypeFlightBackend();
            try
            {
                FlightDynamicsBackendContext nativeContext = new FlightDynamicsBackendContext
                {
                    simulationRoot = nativeRoot.transform,
                    localOrigin = GeodeticReference.Kbdu,
                    jsbsimAircraft = "c172x",
                    jsbsimDataRoot = JSBSimRuntimeDataPaths.BundledDataRoot,
                    fixedDeltaTimeSeconds = FixedStepSeconds
                };
                FlightDynamicsBackendContext unityContext = new FlightDynamicsBackendContext
                {
                    simulationRoot = unityRoot.transform,
                    aircraftState = unityState,
                    unityPrototype = unityPhysics,
                    localOrigin = GeodeticReference.Kbdu,
                    fixedDeltaTimeSeconds = FixedStepSeconds
                };
                if (!native.Initialize(nativeContext)) throw new InvalidOperationException(native.LastError);
                if (!unity.Initialize(unityContext)) throw new InvalidOperationException(unity.LastError);

                NativeValidationReport report = new NativeValidationReport
                {
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    status = "PASS",
                    classification = "native_jsbsim_10_scenario_gate_with_unity_trend_comparison",
                    unityVersion = Application.unityVersion,
                    jsbsimVersion = JSBSimNativeFlightBackend.PinnedJsbsimVersion,
                    jsbsimRevision = JSBSimNativeFlightBackend.PinnedJsbsimRevision,
                    dataRoot = JSBSimRuntimeDataPaths.BundledDataRoot,
                    fixedStepHz = 120.0,
                    localOrigin = GeodeticReference.Kbdu,
                    limitations = new[]
                    {
                        "Unity comparison is matched schedule/trend evidence, not an assertion that the prototype should numerically match JSBSim.",
                        "Windows native timing is not Quest CPU timing.",
                        "The stock JSBSim c172x setup and open-loop controls are not final C172 fidelity or a validated control law."
                    }
                };

                foreach (ScenarioSpec scenario in ScenarioSpecs())
                {
                    NativeScenarioResult result = RunScenario(scenario, native, unity);
                    report.scenarios.Add(result);
                    if (!result.passed) report.status = "FAIL";
                }

                report.scenarioCount = report.scenarios.Count;
                report.passedCount = report.scenarios.Count(item => item.passed);
                report.totalNativeSteps = report.scenarios.Sum(item => item.stepCount);
                report.nativeAverageStepMilliseconds = report.scenarios.Sum(item => item.nativeAverageStepMilliseconds * item.stepCount) /
                                                       Math.Max(1, report.totalNativeSteps);
                report.nativeWorstP95StepMilliseconds = report.scenarios.Max(item => item.nativeP95StepMilliseconds);
                report.windowsPerformanceTargetPass = report.nativeAverageStepMilliseconds < 0.5 && report.nativeWorstP95StepMilliseconds < 1.0;

                string jsonPath = Path.Combine(outputDirectory, "jsbsim_native_scenario_report.json");
                string csvPath = Path.Combine(outputDirectory, "jsbsim_native_scenario_summary.csv");
                File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
                WriteCsv(csvPath, report.scenarios);
                WriteMarkdown(Path.Combine(outputDirectory, "jsbsim_native_scenario_summary.md"), report);
                Debug.Log($"[QuestFlightLab][JSBSimNative] {report.status}: {report.passedCount}/{report.scenarioCount} scenarios; avg {report.nativeAverageStepMilliseconds:0.0000} ms, worst p95 {report.nativeWorstP95StepMilliseconds:0.0000} ms. {outputDirectory}");
                if (report.status != "PASS") throw new InvalidOperationException("One or more native JSBSim scenarios failed physical acceptance. See report.");
            }
            finally
            {
                native.Dispose();
                unity.Dispose();
                UnityEngine.Object.DestroyImmediate(nativeRoot);
                UnityEngine.Object.DestroyImmediate(unityRoot);
                if (runtimeConfig != null) UnityEngine.Object.DestroyImmediate(runtimeConfig);
            }
        }

        private static NativeScenarioResult RunScenario(
            ScenarioSpec spec,
            JSBSimNativeFlightBackend native,
            UnityPrototypeFlightBackend unity)
        {
            FlightDynamicsInitialConditions initial = InitialConditions(spec);
            if (!native.Reset(initial)) throw new InvalidOperationException($"{spec.id} native reset: {native.LastError}");
            if (!unity.Reset(initial)) throw new InvalidOperationException($"{spec.id} Unity reset: {unity.LastError}");

            int steps = (int)Math.Ceiling(spec.durationSeconds / FixedStepSeconds);
            double[] timings = new double[steps];
            AircraftControlState controls = AircraftControlState.Neutral(0.0f);
            FlightDynamicsState nativeStart = native.CurrentState;
            FlightDynamicsState unityStart = unity.CurrentState;
            NativeScenarioResult result = new NativeScenarioResult
            {
                id = spec.id,
                name = spec.name,
                durationSeconds = spec.durationSeconds,
                stepCount = steps,
                initialAirspeedKnots = nativeStart.calibratedAirspeedKnots,
                initialAglFeet = nativeStart.altitudeAglMeters * FlightFrameConversions.MetersToFeet,
                minAirspeedKnots = double.PositiveInfinity,
                minAglFeet = double.PositiveInfinity,
                minPitchDegrees = double.PositiveInfinity,
                minBankDegrees = double.PositiveInfinity,
                minVerticalSpeedFeetPerMinute = double.PositiveInfinity,
                maxAirspeedKnots = double.NegativeInfinity,
                maxAglFeet = double.NegativeInfinity,
                maxVerticalSpeedFeetPerMinute = double.NegativeInfinity,
                maxPitchDegrees = double.NegativeInfinity,
                maxBankDegrees = double.NegativeInfinity,
                secondHalfMinAirspeedKnots = double.PositiveInfinity,
                secondHalfMaxAirspeedKnots = double.NegativeInfinity,
                secondHalfMinAglFeet = double.PositiveInfinity,
                secondHalfMaxAglFeet = double.NegativeInfinity,
                secondHalfMinVerticalSpeedFeetPerMinute = double.PositiveInfinity,
                secondHalfMaxVerticalSpeedFeetPerMinute = double.NegativeInfinity,
                firstTurnMinBankDegrees = spec.id == "shallow_coordinated_left_right_turn" ? double.PositiveInfinity : 0.0,
                firstTurnMaxBankDegrees = spec.id == "shallow_coordinated_left_right_turn" ? double.NegativeInfinity : 0.0,
                secondTurnMinBankDegrees = spec.id == "shallow_coordinated_left_right_turn" ? double.PositiveInfinity : 0.0,
                secondTurnMaxBankDegrees = spec.id == "shallow_coordinated_left_right_turn" ? double.NegativeInfinity : 0.0,
                preRecoveryMinAirspeedKnots = spec.id == "stall_approach_recovery" ? double.PositiveInfinity : 0.0,
                postRecoveryMaxAirspeedKnots = spec.id == "stall_approach_recovery" ? double.NegativeInfinity : 0.0,
                timeToRotationSeconds = -1.0
            };
            double speedSquaredError = 0.0;
            double altitudeSquaredError = 0.0;
            double pitchSquaredError = 0.0;
            double bankSquaredError = 0.0;
            double headingSquaredError = 0.0;
            double firstTurnEndHeading = nativeStart.headingDegrees;
            double secondTurnStartHeading = nativeStart.headingDegrees;
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();

            for (int step = 0; step < steps; step++)
            {
                double time = step * FixedStepSeconds;
                ApplySchedule(spec.id, time, controls);
                native.SetControls(controls);
                unity.SetControls(controls);

                long startTicks = Stopwatch.GetTimestamp();
                bool nativeAdvanced = native.Advance(FixedStepSeconds);
                long endTicks = Stopwatch.GetTimestamp();
                if (!nativeAdvanced) throw new InvalidOperationException($"{spec.id} native step {step}: {native.LastError}");
                if (!unity.Advance(FixedStepSeconds)) throw new InvalidOperationException($"{spec.id} Unity step {step}: {unity.LastError}");
                timings[step] = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;

                FlightDynamicsState n = native.CurrentState;
                FlightDynamicsState u = unity.CurrentState;
                if (!n.IsFinite) throw new InvalidOperationException($"{spec.id} native returned non-finite state at step {step}.");
                double nativeAglFeet = n.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
                double unityAglFeet = u.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
                result.minAirspeedKnots = Math.Min(result.minAirspeedKnots, n.calibratedAirspeedKnots);
                result.maxAirspeedKnots = Math.Max(result.maxAirspeedKnots, n.calibratedAirspeedKnots);
                result.minAglFeet = Math.Min(result.minAglFeet, nativeAglFeet);
                result.maxAglFeet = Math.Max(result.maxAglFeet, nativeAglFeet);
                result.minVerticalSpeedFeetPerMinute = Math.Min(result.minVerticalSpeedFeetPerMinute, n.verticalSpeedFeetPerMinute);
                result.maxVerticalSpeedFeetPerMinute = Math.Max(result.maxVerticalSpeedFeetPerMinute, n.verticalSpeedFeetPerMinute);
                result.minPitchDegrees = Math.Min(result.minPitchDegrees, n.pitchDegrees);
                result.maxPitchDegrees = Math.Max(result.maxPitchDegrees, n.pitchDegrees);
                result.minBankDegrees = Math.Min(result.minBankDegrees, n.bankDegrees);
                result.maxBankDegrees = Math.Max(result.maxBankDegrees, n.bankDegrees);
                result.maxGroundDistanceMeters = Math.Max(result.maxGroundDistanceMeters, Vector3.Distance(nativeStart.positionUnityMeters, n.positionUnityMeters));
                if (result.timeToRotationSeconds < 0.0 && nativeAglFeet > result.initialAglFeet + 2.0) result.timeToRotationSeconds = time;
                result.maxThrottle = Math.Max(result.maxThrottle, controls.throttle);
                result.maxAbsAileron = Math.Max(result.maxAbsAileron, Math.Abs(controls.aileron));
                result.maxAbsElevator = Math.Max(result.maxAbsElevator, Math.Abs(controls.elevator));
                result.maxAbsRudder = Math.Max(result.maxAbsRudder, Math.Abs(controls.rudder));
                result.maxFlaps = Math.Max(result.maxFlaps, controls.flaps);
                result.maxAbsTrim = Math.Max(result.maxAbsTrim, Math.Abs(controls.trim));
                result.maxBrake = Math.Max(result.maxBrake, Math.Max(controls.leftToeBrake, controls.rightToeBrake));
                if (time >= spec.durationSeconds * 0.5)
                {
                    result.secondHalfMinAirspeedKnots = Math.Min(result.secondHalfMinAirspeedKnots, n.calibratedAirspeedKnots);
                    result.secondHalfMaxAirspeedKnots = Math.Max(result.secondHalfMaxAirspeedKnots, n.calibratedAirspeedKnots);
                    result.secondHalfMinAglFeet = Math.Min(result.secondHalfMinAglFeet, nativeAglFeet);
                    result.secondHalfMaxAglFeet = Math.Max(result.secondHalfMaxAglFeet, nativeAglFeet);
                    result.secondHalfMinVerticalSpeedFeetPerMinute = Math.Min(result.secondHalfMinVerticalSpeedFeetPerMinute, n.verticalSpeedFeetPerMinute);
                    result.secondHalfMaxVerticalSpeedFeetPerMinute = Math.Max(result.secondHalfMaxVerticalSpeedFeetPerMinute, n.verticalSpeedFeetPerMinute);
                }
                if (spec.id == "shallow_coordinated_left_right_turn")
                {
                    if (time < 7.0)
                    {
                        result.firstTurnMinBankDegrees = Math.Min(result.firstTurnMinBankDegrees, n.bankDegrees);
                        result.firstTurnMaxBankDegrees = Math.Max(result.firstTurnMaxBankDegrees, n.bankDegrees);
                        firstTurnEndHeading = n.headingDegrees;
                    }
                    else if (time >= 9.0)
                    {
                        if (result.secondTurnMinBankDegrees == double.PositiveInfinity) secondTurnStartHeading = n.headingDegrees;
                        result.secondTurnMinBankDegrees = Math.Min(result.secondTurnMinBankDegrees, n.bankDegrees);
                        result.secondTurnMaxBankDegrees = Math.Max(result.secondTurnMaxBankDegrees, n.bankDegrees);
                    }
                }
                if (spec.id == "stall_approach_recovery")
                {
                    if (time < 12.0) result.preRecoveryMinAirspeedKnots = Math.Min(result.preRecoveryMinAirspeedKnots, n.calibratedAirspeedKnots);
                    else result.postRecoveryMaxAirspeedKnots = Math.Max(result.postRecoveryMaxAirspeedKnots, n.calibratedAirspeedKnots);
                }
                speedSquaredError += Square(n.calibratedAirspeedKnots - u.calibratedAirspeedKnots);
                altitudeSquaredError += Square(nativeAglFeet - unityAglFeet);
                pitchSquaredError += Square(n.pitchDegrees - u.pitchDegrees);
                bankSquaredError += Square(n.bankDegrees - u.bankDegrees);
                headingSquaredError += Square(Mathf.DeltaAngle((float)n.headingDegrees, (float)u.headingDegrees));
            }

            result.managedAllocatedBytesDuringSteadySteps = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            Array.Sort(timings);
            result.nativeAverageStepMilliseconds = timings.Average();
            result.nativeP95StepMilliseconds = timings[(int)Math.Floor((timings.Length - 1) * 0.95)];
            FlightDynamicsState nativeFinal = native.CurrentState;
            FlightDynamicsState unityFinal = unity.CurrentState;
            result.finalAirspeedKnots = nativeFinal.calibratedAirspeedKnots;
            result.finalAglFeet = nativeFinal.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
            result.finalVerticalSpeedFeetPerMinute = nativeFinal.verticalSpeedFeetPerMinute;
            result.finalPitchDegrees = nativeFinal.pitchDegrees;
            result.finalBankDegrees = nativeFinal.bankDegrees;
            result.finalHeadingDegrees = nativeFinal.headingDegrees;
            result.headingChangeDegrees = Mathf.DeltaAngle((float)nativeStart.headingDegrees, (float)nativeFinal.headingDegrees);
            if (spec.id == "shallow_coordinated_left_right_turn")
            {
                result.firstTurnHeadingChangeDegrees = Mathf.DeltaAngle((float)nativeStart.headingDegrees, (float)firstTurnEndHeading);
                result.secondTurnHeadingChangeDegrees = Mathf.DeltaAngle((float)secondTurnStartHeading, (float)nativeFinal.headingDegrees);
            }
            result.meanTurnRateDegreesPerSecond = result.headingChangeDegrees / spec.durationSeconds;
            result.altitudeChangeFeet = result.finalAglFeet - result.initialAglFeet;
            result.airspeedRmseVsUnityKnots = Math.Sqrt(speedSquaredError / steps);
            result.aglRmseVsUnityFeet = Math.Sqrt(altitudeSquaredError / steps);
            result.pitchRmseVsUnityDegrees = Math.Sqrt(pitchSquaredError / steps);
            result.bankRmseVsUnityDegrees = Math.Sqrt(bankSquaredError / steps);
            result.headingRmseVsUnityDegrees = Math.Sqrt(headingSquaredError / steps);
            result.unityFinalAirspeedKnots = unityFinal.calibratedAirspeedKnots;
            result.unityFinalAglFeet = unityFinal.altitudeAglMeters * FlightFrameConversions.MetersToFeet;
            bool finiteAndTimePass = nativeFinal.IsFinite && Math.Abs(nativeFinal.simulationTimeSeconds - spec.durationSeconds) < 0.02;
            result.passed = EvaluatePhysicalAcceptance(spec, result, finiteAndTimePass, out result.acceptanceSummary);
            result.comparisonClassification = "matched_controls_trend_gap_not_acceptance";
            return result;
        }

        private static bool EvaluatePhysicalAcceptance(
            ScenarioSpec spec,
            NativeScenarioResult result,
            bool finiteAndTimePass,
            out string summary)
        {
            List<string> failures = new List<string>();
            if (!finiteAndTimePass) failures.Add("non-finite state or incorrect fixed-step duration");

            switch (spec.id)
            {
                case "ground_idle_brake":
                    Require(result.maxAirspeedKnots <= 2.0, "brake hold exceeded 2 kt", failures);
                    Require(result.maxGroundDistanceMeters <= 2.0, "brake hold moved more than 2 m", failures);
                    Require(result.minAglFeet >= 0.0, "gear penetrated terrain", failures);
                    break;
                case "takeoff_roll":
                    Require(result.finalAirspeedKnots >= 40.0, "did not accelerate to 40 kt", failures);
                    Require(result.maxGroundDistanceMeters >= 150.0, "ground roll was shorter than 150 m", failures);
                    Require(result.maxAglFeet <= result.initialAglFeet + 3.0, "uncommanded liftoff during roll-only case", failures);
                    Require(Math.Abs(result.headingChangeDegrees) <= 15.0, "departed runway heading by more than 15 degrees", failures);
                    break;
                case "rotation_near_vr":
                    Require(result.maxAirspeedKnots >= 50.0, "never reached a credible rotation speed", failures);
                    Require(result.timeToRotationSeconds >= 15.0 && result.timeToRotationSeconds <= 34.0, "liftoff timing outside 15-34 s", failures);
                    Require(result.maxAglFeet >= result.initialAglFeet + 5.0, "did not climb at least 5 ft after rotation", failures);
                    Require(result.minAglFeet >= 0.0, "gear penetrated terrain", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    Require(result.maxPitchDegrees <= 25.0 && result.minPitchDegrees >= -10.0, "pitch departed -10/+25 degree envelope", failures);
                    break;
                case "vy_climb":
                    Require(result.altitudeChangeFeet >= 100.0, "did not gain 100 ft", failures);
                    Require(result.secondHalfMaxVerticalSpeedFeetPerMinute >= 300.0, "second half never exceeded +300 fpm", failures);
                    Require(result.finalAirspeedKnots >= 60.0 && result.finalAirspeedKnots <= 90.0, "final airspeed outside 60-90 kt", failures);
                    Require(result.minAglFeet >= result.initialAglFeet - 100.0, "lost more than 100 ft in climb case", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    break;
                case "level_flight_trim":
                    Require(Math.Abs(result.altitudeChangeFeet) <= 200.0, "altitude drift exceeded 200 ft", failures);
                    Require(result.finalVerticalSpeedFeetPerMinute >= -1000.0 && result.finalVerticalSpeedFeetPerMinute <= 1000.0, "final vertical speed exceeded +/-1000 fpm", failures);
                    Require(MaxAbs(result.minPitchDegrees, result.maxPitchDegrees) <= 20.0, "pitch exceeded 20 degrees", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 20.0, "bank exceeded 20 degrees", failures);
                    break;
                case "shallow_coordinated_left_right_turn":
                    Require(result.firstTurnMinBankDegrees <= -5.0, "left phase did not establish 5 degrees left bank", failures);
                    Require(result.secondTurnMaxBankDegrees >= 5.0, "right phase did not establish 5 degrees right bank", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 35.0, "bank exceeded shallow-turn envelope", failures);
                    Require(result.firstTurnHeadingChangeDegrees < -2.0, "left phase heading did not decrease", failures);
                    Require(result.secondTurnHeadingChangeDegrees > 2.0, "right phase heading did not increase", failures);
                    Require(Math.Abs(result.altitudeChangeFeet) <= 300.0, "turn altitude drift exceeded 300 ft", failures);
                    break;
                case "slow_flight":
                    Require(result.secondHalfMinAirspeedKnots >= 40.0 && result.secondHalfMaxAirspeedKnots <= 80.0, "second-half speed left 40-80 kt envelope", failures);
                    Require(result.minAglFeet >= result.initialAglFeet - 250.0, "lost more than 250 ft", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    break;
                case "stall_approach_recovery":
                    Require(result.preRecoveryMinAirspeedKnots <= 50.0, "approach phase did not decelerate to 50 kt", failures);
                    Require(result.postRecoveryMaxAirspeedKnots >= result.preRecoveryMinAirspeedKnots + 10.0, "recovery did not regain 10 kt", failures);
                    Require(result.secondHalfMaxVerticalSpeedFeetPerMinute > 0.0, "recovery never established positive vertical speed", failures);
                    Require(result.minAglFeet >= result.initialAglFeet - 600.0, "recovery lost more than 600 ft", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 45.0, "bank exceeded 45 degrees", failures);
                    break;
                case "descent_final_approach":
                    Require(result.altitudeChangeFeet <= -100.0 && result.altitudeChangeFeet >= -800.0, "descent was outside 100-800 ft", failures);
                    Require(result.finalAglFeet >= 100.0, "descended below 100 ft AGL", failures);
                    Require(result.finalAirspeedKnots >= 55.0 && result.finalAirspeedKnots <= 85.0, "final airspeed outside 55-85 kt", failures);
                    Require(result.finalVerticalSpeedFeetPerMinute >= -1200.0 && result.finalVerticalSpeedFeetPerMinute <= -200.0, "final descent rate outside -1200/-200 fpm", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 25.0, "bank exceeded 25 degrees", failures);
                    break;
                case "go_around":
                    Require(result.secondHalfMaxAglFeet >= result.secondHalfMinAglFeet + 75.0, "second half did not recover 75 ft", failures);
                    Require(result.secondHalfMaxVerticalSpeedFeetPerMinute >= 300.0, "second half never exceeded +300 fpm", failures);
                    Require(result.finalAglFeet >= 100.0, "ended below 100 ft AGL", failures);
                    Require(result.finalAirspeedKnots >= 55.0 && result.finalAirspeedKnots <= 90.0, "final airspeed outside 55-90 kt", failures);
                    Require(MaxAbs(result.minBankDegrees, result.maxBankDegrees) <= 30.0, "bank exceeded 30 degrees", failures);
                    break;
            }

            summary = failures.Count == 0 ? "PASS: scenario-specific physical acceptance met" : "FAIL: " + string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static void Require(bool condition, string failure, List<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static double MaxAbs(double minimum, double maximum)
        {
            return Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        }

        private static FlightDynamicsInitialConditions InitialConditions(ScenarioSpec spec)
        {
            FlightDynamicsInitialConditions initial = FlightDynamicsInitialConditions.KbduRunway();
            initial.calibratedAirspeedKnots = spec.initialAirspeedKnots;
            initial.altitudeMslMeters = GeodeticReference.Kbdu.altitudeMslMeters + spec.initialAglFeet * FlightFrameConversions.FeetToMeters;
            initial.flightPathAngleDegrees = spec.flightPathAngleDegrees;
            initial.pitchDegrees = spec.initialPitchDegrees;
            return initial;
        }

        private static void ApplySchedule(string id, double time, AircraftControlState controls)
        {
            controls.aileron = 0f;
            controls.elevator = 0f;
            controls.rudder = 0f;
            controls.throttle = 0f;
            controls.mixture = 1f;
            controls.carbHeat = 0f;
            controls.trim = 0f;
            controls.flaps = 0f;
            controls.leftToeBrake = 0f;
            controls.rightToeBrake = 0f;

            switch (id)
            {
                case "ground_idle_brake":
                    controls.throttle = 0.12f;
                    controls.leftToeBrake = controls.rightToeBrake = 1f;
                    break;
                case "takeoff_roll":
                    controls.throttle = time < 1.0 ? (float)time : 1f;
                    break;
                case "rotation_near_vr":
                    controls.throttle = 1f;
                    if (time > 18.0) controls.elevator = 0.18f;
                    if (time > 28.0) controls.elevator = 0.06f;
                    break;
                case "vy_climb":
                    controls.throttle = 1f;
                    controls.elevator = 0.055f;
                    controls.trim = 0.12f;
                    break;
                case "level_flight_trim":
                    controls.throttle = 0.62f;
                    controls.trim = -0.04f;
                    break;
                case "shallow_coordinated_left_right_turn":
                    controls.throttle = 0.68f;
                    if (time < 7.0) { controls.aileron = -0.14f; controls.rudder = -0.045f; }
                    else if (time > 9.0) { controls.aileron = 0.14f; controls.rudder = 0.045f; }
                    break;
                case "slow_flight":
                    controls.throttle = 0.52f;
                    controls.flaps = 0.66f;
                    controls.elevator = 0.08f;
                    controls.trim = 0.16f;
                    break;
                case "stall_approach_recovery":
                    if (time < 12.0) { controls.throttle = 0.25f; controls.elevator = 0.24f; }
                    else { controls.throttle = 1f; controls.elevator = -0.10f; }
                    break;
                case "descent_final_approach":
                    controls.throttle = 0.34f;
                    controls.flaps = 1f;
                    controls.trim = -0.05f;
                    break;
                case "go_around":
                    controls.throttle = 1f;
                    controls.elevator = 0.10f;
                    controls.flaps = time < 4.0 ? 0.66f : time < 9.0 ? 0.33f : 0f;
                    break;
            }
        }

        private static IEnumerable<ScenarioSpec> ScenarioSpecs()
        {
            yield return new ScenarioSpec("ground_idle_brake", "Ground idle and brake hold", 8.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("takeoff_roll", "Takeoff roll", 24.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("rotation_near_vr", "Rotation near Vr", 36.0, 0.0, 4.1, 0.0, 0.0);
            yield return new ScenarioSpec("vy_climb", "Vy climb", 20.0, 74.0, 1000.0, 4.0, 5.0);
            yield return new ScenarioSpec("level_flight_trim", "Level flight trim", 20.0, 100.0, 1200.0, 0.0, 2.0);
            yield return new ScenarioSpec("shallow_coordinated_left_right_turn", "Shallow coordinated left/right turn", 18.0, 95.0, 1200.0, 0.0, 2.0);
            yield return new ScenarioSpec("slow_flight", "Slow flight", 20.0, 58.0, 1000.0, 0.0, 5.0);
            yield return new ScenarioSpec("stall_approach_recovery", "Stall approach and recovery", 24.0, 55.0, 1800.0, 0.0, 8.0);
            yield return new ScenarioSpec("descent_final_approach", "Descent and final approach", 20.0, 70.0, 900.0, -3.0, 1.0);
            yield return new ScenarioSpec("go_around", "Go-around", 20.0, 65.0, 300.0, -2.0, 2.0);
        }

        private static void WriteCsv(string path, IEnumerable<NativeScenarioResult> scenarios)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("id,pass,duration_s,steps,native_avg_ms,native_p95_ms,allocated_bytes,initial_kt,final_kt,min_kt,max_kt,initial_agl_ft,final_agl_ft,altitude_change_ft,min_agl_ft,max_agl_ft,min_vsi_fpm,max_vsi_fpm,heading_change_deg,turn_rate_deg_s,ground_distance_m,time_to_rotation_s,speed_rmse_vs_unity_kt,agl_rmse_vs_unity_ft,pitch_rmse_vs_unity_deg,bank_rmse_vs_unity_deg,heading_rmse_vs_unity_deg,acceptance");
            foreach (NativeScenarioResult item in scenarios)
            {
                csv.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2:0.000},{3},{4:0.000000},{5:0.000000},{6},{7:0.000},{8:0.000},{9:0.000},{10:0.000},{11:0.000},{12:0.000},{13:0.000},{14:0.000},{15:0.000},{16:0.000},{17:0.000},{18:0.000},{19:0.000},{20:0.000},{21:0.000},{22:0.000},{23:0.000},{24:0.000},{25:0.000},{26:0.000},{27}\n",
                    item.id, item.passed, item.durationSeconds, item.stepCount, item.nativeAverageStepMilliseconds,
                    item.nativeP95StepMilliseconds, item.managedAllocatedBytesDuringSteadySteps, item.initialAirspeedKnots,
                    item.finalAirspeedKnots, item.minAirspeedKnots, item.maxAirspeedKnots, item.initialAglFeet,
                    item.finalAglFeet, item.altitudeChangeFeet, item.minAglFeet, item.maxAglFeet,
                    item.minVerticalSpeedFeetPerMinute, item.maxVerticalSpeedFeetPerMinute,
                    item.headingChangeDegrees, item.meanTurnRateDegreesPerSecond, item.maxGroundDistanceMeters,
                    item.timeToRotationSeconds, item.airspeedRmseVsUnityKnots, item.aglRmseVsUnityFeet,
                    item.pitchRmseVsUnityDegrees, item.bankRmseVsUnityDegrees, item.headingRmseVsUnityDegrees,
                    CsvEscape(item.acceptanceSummary));
            }
            File.WriteAllText(path, csv.ToString());
        }

        private static void WriteMarkdown(string path, NativeValidationReport report)
        {
            StringBuilder markdown = new StringBuilder();
            markdown.AppendLine("# JSBSim Native Scenario Gate");
            markdown.AppendLine();
            markdown.AppendLine($"- Status: `{report.status}` ({report.passedCount}/{report.scenarioCount})");
            markdown.AppendLine($"- Revision: `{report.jsbsimRevision}`");
            markdown.AppendLine($"- Windows native average/p95 target: {report.nativeAverageStepMilliseconds:0.0000} / {report.nativeWorstP95StepMilliseconds:0.0000} ms");
            markdown.AppendLine($"- Performance target pass: `{report.windowsPerformanceTargetPass}`");
            markdown.AppendLine();
            markdown.AppendLine("| Scenario | Pass | Speed kt initial/final | AGL ft initial/final | VSI range fpm | Heading change | Native avg/p95 ms | Acceptance |");
            markdown.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |");
            foreach (NativeScenarioResult item in report.scenarios)
            {
                markdown.AppendLine($"| {item.name} | {item.passed} | {item.initialAirspeedKnots:0.0}/{item.finalAirspeedKnots:0.0} | {item.initialAglFeet:0}/{item.finalAglFeet:0} | {item.minVerticalSpeedFeetPerMinute:0}/{item.maxVerticalSpeedFeetPerMinute:0} | {item.headingChangeDegrees:0.0}° | {item.nativeAverageStepMilliseconds:0.0000}/{item.nativeP95StepMilliseconds:0.0000} | {item.acceptanceSummary.Replace("|", "/")} |");
            }
            File.WriteAllText(path, markdown.ToString());
        }

        private static string CsvEscape(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static double Square(double value) => value * value;

        private readonly struct ScenarioSpec
        {
            public readonly string id;
            public readonly string name;
            public readonly double durationSeconds;
            public readonly double initialAirspeedKnots;
            public readonly double initialAglFeet;
            public readonly double flightPathAngleDegrees;
            public readonly double initialPitchDegrees;

            public ScenarioSpec(string id, string name, double durationSeconds, double initialAirspeedKnots,
                double initialAglFeet, double flightPathAngleDegrees, double initialPitchDegrees)
            {
                this.id = id;
                this.name = name;
                this.durationSeconds = durationSeconds;
                this.initialAirspeedKnots = initialAirspeedKnots;
                this.initialAglFeet = initialAglFeet;
                this.flightPathAngleDegrees = flightPathAngleDegrees;
                this.initialPitchDegrees = initialPitchDegrees;
            }
        }

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
            public int scenarioCount;
            public int passedCount;
            public int totalNativeSteps;
            public double nativeAverageStepMilliseconds;
            public double nativeWorstP95StepMilliseconds;
            public bool windowsPerformanceTargetPass;
            public List<NativeScenarioResult> scenarios = new List<NativeScenarioResult>();
            public string[] limitations;
        }

        [Serializable]
        private sealed class NativeScenarioResult
        {
            public string id;
            public string name;
            public bool passed;
            public string acceptanceSummary;
            public string comparisonClassification;
            public double durationSeconds;
            public int stepCount;
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
            public double secondHalfMinVerticalSpeedFeetPerMinute;
            public double secondHalfMaxVerticalSpeedFeetPerMinute;
            public double minPitchDegrees;
            public double maxPitchDegrees;
            public double finalPitchDegrees;
            public double minBankDegrees;
            public double maxBankDegrees;
            public double finalBankDegrees;
            public double finalHeadingDegrees;
            public double headingChangeDegrees;
            public double meanTurnRateDegreesPerSecond;
            public double firstTurnMinBankDegrees;
            public double firstTurnMaxBankDegrees;
            public double secondTurnMinBankDegrees;
            public double secondTurnMaxBankDegrees;
            public double firstTurnHeadingChangeDegrees;
            public double secondTurnHeadingChangeDegrees;
            public double preRecoveryMinAirspeedKnots;
            public double postRecoveryMaxAirspeedKnots;
            public double maxGroundDistanceMeters;
            public double timeToRotationSeconds;
            public double maxThrottle;
            public double maxAbsAileron;
            public double maxAbsElevator;
            public double maxAbsRudder;
            public double maxFlaps;
            public double maxAbsTrim;
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
