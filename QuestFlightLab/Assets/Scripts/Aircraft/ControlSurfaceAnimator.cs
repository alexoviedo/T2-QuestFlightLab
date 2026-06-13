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
        public Transform mixtureLever;
        public Transform carbHeatLever;
        public Transform trimIndicator;
        public Transform flapIndicator;

        private void Awake()
        {
            if (controls == null) controls = FindFirstObjectByType<Usb2BleInputMapper>();
            EnsureOptionalIndicators();
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
            if (mixtureLever != null) mixtureLever.localRotation = Quaternion.Euler(Mathf.Lerp(-30f, 30f, c.mixture), 0f, 0f);
            if (carbHeatLever != null) carbHeatLever.localRotation = Quaternion.Euler(Mathf.Lerp(-25f, 25f, c.carbHeat), 0f, 0f);
            if (trimIndicator != null) trimIndicator.localRotation = Quaternion.Euler(0f, 0f, -c.trim * 120f);
            if (flapIndicator != null) flapIndicator.localPosition = new Vector3(-0.75f, -0.18f + c.flaps * 0.35f, 0.35f);
        }

        private void EnsureOptionalIndicators()
        {
            Transform parent = throttleLever != null ? throttleLever.parent : transform;
            Material material = throttleLever != null && throttleLever.TryGetComponent(out Renderer renderer)
                ? renderer.sharedMaterial
                : null;

            if (mixtureLever == null) mixtureLever = CreateIndicator(parent, "MixturePlaceholder", new Vector3(0.92f, -0.12f, 0.35f), new Vector3(0.07f, 0.34f, 0.07f), material);
            if (carbHeatLever == null) carbHeatLever = CreateIndicator(parent, "CarbHeatPlaceholder", new Vector3(1.08f, -0.12f, 0.35f), new Vector3(0.07f, 0.28f, 0.07f), material);
            if (trimIndicator == null) trimIndicator = CreateIndicator(parent, "TrimWheelIndicator", new Vector3(-0.92f, -0.16f, 0.35f), new Vector3(0.22f, 0.05f, 0.22f), material);
            if (flapIndicator == null) flapIndicator = CreateIndicator(parent, "FlapPositionIndicator", new Vector3(-0.75f, -0.18f, 0.35f), new Vector3(0.09f, 0.09f, 0.09f), material);
        }

        private static Transform CreateIndicator(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null && go.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }
            return go.transform;
        }
    }
}
