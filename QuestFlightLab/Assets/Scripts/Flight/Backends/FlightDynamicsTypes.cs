using System;
using UnityEngine;

namespace QuestFlightLab.Flight.Backends
{
    public enum FlightDynamicsBackendKind
    {
        UnityPrototype = 0,
        JSBSimEditorSidecar = 1,
        JSBSimNative = 2
    }

    [Serializable]
    public struct GeodeticReference
    {
        public double latitudeDegrees;
        public double longitudeDegrees;
        public double altitudeMslMeters;

        public GeodeticReference(double latitudeDegrees, double longitudeDegrees, double altitudeMslMeters)
        {
            this.latitudeDegrees = latitudeDegrees;
            this.longitudeDegrees = longitudeDegrees;
            this.altitudeMslMeters = altitudeMslMeters;
        }

        // FAA ADIP airport reference point/elevation, effective 2026-04-16.
        public static GeodeticReference Kbdu => new GeodeticReference(40.03936527, -105.22608958, 1611.7824);
    }

    [Serializable]
    public struct FlightDynamicsInitialConditions
    {
        // FAA runway geometry is approximately 89.972 degrees true. The 08/26
        // runway designation is magnetic and must not be used as an ENU yaw.
        public const double KbduRunwayTrueHeadingDegrees = 89.972;
        public double latitudeDegrees;
        public double longitudeDegrees;
        public double altitudeMslMeters;
        public double terrainElevationMslMeters;
        public double calibratedAirspeedKnots;
        public double headingDegrees;
        public double pitchDegrees;
        public double bankDegrees;
        public double flightPathAngleDegrees;
        public bool engineRunning;

        public static FlightDynamicsInitialConditions KbduRunway(double headingDegrees = KbduRunwayTrueHeadingDegrees)
        {
            GeodeticReference origin = GeodeticReference.Kbdu;
            return new FlightDynamicsInitialConditions
            {
                latitudeDegrees = origin.latitudeDegrees,
                longitudeDegrees = origin.longitudeDegrees,
                altitudeMslMeters = origin.altitudeMslMeters + 1.25,
                terrainElevationMslMeters = origin.altitudeMslMeters,
                calibratedAirspeedKnots = 0.0,
                headingDegrees = headingDegrees,
                pitchDegrees = 0.0,
                bankDegrees = 0.0,
                flightPathAngleDegrees = 0.0,
                engineRunning = true
            };
        }
    }

    [Serializable]
    public struct FlightDynamicsAtmosphere
    {
        public double windNorthMetersPerSecond;
        public double windEastMetersPerSecond;
        public double windDownMetersPerSecond;

        public static FlightDynamicsAtmosphere Calm => default;
    }

    [Serializable]
    public struct FlightDynamicsState
    {
        public double simulationTimeSeconds;
        public double latitudeDegrees;
        public double longitudeDegrees;
        public double altitudeMslMeters;
        public double altitudeAglMeters;
        public double calibratedAirspeedKnots;
        public double groundSpeedKnots;
        public double verticalSpeedFeetPerMinute;
        public double headingDegrees;
        public double pitchDegrees;
        public double bankDegrees;
        public double angleOfAttackDegrees;
        public double sideslipDegrees;
        public double engineRpm;
        public double loadFactorG;
        public bool weightOnWheels;
        public Vector3 positionUnityMeters;
        public Quaternion rotationUnity;
        public Vector3 velocityUnityMetersPerSecond;
        public Vector3 angularVelocityBodyDegreesPerSecond;

        public bool IsFinite =>
            IsFiniteNumber(simulationTimeSeconds) &&
            IsFiniteNumber(latitudeDegrees) &&
            IsFiniteNumber(longitudeDegrees) &&
            IsFiniteNumber(altitudeMslMeters) &&
            IsFiniteNumber(calibratedAirspeedKnots) &&
            IsFiniteNumber(positionUnityMeters.x) &&
            IsFiniteNumber(positionUnityMeters.y) &&
            IsFiniteNumber(positionUnityMeters.z) &&
            IsFiniteNumber(rotationUnity.x) &&
            IsFiniteNumber(rotationUnity.y) &&
            IsFiniteNumber(rotationUnity.z) &&
            IsFiniteNumber(rotationUnity.w);

        private static bool IsFiniteNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class FlightDynamicsBackendContext
    {
        public Transform simulationRoot;
        public AircraftState aircraftState;
        public SimpleAircraftPhysics unityPrototype;
        public GeodeticReference localOrigin = GeodeticReference.Kbdu;
        public string jsbsimDataRoot = string.Empty;
        public string jsbsimAircraft = "c172x";
        public string sidecarScriptPath = string.Empty;
        public double fixedDeltaTimeSeconds = 1.0 / 120.0;
    }
}
