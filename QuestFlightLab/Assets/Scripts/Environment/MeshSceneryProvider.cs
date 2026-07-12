using UnityEngine;

namespace QuestFlightLab.Environment
{
    public class MeshSceneryProvider : SceneryVisualProvider
    {
        public const string AirportRootName = "KBDU_Approx_Airport_NotForNavigation";

        public override SceneryMode Mode => SceneryMode.MeshFallback;

        public override SceneryProviderStatus ActivateProvider(Transform parent)
        {
            if (ProductionEnvironmentActivation.IsProductionVerticalSliceActive())
            {
                return ProductionEnvironmentActivation.BakedProductionStatus(nameof(MeshSceneryProvider), Mode);
            }
            GameObject root = GameObject.Find(AirportRootName);
            if (root == null)
            {
                root = KbduApproxAirport.Build(parent);
            }
            else if (parent != null && root.transform.parent != parent)
            {
                root.transform.SetParent(parent, true);
            }

            AirportRuntimeEnhancer.EnhanceExistingScene();
            root.SetActive(true);

            SceneryProviderStatus status = Status(nameof(MeshSceneryProvider), Mode, SceneryMode.MeshFallback);
            status.fallbackUsed = false;
            status.rendererAvailable = true;
            status.warnings.Add("Mesh/terrain fallback is active and remains the default scenery path.");
            Debug.Log("[QuestFlightLab][Scenery] Mesh fallback scenery active.");
            return status;
        }
    }
}
