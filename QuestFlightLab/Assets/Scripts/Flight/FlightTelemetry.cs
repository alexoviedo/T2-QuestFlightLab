using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Flight
{
    public class FlightTelemetry : MonoBehaviour
    {
        public AircraftState aircraftState;
        public PerformanceHud performanceHud;
        public FlightTelemetrySnapshot Current { get; private set; } = new FlightTelemetrySnapshot();

        private void Awake()
        {
            if (aircraftState == null) aircraftState = FindFirstObjectByType<AircraftState>();
            if (performanceHud == null) performanceHud = FindFirstObjectByType<PerformanceHud>();
        }

        private void Update()
        {
            if (aircraftState == null) return;

            Current = new FlightTelemetrySnapshot
            {
                timestamp = Time.unscaledTime,
                airspeedKts = aircraftState.airspeedKts,
                altitudeFt = aircraftState.altitudeFt,
                verticalSpeedFpm = aircraftState.verticalSpeedFpm,
                headingDeg = aircraftState.headingDeg,
                pitchDeg = aircraftState.pitchDeg,
                bankDeg = aircraftState.bankDeg,
                angleOfAttackDeg = aircraftState.angleOfAttackDeg,
                stallWarning = aircraftState.stallWarning,
                onGround = aircraftState.onGround,
                fps = performanceHud != null ? performanceHud.CurrentFps : 0f
            };
        }
    }
}

