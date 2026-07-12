using UnityEngine;

namespace QuestFlightLab.Environment
{
    public class SceneryModeController : MonoBehaviour
    {
        public SceneryMode requestedMode = SceneryMode.MeshFallback;
        public bool enableExperimentalSplatProxy;
        public int syntheticSplatCount = 5000;
        public string splatSampleKey = QuestSplatRuntimeConfig.SyntheticProfile;
        public string splatBudgetProfile = "synthetic";

        public SceneryProviderStatus LastStatus { get; private set; }

        private MeshSceneryProvider meshProvider;
        private SplatSceneryProvider splatProvider;

        private void Start()
        {
            if (ProductionEnvironmentActivation.IsProductionVerticalSliceActive())
            {
                LastStatus = ProductionEnvironmentActivation.BakedProductionStatus(nameof(SceneryModeController), requestedMode);
                enabled = false;
                return;
            }
            if (LastStatus != null)
            {
                return;
            }

            ApplyMode(requestedMode);
        }

        public SceneryProviderStatus ApplyMode(SceneryMode mode)
        {
            if (ProductionEnvironmentActivation.IsProductionVerticalSliceActive())
            {
                LastStatus = ProductionEnvironmentActivation.BakedProductionStatus(nameof(SceneryModeController), mode);
                return LastStatus;
            }
            EnsureProviders();
            if (mode == SceneryMode.MeshFallback)
            {
                splatProvider.DeactivateProvider();
                LastStatus = meshProvider.ActivateProvider(null);
                return LastStatus;
            }

            splatProvider.enableExperimentalProxy = enableExperimentalSplatProxy;
            splatProvider.syntheticSplatCount = syntheticSplatCount;
            splatProvider.runtimeSampleKey = splatSampleKey;
            splatProvider.runtimeBudgetProfile = splatBudgetProfile;
            SceneryProviderStatus splatStatus = splatProvider.ActivateProvider(transform);
            if (splatStatus.fallbackUsed || splatStatus.activeMode == SceneryMode.MeshFallback.ToString())
            {
                meshProvider.ActivateProvider(null);
            }

            LastStatus = splatStatus;
            return LastStatus;
        }

        private void EnsureProviders()
        {
            if (meshProvider == null)
            {
                meshProvider = GetComponent<MeshSceneryProvider>();
                if (meshProvider == null) meshProvider = gameObject.AddComponent<MeshSceneryProvider>();
            }

            if (splatProvider == null)
            {
                splatProvider = GetComponent<SplatSceneryProvider>();
                if (splatProvider == null) splatProvider = gameObject.AddComponent<SplatSceneryProvider>();
            }
        }
    }
}
