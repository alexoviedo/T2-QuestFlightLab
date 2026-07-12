using System;
using System.IO;
using QuestFlightLab.Flight.Backends;
using UnityEditor;

namespace QuestFlightLab.Editor
{
    public static class JSBSimNativeValidationRunner
    {
        private const string OutputEnvironmentVariable = "QFL_JSBSIM_NATIVE_VALIDATION_DIR";

        [MenuItem("Quest Flight Lab/Run JSBSim Native Validation")]
        public static void Run()
        {
            string outputDirectory = System.Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.GetFullPath(Path.Combine(
                    "..",
                    "T2-QuestFlightLab-setup-artifacts",
                    $"jsbsim_native_validation_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            Directory.CreateDirectory(outputDirectory);
            JSBSimPluginImporterConfigurator.Configure();
            if (!JSBSimNativeFlightBackend.ProbeLibrary(out string probeError))
            {
                throw new InvalidOperationException($"Native JSBSim plugin probe failed: {probeError}");
            }

            JSBSimNativeScenarioGateV2.Run(outputDirectory);
        }
    }
}
