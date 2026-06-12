using System;
using System.Collections.Generic;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class InputEvidenceSession
    {
        public string appName = "Quest Flight Input Lab";
        public string appVersion = "0.1.0";
        public string startedUtc = "";
        public string platform = "";
        public string unityVersion = "";
        public string deviceModel = "";
        public string graphicsDevice = "";
        public string evidencePath = "";
        public string activeGamepadName = "";
        public string activeGamepadLayout = "";
        public string activeGamepadDescription = "";

        public List<string> axesObserved = new List<string>();
        public List<string> buttonsObserved = new List<string>();
        public List<InputEvidenceEvent> events = new List<InputEvidenceEvent>();
        public List<InputEvidenceSample> samples = new List<InputEvidenceSample>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public class InputEvidenceEvent
    {
        public float timestamp;
        public string kind = "";
        public string detail = "";
    }

    [Serializable]
    public class InputEvidenceSample
    {
        public float timestamp;
        public GamepadInputSnapshot gamepad;
        public AircraftControlState controls;
        public FlightTelemetrySnapshot flight;
    }
}

