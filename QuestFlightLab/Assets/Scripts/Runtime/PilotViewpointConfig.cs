using UnityEngine;

namespace QuestFlightLab.Runtime
{
    public static class PilotViewpointConfig
    {
        // Aircraft local +Z is forward in the calibrated cockpit frame. Alex's
        // headset witness found that the previous default required a physical
        // step backward, so the aircraft default now includes a 0.10 m -Z
        // correction. Per-aircraft user calibration remains additive to this.
        public const float DefaultPilotEyeAftMeters = 0.10f;
        public const float MaximumDefaultPilotEyeAftMeters = 0.25f;

        // The imported model's left-seat eye is 0.28 m left of its centerline.
        // Keeping that lateral offset in the seat frame leaves the aircraft
        // model/CG on x=0 instead of making the aircraft rotate about the pilot.
        public static readonly Vector3 ImportedC172SeatReferenceLocal = new Vector3(-0.28f, 0.72f, 0f);
        public static readonly Vector3 ImportedC172DefaultPilotViewOffset =
            new Vector3(0f, 0.22f, -DefaultPilotEyeAftMeters);
        public static readonly Vector3 ImportedC172DefaultPilotEyeLocal = ImportedC172SeatReferenceLocal + ImportedC172DefaultPilotViewOffset;

        public static Vector3 ImportedC172DefaultPilotEyeLocalForAftDistance(float aftMeters)
        {
            float boundedAft = Mathf.Clamp(aftMeters, 0f, MaximumDefaultPilotEyeAftMeters);
            return ImportedC172SeatReferenceLocal + new Vector3(0f, 0.22f, -boundedAft);
        }

        public const float MinimumCockpitOutsideViewRatio = 0.14f;
        public const float MinimumDefaultEyeHeightMeters = 0.90f;
        public const float MaximumCalibrationLateralMeters = 0.25f;
        public const float MaximumCalibrationVerticalMeters = 0.30f;
        public const float MaximumCalibrationLongitudinalMeters = 0.35f;
        public const float MaximumCalibrationYawDegrees = 15f;
    }
}
