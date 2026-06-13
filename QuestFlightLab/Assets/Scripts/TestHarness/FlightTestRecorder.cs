using System;
using System.Collections.Generic;
using QuestFlightLab.Runtime;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class FlightScenarioSample
    {
        public float timestamp;
        public AircraftControlState controls;
        public FlightTelemetrySnapshot flight;
        public float leftAileronDeg;
        public float rightAileronDeg;
        public float elevatorDeg;
        public float rudderDeg;
        public float flapDeg;
    }

    [Serializable]
    public class FlightScenarioStats
    {
        public float minAirspeedKts = float.MaxValue;
        public float maxAirspeedKts = float.MinValue;
        public float initialAirspeedKts;
        public float finalAirspeedKts;
        public float minAltitudeFt = float.MaxValue;
        public float maxAltitudeFt = float.MinValue;
        public float initialAltitudeFt;
        public float finalAltitudeFt;
        public float altitudeDeltaFt;
        public float minVerticalSpeedFpm = float.MaxValue;
        public float maxVerticalSpeedFpm = float.MinValue;
        public float minHeadingDeg = float.MaxValue;
        public float maxHeadingDeg = float.MinValue;
        public float initialHeadingDeg;
        public float finalHeadingDeg;
        public float headingChangeDeg;
        public float minPitchDeg = float.MaxValue;
        public float maxPitchDeg = float.MinValue;
        public float minBankDeg = float.MaxValue;
        public float maxBankDeg = float.MinValue;
        public float minAileron = float.MaxValue;
        public float maxAileron = float.MinValue;
        public float minElevator = float.MaxValue;
        public float maxElevator = float.MinValue;
        public float minRudder = float.MaxValue;
        public float maxRudder = float.MinValue;
        public float minThrottle = float.MaxValue;
        public float maxThrottle = float.MinValue;
        public float maxLeftToeBrake;
        public float maxRightToeBrake;
        public float maxFlapDegrees;
        public float minTrim = float.MaxValue;
        public float maxTrim = float.MinValue;
        public float maxGroundRollMeters;
        public float maxRunwayOffsetAbsMeters;
        public float maxLoadFactorG;
        public float maxStallIntensity;
        public int stallWarningSamples;
        public float stallWarningOnsetSeconds = -1f;
        public float maxReferenceSpeedErrorAbsKts;
        public bool stallWarningObserved;
        public int sampleCount;
    }

    [Serializable]
    public class FlightScenarioResult
    {
        public string id = "";
        public string name = "";
        public string purpose = "";
        public bool passed;
        public string passReason = "";
        public float durationSeconds;
        public float timeStepSeconds;
        public FlightScenarioStats stats = new FlightScenarioStats();
        public InstrumentVerificationSnapshot instrumentVerification = new InstrumentVerificationSnapshot();
        public TrainingVerificationSnapshot trainingVerification = new TrainingVerificationSnapshot();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
        public List<FlightScenarioSample> samples = new List<FlightScenarioSample>();
    }

    [Serializable]
    public class FlightScenarioSuiteResult
    {
        public string appName = "Quest Flight Input Lab";
        public string suiteName = "Flight Sim Core v0.2 Autonomous Scenario Suite";
        public string startedUtc = "";
        public string unityVersion = "";
        public string testMode = "Unity batchmode editor scenario runner";
        public string metaXrSimulatorStatus = "";
        public int scenarioCount;
        public int passedCount;
        public int failedCount;
        public float simulatedSeconds;
        public float fixedTimeStepSeconds;
        public List<FlightScenarioResult> scenarios = new List<FlightScenarioResult>();
        public List<string> limitations = new List<string>();
    }

    public class FlightTestRecorder
    {
        private readonly FlightScenarioResult _result;

        public FlightTestRecorder(FlightScenarioResult result)
        {
            _result = result;
        }

        public void AddSample(FlightScenarioSample sample)
        {
            _result.samples.Add(sample);
            FlightScenarioStats s = _result.stats;
            if (s.sampleCount == 0)
            {
                s.initialAirspeedKts = sample.flight.airspeedKts;
                s.initialAltitudeFt = sample.flight.altitudeFt;
                s.initialHeadingDeg = sample.flight.headingDeg;
            }

            s.sampleCount++;
            s.minAirspeedKts = Min(s.minAirspeedKts, sample.flight.airspeedKts);
            s.maxAirspeedKts = Max(s.maxAirspeedKts, sample.flight.airspeedKts);
            s.finalAirspeedKts = sample.flight.airspeedKts;
            s.minAltitudeFt = Min(s.minAltitudeFt, sample.flight.altitudeFt);
            s.maxAltitudeFt = Max(s.maxAltitudeFt, sample.flight.altitudeFt);
            s.finalAltitudeFt = sample.flight.altitudeFt;
            s.altitudeDeltaFt = s.finalAltitudeFt - s.initialAltitudeFt;
            s.minVerticalSpeedFpm = Min(s.minVerticalSpeedFpm, sample.flight.verticalSpeedFpm);
            s.maxVerticalSpeedFpm = Max(s.maxVerticalSpeedFpm, sample.flight.verticalSpeedFpm);
            s.minHeadingDeg = Min(s.minHeadingDeg, sample.flight.headingDeg);
            s.maxHeadingDeg = Max(s.maxHeadingDeg, sample.flight.headingDeg);
            s.finalHeadingDeg = sample.flight.headingDeg;
            s.headingChangeDeg = Math.Abs(DeltaAngle(s.initialHeadingDeg, s.finalHeadingDeg));
            s.minPitchDeg = Min(s.minPitchDeg, sample.flight.pitchDeg);
            s.maxPitchDeg = Max(s.maxPitchDeg, sample.flight.pitchDeg);
            s.minBankDeg = Min(s.minBankDeg, sample.flight.bankDeg);
            s.maxBankDeg = Max(s.maxBankDeg, sample.flight.bankDeg);
            s.minAileron = Min(s.minAileron, sample.controls.aileron);
            s.maxAileron = Max(s.maxAileron, sample.controls.aileron);
            s.minElevator = Min(s.minElevator, sample.controls.elevator);
            s.maxElevator = Max(s.maxElevator, sample.controls.elevator);
            s.minRudder = Min(s.minRudder, sample.controls.rudder);
            s.maxRudder = Max(s.maxRudder, sample.controls.rudder);
            s.minThrottle = Min(s.minThrottle, sample.controls.throttle);
            s.maxThrottle = Max(s.maxThrottle, sample.controls.throttle);
            s.maxLeftToeBrake = Max(s.maxLeftToeBrake, sample.controls.leftToeBrake);
            s.maxRightToeBrake = Max(s.maxRightToeBrake, sample.controls.rightToeBrake);
            s.maxFlapDegrees = Max(s.maxFlapDegrees, sample.flight.flapDegrees);
            s.minTrim = Min(s.minTrim, sample.controls.trim);
            s.maxTrim = Max(s.maxTrim, sample.controls.trim);
            s.maxGroundRollMeters = Max(s.maxGroundRollMeters, sample.flight.groundRollMeters);
            s.maxRunwayOffsetAbsMeters = Max(s.maxRunwayOffsetAbsMeters, System.Math.Abs(sample.flight.runwayLateralOffsetMeters));
            s.maxLoadFactorG = Max(s.maxLoadFactorG, sample.flight.loadFactorG);
            s.maxStallIntensity = Max(s.maxStallIntensity, sample.flight.stallIntensity);
            s.maxReferenceSpeedErrorAbsKts = Max(s.maxReferenceSpeedErrorAbsKts, System.Math.Abs(sample.flight.targetSpeedErrorKts));
            if (sample.flight.stallWarning)
            {
                s.stallWarningSamples++;
                if (s.stallWarningOnsetSeconds < 0f) s.stallWarningOnsetSeconds = sample.timestamp;
            }
            s.stallWarningObserved |= sample.flight.stallWarning;
        }

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
        private static float DeltaAngle(float from, float to)
        {
            float delta = (to - from + 540f) % 360f - 180f;
            return delta;
        }
    }
}
