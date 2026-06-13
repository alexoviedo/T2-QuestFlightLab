using UnityEngine;

namespace QuestFlightLab.Environment
{
    public class SceneryModeController : MonoBehaviour
    {
        public SceneryMode requestedMode = SceneryMode.MeshFallback;
        public bool enableExperimentalSplatProxy;
        public int syntheticSplatCount = 5000;

        public SceneryProviderStatus LastStatus { get; private set; }

        private MeshSceneryProvider meshProvider;
        private SplatSceneryProvider splatProvider;

        private void Start()
        {
            ApplyMode(requestedMode);
        }

        public SceneryProviderStatus ApplyMode(SceneryMode mode)
        {
            EnsureProviders();
            if (mode == SceneryMode.MeshFallback)
            {
                LastStatus = meshProvider.ActivateProvider(null);
                return LastStatus;
            }

            splatProvider.enableExperimentalProxy = enableExperimentalSplatProxy;
            splatProvider.syntheticSplatCount = syntheticSplatCount;
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
