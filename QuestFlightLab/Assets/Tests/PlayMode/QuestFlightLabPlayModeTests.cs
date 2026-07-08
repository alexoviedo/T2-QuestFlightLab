#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Environment;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.TestHarness;
using QuestFlightLab.UI;
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

        [Test]
        public void PlaytestHudSuppressesVerbosePanelsAndRendersCompactText()
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();

            GameObject telemetryRoot = new GameObject("Telemetry Panel");
            TelemetryPanel telemetry = telemetryRoot.AddComponent<TelemetryPanel>();
            telemetry.panelRoot = telemetryRoot;

            GameObject cockpitRoot = new GameObject("Cockpit Instrument Panel v0.5");
            cockpitRoot.AddComponent<CockpitInstrumentPanel>();

            GameObject menuRoot = new GameObject("Touch Controller Menu Placeholder");
            menuRoot.AddComponent<TouchMenuPlaceholder>();

            GameObject performanceRoot = new GameObject("Performance HUD");
            PerformanceHud performanceHud = performanceRoot.AddComponent<PerformanceHud>();
            GameObject fpsText = new GameObject("FpsText");
            fpsText.transform.SetParent(performanceRoot.transform, false);
            performanceHud.text = fpsText.AddComponent<TextMesh>();

            GameObject hudRoot = new GameObject("Playtest HUD Probe");
            PlaytestHud hud = hudRoot.AddComponent<PlaytestHud>();
            hud.InitializeForTest(camera);

            Assert.That(telemetryRoot.activeSelf, Is.False);
            Assert.That(cockpitRoot.activeSelf, Is.False);
            Assert.That(menuRoot.activeSelf, Is.False);
            Assert.That(performanceRoot.activeSelf, Is.False);
            Assert.That(hud.HiddenVerbosePanelCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(hud.VisibleLineCount, Is.LessThanOrEqualTo(6));
            Assert.That(hud.LastRenderedText, Does.Contain("QUEST PLAYTEST"));

            Object.DestroyImmediate(hudRoot);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(telemetryRoot);
            Object.DestroyImmediate(cockpitRoot);
            Object.DestroyImmediate(menuRoot);
            Object.DestroyImmediate(performanceRoot);
        }

        [Test]
        public void ImportedC172AssetLoadsWithRenderableGeometry()
        {
            GameObject prefab = Resources.Load<GameObject>(QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            Assert.That(prefab, Is.Not.Null, $"Expected imported C172 at Resources/{QuestFirstViewRuntimeRepair.ImportedC172ResourcePath}");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(10));

                bool hasBounds = false;
                Bounds bounds = default;
                foreach (Renderer renderer in renderers)
                {
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                Assert.That(hasBounds, Is.True);
                TestContext.WriteLine($"Imported C172 renderer count={renderers.Length} bounds center={bounds.center} size={bounds.size}");
                Assert.That(bounds.size.x, Is.GreaterThan(4f));
                Assert.That(bounds.size.y, Is.GreaterThan(1f));
                Assert.That(bounds.size.z, Is.GreaterThan(4f));
                Assert.That(bounds.size.x, Is.LessThan(25f));
                Assert.That(bounds.size.y, Is.LessThan(10f));
                Assert.That(bounds.size.z, Is.LessThan(25f));

                instance.transform.localRotation = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler);
                Bounds alignedBounds = BoundsForTest(renderers);
                TestContext.WriteLine($"Imported C172 aligned full bounds center={alignedBounds.center} size={alignedBounds.size}");
                Assert.That(alignedBounds.size.y, Is.LessThan(5f));
                Assert.That(alignedBounds.size.z, Is.GreaterThan(7f));

                foreach (Renderer renderer in renderers)
                {
                    string path = PathForTest(renderer.transform);
                    if (!path.Contains("Cessna_Interior") &&
                        !path.Contains("GlassPartNew") &&
                        !path.Contains("Steering") &&
                        !path.Contains("Meter") &&
                        !path.Contains("Seats"))
                    {
                        continue;
                    }

                    TestContext.WriteLine($"Imported C172 aligned renderer={renderer.name} center={renderer.bounds.center} size={renderer.bounds.size}");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ImportedC172PilotEyeStartsInLeftSeatCabinEnvelope()
        {
            Vector3 pilotEye = QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal;
            Assert.That(Mathf.Abs(pilotEye.x), Is.LessThan(0.2f));
            Assert.That(pilotEye.y, Is.GreaterThan(0.45f));
            Assert.That(pilotEye.y, Is.LessThan(1.1f));
            Assert.That(Mathf.Abs(pilotEye.z), Is.LessThan(0.3f));

            Vector3 importedEye = QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye;
            Assert.That(importedEye.x, Is.LessThan(-0.15f));
            Assert.That(importedEye.x, Is.GreaterThan(-0.45f));
            Assert.That(importedEye.y, Is.GreaterThan(-0.8f));
            Assert.That(importedEye.y, Is.LessThan(-0.2f));
            Assert.That(importedEye.z, Is.GreaterThan(1.55f));
            Assert.That(importedEye.z, Is.LessThan(1.8f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler.x, Is.EqualTo(-90f).Within(0.01f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye.x, Is.LessThan(0f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEyeEuler.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEyeEuler.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEyeEuler.z, Is.EqualTo(0f).Within(0.01f));

            Quaternion modelInCamera = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler);
            Assert.That(Vector3.Dot(modelInCamera * Vector3.forward, Vector3.up), Is.GreaterThan(0.95f));
            Assert.That(Vector3.Dot(modelInCamera * Vector3.up, Vector3.back), Is.GreaterThan(0.95f));
        }

        private static Bounds BoundsForTest(Renderer[] renderers)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static string PathForTest(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        [Test]
        public void ShortPlaytestDemoPilotSequenceProvidesTakeoffControls()
        {
            AircraftControlState sweep = ShortPlaytestDemoPilot.ControlsForElapsedSeconds(5f, out string sweepPhase);
            AircraftControlState roll = ShortPlaytestDemoPilot.ControlsForElapsedSeconds(30f, out string rollPhase);
            AircraftControlState rotate = ShortPlaytestDemoPilot.ControlsForElapsedSeconds(52f, out string rotatePhase);

            Assert.That(sweepPhase, Is.EqualTo("control surface sweep"));
            Assert.That(Mathf.Abs(sweep.aileron), Is.GreaterThan(0.1f));
            Assert.That(rollPhase, Is.EqualTo("takeoff roll"));
            Assert.That(roll.throttle, Is.EqualTo(1f).Within(0.01f));
            Assert.That(rotatePhase, Is.EqualTo("rotate/climb"));
            Assert.That(rotate.elevator, Is.GreaterThan(0.25f));
        }

        [Test]
        public void ShortPlaytestDemoPilotVisualEnvelopeClimbsAndBanks()
        {
            bool hasPose = ShortPlaytestDemoPilot.TryGetVisualFlightPoseForElapsedSeconds(
                72f,
                new Vector3(-560f, 1.25f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                out Vector3 position,
                out Vector3 euler,
                out Vector3 velocityWorld);

            Assert.That(hasPose, Is.True);
            Assert.That(position.y, Is.GreaterThan(20f));
            Assert.That(Mathf.Abs(euler.z), Is.GreaterThan(2f));
            Assert.That(velocityWorld.magnitude, Is.GreaterThan(25f));
        }
    }
}
#endif
