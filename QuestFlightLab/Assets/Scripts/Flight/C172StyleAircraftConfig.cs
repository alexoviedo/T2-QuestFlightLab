using UnityEngine;

namespace QuestFlightLab.Flight
{
    [CreateAssetMenu(menuName = "Quest Flight Lab/C172 Style Aircraft Config")]
    public class C172StyleAircraftConfig : ScriptableObject
    {
        [Header("Approximate C172-style reference values")]
        public float massKg = 1111f;
        public float wingAreaM2 = 16.2f;
        public float wingSpanM = 11.0f;
        public float aspectRatio = 7.45f;
        public float stallSpeedCleanKts = 48f;
        public float stallSpeedLandingKts = 40f;
        public float cruiseSpeedKts = 105f;
        public float bestAngleClimbSpeedKts = 62f;
        public float rotationSpeedKts = 55f;
        public float bestClimbSpeedKts = 74f;
        public float maxFlapExtendedSpeedKts = 85f;
        public float neverExceedSpeedKts = 163f;

        [Header("Prototype engine and propeller")]
        public float maxEnginePowerHp = 180f;
        public float propEfficiency = 0.74f;
        public float staticThrustNewtons = 3600f;
        public float maxThrustNewtons = 4200f;
        public float idleRpm = 700f;
        public float maxRpm = 2700f;
        public float carbHeatPowerLoss = 0.08f;
        public float minimumMixturePower = 0.35f;

        [Header("Prototype aerodynamics")]
        public float airDensityKgM3 = 1.0f;
        public float zeroLiftAoADeg = -2f;
        public float criticalAoADeg = 15f;
        public float liftCoefficientBase = 0.45f;
        public float liftCurveSlopePerRad = 5.1f;
        public float maximumLiftCoefficient = 1.55f;
        public float postStallLiftMultiplier = 0.55f;
        public float elevatorAoAAuthorityDeg = 7.5f;
        public float trimAoAAuthorityDeg = 4.5f;
        public float flapLiftBonus = 0.55f;
        public float dragCoefficientBase = 0.035f;
        public float inducedDragFactor = 0.054f;
        public float flapDragBonus = 0.16f;
        public float sideSlipDragFactor = 0.04f;

        [Header("Prototype controls and stability")]
        public float pitchRateMaxDegPerSec = 42f;
        public float rollRateMaxDegPerSec = 64f;
        public float yawRateMaxDegPerSec = 28f;
        public float pitchDamping = 2.4f;
        public float rollDamping = 2.8f;
        public float yawDamping = 2.1f;
        public float pitchAttitudeStability = 0.85f;
        public float rollAttitudeStability = 0.45f;
        public float maxPrototypePitchDeg = 28f;
        public float maxPrototypeBankDeg = 65f;
        public float coordinatedTurnCoupling = 0.7f;
        public float flapPitchMomentDegPerSec = -8f;

        [Header("Prototype flaps and trim")]
        public float[] flapSettingsDeg = { 0f, 10f, 20f, 30f };
        public float maxTrim = 1f;

        [Header("Prototype ground handling")]
        public float runwayFrictionCoefficient = 0.035f;
        public float lateralGroundFriction = 2.7f;
        public float brakeStrengthNewtons = 6500f;
        public float nosewheelSteerDegPerSec = 22f;
        public float groundEffectHeightM = 4f;

        public static C172StyleAircraftConfig CreateRuntimeDefault()
        {
            C172StyleAircraftConfig config = CreateInstance<C172StyleAircraftConfig>();
            config.name = "C172StyleRuntimeDefault";
            return config;
        }
    }
}
