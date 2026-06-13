#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Environment;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.TestHarness;
using UnityEngine;

namespace QuestFlightLab.Tests.PlayMode
{
    public class QuestFlightLabPlayModeTests
    {
        [Test]
        public void DeterministicInputSourcePublishesMappedControls()
        {
            GameObject go = new GameObject("DeterministicInputPlayModeProbe");
            DeterministicGamepadInputSource source = go.AddComponent<DeterministicGamepadInputSource>();
            Usb2BleInputMapper mapper = go.AddComponent<Usb2BleInputMapper>();
            mapper.deterministicInput = source;
            mapper.preferDeterministicInput = true;

            source.SetControls(new AircraftControlState
            {
                aileron = 0.75f,
                elevator = 0.25f,
                rudder = -0.5f,
                throttle = 0.8f,
                mixture = 1f,
                trim = 0.2f,
                flaps = 0.5f,
                leftToeBrake = 0.3f,
                rightToeBrake = 0.4f
            });

            mapper.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            mapper.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            Assert.That(mapper.Current.aileron, Is.EqualTo(0.75f).Within(0.01f));
            Assert.That(mapper.Current.elevator, Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(mapper.Current.rudder, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(mapper.Current.throttle, Is.EqualTo(0.8f).Within(0.01f));
            Assert.That(mapper.Current.leftToeBrake, Is.EqualTo(0.3f).Within(0.01f));

            Object.Destroy(go);
        }

        [Test]
        public void CockpitInstrumentPanelPublishesRequiredUpdatedFields()
        {
            InstrumentVerificationSnapshot snapshot = InstrumentVerification.Capture();
            Assert.That(snapshot.allRequiredPresent, Is.True, snapshot.summary);
            Assert.That(snapshot.valuesUpdated, Is.True, snapshot.summary);
            Assert.That(snapshot.approachFieldsPresent, Is.True, snapshot.summary);
            Assert.That(snapshot.approachFieldCount, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void SceneryModeControllerDefaultsToMeshFallback()
        {
            GameObject existingAirport = GameObject.Find(MeshSceneryProvider.AirportRootName);
            GameObject go = new GameObject("SceneryModeControllerPlayModeProbe");
            SceneryModeController controller = go.AddComponent<SceneryModeController>();

            SceneryProviderStatus status = controller.ApplyMode(SceneryMode.MeshFallback);

            Assert.That(status.activeMode, Is.EqualTo(SceneryMode.MeshFallback.ToString()));
            Assert.That(status.fallbackUsed, Is.False);
            Assert.That(GameObject.Find(MeshSceneryProvider.AirportRootName), Is.Not.Null);

            Object.DestroyImmediate(go);
            if (existingAirport == null)
            {
                Object.DestroyImmediate(GameObject.Find(MeshSceneryProvider.AirportRootName));
            }
        }

        [Test]
        public void SceneryModeControllerCarriesScenicBudgetMetadata()
        {
            GameObject go = new GameObject("ScenicSplatMetadataPlayModeProbe");
            SceneryModeController controller = go.AddComponent<SceneryModeController>();
            controller.syntheticSplatCount = 25000;
            controller.splatSampleKey = QuestSplatRuntimeConfig.ScenicProfile;
            controller.splatBudgetProfile = "scenic_splat_low";
            controller.enableExperimentalSplatProxy = false;

            SceneryProviderStatus status = controller.ApplyMode(SceneryMode.ExperimentalSplatRenderer);

            Assert.That(status.sampleKey, Is.EqualTo(QuestSplatRuntimeConfig.ScenicProfile));
            Assert.That(status.budgetProfile, Is.EqualTo("scenic_splat_low"));
            Assert.That(status.splatCount, Is.EqualTo(25000));
            bool expectedGraphicsFallback = status.warnings.Exists(w => w.Contains("unsupported on Direct3D11"));
            if (expectedGraphicsFallback)
            {
                Assert.That(status.activeMode, Is.EqualTo(SceneryMode.MeshFallback.ToString()));
                Assert.That(status.fallbackUsed, Is.True);
            }
            else if (SplatSceneryProvider.IsGaussianSplatRendererAvailable())
            {
                Assert.That(status.activeMode, Is.EqualTo(SceneryMode.ExperimentalSplatRenderer.ToString()), string.Join("; ", status.warnings));
                Assert.That(status.fallbackUsed, Is.False, string.Join("; ", status.warnings));
                Assert.That(status.hasValidAsset, Is.True, string.Join("; ", status.warnings));
                Assert.That(status.hasValidRenderSetup, Is.True, string.Join("; ", status.warnings));
                Assert.That(status.sampleName, Does.Contain("scenic_airfield_low_25000"));
            }

            Object.DestroyImmediate(go);
        }
    }
}
#endif
