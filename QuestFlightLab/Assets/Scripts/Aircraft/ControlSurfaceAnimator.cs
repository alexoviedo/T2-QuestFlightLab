using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Aircraft
{
    public class ControlSurfaceAnimator : MonoBehaviour
    {
        public Usb2BleInputMapper controls;

        [Header("Aircraft surfaces")]
        public Transform leftAileron;
        public Transform rightAileron;
        public Transform elevator;
        public Transform rudder;
        public Transform leftFlap;
        public Transform rightFlap;

        [Header("Cockpit/control indicators")]
        public Transform yoke;
        public Transform rudderPedals;
        public Transform leftBrakeBar;
        public Transform rightBrakeBar;
        public Transform throttleLever;

        private void Awake()
        {
            if (controls == null) controls = FindFirstObjectByType<Usb2BleInputMapper>();
        }

        private void Update()
        {
            AircraftControlState c = controls != null ? controls.Current : AircraftControlState.Neutral();

            if (leftAileron != null) leftAileron.localRotation = Quaternion.Euler(c.aileron * 20f, 0f, 0f);
            if (rightAileron != null) rightAileron.localRotation = Quaternion.Euler(-c.aileron * 20f, 0f, 0f);
            if (elevator != null) elevator.localRotation = Quaternion.Euler(c.elevator * 24f, 0f, 0f);
            if (rudder != null) rudder.localRotation = Quaternion.Euler(0f, c.rudder * 28f, 0f);
            if (leftFlap != null) leftFlap.localRotation = Quaternion.Euler(c.flaps * 35f, 0f, 0f);
            if (rightFlap != null) rightFlap.localRotation = Quaternion.Euler(c.flaps * 35f, 0f, 0f);

            if (yoke != null)
            {
                yoke.localRotation = Quaternion.Euler(c.elevator * 18f, 0f, -c.aileron * 55f);
                yoke.localPosition = new Vector3(c.aileron * 0.08f, -0.05f + c.elevator * 0.04f, 0.35f);
            }

            if (rudderPedals != null) rudderPedals.localPosition = new Vector3(c.rudder * 0.18f, 0f, 0f);
            if (leftBrakeBar != null) leftBrakeBar.localScale = new Vector3(0.08f, Mathf.Lerp(0.05f, 0.55f, c.leftToeBrake), 0.08f);
            if (rightBrakeBar != null) rightBrakeBar.localScale = new Vector3(0.08f, Mathf.Lerp(0.05f, 0.55f, c.rightToeBrake), 0.08f);
            if (throttleLever != null) throttleLever.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, 35f, c.throttle), 0f, 0f);
        }
    }
}

