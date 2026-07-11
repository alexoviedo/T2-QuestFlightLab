#if UNITY_INCLUDE_TESTS
using System.IO;
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
            Assert.That(pilotEye.x, Is.LessThan(-0.15f));
            Assert.That(pilotEye.x, Is.GreaterThan(-0.45f));
            Assert.That(pilotEye.y, Is.GreaterThan(0.45f));
            Assert.That(pilotEye.y, Is.LessThan(1.1f));
            Assert.That(pilotEye.z, Is.EqualTo(-PilotViewpointConfig.DefaultPilotEyeAftMeters).Within(0.0001f));
            Assert.That(PilotViewpointConfig.DefaultPilotEyeAftMeters, Is.EqualTo(0.10f).Within(0.0001f));
            Assert.That(Vector3.Dot(
                    PilotViewpointConfig.ImportedC172DefaultPilotViewOffset,
                    Vector3.forward),
                Is.LessThan(0f),
                "Aircraft local +Z is forward, so the aircraft default aft correction must be local -Z.");

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
            Vector3 modelRoot = QuestFirstViewRuntimeRepair.ImportedC172SeatReferenceLocal - modelInCamera * importedEye;
            Assert.That(Mathf.Abs(modelRoot.x), Is.LessThan(0.001f),
                "The model centerline must remain on simulation-root x=0; the left seat carries the lateral offset.");
            Assert.That(Vector3.Dot(modelInCamera * Vector3.forward, Vector3.up), Is.GreaterThan(0.95f));
            Assert.That(Vector3.Dot(modelInCamera * Vector3.up, Vector3.back), Is.GreaterThan(0.95f));
        }

        [Test]
        public void CorrectedDefaultEyeIncreasesDistanceFromImportedPanelGeometry()
        {
            GameObject prefab = Resources.Load<GameObject>(QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            GameObject aircraftFrame = new GameObject("AircraftVisualRootEyeGeometryProbe");
            GameObject cockpit = Object.Instantiate(prefab, aircraftFrame.transform);
            try
            {
                cockpit.transform.localRotation = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler);
                cockpit.transform.localPosition = QuestFirstViewRuntimeRepair.ImportedC172SeatReferenceLocal -
                                                  cockpit.transform.localRotation *
                                                  QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye;

                Vector3 previousDefaultEye = QuestFirstViewRuntimeRepair.ImportedC172SeatReferenceLocal +
                                             new Vector3(0f, 0.22f, 0f);
                Assert.That(QuestFirstViewRuntimeRepair.TryMeasureEyeToPanelDistance(
                    cockpit.transform,
                    aircraftFrame.transform,
                    previousDefaultEye,
                    out float previousDistance), Is.True);
                Assert.That(QuestFirstViewRuntimeRepair.TryMeasureEyeToPanelDistance(
                    cockpit.transform,
                    aircraftFrame.transform,
                    QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal,
                    out float correctedDistance), Is.True);

                TestContext.WriteLine(
                    $"Imported panel distance previous={previousDistance:F3}m corrected={correctedDistance:F3}m");
                Assert.That(correctedDistance, Is.GreaterThan(previousDistance + 0.06f));
                Assert.That(correctedDistance, Is.InRange(0.35f, 1.50f));
            }
            finally
            {
                Object.DestroyImmediate(aircraftFrame);
            }
        }

        [Test]
        public void CockpitViewpointPersistenceSavesLoadsAndResetsCurrentCalibration()
        {
            string root = Path.Combine(Application.temporaryCachePath, "qfl_viewpoint_persistence_test");
            if (Directory.Exists(root)) Directory.Delete(root, true);

            CockpitViewpointCalibrationState state = new CockpitViewpointCalibrationState
            {
                schemaVersion = CockpitViewpointPersistence.SchemaVersion,
                generatedUtc = "2026-07-07T00:00:00.0000000Z",
                sceneryMode = "visual_qa",
                demoMode = "short_playtest",
                importedC172CockpitModelEye = QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye,
                importedC172PilotViewOffset = new Vector3(-0.21f, 0.17f, 0.05f),
                importedC172CockpitYawDeg = 3.5f,
                calibrationOffset = new Vector3(-0.21f, 0.17f, 0.05f),
                calibrationYawDeg = 3.5f,
                pilotEyeLocal = QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal + new Vector3(-0.21f, 0.17f, 0.05f),
                importedC172LocalPosition = Vector3.zero,
                instructions = "Test calibration."
            };

            string savedPath = CockpitViewpointPersistence.SaveCurrent(state, root);
            Assert.That(File.Exists(savedPath), Is.True);

            bool loaded = CockpitViewpointPersistence.TryLoadCurrent(
                out CockpitViewpointCalibrationState restored,
                out string loadedPath,
                out string loadError,
                root);
            Assert.That(loaded, Is.True, loadError);
            Assert.That(loadedPath, Is.EqualTo(savedPath));
            Assert.That(restored.importedC172PilotViewOffset.x, Is.EqualTo(state.importedC172PilotViewOffset.x).Within(0.0001f));
            Assert.That(restored.importedC172PilotViewOffset.y, Is.EqualTo(state.importedC172PilotViewOffset.y).Within(0.0001f));
            Assert.That(restored.importedC172PilotViewOffset.z, Is.EqualTo(state.importedC172PilotViewOffset.z).Within(0.0001f));
            Assert.That(restored.importedC172CockpitYawDeg, Is.EqualTo(3.5f).Within(0.0001f));
            string canonicalJson = File.ReadAllText(savedPath);
            Assert.That(canonicalJson, Does.Not.Contain("trackedCamera"));
            Assert.That(canonicalJson, Does.Not.Contain("pilotEyeLocal"));
            Assert.That(canonicalJson, Does.Not.Contain("importedC172CockpitModelEye"));

            bool deleted = CockpitViewpointPersistence.DeleteCurrent(out string deletedPath, out string deleteError, root);
            Assert.That(deleted, Is.True, deleteError);
            Assert.That(deletedPath, Is.EqualTo(savedPath));
            Assert.That(File.Exists(savedPath), Is.False);
            Directory.Delete(root, true);
        }

        [Test]
        public void SeatFrameRemainsInvariantAcrossAircraftHeadingAndBank()
        {
            CreateReferenceFrameProbe(out GameObject simulation, out Camera camera, out AircraftReferenceFrameRig rig);
            try
            {
                Vector3 expectedSeatLocal = rig.PilotSeatAnchor.localPosition;
                Vector3 cgWorld = simulation.transform.position;
                foreach (float heading in new[] { 0f, 90f, 180f, 270f })
                {
                    foreach (float bank in new[] { 0f, 30f, -30f })
                    {
                        simulation.transform.rotation = Quaternion.Euler(0f, heading, bank);
                        rig.ApplyPresentationPoseForTest(simulation.transform.position, simulation.transform.rotation);

                        Assert.That(Vector3.Distance(rig.PilotSeatAnchor.localPosition, expectedSeatLocal), Is.LessThan(0.0001f));
                        Assert.That(Vector3.Distance(rig.CenterOfGravityReference.position, cgWorld), Is.LessThan(0.0001f));
                        Assert.That(camera.transform.IsChildOf(rig.XrOrigin), Is.True);
                        Assert.That(ReferenceEquals(camera.transform, rig.CenterOfGravityReference), Is.False);
                        Assert.That(Vector3.Distance(simulation.transform.position, cgWorld), Is.LessThan(0.0001f));
                        Assert.That(Vector3.Distance(rig.PilotSeatAnchor.position, cgWorld),
                            Is.EqualTo(expectedSeatLocal.magnitude).Within(0.001f),
                            "The left seat must orbit the fixed aircraft CG, not become the pivot.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void SyntheticHeadMotionChangesOnlyCameraPoseRelativeToSeat()
        {
            CreateReferenceFrameProbe(out GameObject simulation, out Camera camera, out AircraftReferenceFrameRig rig);
            try
            {
                Vector3 aircraftPosition = simulation.transform.position;
                Quaternion aircraftRotation = simulation.transform.rotation;
                Vector3 seatLocal = rig.PilotSeatAnchor.localPosition;

                camera.transform.localPosition = new Vector3(0.12f, 0.05f, -0.08f);
                camera.transform.localRotation = Quaternion.Euler(4f, 18f, 0f);

                Assert.That(Vector3.Distance(simulation.transform.position, aircraftPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(simulation.transform.rotation, aircraftRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(rig.PilotSeatAnchor.localPosition, seatLocal), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(camera.transform.localPosition, new Vector3(0.12f, 0.05f, -0.08f)), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void TrackingSpaceRecenterAlignsHeadToSeatWithoutWritingTrackedCamera()
        {
            CreateReferenceFrameProbe(out GameObject simulation, out Camera camera, out AircraftReferenceFrameRig rig);
            try
            {
                Vector3 correctedDefaultSeat = rig.PilotSeatAnchor.localPosition;
                Vector3 savedCalibration = new Vector3(0.04f, 0.03f, -0.06f);
                rig.ApplyCalibration(savedCalibration, 2.5f);
                Transform cameraOffset = camera.transform.parent;
                cameraOffset.localPosition = new Vector3(0.04f, 0.02f, -0.03f);
                cameraOffset.localRotation = Quaternion.Euler(0f, 3f, 0f);
                camera.transform.localPosition = new Vector3(0.31f, 1.58f, -0.46f);
                camera.transform.localRotation = Quaternion.Euler(-7f, 28f, 4f);

                Vector3 trackedCameraLocalPosition = camera.transform.localPosition;
                Quaternion trackedCameraLocalRotation = camera.transform.localRotation;
                Vector3 aircraftPosition = simulation.transform.position;
                Quaternion aircraftRotation = simulation.transform.rotation;

                Assert.That(rig.RecenterTrackingSpaceToSeat(), Is.True);

                Assert.That(
                    rig.UserViewCalibrationOffset.InverseTransformPoint(camera.transform.position).magnitude,
                    Is.LessThan(0.0001f),
                    "The physical head position should land on the calibrated pilot eye.");
                Quaternion cameraInSeat = Quaternion.Inverse(rig.UserViewCalibrationOffset.rotation) * camera.transform.rotation;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, cameraInSeat.eulerAngles.y)), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(camera.transform.localPosition, trackedCameraLocalPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(camera.transform.localRotation, trackedCameraLocalRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(rig.PilotSeatAnchor.localPosition, correctedDefaultSeat), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(
                        rig.PilotSeatAnchor.localPosition,
                        PilotViewpointConfig.ImportedC172DefaultPilotEyeLocal),
                    Is.LessThan(0.0001f),
                    "Startup recenter must preserve the corrected aircraft default seat anchor.");
                Assert.That(Vector3.Distance(rig.CalibrationOffset, savedCalibration), Is.LessThan(0.0001f),
                    "Saved calibration remains additive and must survive origin-only recentering.");

                rig.ApplyCalibration(Vector3.zero, 0f);
                Assert.That(rig.RecenterTrackingSpaceToSeat(), Is.True);
                Assert.That(Vector3.Distance(rig.CalibrationOffset, Vector3.zero), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(
                        rig.UserViewCalibrationOffset.InverseTransformPoint(camera.transform.position),
                        Vector3.zero),
                    Is.LessThan(0.0001f),
                    "Reset must place the current tracked head on the corrected default without writing the camera.");
                Assert.That(Vector3.Distance(camera.transform.localPosition, trackedCameraLocalPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(simulation.transform.position, aircraftPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(simulation.transform.rotation, aircraftRotation), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void StartupSeatAlignmentRequiresConsecutiveStablePoseFrames()
        {
            Vector3 previousPosition = Vector3.zero;
            Quaternion previousRotation = Quaternion.identity;
            int stableFrames = QuestFirstViewRuntimeRepair.AdvanceStablePoseFrameCount(
                true,
                false,
                previousPosition,
                previousRotation,
                previousPosition,
                previousRotation,
                0,
                0.02f,
                2f);
            Assert.That(stableFrames, Is.EqualTo(1));

            Vector3 gentlyMoved = new Vector3(0.004f, -0.002f, 0.003f);
            Quaternion gentlyRotated = Quaternion.Euler(0.4f, 0.8f, 0f);
            stableFrames = QuestFirstViewRuntimeRepair.AdvanceStablePoseFrameCount(
                true,
                true,
                previousPosition,
                previousRotation,
                gentlyMoved,
                gentlyRotated,
                stableFrames,
                0.02f,
                2f);
            Assert.That(stableFrames, Is.EqualTo(2));

            Vector3 largeMove = gentlyMoved + new Vector3(0.08f, 0f, 0f);
            stableFrames = QuestFirstViewRuntimeRepair.AdvanceStablePoseFrameCount(
                true,
                true,
                gentlyMoved,
                gentlyRotated,
                largeMove,
                gentlyRotated,
                stableFrames,
                0.02f,
                2f);
            Assert.That(stableFrames, Is.EqualTo(1), "Movement outside the stability window starts a new sequence.");

            stableFrames = QuestFirstViewRuntimeRepair.AdvanceStablePoseFrameCount(
                false,
                true,
                largeMove,
                gentlyRotated,
                largeMove,
                gentlyRotated,
                stableFrames,
                0.02f,
                2f);
            Assert.That(stableFrames, Is.Zero, "Tracking/presence loss must invalidate accumulated stable frames.");
        }

        [Test]
        public void PresentationHierarchyNeverMovesAuthoritativeCollisionProxy()
        {
            GameObject simulation = new GameObject("CollisionOwnershipSimulationRoot");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Prototype Visual With Generated Collider";
            visual.transform.SetParent(simulation.transform, false);
            GameObject collisionProxy = new GameObject("Authoritative Collision Proxy");
            collisionProxy.transform.SetParent(simulation.transform, false);
            BoxCollider authoritativeCollider = collisionProxy.AddComponent<BoxCollider>();

            GameObject origin = new GameObject("XR Origin");
            GameObject offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(offset.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();

            try
            {
                AircraftReferenceFrameRig rig = AircraftReferenceFrameRig.Ensure(
                    simulation.transform,
                    origin.transform,
                    camera,
                    QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal);

                Assert.That(visual.transform.parent, Is.EqualTo(rig.AircraftVisualRoot));
                Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
                Assert.That(collisionProxy.transform.parent, Is.EqualTo(simulation.transform));
                Assert.That(authoritativeCollider.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void TrackedMainCameraHasExplicitCenterEyeBindings()
        {
            GameObject cameraObject = new GameObject("TrackedCameraBindingProbe");
            Camera camera = cameraObject.AddComponent<Camera>();
            try
            {
                Assert.That(TrackedXrCameraPoseDriver.HasRequiredBindings(camera), Is.False);
                UnityEngine.InputSystem.XR.TrackedPoseDriver driver = TrackedXrCameraPoseDriver.Ensure(camera);

                Assert.That(driver, Is.Not.Null);
                Assert.That(TrackedXrCameraPoseDriver.HasRequiredBindings(camera), Is.True);
                Assert.That(TrackedXrCameraPoseDriver.PositionBindingPath(camera),
                    Is.EqualTo(TrackedXrCameraPoseDriver.PositionBinding).IgnoreCase);
                Assert.That(TrackedXrCameraPoseDriver.RotationBindingPath(camera),
                    Is.EqualTo(TrackedXrCameraPoseDriver.RotationBinding).IgnoreCase);
                Assert.That(driver.positionInput.action.enabled, Is.True);
                Assert.That(driver.rotationInput.action.enabled, Is.True);
                Assert.That(driver.trackingStateInput.action.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void AircraftMotionCarriesSeatAndTrackedRigWithoutChangingHeadOffset()
        {
            CreateReferenceFrameProbe(out GameObject simulation, out Camera camera, out AircraftReferenceFrameRig rig);
            try
            {
                camera.transform.localPosition = new Vector3(-0.07f, 0.03f, 0.11f);
                camera.transform.localRotation = Quaternion.Euler(-3f, 12f, 0f);
                Vector3 headLocal = camera.transform.localPosition;
                Quaternion headLocalRotation = camera.transform.localRotation;

                simulation.transform.SetPositionAndRotation(new Vector3(125f, 42f, -310f), Quaternion.Euler(-8f, 135f, 24f));
                rig.ApplyPresentationPoseForTest(simulation.transform.position, simulation.transform.rotation);

                Vector3 expectedSeatWorld = simulation.transform.TransformPoint(QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal);
                Assert.That(Vector3.Distance(rig.PilotSeatAnchor.position, expectedSeatWorld), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(camera.transform.localPosition, headLocal), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(camera.transform.localRotation, headLocalRotation), Is.LessThan(0.001f));
                Assert.That(rig.ValidateHierarchy(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void CalibrationPanelRemainsInTrackingSpaceAtAllowedExtremes()
        {
            CreateReferenceFrameProbe(out GameObject simulation, out Camera camera, out AircraftReferenceFrameRig rig);
            GameObject panel = null;
            try
            {
                panel = QuestFirstViewRuntimeRepair.BuildSeatCalibrationPanelVisual(
                    rig.UserViewCalibrationOffset,
                    out TextMesh text);
                panel.transform.localPosition = new Vector3(0.48f, -0.08f, 1.05f);
                Vector3 initialRelative = rig.XrOrigin.InverseTransformPoint(panel.transform.position);

                rig.ApplyCalibration(
                    new Vector3(
                        PilotViewpointConfig.MaximumCalibrationLateralMeters,
                        PilotViewpointConfig.MaximumCalibrationVerticalMeters,
                        PilotViewpointConfig.MaximumCalibrationLongitudinalMeters),
                    PilotViewpointConfig.MaximumCalibrationYawDegrees);

                Vector3 extremeRelative = rig.XrOrigin.InverseTransformPoint(panel.transform.position);
                Assert.That(Vector3.Distance(initialRelative, extremeRelative), Is.LessThan(0.0001f));
                Assert.That(Vector3.Dot(extremeRelative.normalized, Vector3.forward), Is.GreaterThan(0.75f));
                Assert.That(text.text, Does.Contain("A save      B cancel"));
                Assert.That(text.text, Does.Contain("X recenter   Y default"));
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void CalibrationRecordRoundTripResetRestoresAircraftDefault()
        {
            string root = Path.Combine(Application.temporaryCachePath, "qfl_viewpoint_v4_roundtrip");
            if (Directory.Exists(root)) Directory.Delete(root, true);

            Vector3 adjusted = new Vector3(0.08f, 0.12f, -0.16f);
            CockpitViewpointCalibrationState state = new CockpitViewpointCalibrationState
            {
                aircraftId = CockpitViewpointPersistence.DefaultAircraftId,
                importedC172PilotViewOffset = adjusted,
                importedC172CockpitYawDeg = 2.5f,
                calibrationOffset = adjusted,
                calibrationYawDeg = 2.5f
            };

            string path = CockpitViewpointPersistence.SaveCurrent(state, root);
            Assert.That(CockpitViewpointPersistence.TryLoadCurrent(out CockpitViewpointCalibrationState loaded, out _, out string error, root), Is.True, error);
            Assert.That(Vector3.Distance(loaded.calibrationOffset, adjusted), Is.LessThan(0.0001f));
            Assert.That(loaded.calibrationYawDeg, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(File.Exists(path), Is.True);

            Assert.That(CockpitViewpointPersistence.DeleteCurrent(out _, out string deleteError, root), Is.True, deleteError);
            Assert.That(CockpitViewpointPersistence.TryLoadCurrent(out _, out _, out _, root), Is.False);
            Assert.That(QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal,
                Is.EqualTo(PilotViewpointConfig.ImportedC172DefaultPilotEyeLocal));
            Directory.Delete(root, true);
        }

        private static void CreateReferenceFrameProbe(
            out GameObject simulation,
            out Camera camera,
            out AircraftReferenceFrameRig rig)
        {
            simulation = new GameObject("AircraftSimulationRootTest");
            simulation.transform.SetPositionAndRotation(new Vector3(12f, 5f, -8f), Quaternion.identity);
            GameObject visual = new GameObject("ExistingAircraftModelTest");
            visual.transform.SetParent(simulation.transform, false);

            GameObject origin = new GameObject("XR Origin Test");
            GameObject cameraOffset = new GameObject("Camera Offset Test");
            cameraOffset.transform.SetParent(origin.transform, false);
            GameObject cameraObject = new GameObject("Tracked Main Camera Test");
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            camera = cameraObject.AddComponent<Camera>();

            rig = AircraftReferenceFrameRig.Ensure(
                simulation.transform,
                origin.transform,
                camera,
                QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal);
            rig.SetPresentationInterpolationForTest(false);
            Assert.That(rig.HierarchyReady, Is.True);
            Assert.That(visual.transform.parent, Is.EqualTo(rig.AircraftVisualRoot));
            Assert.That(rig.LeftController.parent, Is.EqualTo(rig.XrOrigin));
            Assert.That(rig.RightController.parent, Is.EqualTo(rig.XrOrigin));
            Assert.That(TrackedXrControllerPoseDrivers.HasRequiredHierarchy(rig.XrOrigin), Is.True);
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
