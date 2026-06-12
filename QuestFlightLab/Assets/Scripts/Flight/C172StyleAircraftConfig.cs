using UnityEngine;

namespace QuestFlightLab.Flight
{
    [CreateAssetMenu(menuName = "Quest Flight Lab/C172 Style Aircraft Config")]
    public class C172StyleAircraftConfig : ScriptableObject
    {
        [Header("Approximate C172-style values")]
        public float massKg = 1111f;
        public float wingAreaM2 = 16.2f;
        public float stallSpeedCleanKts = 48f;
        public float stallSpeedLandingKts = 40f;
        public float cruiseSpeedKts = 105f;
        public float rotationSpeedKts = 55f;
        public float bestClimbSpeedKts = 74f;
        public float neverExceedSpeedKts = 163f;

        [Header("Prototype aerodynamics")]
        public float maxThrustNewtons = 2600f;
        public float liftCoefficientBase = 0.32f;
        public float liftCoefficientPerAoA = 4.8f;
        public float elevatorLiftAuthority = 0.45f;
        public float flapLiftBonus = 0.55f;
        public float dragCoefficientBase = 0.038f;
        public float inducedDragFactor = 0.055f;
        public float flapDragBonus = 0.14f;
        public float controlResponse = 70f;
        public float angularDamping = 1.8f;

        public static C172StyleAircraftConfig CreateRuntimeDefault()
        {
            C172StyleAircraftConfig config = CreateInstance<C172StyleAircraftConfig>();
            config.name = "C172StyleRuntimeDefault";
            return config;
        }
    }
}

