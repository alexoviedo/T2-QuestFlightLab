using QuestFlightLab.Flight;
using QuestFlightLab.Flight.Backends;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.UI
{
    public class TouchMenuPlaceholder : MonoBehaviour
    {
        public Usb2BleInputMapper mapper;
        public SimpleAircraftPhysics aircraftPhysics;
        public TextMesh text;

        private void Awake()
        {
            if (mapper == null) mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (aircraftPhysics == null) aircraftPhysics = FindFirstObjectByType<SimpleAircraftPhysics>();
            if (text == null) text = GetComponentInChildren<TextMesh>();
        }

        private void Start()
        {
            RefreshText();
        }

        public void Recenter()
        {
            QuestFirstViewRuntimeRepair.Instance?.RecenterSeatView();
            RefreshText();
        }

        public void ResetAircraft()
        {
            // Never overwrite a coordinator-owned native or fallback backend.
            if (aircraftPhysics != null && aircraftPhysics.GetComponent<FlightDynamicsCoordinator>() == null)
            {
                aircraftPhysics.ResetToRunway();
            }
            RefreshText();
        }

        public void RefreshText()
        {
            if (text == null) return;
            text.text =
                "TOUCH MENU\n" +
                "Seat/view calibration (Left Menu)\n" +
                "Recenter inside calibration (X)\n" +
                "Pause / resume\n" +
                "Toggle telemetry\n\n" +
                "Touch controls never consume flight axes.";
        }
    }
}
