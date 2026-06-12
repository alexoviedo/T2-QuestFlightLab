using UnityEngine;

namespace QuestFlightLab.Flight
{
    public class AircraftState : MonoBehaviour
    {
        public C172StyleAircraftConfig config;
        public Vector3 velocityWorld;
        public Vector3 angularVelocityDeg;
        public float airspeedKts;
        public float altitudeFt;
        public float verticalSpeedFpm;
        public float headingDeg;
        public float pitchDeg;
        public float bankDeg;
        public float angleOfAttackDeg;
        public bool stallWarning;
        public bool onGround;

        public void ResetState(Vector3 velocity)
        {
            velocityWorld = velocity;
            angularVelocityDeg = Vector3.zero;
            stallWarning = false;
            onGround = true;
            RefreshFromTransform(transform);
        }

        public void RefreshFromTransform(Transform aircraftTransform)
        {
            airspeedKts = velocityWorld.magnitude * 1.943844f;
            altitudeFt = aircraftTransform.position.y * 3.28084f;
            verticalSpeedFpm = velocityWorld.y * 196.8504f;
            headingDeg = Mathf.Repeat(aircraftTransform.eulerAngles.y, 360f);
            pitchDeg = NormalizeAngle(aircraftTransform.eulerAngles.x);
            bankDeg = NormalizeAngle(aircraftTransform.eulerAngles.z);
        }

        public static float NormalizeAngle(float angle)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return angle;
        }
    }
}

