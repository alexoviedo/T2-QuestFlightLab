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
        private Vector3 _lastPosition;
        private Vector3 _runwayForward;
        private Vector3 _runwayRight;

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
            _velocityWorld = transform.forward * (initialForwardSpeedKts * AircraftUnitConversions.KnotsToMetersPerSecond);
            _angularVelocityDeg = Vector3.zero;
            _lastPosition = transform.position;
            _runwayForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            _runwayRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            state.ResetState(_velocityWorld);
        }

        public void SetStateForTest(Vector3 position, Vector3 euler, Vector3 velocityWorld)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            _velocityWorld = velocityWorld;
            _angularVelocityDeg = Vector3.zero;
            _lastPosition = position;
            _runwayForward = Vector3.ProjectOnPlane(Quaternion.Euler(runwayResetEuler) * Vector3.forward, Vector3.up).normalized;
            _runwayRight = Vector3.ProjectOnPlane(Quaternion.Euler(runwayResetEuler) * Vector3.right, Vector3.up).normalized;
            state.ResetState(_velocityWorld);
            state.onGround = position.y <= groundHeightMeters + 0.05f;
            state.RefreshFromTransform(transform);
        }

        public void StepSimulation(AircraftControlState c, float dt)
        {
            if (dt <= 0f) return;
            c ??= AircraftControlState.Neutral();

            float mass = Mathf.Max(1f, config.massKg);
            float speed = _velocityWorld.magnitude;
            Vector3 localVelocity = transform.InverseTransformDirection(_velocityWorld);
            float forwardSpeed = Mathf.Max(0.1f, Mathf.Abs(localVelocity.z));
            float aoaRad = Mathf.Atan2(-localVelocity.y, forwardSpeed);
            float aoaDeg = aoaRad * Mathf.Rad2Deg;
            float flapDeg = GetFlapDegrees(c.flaps);
            float flapFraction = Mathf.InverseLerp(0f, 30f, flapDeg);
            float trim = Mathf.Clamp(c.trim, -config.maxTrim, config.maxTrim);
            float effectiveAoADeg = aoaDeg
                                    + c.elevator * config.elevatorAoAAuthorityDeg
                                    + trim * config.trimAoAAuthorityDeg;

            float q = 0.5f * config.airDensityKgM3 * speed * speed;
            float liftCoefficient = ComputeLiftCoefficient(effectiveAoADeg, flapFraction, out bool stalledByAoA);
            float stallKts = Mathf.Lerp(config.stallSpeedCleanKts, config.stallSpeedLandingKts, flapFraction);
            bool stall = state.airspeedKts > 8f && (state.airspeedKts < stallKts * 0.96f || stalledByAoA);
            if (stall) liftCoefficient *= config.postStallLiftMultiplier;

            float sideSlip = Mathf.Clamp(localVelocity.x / Mathf.Max(1f, speed), -1f, 1f);
            float dragCoefficient = config.dragCoefficientBase
                                    + config.inducedDragFactor * liftCoefficient * liftCoefficient
                                    + flapFraction * config.flapDragBonus
                                    + Mathf.Abs(sideSlip) * config.sideSlipDragFactor;

            float groundEffect = state.onGround
                ? Mathf.Clamp01(1f - transform.position.y / Mathf.Max(0.1f, config.groundEffectHeightM)) * 0.08f
                : 0f;
            Vector3 lift = transform.up * (q * config.wingAreaM2 * liftCoefficient * (1f + groundEffect));
            Vector3 drag = speed > 0.01f ? -_velocityWorld.normalized * (q * config.wingAreaM2 * dragCoefficient) : Vector3.zero;
            Vector3 thrust = transform.forward * ComputeThrustNewtons(c, speed);
            Vector3 gravity = Physics.gravity * mass;

            Vector3 force = lift + drag + thrust + gravity;

            if (state.onGround)
            {
                float brake = Mathf.Clamp01((c.leftToeBrake + c.rightToeBrake) * 0.5f);
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(_velocityWorld, Vector3.up);
                Vector3 forwardHorizontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 lateralVelocity = Vector3.Project(horizontalVelocity, transform.right);
                Vector3 rollingResistance = horizontalVelocity.sqrMagnitude > 0.01f
                    ? -horizontalVelocity.normalized * (mass * 9.81f * config.runwayFrictionCoefficient)
                    : Vector3.zero;
                Vector3 braking = horizontalVelocity.sqrMagnitude > 0.01f
                    ? -horizontalVelocity.normalized * (brake * config.brakeStrengthNewtons)
                    : Vector3.zero;
                Vector3 lateralFriction = -lateralVelocity * (mass * config.lateralGroundFriction);
                force += rollingResistance + braking + lateralFriction;

                if (speed < 0.4f && c.throttle < 0.05f && brake > 0.2f)
                {
                    force = new Vector3(0f, force.y, 0f);
                    _velocityWorld = Vector3.zero;
                }
            }

            _velocityWorld += force / mass * dt;

            float airAuthority = Mathf.InverseLerp(22f, 75f, state.airspeedKts);
            float groundAuthority = state.onGround ? Mathf.InverseLerp(4f, 28f, state.airspeedKts) : 0f;
            float authority = Mathf.Clamp01(Mathf.Max(airAuthority, groundAuthority * 0.65f));
            float bankDeg = AircraftState.NormalizeAngle(transform.eulerAngles.z);
            float coordinatedYaw = 0f;
            if (speed > 8f)
            {
                coordinatedYaw = Mathf.Sin(-bankDeg * Mathf.Deg2Rad) * 9.81f / speed * Mathf.Rad2Deg * config.coordinatedTurnCoupling;
            }

            Vector3 localAngularTarget = new Vector3(
                -(c.elevator + trim * 0.35f) * config.pitchRateMaxDegPerSec * authority - flapFraction * config.flapPitchMomentDegPerSec,
                (c.rudder * config.yawRateMaxDegPerSec * Mathf.Max(authority, groundAuthority) + coordinatedYaw),
                -c.aileron * config.rollRateMaxDegPerSec * authority);

            float pitchDeg = state.pitchDeg;
            localAngularTarget.x += pitchDeg * config.pitchAttitudeStability;
            localAngularTarget.z += -bankDeg * config.rollAttitudeStability;

            if (!state.onGround)
            {
                if (pitchDeg > config.maxPrototypePitchDeg)
                {
                    localAngularTarget.x = Mathf.Max(localAngularTarget.x, (pitchDeg - config.maxPrototypePitchDeg) * 2f);
                }
                else if (pitchDeg < -config.maxPrototypePitchDeg)
                {
                    localAngularTarget.x = Mathf.Min(localAngularTarget.x, (pitchDeg + config.maxPrototypePitchDeg) * 2f);
                }

                if (bankDeg > config.maxPrototypeBankDeg)
                {
                    localAngularTarget.z = Mathf.Min(localAngularTarget.z, -(bankDeg - config.maxPrototypeBankDeg) * 2f);
                }
                else if (bankDeg < -config.maxPrototypeBankDeg)
                {
                    localAngularTarget.z = Mathf.Max(localAngularTarget.z, (-bankDeg - config.maxPrototypeBankDeg) * 2f);
                }
            }

            if (state.onGround)
            {
                bool canRotate = state.airspeedKts > config.rotationSpeedKts - 4f && c.elevator > 0.15f;
                localAngularTarget.x = canRotate ? -c.elevator * 10f : 0f;
                localAngularTarget.z = 0f;
            }

            Vector3 damping = new Vector3(config.pitchDamping, config.yawDamping, config.rollDamping);
            _angularVelocityDeg.x = Mathf.Lerp(_angularVelocityDeg.x, localAngularTarget.x, dt * damping.x);
            _angularVelocityDeg.y = Mathf.Lerp(_angularVelocityDeg.y, localAngularTarget.y, dt * damping.y);
            _angularVelocityDeg.z = Mathf.Lerp(_angularVelocityDeg.z, localAngularTarget.z, dt * damping.z);

            if (state.onGround && state.airspeedKts < 45f)
            {
                _angularVelocityDeg.x = Mathf.Min(_angularVelocityDeg.x, 5f);
                _angularVelocityDeg.z *= 0.15f;
                _angularVelocityDeg.y += c.rudder * config.nosewheelSteerDegPerSec * groundAuthority * dt;
            }

            Quaternion delta = Quaternion.Euler(_angularVelocityDeg * dt);
            transform.rotation = transform.rotation * delta;

            if (state.onGround)
            {
                float pitch = AircraftState.NormalizeAngle(transform.eulerAngles.x);
                pitch = Mathf.Clamp(pitch, -9f, 1.5f);
                transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
                if (state.airspeedKts > config.rotationSpeedKts && c.elevator > 0.25f)
                {
                    _velocityWorld.y = Mathf.Max(_velocityWorld.y, 1.6f + c.elevator * 2.8f);
                }
            }

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
            state.engineRpm = Mathf.Lerp(config.idleRpm, config.maxRpm, Mathf.Clamp01(c.throttle));
            state.powerPercent = ComputePowerPercent(c);
            state.flapDegrees = flapDeg;
            state.trimPercent = trim;
            state.loadFactorG = Mathf.Clamp(lift.magnitude / Mathf.Max(1f, mass * 9.81f), 0f, 4f);
            state.runwayLateralOffsetMeters = Vector3.Dot(transform.position - runwayResetPosition, _runwayRight);
            if (state.onGround)
            {
                state.groundRollMeters += Vector3.Distance(
                    Vector3.ProjectOnPlane(_lastPosition, Vector3.up),
                    Vector3.ProjectOnPlane(transform.position, Vector3.up));
            }
            _lastPosition = transform.position;
            state.RefreshFromTransform(transform);
        }

        private float ComputeLiftCoefficient(float effectiveAoADeg, float flapFraction, out bool stalled)
        {
            float aoaFromZeroRad = (effectiveAoADeg - config.zeroLiftAoADeg) * Mathf.Deg2Rad;
            float raw = config.liftCoefficientBase + config.liftCurveSlopePerRad * aoaFromZeroRad + flapFraction * config.flapLiftBonus;
            float critical = config.criticalAoADeg + flapFraction * 2.5f;
            stalled = Mathf.Abs(effectiveAoADeg) > critical;
            float maxCl = config.maximumLiftCoefficient + flapFraction * 0.35f;
            return Mathf.Clamp(raw, -maxCl * 0.7f, maxCl);
        }

        private float ComputeThrustNewtons(AircraftControlState c, float speedMps)
        {
            float throttle = Mathf.Clamp01(c.throttle);
            float mixture = Mathf.Lerp(config.minimumMixturePower, 1f, Mathf.Clamp01(c.mixture));
            float carbHeat = 1f - Mathf.Clamp01(c.carbHeat) * config.carbHeatPowerLoss;
            float powerW = config.maxEnginePowerHp * AircraftUnitConversions.HorsepowerToWatts * throttle * mixture * carbHeat;
            float propThrust = powerW * config.propEfficiency / Mathf.Max(14f, speedMps);
            float staticBlend = Mathf.Lerp(config.staticThrustNewtons, config.maxThrustNewtons, Mathf.InverseLerp(0f, 32f, speedMps));
            return Mathf.Clamp(propThrust, 0f, staticBlend);
        }

        private float ComputePowerPercent(AircraftControlState c)
        {
            float mixture = Mathf.Lerp(config.minimumMixturePower, 1f, Mathf.Clamp01(c.mixture));
            float carbHeat = 1f - Mathf.Clamp01(c.carbHeat) * config.carbHeatPowerLoss;
            return Mathf.Clamp01(c.throttle * mixture * carbHeat) * 100f;
        }

        public float GetFlapDegrees(float normalizedFlaps)
        {
            if (config.flapSettingsDeg == null || config.flapSettingsDeg.Length == 0)
            {
                return Mathf.Clamp01(normalizedFlaps) * 30f;
            }

            float scaled = Mathf.Clamp01(normalizedFlaps) * (config.flapSettingsDeg.Length - 1);
            int low = Mathf.FloorToInt(scaled);
            int high = Mathf.Min(config.flapSettingsDeg.Length - 1, low + 1);
            return Mathf.Lerp(config.flapSettingsDeg[low], config.flapSettingsDeg[high], scaled - low);
        }

        private void StepPhysics(AircraftControlState c, float dt)
        {
            StepSimulation(c, dt);
        }
    }
}
