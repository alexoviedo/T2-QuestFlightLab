using System;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class FlightTelemetrySnapshot
    {
        public float timestamp;
        public float airspeedKts;
        public float altitudeFt;
        public float verticalSpeedFpm;
        public float headingDeg;
        public float pitchDeg;
        public float bankDeg;
        public float angleOfAttackDeg;
        public float stallIntensity;
        public float slipSkid;
        public float referenceSpeedKts;
        public float targetSpeedErrorKts;
        public bool stallWarning;
        public bool onGround;
        public float fps;
        public float engineRpm;
        public float powerPercent;
        public float flapDegrees;
        public float trimPercent;
        public float loadFactorG;
        public float groundRollMeters;
        public float runwayLateralOffsetMeters;
    }
}
