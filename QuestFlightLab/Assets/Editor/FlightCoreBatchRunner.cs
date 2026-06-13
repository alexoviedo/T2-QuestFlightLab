using System;
using System.IO;
using System.Linq;
using QuestFlightLab.TestHarness;
using UnityEditor;
using UnityEngine;

namespace QuestFlightLab.Editor
{
    public static class FlightCoreBatchRunner
    {
        [MenuItem("Quest Flight Lab/Run Flight Core Scenarios")]
        public static void RunDefaultScenarios()
        {
            string artifactDir = System.Environment.GetEnvironmentVariable("QFL_ARTIFACT_DIR");
            if (string.IsNullOrWhiteSpace(artifactDir))
            {
                artifactDir = Path.GetFullPath(Path.Combine("..", "T2-QuestFlightLab-setup-artifacts", $"flight_core_editor_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            Directory.CreateDirectory(artifactDir);
            string metaXrSimulatorStatus = DetectMetaXrSimulatorStatus();
            FlightScenarioSuiteResult suite = FlightScenarioRunner.RunDefaultSuite(artifactDir, metaXrSimulatorStatus);
            string summary = $"[QuestFlightLab][FlightCore] {suite.passedCount}/{suite.scenarioCount} scenarios passed. Evidence: {artifactDir}";
            Debug.Log(summary);

            if (suite.failedCount > 0)
            {
                string failures = string.Join(", ", suite.scenarios.Where(s => !s.passed).Select(s => s.id));
                throw new Exception($"Flight core scenarios failed: {failures}. Evidence: {artifactDir}");
            }
        }

        [MenuItem("Quest Flight Lab/Detect Meta XR Simulator")]
        public static void DetectMetaXrSimulator()
        {
            Debug.Log($"[QuestFlightLab][FlightCore] {DetectMetaXrSimulatorStatus()}");
        }

        public static string DetectMetaXrSimulatorStatus()
        {
            string[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            bool hasMetaSimulatorAssembly = assemblies.Any(n =>
                n.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) >= 0 &&
                n.IndexOf("Simulator", StringComparison.OrdinalIgnoreCase) >= 0);

            if (hasMetaSimulatorAssembly)
            {
                return "Meta XR Simulator-like assembly detected in the Unity editor domain; this run still used the deterministic editor scenario fallback.";
            }

            return "Meta XR Simulator package/assembly not detected; deterministic Unity editor scenario fallback used.";
        }
    }
}
