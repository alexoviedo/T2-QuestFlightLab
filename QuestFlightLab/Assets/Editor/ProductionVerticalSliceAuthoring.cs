using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestFlightLab.Aircraft;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.Flight;
using QuestFlightLab.Flight.Backends;
using QuestFlightLab.Environment;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuestFlightLab.Editor
{
    public static class ProductionVerticalSliceAuthoring
    {
        public const string ProductionRoot = "Assets/Production";
        public const string ProfilePath = ProductionRoot + "/Profiles/ImportedC172PilotSeatProfile.asset";
        public const string AircraftPrefabPath = ProductionRoot + "/Prefabs/ProductionC172AircraftRig.prefab";
        public const string WorldPrefabPath = "Assets/Resources/QuestFlightLab/Production/ProductionWorldRoot.prefab";
        public const string PanelMaterialPath = ProductionRoot + "/Materials/SeatCalibrationPanel.mat";
        public const string EnvironmentPrefabPath = ProductionRoot + "/Environment/ProductionEnvironmentRoot.prefab";
        public const string ScenePath = "Assets/Scenes/ProductionVerticalSlice.unity";
        public const string ImportedC172Path = "Assets/Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172.glb";

        private const string StaticPropellerNodeName = "Cessna_Exterior_Body_MAT_0.001";
        private const string StaticPropellerRendererName = "Cessna_Exterior_Body_MAT_0.001_Body_MAT_0";

        private static readonly Vector3 ImportedCockpitModelEye = new Vector3(-0.28f, -0.45f, 1.69f);
        private static readonly Quaternion ImportedModelRotation = Quaternion.Euler(-90f, 0f, 0f);

        [MenuItem("Quest Flight Lab/Production Vertical Slice V2/Rebuild Authored Scene")]
        public static void BuildFromMenu() => BuildAll();

        public static void BuildFromBatch() => BuildAll();

        public static void BuildAll()
        {
            EnsureFolder(ProductionRoot);
            EnsureFolder(ProductionRoot + "/Profiles");
            EnsureFolder(ProductionRoot + "/Prefabs");
            EnsureFolder(ProductionRoot + "/Materials");
            EnsureFolder("Assets/Resources/QuestFlightLab/Production");

            PilotSeatProfile profile = CreateOrUpdateSeatProfile();
            Material panelMaterial = CreateOrUpdatePanelMaterial();

            Scene authoringScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject aircraftRig = BuildAircraftRig(profile, panelMaterial, out AircraftAuthoringReport report);
            GameObject aircraftPrefab = PrefabUtility.SaveAsPrefabAsset(aircraftRig, AircraftPrefabPath);
            if (aircraftPrefab == null) throw new InvalidOperationException("Could not save " + AircraftPrefabPath);
            UnityEngine.Object.DestroyImmediate(aircraftRig);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene productionScene = BuildProductionScene(aircraftPrefab);
            if (!EditorSceneManager.SaveScene(productionScene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);

            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateSavedScene();

            Debug.Log(
                "[QuestFlightLab][ProductionAuthoring] Authored production scene rebuilt. " +
                $"interiorRenderers={report.interiorRendererCount} exteriorRenderers={report.exteriorRendererCount} " +
                $"animatedSurfaces={report.animatedSurfaceCount} yoke={report.pilotYokeAnimated} " +
                $"cockpitStaticPropellerOccluder={report.staticPropellerRendererName} " +
                $"propellerBounds={report.staticPropellerBounds} " +
                $"defaultEye={profile.nominalEyeLocalPosition} eyeToPanel={profile.EyeToPanelForwardDistanceMeters():0.000}m. " +
                "Asset limitations: rudder, pedals, engine controls, trim, steering and wheel animation are not safely separable by semantic node/pivot in the source GLB.");
        }

        private static PilotSeatProfile CreateOrUpdateSeatProfile()
        {
            PilotSeatProfile profile = AssetDatabase.LoadAssetAtPath<PilotSeatProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PilotSeatProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.aircraftId = CockpitViewpointPersistence.DefaultAircraftId;
            profile.seatAnchorLocalPosition = new Vector3(-0.28f, 0.72f, 0f);
            // Legacy 0.10 m aft measured 0.417 m eye-to-panel and still required
            // a physical step. The production profile adds 0.08 m, yielding the
            // geometry-derived 0.497 m target without changing legacy behavior.
            profile.nominalEyeLocalPosition = new Vector3(-0.28f, 0.94f, -0.18f);
            profile.minimumCalibrationOffset = new Vector3(-0.25f, -0.30f, -0.35f);
            profile.maximumCalibrationOffset = new Vector3(0.25f, 0.30f, 0.35f);
            profile.maximumCalibrationYawDegrees = 15f;
            profile.instrumentPanelReferencePoint = new Vector3(-0.28f, 0.94f, 0.317f);
            profile.instrumentPanelReferenceNormal = Vector3.back;
            profile.glareShieldReferencePoint = new Vector3(-0.28f, 0.98f, 0.30f);
            profile.windshieldHorizonReferencePoint = new Vector3(-0.28f, 1.12f, 0.72f);
            profile.minimumOutsideViewRatio = 0.20f;
            profile.targetEyeToPanelDistanceMeters = 0.497f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material CreateOrUpdatePanelMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PanelMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                if (shader == null) throw new InvalidOperationException("No built-in panel shader is available.");
                material = new Material(shader) { name = "Production Seat Calibration Panel" };
                AssetDatabase.CreateAsset(material, PanelMaterialPath);
            }
            material.color = new Color(0.012f, 0.018f, 0.022f, 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildAircraftRig(
            PilotSeatProfile profile,
            Material panelMaterial,
            out AircraftAuthoringReport report)
        {
            report = new AircraftAuthoringReport();
            GameObject simulation = new GameObject("AircraftSimulationRoot");
            Transform cg = Child(simulation.transform, "CenterOfGravityReference");
            Transform visual = Child(simulation.transform, "AircraftVisualRoot");
            Transform exterior = Child(visual, "ImportedAircraftExterior");
            Transform interior = Child(visual, "ImportedCockpitInterior");
            Transform animated = Child(visual, "AnimatedControlSurfaces");

            AircraftState aircraftState = simulation.AddComponent<AircraftState>();
            Rigidbody authoritativeBody = simulation.AddComponent<Rigidbody>();
            authoritativeBody.isKinematic = true;
            authoritativeBody.useGravity = false;
            SimpleAircraftPhysics unityPrototype = simulation.AddComponent<SimpleAircraftPhysics>();
            unityPrototype.state = aircraftState;
            unityPrototype.config = AssetDatabase.LoadAssetAtPath<C172StyleAircraftConfig>("Assets/Resources/C172StyleAircraftConfig.asset");
            FlightDynamicsInitialConditionProvider initialConditions = simulation.AddComponent<FlightDynamicsInitialConditionProvider>();
            initialConditions.derivePositionFromSpawnTransform = true;
            initialConditions.deriveHeadingFromSpawnTransform = true;
            initialConditions.spawnTransform = simulation.transform;
            initialConditions.spawnHeightAboveTerrainMeters = 1.25f;
            initialConditions.engineRunning = true;
            FlightDynamicsCoordinator coordinator = simulation.AddComponent<FlightDynamicsCoordinator>();
            coordinator.requestedBackend = FlightDynamicsBackendKind.UnityPrototype;
            coordinator.allowUnityFallback = true;
            coordinator.simulationRoot = simulation.transform;
            coordinator.presentationRoot = simulation.transform;
            coordinator.aircraftState = aircraftState;
            coordinator.unityPrototype = unityPrototype;
            coordinator.authoritativeRigidbody = authoritativeBody;
            coordinator.initialConditionProvider = initialConditions;

            GameObject importedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedC172Path);
            if (importedAsset == null) throw new InvalidOperationException("Imported C172 is missing at " + ImportedC172Path);
            GameObject imported = PrefabUtility.InstantiatePrefab(importedAsset) as GameObject;
            if (imported == null) throw new InvalidOperationException("Imported C172 could not be instantiated.");
            imported.name = "ImportedC172Source";
            imported.transform.SetParent(visual, false);
            imported.transform.localScale = Vector3.one;
            imported.transform.localRotation = ImportedModelRotation;
            imported.transform.localPosition = profile.seatAnchorLocalPosition - ImportedModelRotation * ImportedCockpitModelEye;
            PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform aircraftNode = FindDescendant(imported.transform, "Cessna-172");
            if (aircraftNode == null) throw new InvalidOperationException("Imported C172 hierarchy has no Cessna-172 node.");
            List<Transform> authoredModelChildren = DirectChildren(aircraftNode);
            Transform exteriorSource = authoredModelChildren.FirstOrDefault(child => child.name == "Cessna_Exterior");
            if (exteriorSource == null) throw new InvalidOperationException("Imported C172 has no Cessna_Exterior hierarchy.");
            exteriorSource.SetParent(exterior, true);
            foreach (Transform child in authoredModelChildren)
            {
                if (child != exteriorSource) child.SetParent(interior, true);
            }
            UnityEngine.Object.DestroyImmediate(imported);

            foreach (Collider collider in simulation.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            List<ProductionAircraftControlAnimator.Binding> bindings = new List<ProductionAircraftControlAnimator.Binding>();
            List<Renderer> animatedExteriorRenderers = new List<Renderer>();
            report.animatedSurfaceCount += AddSurface(
                exterior, animated, visual, "Cessna_Exterior_Body_MAT_0.014", "LeftAileronPivot",
                ProductionAircraftControlAnimator.ControlChannel.Aileron, -20f, 20f, bindings, animatedExteriorRenderers);
            report.animatedSurfaceCount += AddSurface(
                exterior, animated, visual, "Cessna_Exterior_Body_MAT_0.015", "RightAileronPivot",
                ProductionAircraftControlAnimator.ControlChannel.Aileron, 20f, -20f, bindings, animatedExteriorRenderers);
            report.animatedSurfaceCount += AddSurface(
                exterior, animated, visual, "Cessna_Exterior_Body_MAT_0.016", "LeftFlapPivot",
                ProductionAircraftControlAnimator.ControlChannel.Flaps, 0f, -35f, bindings, animatedExteriorRenderers);
            report.animatedSurfaceCount += AddSurface(
                exterior, animated, visual, "Cessna_Exterior_Body_MAT_0.017", "RightFlapPivot",
                ProductionAircraftControlAnimator.ControlChannel.Flaps, 0f, -35f, bindings, animatedExteriorRenderers);
            report.animatedSurfaceCount += AddSurface(
                exterior, animated, visual, "Cessna_Exterior_Body_MAT_0.013", "LeftElevatorPivot",
                ProductionAircraftControlAnimator.ControlChannel.Elevator, -24f, 24f, bindings, animatedExteriorRenderers);
            report.animatedSurfaceCount += AddSurface(
                exterior, animated, visual, "Cessna_Exterior_Body_MAT_0.012", "RightElevatorPivot",
                ProductionAircraftControlAnimator.ControlChannel.Elevator, -24f, 24f, bindings, animatedExteriorRenderers);

            List<Renderer> animatedInteriorRenderers = new List<Renderer>();
            report.pilotYokeAnimated = AddPilotYoke(
                interior, animated, visual, bindings, animatedInteriorRenderers);

            ProductionAircraftControlAnimator animator = animated.gameObject.AddComponent<ProductionAircraftControlAnimator>();
            animator.ConfigureAuthoredBindings(coordinator, null, bindings.ToArray());

            List<Renderer> interiorRenderers = interior.GetComponentsInChildren<Renderer>(true).ToList();
            interiorRenderers.AddRange(animatedInteriorRenderers);
            List<Renderer> exteriorRenderers = exterior.GetComponentsInChildren<Renderer>(true).ToList();
            exteriorRenderers.AddRange(animatedExteriorRenderers);
            Transform staticPropeller = FindDescendant(exterior, StaticPropellerNodeName);
            Renderer[] staticPropellerRenderers = RendererSet(staticPropeller);
            if (staticPropellerRenderers.Length != 1 || staticPropellerRenderers[0].name != StaticPropellerRendererName)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one authored static propeller renderer named {StaticPropellerRendererName}; " +
                    $"found {staticPropellerRenderers.Length}.");
            }
            if (!TryLocalBounds(staticPropeller, visual, out Bounds staticPropellerBounds))
                throw new InvalidOperationException("The authored static propeller renderer has no measurable bounds.");
            report.staticPropellerRendererName = staticPropellerRenderers[0].name;
            report.staticPropellerBounds = staticPropellerBounds;
            Renderer[] cockpitOccluders = RendererSet(
                FindDescendant(exterior, "Cessna_Exterior_Body_MAT_0"),
                FindDescendant(exterior, "Cessna_Exterior_Glass_MAT_0"),
                staticPropeller);

            AircraftVisibilityProfileController visibility = visual.gameObject.AddComponent<AircraftVisibilityProfileController>();
            visibility.ConfigureAuthoredRenderers(
                interiorRenderers.Where(renderer => renderer != null).Distinct().ToArray(),
                exteriorRenderers.Where(renderer => renderer != null).Distinct().ToArray(),
                cockpitOccluders);
            ProductionAircraftLightingController lighting = visual.gameObject.AddComponent<ProductionAircraftLightingController>();
            lighting.ConfigureAuthoredRoot(visual.gameObject);

            Transform pilotSeat = Child(visual, "PilotSeatAnchor");
            pilotSeat.localPosition = profile.nominalEyeLocalPosition;
            Transform calibration = Child(pilotSeat, "UserViewCalibrationOffset");
            Transform xrOriginTransform = Child(calibration, "XR Origin");
            XROrigin xrOrigin = xrOriginTransform.gameObject.AddComponent<XROrigin>();
            Transform cameraOffset = Child(xrOriginTransform, "Camera Offset");
            GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(cameraOffset, false);
            Camera cockpitCamera = cameraObject.AddComponent<Camera>();
            cockpitCamera.nearClipPlane = 0.03f;
            cockpitCamera.farClipPlane = 24000f;
            cameraObject.AddComponent<AudioListener>();
            TrackedXrCameraPoseDriver.Ensure(cockpitCamera);
            Transform leftController = TrackedXrControllerPoseDrivers.EnsureLeft(xrOriginTransform);
            Transform rightController = TrackedXrControllerPoseDrivers.EnsureRight(xrOriginTransform);
            xrOrigin.Camera = cockpitCamera;
            xrOrigin.CameraFloorOffsetObject = cameraOffset.gameObject;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            xrOrigin.CameraYOffset = 0f;

            AircraftReferenceFrameRig referenceRig = simulation.AddComponent<AircraftReferenceFrameRig>();
            referenceRig.ConfigureAuthoredHierarchy(
                cg, visual, pilotSeat, calibration, xrOriginTransform,
                leftController, rightController, cockpitCamera);
            if (!referenceRig.ValidateHierarchy())
                throw new InvalidOperationException("Authored aircraft/XR hierarchy failed validation before prefab save.");

            CreateSeatCalibrationPanel(calibration, panelMaterial, out GameObject panelRoot, out TextMesh panelText);
            ProductionSeatCalibrationController calibrationController = pilotSeat.gameObject.AddComponent<ProductionSeatCalibrationController>();
            calibrationController.ConfigureAuthoredReferences(referenceRig, profile, panelRoot, panelText);

            Transform externalAnchor = Child(visual, "ExternalViewAnchor");
            externalAnchor.localPosition = new Vector3(7.5f, 3.0f, -10f);
            externalAnchor.localRotation = Quaternion.LookRotation(-externalAnchor.localPosition + new Vector3(0f, 0.5f, 0f));
            Camera externalCamera = externalAnchor.gameObject.AddComponent<Camera>();
            externalCamera.nearClipPlane = 0.1f;
            externalCamera.farClipPlane = 24000f;
            externalCamera.enabled = false;
            ProductionAircraftViewController viewController = visual.gameObject.AddComponent<ProductionAircraftViewController>();
            viewController.ConfigureAuthoredReferences(cockpitCamera, externalCamera, visibility);

            report.interiorRendererCount = interiorRenderers.Count;
            report.exteriorRendererCount = exteriorRenderers.Count;
            return simulation;
        }

        private static Scene BuildProductionScene(GameObject aircraftPrefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject world = new GameObject("ProductionWorldRoot");
            GameObject marker = new GameObject("ProductionVerticalSliceRoot");
            marker.transform.SetParent(world.transform, false);
            ProductionVerticalSliceRoot markerComponent = marker.AddComponent<ProductionVerticalSliceRoot>();

            Transform environmentAnchor = Child(world.transform, "EnvironmentRoot");
            Transform runwayAnchor = Child(world.transform, "RunwayRoot");
            ProductionEnvironmentRoot environmentContract = null;
            GameObject environmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
            if (environmentPrefab != null)
            {
                GameObject environment = PrefabUtility.InstantiatePrefab(environmentPrefab) as GameObject;
                environment.transform.SetParent(environmentAnchor, false);
                environmentContract = environment.GetComponent<ProductionEnvironmentRoot>();
            }
            else
            {
                Debug.LogWarning("[QuestFlightLab][ProductionAuthoring] Environment prefab is not present yet; EnvironmentRoot remains an authored integration anchor.");
            }

            GameObject inputRoot = new GameObject("ProductionInputRoot");
            inputRoot.transform.SetParent(world.transform, false);
            GamepadInputReader reader = inputRoot.AddComponent<GamepadInputReader>();
            DeterministicGamepadInputSource deterministicInput = inputRoot.AddComponent<DeterministicGamepadInputSource>();
            Usb2BleInputMapper mapper = inputRoot.AddComponent<Usb2BleInputMapper>();
            mapper.reader = reader;
            mapper.deterministicInput = deterministicInput;
            mapper.preferDeterministicInput = true;
            inputRoot.AddComponent<ShortPlaytestDemoPilot>();

            GameObject aircraft = PrefabUtility.InstantiatePrefab(aircraftPrefab) as GameObject;
            if (aircraft == null) throw new InvalidOperationException("Production aircraft prefab could not be instantiated.");
            aircraft.name = "AircraftSimulationRoot";
            aircraft.transform.SetParent(world.transform, false);
            if (environmentContract != null)
            {
                Vector3 runwayDirectionLocal = (environmentContract.runway26EndLocal - environmentContract.runway08EndLocal).normalized;
                Vector3 runwaySpawnLocal = Vector3.Lerp(
                    environmentContract.runway08EndLocal,
                    environmentContract.runway26EndLocal,
                    0.06f) + Vector3.up * 1.25f;
                aircraft.transform.position = environmentContract.transform.TransformPoint(runwaySpawnLocal);
                aircraft.transform.rotation = Quaternion.LookRotation(
                    environmentContract.transform.TransformDirection(runwayDirectionLocal),
                    Vector3.up);
            }
            ProductionAircraftControlAnimator animator = aircraft.GetComponentInChildren<ProductionAircraftControlAnimator>(true);
            FlightDynamicsCoordinator coordinator = aircraft.GetComponent<FlightDynamicsCoordinator>();
            FlightDynamicsInitialConditionProvider initialConditions = aircraft.GetComponent<FlightDynamicsInitialConditionProvider>();
            SimpleAircraftPhysics unityPrototype = aircraft.GetComponent<SimpleAircraftPhysics>();
            AircraftState aircraftState = aircraft.GetComponent<AircraftState>();
            coordinator.controls = mapper;
            coordinator.simulationRoot = aircraft.transform;
            coordinator.presentationRoot = aircraft.transform;
            coordinator.unityPrototype = unityPrototype;
            coordinator.aircraftState = aircraftState;
            coordinator.authoritativeRigidbody = aircraft.GetComponent<Rigidbody>();
            coordinator.initialConditionProvider = initialConditions;
            initialConditions.spawnTransform = aircraft.transform;
            initialConditions.derivePositionFromSpawnTransform = true;
            initialConditions.deriveHeadingFromSpawnTransform = true;
            initialConditions.spawnHeightAboveTerrainMeters = 1.25f;
            GeodeticReference spawnGeodetic = FlightFrameConversions.UnityToGeodetic(
                aircraft.transform.position,
                GeodeticReference.Kbdu);
            initialConditions.latitudeDegrees = spawnGeodetic.latitudeDegrees;
            initialConditions.longitudeDegrees = spawnGeodetic.longitudeDegrees;
            initialConditions.altitudeMslMeters = spawnGeodetic.altitudeMslMeters;
            initialConditions.terrainElevationMslMeters = spawnGeodetic.altitudeMslMeters - initialConditions.spawnHeightAboveTerrainMeters;
            Vector3 runwayForward = Vector3.ProjectOnPlane(aircraft.transform.forward, Vector3.up).normalized;
            initialConditions.headingDegrees = Mathf.Repeat(
                Mathf.Atan2(runwayForward.x, runwayForward.z) * Mathf.Rad2Deg,
                360f);
            coordinator.RefreshConfiguredInitialConditions();
            unityPrototype.controls = mapper;
            unityPrototype.state = aircraftState;
            unityPrototype.runwayResetPosition = aircraft.transform.position;
            unityPrototype.runwayResetEuler = aircraft.transform.eulerAngles;
            unityPrototype.groundHeightMeters = aircraft.transform.position.y;
            animator.ConfigureControlSources(coordinator, mapper);
            FlightTelemetry telemetry = inputRoot.AddComponent<FlightTelemetry>();
            telemetry.aircraftState = aircraftState;

            markerComponent.ConfigureAuthoredReferences(
                aircraft.GetComponent<AircraftReferenceFrameRig>(),
                AssetDatabase.LoadAssetAtPath<PilotSeatProfile>(ProfilePath),
                aircraft.GetComponentInChildren<ProductionSeatCalibrationController>(true),
                aircraft.GetComponentInChildren<AircraftVisibilityProfileController>(true),
                animator,
                coordinator,
                environmentAnchor,
                runwayAnchor);

            CreateProductionLighting(world.transform);

            GameObject worldPrefab = PrefabUtility.SaveAsPrefabAsset(world, WorldPrefabPath);
            if (worldPrefab == null) throw new InvalidOperationException("Could not save " + WorldPrefabPath);
            UnityEngine.Object.DestroyImmediate(world);
            GameObject worldInstance = PrefabUtility.InstantiatePrefab(worldPrefab) as GameObject;
            if (worldInstance == null) throw new InvalidOperationException("Production world prefab could not be instantiated.");
            worldInstance.name = "ProductionWorldRoot";

            return scene;
        }

        private static void CreateProductionLighting(Transform world)
        {
            Transform lightingRoot = Child(world, "ProductionLightingRoot");
            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(lightingRoot, false);
            sunObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.None;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.58f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.15f, 0.12f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.61f, 0.68f, 0.76f);
            RenderSettings.fogStartDistance = 6500f;
            RenderSettings.fogEndDistance = 22000f;
        }

        private static int AddSurface(
            Transform exterior,
            Transform animated,
            Transform aircraftFrame,
            string sourceName,
            string pivotName,
            ProductionAircraftControlAnimator.ControlChannel channel,
            float minimumAngle,
            float maximumAngle,
            ICollection<ProductionAircraftControlAnimator.Binding> bindings,
            ICollection<Renderer> animatedRenderers)
        {
            Transform source = FindDescendant(exterior, sourceName);
            if (source == null) return 0;
            if (!TryLocalBounds(source, aircraftFrame, out Bounds bounds)) return 0;

            GameObject pivotObject = new GameObject(pivotName);
            pivotObject.transform.SetParent(animated, false);
            pivotObject.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
            source.SetParent(pivotObject.transform, true);
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true)) animatedRenderers.Add(renderer);
            bindings.Add(new ProductionAircraftControlAnimator.Binding
            {
                label = pivotName,
                target = pivotObject.transform,
                channel = channel,
                localRotationAxis = Vector3.right,
                minimumAngleDegrees = minimumAngle,
                maximumAngleDegrees = maximumAngle
            });
            return 1;
        }

        private static bool AddPilotYoke(
            Transform interior,
            Transform animated,
            Transform aircraftFrame,
            ICollection<ProductionAircraftControlAnimator.Binding> bindings,
            ICollection<Renderer> animatedRenderers)
        {
            Transform first = FindDescendant(interior, "Cessna_Interior_Steering_MAT_0");
            Transform second = FindDescendant(interior, "Cessna_Interior_Steering_MAT_0.001");
            Transform source = ClosestBoundsCenterX(first, second, aircraftFrame, -0.28f);
            if (source == null || !TryLocalBounds(source, aircraftFrame, out Bounds bounds)) return false;

            GameObject pitchPivot = new GameObject("PilotYokePitchPivot");
            pitchPivot.transform.SetParent(animated, false);
            pitchPivot.transform.localPosition = bounds.center;
            GameObject rollPivot = new GameObject("PilotYokeRollPivot");
            rollPivot.transform.SetParent(pitchPivot.transform, false);
            source.SetParent(rollPivot.transform, true);
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true)) animatedRenderers.Add(renderer);

            bindings.Add(new ProductionAircraftControlAnimator.Binding
            {
                label = "Pilot yoke roll",
                target = rollPivot.transform,
                channel = ProductionAircraftControlAnimator.ControlChannel.YokeRoll,
                localRotationAxis = Vector3.forward,
                minimumAngleDegrees = 55f,
                maximumAngleDegrees = -55f
            });
            bindings.Add(new ProductionAircraftControlAnimator.Binding
            {
                label = "Pilot yoke pitch travel",
                target = pitchPivot.transform,
                channel = ProductionAircraftControlAnimator.ControlChannel.YokePitch,
                localRotationAxis = Vector3.right,
                localTranslationAxis = Vector3.forward,
                minimumTranslationMeters = -0.04f,
                maximumTranslationMeters = 0.04f
            });
            return true;
        }

        private static Transform ClosestBoundsCenterX(
            Transform first,
            Transform second,
            Transform aircraftFrame,
            float targetX)
        {
            bool firstValid = TryLocalBounds(first, aircraftFrame, out Bounds firstBounds);
            bool secondValid = TryLocalBounds(second, aircraftFrame, out Bounds secondBounds);
            if (!firstValid) return secondValid ? second : null;
            if (!secondValid) return first;
            return Mathf.Abs(firstBounds.center.x - targetX) <= Mathf.Abs(secondBounds.center.x - targetX) ? first : second;
        }

        private static bool TryLocalBounds(Transform root, Transform frame, out Bounds bounds)
        {
            bounds = default;
            if (root == null || frame == null) return false;
            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 worldCorner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                    Vector3 local = frame.InverseTransformPoint(worldCorner);
                    if (!hasBounds) { bounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return hasBounds;
        }

        private static Renderer[] RendererSet(params Transform[] roots)
        {
            return roots.Where(root => root != null)
                .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer != null)
                .Distinct()
                .ToArray();
        }

        private static void CreateSeatCalibrationPanel(
            Transform calibration,
            Material panelMaterial,
            out GameObject root,
            out TextMesh text)
        {
            root = new GameObject("SeatCalibrationTouchPanel");
            root.transform.SetParent(calibration, false);
            root.transform.localPosition = new Vector3(0.48f, -0.08f, 1.05f);

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "PanelBackground";
            background.transform.SetParent(root.transform, false);
            background.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            background.transform.localScale = new Vector3(1.15f, 0.58f, 0.025f);
            background.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>());

            GameObject textObject = new GameObject("PanelText");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(-0.53f, 0.25f, -0.02f);
            text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.UpperLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 34;
            text.characterSize = 0.0085f;
            text.lineSpacing = 0.86f;
            text.color = new Color(0.82f, 1f, 0.88f, 1f);
            text.richText = false;
            text.text = "SEAT / VIEW (Touch only)";
            root.SetActive(false);
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != ScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateSavedScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform world = scene.GetRootGameObjects().Select(root => root.transform)
                .FirstOrDefault(root => root.name == "ProductionWorldRoot");
            if (world == null) throw new InvalidOperationException("ProductionWorldRoot is missing.");
            string[] requiredPaths =
            {
                "ProductionVerticalSliceRoot",
                "EnvironmentRoot",
                "RunwayRoot",
                "AircraftSimulationRoot/CenterOfGravityReference",
                "AircraftSimulationRoot/AircraftVisualRoot/ImportedAircraftExterior",
                "AircraftSimulationRoot/AircraftVisualRoot/ImportedCockpitInterior",
                "AircraftSimulationRoot/AircraftVisualRoot/AnimatedControlSurfaces",
                "AircraftSimulationRoot/AircraftVisualRoot/PilotSeatAnchor/UserViewCalibrationOffset/XR Origin/Camera Offset/Main Camera",
                "AircraftSimulationRoot/AircraftVisualRoot/PilotSeatAnchor/UserViewCalibrationOffset/XR Origin/Left Touch Controller",
                "AircraftSimulationRoot/AircraftVisualRoot/PilotSeatAnchor/UserViewCalibrationOffset/XR Origin/Right Touch Controller"
            };
            foreach (string path in requiredPaths)
                if (world.Find(path) == null) throw new InvalidOperationException("Missing authored scene path: " + path);

            XROrigin[] origins = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (origins.Length != 1 || !origins[0].isActiveAndEnabled)
                throw new InvalidOperationException($"Expected exactly one enabled XR Origin, found {origins.Length}.");
            Camera main = GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>();
            if (main == null || !TrackedXrCameraPoseDriver.HasRequiredBindings(main))
                throw new InvalidOperationException("Main Camera is not owned by the authored TrackedPoseDriver bindings.");
            AircraftReferenceFrameRig rig = UnityEngine.Object.FindFirstObjectByType<AircraftReferenceFrameRig>(FindObjectsInactive.Include);
            if (rig == null || !rig.ValidateHierarchy())
                throw new InvalidOperationException("Saved AircraftReferenceFrameRig hierarchy is invalid.");
            if (UnityEngine.Object.FindFirstObjectByType<QuestFirstViewRuntimeRepair>(FindObjectsInactive.Include) != null)
                throw new InvalidOperationException("Production scene contains legacy QuestFirstViewRuntimeRepair.");
            if (!ProductionVerticalSliceRoot.IsProductionSceneLoaded())
                throw new InvalidOperationException("Production scene ownership marker is not active.");
            EditorSceneManager.SaveScene(scene);
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendant(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private static List<Transform> DirectChildren(Transform parent)
        {
            List<Transform> children = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++) children.Add(parent.GetChild(i));
            return children;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Invalid asset folder " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class AircraftAuthoringReport
        {
            public int interiorRendererCount;
            public int exteriorRendererCount;
            public int animatedSurfaceCount;
            public bool pilotYokeAnimated;
            public string staticPropellerRendererName;
            public Bounds staticPropellerBounds;
        }
    }
}
