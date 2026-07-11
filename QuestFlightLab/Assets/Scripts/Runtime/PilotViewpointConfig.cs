using UnityEngine;

namespace QuestFlightLab.Runtime
{
    public static class PilotViewpointConfig
    {
        // The imported model's left-seat eye is 0.28 m left of its centerline.
        // Keeping that lateral offset in the seat frame leaves the aircraft
        // model/CG on x=0 instead of making the aircraft rotate about the pilot.
        public static readonly Vector3 ImportedC172SeatReferenceLocal = new Vector3(-0.28f, 0.72f, 0f);
        public static readonly Vector3 ImportedC172DefaultPilotViewOffset = new Vector3(0f, 0.22f, 0f);
        public static readonly Vector3 ImportedC172DefaultPilotEyeLocal = ImportedC172SeatReferenceLocal + ImportedC172DefaultPilotViewOffset;

        public const float MinimumCockpitOutsideViewRatio = 0.14f;
        public const float MinimumDefaultEyeHeightMeters = 0.90f;
        public const float MaximumCalibrationLateralMeters = 0.25f;
        public const float MaximumCalibrationVerticalMeters = 0.30f;
        public const float MaximumCalibrationLongitudinalMeters = 0.35f;
        public const float MaximumCalibrationYawDegrees = 15f;
    }
}
