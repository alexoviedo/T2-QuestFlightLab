using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Flight
{
    public class SimpleAircraftPhysics : MonoBehaviour
    {
        public Usb2BleInputMapper controls;
        public AircraftState state;
        public C172StyleAircraftConfig config;
        public Vector3 runwayResetPosition = new Vector3(0f, 1.25f, -520f);
        public Vector3 runwayResetEuler = new Vector3(0f, 78f, 0f);
        public float initialForwardSpeedKts = 0f;
        public float groundHeightMeters = 1.25f;

        private Vector3 _velocityWorld;
        private Vector3 _angularVelocityDeg;

        private void Awake()
        {
            if (controls == null) controls = FindFirstObjectByType<Usb2BleInputMapper>();
            if (state == null) state = GetComponent<AircraftState>();
            if (state == null) state = gameObject.AddComponent<AircraftState>();
            if (config == null) config = C172StyleAircraftConfig.CreateRuntimeDefault();
            state.config = config;
        }

        private void Start()
        {
            ResetToRunway();
        }

        private void FixedUpdate()
        {
            if (controls != null && controls.ConsumeResetRequest())
            {
                ResetToRunway();
                return;
            }

            if (controls != null && controls.Paused) return;

            AircraftControlState c = controls != null ? controls.Current : AircraftControlState.Neutral();
            StepPhysics(c, Time.fixedDeltaTime);
        }

        public void ResetToRunway()
        {
            transform.SetPositionAndRotation(runwayResetPosition, Quaternion.Euler(runwayResetEuler));
            _velocityWorld = transform.forward * (initialForwardSpeedKts * 0.514444f);
            _angularVelocityDeg = Vector3.zero;
            state.ResetState(_velocityWorld);
        }

        private void StepPhysics(AircraftControlState c, float dt)
        {
            float mass = Mathf.Max(1f, config.massKg);
            float speed = _velocityWorld.magnitude;
            Vector3 localVelocity = transform.InverseTransformDirection(_velocityWorld);
            float forwardSpeed = Mathf.Max(0.1f, localVelocity.z);
            float aoaRad = Mathf.Atan2(localVelocity.y, forwardSpeed);
            float aoaDeg = aoaRad * Mathf.Rad2Deg;

            float airDensity = 1.0f;
            float q = 0.5f * airDensity * speed * speed;
            float cl = config.liftCoefficientBase
                       + config.liftCoefficientPerAoA * aoaRad
                       + c.elevator * config.elevatorLiftAuthority
                       + c.flaps * config.flapLiftBonus;

            float stallKts = Mathf.Lerp(config.stallSpeedCleanKts, config.stallSpeedLandingKts, c.flaps);
            bool stall = state.airspeedKts > 5f && (state.airspeedKts < stallKts || Mathf.Abs(aoaDeg) > 16f);
            if (stall) cl *= 0.45f;

            float cd = config.dragCoefficientBase
                       + config.inducedDragFactor * cl * cl
                       + c.flaps * config.flapDragBonus
                       + (c.leftToeBrake + c.rightToeBrake) * 0.08f;

            Vector3 lift = transform.up * (q * config.wingAreaM2 * cl);
            Vector3 drag = speed > 0.01f ? -_velocityWorld.normalized * (q * config.wingAreaM2 * cd) : Vector3.zero;
            Vector3 thrust = transform.forward * (config.maxThrustNewtons * Mathf.Clamp01(c.throttle));
            Vector3 gravity = Physics.gravity * mass;

            Vector3 force = lift + drag + thrust + gravity;

            if (state.onGround)
            {
                float brake = Mathf.Clamp01((c.leftToeBrake + c.rightToeBrake) * 0.5f);
                force += -Vector3.ProjectOnPlane(_velocityWorld, Vector3.up) * (900f + brake * 5500f);
                if (speed < 0.5f && c.throttle < 0.05f) force = new Vector3(0f, force.y, 0f);
            }

            _velocityWorld += force / mass * dt;

            Vector3 localAngularTarget = new Vector3(
                (-c.elevator + c.trim * 0.35f) * config.controlResponse,
                c.rudder * config.controlResponse * 0.55f,
                -c.aileron * config.controlResponse);

            _angularVelocityDeg = Vector3.Lerp(_angularVelocityDeg, localAngularTarget, dt * 2.6f);
            _angularVelocityDeg = Vector3.Lerp(_angularVelocityDeg, Vector3.zero, dt * config.angularDamping * 0.15f);

            Quaternion delta = Quaternion.Euler(_angularVelocityDeg * dt);
            transform.rotation = transform.rotation * delta;
            transform.position += _velocityWorld * dt;

            if (transform.position.y <= groundHeightMeters)
            {
                transform.position = new Vector3(transform.position.x, groundHeightMeters, transform.position.z);
                if (_velocityWorld.y < 0f) _velocityWorld.y = 0f;
                state.onGround = true;
            }
            else
            {
                state.onGround = false;
            }

            state.velocityWorld = _velocityWorld;
            state.angularVelocityDeg = _angularVelocityDeg;
            state.angleOfAttackDeg = aoaDeg;
            state.stallWarning = stall;
            state.RefreshFromTransform(transform);
        }
    }
}

