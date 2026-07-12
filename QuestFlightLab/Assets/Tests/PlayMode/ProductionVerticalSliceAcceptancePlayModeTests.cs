#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using QuestFlightLab.Environment;
using QuestFlightLab.Runtime;
using QuestFlightLab.Training;
using QuestFlightLab.UI;
using UnityEngine;

namespace QuestFlightLab.Tests.PlayMode
{
    public sealed class ProductionVerticalSliceAcceptancePlayModeTests
    {
        private const string WorldResourcePath = "QuestFlightLab/Production/ProductionWorldRoot";
        private GameObject _worldInstance;

        [TearDown]
        public void TearDown()
        {
            if (_worldInstance != null) Object.DestroyImmediate(_worldInstance);
        }

        [Test]
        public void ProductionSceneBecomesReadyWithoutLegacyRepairOrDefaultDebugUi()
        {
            Transform world = LoadProductionWorld();
            Assert.That(ComponentsInWorld<QuestFirstViewRuntimeRepair>(world), Is.Empty);
            Assert.That(ComponentsInWorld<FirstViewPlaytestDiagnostics>(world), Is.Empty);
            Assert.That(ComponentsInWorld<PlaytestHud>(world), Is.Empty);
            Assert.That(ComponentsInWorld<CockpitInstrumentPanel>(world), Is.Empty);
            Assert.That(ComponentsInWorld<TrainingModeController>(world), Is.Empty);
            Assert.That(ComponentsInWorld<SceneryModeController>(world), Is.Empty);
            Assert.That(ComponentsInWorld<MeshSceneryProvider>(world), Is.Empty);
            Assert.That(ComponentsInWorld<SplatSceneryProvider>(world), Is.Empty);
            Assert.That(ComponentsInWorld<QuestSplatRuntimeGateController>(world), Is.Empty);
            Assert.That(ComponentsInWorld<Canvas>(world).Count(canvas => canvas.gameObject.activeInHierarchy), Is.Zero,
                "Production startup must not cover the cockpit with a legacy/debug Canvas.");

            Assert.That(QuestTemporalVisualGateRecorder.EvaluateSceneReadiness(out string status), Is.True, status);
            Assert.That(status, Does.Contain("authored production hierarchy"));
        }

        [Test]
        public void ProductionMountainProbeRecognizesAuthoredUsgsMeshesAsImmutableAuthority()
        {
            Transform world = LoadProductionWorld();
            ProductionVerticalSliceRoot marker = ComponentsInWorld<ProductionVerticalSliceRoot>(world).Single();
            ProductionEnvironmentRoot environment = marker.EnvironmentRoot.GetComponentInChildren<ProductionEnvironmentRoot>(true);
            MountainTemporalStabilityProbe probe = environment.gameObject.AddComponent<MountainTemporalStabilityProbe>();
            probe.autoCaptureInPlayer = false;
            probe.Initialize(environment.transform, environment.transform);
            MountainTemporalStabilityReport report = probe.CaptureSingleFrameForTest(marker.AircraftRig.TrackedCamera);

            Assert.That(report.realDataRootActive, Is.True);
            Assert.That(report.expectedRendererCount, Is.EqualTo(4));
            Assert.That(report.immutableTransforms, Is.True, string.Join("; ", report.violations));
            Assert.That(report.immutableMeshes, Is.True, string.Join("; ", report.violations));
            Assert.That(report.immutableRendererSet, Is.True, string.Join("; ", report.violations));
            Assert.That(report.noTerrainLodOrDither, Is.True, string.Join("; ", report.violations));
            Assert.That(report.oneMountainSource, Is.True, string.Join("; ", report.violations));
            Assert.That(report.passed, Is.True, string.Join("; ", report.violations));
        }

        [Test]
        public void ProductionRuntimeBudgetMetadataMatchesVerticalSliceTargets()
        {
            Assert.That(QuestTemporalVisualGateRecorder.ProductionDrawCallGate, Is.EqualTo(180));
            Assert.That(QuestTemporalVisualGateRecorder.ProductionVisibleTriangleGate, Is.EqualTo(700000L));
            Assert.That(QuestTemporalVisualGateRecorder.ProductionSceneMaterialGate, Is.EqualTo(40));
        }

        private Transform LoadProductionWorld()
        {
            GameObject prefab = Resources.Load<GameObject>(WorldResourcePath);
            Assert.That(prefab, Is.Not.Null, "Authored production world resource is missing.");
            _worldInstance = Object.Instantiate(prefab);
            return _worldInstance.transform;
        }

        private static T[] ComponentsInWorld<T>(Transform world) where T : Component =>
            world.GetComponentsInChildren<T>(true);
    }
}
#endif
