using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Aircraft
{
    /// <summary>
    /// Applies the proven mobile cockpit-lighting policy to the already-authored
    /// aircraft renderers. It does not create or re-parent scene objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionAircraftLightingController : MonoBehaviour
    {
        [SerializeField] private GameObject importedAircraftRoot;
        [SerializeField, Range(0f, QuestCockpitLightingPolicy.MaximumStaticDepthStrength)]
        private float staticDepthStrength = QuestCockpitLightingPolicy.DefaultStaticDepthStrength;

        public CockpitLightingReport Report { get; private set; }

        public void ConfigureAuthoredRoot(GameObject root) => importedAircraftRoot = root;

        private void Awake()
        {
            if (importedAircraftRoot == null)
            {
                Debug.LogError("[QuestFlightLab][ProductionAircraft] Authored lighting root is missing.", this);
                enabled = false;
                return;
            }

            Report = QuestCockpitLightingPolicy.ConfigureImportedAircraft(
                importedAircraftRoot,
                staticDepthStrength);
        }
    }
}
