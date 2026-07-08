using UnityEngine;

namespace QuestFlightLab.Runtime
{
    public static class PilotViewpointConfig
    {
        public static readonly Vector3 ImportedC172SeatReferenceLocal = new Vector3(0f, 0.72f, 0f);
        public static readonly Vector3 ImportedC172DefaultPilotViewOffset = new Vector3(0f, 0.22f, 0f);
        public static readonly Vector3 ImportedC172DefaultPilotEyeLocal = ImportedC172SeatReferenceLocal + ImportedC172DefaultPilotViewOffset;

        public const float MinimumCockpitOutsideViewRatio = 0.14f;
        public const float MinimumDefaultEyeHeightMeters = 0.90f;
    }
}
