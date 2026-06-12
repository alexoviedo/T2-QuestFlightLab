using UnityEngine;

namespace QuestFlightLab.Environment
{
    public class GaussianSplatSpikeStub : MonoBehaviour
    {
        public bool enableExperimentalMarker;

        private void Start()
        {
            if (!enableExperimentalMarker)
            {
                Debug.Log("[QuestFlightLab][Splats] Gaussian splat spike disabled. Mesh/terrain fallback active.");
                return;
            }

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "GaussianSplatSpikePlaceholder";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 5f, 18f);
            marker.transform.localScale = Vector3.one * 2f;
            Debug.Log("[QuestFlightLab][Splats] Placeholder only. No splat renderer is included in v0.1 fallback build.");
        }
    }
}

