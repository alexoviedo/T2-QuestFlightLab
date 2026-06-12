using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using UnityEngine;
using UnityEngine.XR;

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
            InputTracking.Recenter();
            RefreshText();
        }

        public void ResetAircraft()
        {
            if (aircraftPhysics != null) aircraftPhysics.ResetToRunway();
            RefreshText();
        }

        public void RefreshText()
        {
            if (text == null) return;
            text.text =
                "TOUCH MENU PLACEHOLDER\n" +
                "Recenter\n" +
                "Reset aircraft\n" +
                "Pause / resume\n" +
                "Toggle telemetry\n\n" +
                "Touch controller interaction shell only.\n" +
                "Primary flight control is the BLE gamepad.";
        }
    }
}

