using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Flight.Backends
{
    /// <summary>
    /// Opt-in launch hook. No option means the established Unity runtime path
    /// remains untouched; native cannot become the demo default accidentally.
    /// </summary>
    public static class FlightDynamicsRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapRequestedBackend()
        {
            string option = QuestLaunchOptions.FlightBackend();
            if (string.IsNullOrWhiteSpace(option)) return;

            FlightDynamicsBackendKind kind;
            switch (option)
            {
                case "jsbsim_native":
                case "native":
                    kind = FlightDynamicsBackendKind.JSBSimNative;
                    break;
                case "jsbsim_editor_sidecar":
                case "sidecar":
                    kind = FlightDynamicsBackendKind.JSBSimEditorSidecar;
                    break;
                case "unity":
                case "unity_prototype":
                case "prototype":
                    kind = FlightDynamicsBackendKind.UnityPrototype;
                    break;
                default:
                    Debug.LogWarning($"[QuestFlightLab][FlightBackend] Ignoring unknown {QuestLaunchOptions.FlightBackendKey}={option}.");
                    return;
            }

            SimpleAircraftPhysics prototype = Object.FindFirstObjectByType<SimpleAircraftPhysics>();
            if (prototype == null)
            {
                Debug.LogWarning("[QuestFlightLab][FlightBackend] Launch option requested a backend but no SimpleAircraftPhysics aircraft root exists.");
                return;
            }

            FlightDynamicsCoordinator coordinator = prototype.GetComponent<FlightDynamicsCoordinator>();
            if (coordinator == null) coordinator = prototype.gameObject.AddComponent<FlightDynamicsCoordinator>();
            coordinator.requestedBackend = kind;
            coordinator.allowUnityFallback = true;
            coordinator.simulationRoot = prototype.transform;
            // AircraftReferenceFrameRig is the sole visual interpolation owner.
            coordinator.presentationRoot = prototype.transform;
            coordinator.unityPrototype = prototype;
            coordinator.aircraftState = prototype.state != null
                ? prototype.state
                : prototype.GetComponent<AircraftState>();
            Debug.Log($"[QuestFlightLab][FlightBackend] Opt-in launch option selected {kind}; native remains non-default.");
        }
    }
}
