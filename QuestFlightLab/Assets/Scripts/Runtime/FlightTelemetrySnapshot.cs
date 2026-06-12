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
        public bool stallWarning;
        public bool onGround;
        public float fps;
    }
}

