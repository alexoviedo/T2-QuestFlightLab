using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    [Serializable]
    public class SceneryProviderStatus
    {
        public string providerName;
        public string requestedMode;
        public string activeMode;
        public bool fallbackUsed;
        public bool rendererAvailable;
        public bool proxyUsed;
        public string sampleAssetPath;
        public string sampleName;
        public string sampleKey;
        public string budgetProfile;
        public int splatCount;
        public long assetBytes;
        public long estimatedGpuBytes;
        public bool rendererInstantiated;
        public bool hasValidAsset;
        public bool hasValidRenderSetup;
        public float loadMs;
        public string loadError;
        public float averageFrameMs;
        public float estimatedFps;
        public List<string> warnings = new List<string>();
    }

    public abstract class SceneryVisualProvider : MonoBehaviour
    {
        public abstract SceneryMode Mode { get; }

        public abstract SceneryProviderStatus ActivateProvider(Transform parent);

        public virtual void DeactivateProvider()
        {
            gameObject.SetActive(false);
        }

        protected static SceneryProviderStatus Status(string providerName, SceneryMode requested, SceneryMode active)
        {
            return new SceneryProviderStatus
            {
                providerName = providerName,
                requestedMode = requested.ToString(),
                activeMode = active.ToString()
            };
        }
    }
}
