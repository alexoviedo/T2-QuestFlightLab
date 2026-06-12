using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using QuestFlightLab.Aircraft;
using QuestFlightLab.Environment;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuestFlightLab.Editor
{
    public static class QuestProjectBootstrap
    {
        private const string ScenePath = "Assets/Scenes/InputLab.unity";
        private const string ConfigPath = "Assets/Resources/C172StyleAircraftConfig.asset";

        [MenuItem("Quest Flight Lab/Configure Project")]
        public static void ConfigureProject()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.companyName = "Alex Oviedo / T2";
            PlayerSettings.productName = "Quest Flight Input Lab";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.alexoviedo.t2.questflightlab");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = true;

            SetActiveInputHandling();
            EnableOpenXRLoaderAndroid();
            EnableOpenXRFeaturesAndroid();

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
            Time.fixedDeltaTime = 1f / 72f;

            EnsureConfigAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QuestFlightLab] Project configured for Quest Android/OpenXR/Input System.");
        }

        [MenuItem("Quest Flight Lab/Create Input Lab Scene")]
        public static void CreateInputLabScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Resources");

            C172StyleAircraftConfig config = EnsureConfigAsset();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "InputLab";

            Material aircraftMat = NewMaterial("Trainer White", new Color(0.86f, 0.88f, 0.86f));
            Material blueMat = NewMaterial("Trainer Blue", new Color(0.05f, 0.18f, 0.55f));
            Material glassMat = NewMaterial("Canopy", new Color(0.16f, 0.28f, 0.34f, 0.8f));
            Material darkMat = NewMaterial("Panel Dark", new Color(0.025f, 0.028f, 0.03f));
            Material greenTextMat = NewMaterial("HUD Green", new Color(0.25f, 1f, 0.55f));

            CreateLighting();
            Transform cameraTransform = CreateCameraRig();

            GameObject systems = new GameObject("Runtime Systems");
            GamepadInputReader reader = systems.AddComponent<GamepadInputReader>();
            Usb2BleInputMapper mapper = systems.AddComponent<Usb2BleInputMapper>();
            mapper.reader = reader;
            mapper.invertElevator = true;
            mapper.defaultThrottle = 0.72f;

            KbduApproxAirport.Build(null);

            GameObject aircraft = CreateTrainerAircraft(aircraftMat, blueMat, glassMat, out ControlSurfaceAnimator animator);
            AircraftState aircraftState = aircraft.AddComponent<AircraftState>();
            aircraftState.config = config;
            SimpleAircraftPhysics physics = aircraft.AddComponent<SimpleAircraftPhysics>();
            physics.controls = mapper;
            physics.state = aircraftState;
            physics.config = config;
            FlightTelemetry flightTelemetry = aircraft.AddComponent<FlightTelemetry>();
            flightTelemetry.aircraftState = aircraftState;

            animator.controls = mapper;

            PerformanceHud performanceHud = CreatePerformanceHud(cameraTransform, greenTextMat);
            flightTelemetry.performanceHud = performanceHud;

            InputEvidenceLogger logger = systems.AddComponent<InputEvidenceLogger>();
            logger.reader = reader;
            logger.mapper = mapper;
            logger.flightTelemetry = flightTelemetry;

            TelemetryPanel telemetryPanel = CreateTelemetryPanel(cameraTransform, darkMat, greenTextMat);
            telemetryPanel.reader = reader;
            telemetryPanel.mapper = mapper;
            telemetryPanel.flightTelemetry = flightTelemetry;
            telemetryPanel.evidenceLogger = logger;

            TouchMenuPlaceholder menu = CreateTouchMenuPlaceholder(cameraTransform, darkMat, greenTextMat);
            menu.mapper = mapper;
            menu.aircraftPhysics = physics;

            GameObject splatStub = new GameObject("Gaussian Splat Spike Stub");
            splatStub.AddComponent<GaussianSplatSpikeStub>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[QuestFlightLab] Scene generated at {ScenePath}.");
        }

        [MenuItem("Quest Flight Lab/Validate Project")]
        public static void ValidateProject()
        {
            string[] requiredFiles =
            {
                "Assets/Scripts/Input/GamepadInputReader.cs",
                "Assets/Scripts/Input/Usb2BleInputMapper.cs",
                "Assets/Scripts/Input/InputEvidenceLogger.cs",
                "Assets/Scripts/Flight/SimpleAircraftPhysics.cs",
                "Assets/Scripts/Aircraft/ControlSurfaceAnimator.cs"
            };

            foreach (string file in requiredFiles)
            {
                if (!File.Exists(file)) throw new Exception($"Missing required file: {file}");
            }

            GamepadInputSnapshot snapshot = new GamepadInputSnapshot
            {
                connected = true,
                leftStickX = 0.5f,
                leftStickY = 1f,
                rightStickX = -0.25f,
                leftTrigger = 0.3f,
                rightTrigger = 0.8f
            };

            AircraftControlState mapped = Usb2BleInputMapper.MapSnapshotForTest(snapshot, invertElevator: true, throttle: 0.72f);
            AssertClose(mapped.aileron, 0.5f, "aileron");
            AssertClose(mapped.elevator, -1f, "elevator");
            AssertClose(mapped.rudder, -0.25f, "rudder");
            AssertClose(mapped.leftToeBrake, 0.3f, "left toe brake");
            AssertClose(mapped.rightToeBrake, 0.8f, "right toe brake");
            AssertClose(mapped.throttle, 0.72f, "throttle placeholder");

            if (!File.Exists(ScenePath)) throw new Exception($"Scene missing: {ScenePath}");
            Debug.Log("[QuestFlightLab] Validation passed.");
        }

        private static C172StyleAircraftConfig EnsureConfigAsset()
        {
            C172StyleAircraftConfig existing = AssetDatabase.LoadAssetAtPath<C172StyleAircraftConfig>(ConfigPath);
            if (existing != null) return existing;

            C172StyleAircraftConfig config = C172StyleAircraftConfig.CreateRuntimeDefault();
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "Assets/Resources");
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        private static Transform CreateCameraRig()
        {
            Type xrOriginType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            GameObject origin = new GameObject("XR Origin");
            Transform cameraParent = origin.transform;

            if (xrOriginType != null)
            {
                origin.AddComponent(xrOriginType);
                GameObject offset = new GameObject("Camera Offset");
                offset.transform.SetParent(origin.transform, false);
                offset.transform.localPosition = Vector3.zero;
                cameraParent = offset.transform;
            }

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(cameraParent, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.65f, -4.2f);
            cameraGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 4000f;
            cameraGo.AddComponent<AudioListener>();

            Type trackedPoseType = Type.GetType("UnityEngine.InputSystem.XR.TrackedPoseDriver, Unity.InputSystem");
            if (trackedPoseType != null) cameraGo.AddComponent(trackedPoseType);

            return cameraGo.transform;
        }

        private static GameObject CreateTrainerAircraft(Material aircraftMat, Material blueMat, Material glassMat, out ControlSurfaceAnimator animator)
        {
            GameObject root = new GameObject("C172_Style_Trainer_Prototype");
            root.transform.position = new Vector3(0f, 1.25f, -520f);
            root.transform.rotation = Quaternion.Euler(0f, 78f, 0f);

            Cube(root.transform, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(1.2f, 1f, 7.2f), aircraftMat);
            Cube(root.transform, "Nose", new Vector3(0f, -0.05f, 4.25f), new Vector3(0.9f, 0.75f, 1.4f), aircraftMat);
            Cube(root.transform, "CabinGlass", new Vector3(0f, 0.55f, 0.85f), new Vector3(1.05f, 0.45f, 1.55f), glassMat);
            Cube(root.transform, "LeftWing", new Vector3(-4.2f, 0.12f, 0.25f), new Vector3(7.4f, 0.16f, 1.35f), aircraftMat);
            Cube(root.transform, "RightWing", new Vector3(4.2f, 0.12f, 0.25f), new Vector3(7.4f, 0.16f, 1.35f), aircraftMat);
            Cube(root.transform, "TailBoom", new Vector3(0f, 0.08f, -3.95f), new Vector3(0.7f, 0.6f, 1.55f), aircraftMat);
            Cube(root.transform, "VerticalStab", new Vector3(0f, 1.05f, -3.7f), new Vector3(0.22f, 1.8f, 1.2f), aircraftMat);
            Cube(root.transform, "HorizontalStab", new Vector3(0f, 0.42f, -4.1f), new Vector3(3.8f, 0.12f, 0.9f), aircraftMat);

            GameObject leftAileron = Cube(root.transform, "LeftAileron", new Vector3(-5.55f, 0.05f, -0.48f), new Vector3(2.3f, 0.08f, 0.28f), blueMat);
            GameObject rightAileron = Cube(root.transform, "RightAileron", new Vector3(5.55f, 0.05f, -0.48f), new Vector3(2.3f, 0.08f, 0.28f), blueMat);
            GameObject leftFlap = Cube(root.transform, "LeftFlap", new Vector3(-2.45f, 0.04f, -0.52f), new Vector3(2.3f, 0.08f, 0.32f), blueMat);
            GameObject rightFlap = Cube(root.transform, "RightFlap", new Vector3(2.45f, 0.04f, -0.52f), new Vector3(2.3f, 0.08f, 0.32f), blueMat);
            GameObject elevator = Cube(root.transform, "Elevator", new Vector3(0f, 0.35f, -4.62f), new Vector3(3.2f, 0.1f, 0.3f), blueMat);
            GameObject rudder = Cube(root.transform, "Rudder", new Vector3(0f, 1.06f, -4.28f), new Vector3(0.18f, 1.2f, 0.34f), blueMat);

            GameObject cockpit = new GameObject("Control Indicators");
            cockpit.transform.SetParent(root.transform, false);
            cockpit.transform.localPosition = new Vector3(0f, 0.62f, 1.55f);
            GameObject yoke = Cube(cockpit.transform, "YokeIndicator", new Vector3(0f, -0.05f, 0.35f), new Vector3(0.65f, 0.08f, 0.08f), blueMat);
            GameObject pedals = Cube(cockpit.transform, "RudderPedalIndicator", new Vector3(0f, -0.52f, 0.65f), new Vector3(0.85f, 0.12f, 0.08f), blueMat);
            GameObject leftBrake = Cube(cockpit.transform, "LeftToeBrakeBar", new Vector3(-0.55f, -0.42f, 0.85f), new Vector3(0.08f, 0.05f, 0.08f), blueMat);
            GameObject rightBrake = Cube(cockpit.transform, "RightToeBrakeBar", new Vector3(0.55f, -0.42f, 0.85f), new Vector3(0.08f, 0.05f, 0.08f), blueMat);
            GameObject throttle = Cube(cockpit.transform, "ThrottlePlaceholder", new Vector3(0.75f, -0.12f, 0.35f), new Vector3(0.08f, 0.38f, 0.08f), blueMat);

            animator = root.AddComponent<ControlSurfaceAnimator>();
            animator.leftAileron = leftAileron.transform;
            animator.rightAileron = rightAileron.transform;
            animator.leftFlap = leftFlap.transform;
            animator.rightFlap = rightFlap.transform;
            animator.elevator = elevator.transform;
            animator.rudder = rudder.transform;
            animator.yoke = yoke.transform;
            animator.rudderPedals = pedals.transform;
            animator.leftBrakeBar = leftBrake.transform;
            animator.rightBrakeBar = rightBrake.transform;
            animator.throttleLever = throttle.transform;

            return root;
        }

        private static TelemetryPanel CreateTelemetryPanel(Transform cameraTransform, Material panelMat, Material textMat)
        {
            GameObject panel = new GameObject("Telemetry Panel");
            panel.transform.SetParent(cameraTransform, false);
            panel.transform.localPosition = new Vector3(-0.12f, -0.15f, 2.2f);
            panel.transform.localRotation = Quaternion.identity;

            Cube(panel.transform, "PanelBackground", new Vector3(0f, 0f, 0.04f), new Vector3(1.9f, 1.08f, 0.03f), panelMat);
            TextMesh text = CreateText(panel.transform, "TelemetryText", new Vector3(-0.9f, 0.48f, -0.02f), 30, 0.022f, textMat);
            TelemetryPanel telemetry = panel.AddComponent<TelemetryPanel>();
            telemetry.text = text;
            telemetry.panelRoot = panel;
            return telemetry;
        }

        private static PerformanceHud CreatePerformanceHud(Transform cameraTransform, Material textMat)
        {
            GameObject hud = new GameObject("Performance HUD");
            hud.transform.SetParent(cameraTransform, false);
            hud.transform.localPosition = new Vector3(0.72f, 0.53f, 2.05f);
            TextMesh text = CreateText(hud.transform, "FpsText", Vector3.zero, 36, 0.03f, textMat);
            PerformanceHud perf = hud.AddComponent<PerformanceHud>();
            perf.text = text;
            return perf;
        }

        private static TouchMenuPlaceholder CreateTouchMenuPlaceholder(Transform cameraTransform, Material panelMat, Material textMat)
        {
            GameObject menu = new GameObject("Touch Controller Menu Placeholder");
            menu.transform.SetParent(cameraTransform, false);
            menu.transform.localPosition = new Vector3(1.15f, -0.08f, 2.35f);
            Cube(menu.transform, "MenuBackground", new Vector3(0f, 0f, 0.04f), new Vector3(0.8f, 0.72f, 0.03f), panelMat);
            TextMesh text = CreateText(menu.transform, "MenuText", new Vector3(-0.36f, 0.31f, -0.02f), 24, 0.02f, textMat);
            TouchMenuPlaceholder placeholder = menu.AddComponent<TouchMenuPlaceholder>();
            placeholder.text = text;
            return placeholder;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        private static TextMesh CreateText(Transform parent, string name, Vector3 localPosition, int fontSize, float characterSize, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            TextMesh text = go.AddComponent<TextMesh>();
            text.anchor = TextAnchor.UpperLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.lineSpacing = 0.9f;
            text.color = material.color;
            return text;
        }

        private static Material NewMaterial(string name, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }

        private static void AssertClose(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) > 0.001f)
            {
                throw new Exception($"{label} expected {expected} but got {actual}");
            }
        }

        private static void SetActiveInputHandling()
        {
            try
            {
                Type inputHandlingType = typeof(PlayerSettings).GetNestedType("ActiveInputHandling", BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo method = typeof(PlayerSettings).GetMethod("SetActiveInputHandling", BindingFlags.Public | BindingFlags.Static);
                if (inputHandlingType == null || method == null)
                {
                    Debug.LogWarning("[QuestFlightLab] Could not find PlayerSettings active input handling API.");
                    return;
                }

                object value = Enum.Parse(inputHandlingType, "InputSystemPackage");
                method.Invoke(null, new[] { value });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuestFlightLab] Could not set active input handling: {ex.Message}");
            }
        }

        private static void EnableOpenXRLoaderAndroid()
        {
            Type settingsPerBuildTargetType = Type.GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
            Type metadataStoreType = Type.GetType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore, Unity.XR.Management.Editor");
            Type generalSettingsType = Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management")
                                       ?? Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management.Runtime");
            Type managerSettingsType = Type.GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management")
                                       ?? Type.GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management.Runtime");

            if (settingsPerBuildTargetType == null || metadataStoreType == null || generalSettingsType == null || managerSettingsType == null)
            {
                Debug.LogWarning("[QuestFlightLab] XR Management types unavailable before package restore.");
                return;
            }

            object xrSettings = settingsPerBuildTargetType
                .GetMethod("XRGeneralSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { BuildTargetGroup.Android });

            if (xrSettings == null)
            {
                xrSettings = ScriptableObject.CreateInstance(generalSettingsType);
                settingsPerBuildTargetType
                    .GetMethod("SetSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new[] { (object)BuildTargetGroup.Android, xrSettings });
            }

            PropertyInfo assignedSettingsProp = xrSettings.GetType().GetProperty("AssignedSettings")
                                                ?? xrSettings.GetType().GetProperty("Manager");
            object manager = assignedSettingsProp?.GetValue(xrSettings);
            if (manager == null)
            {
                manager = ScriptableObject.CreateInstance(managerSettingsType);
                assignedSettingsProp?.SetValue(xrSettings, manager);
            }

            MethodInfo assignLoader = metadataStoreType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
            if (assignLoader == null) return;

            ParameterInfo[] parameters = assignLoader.GetParameters();
            object result = parameters.Length == 3
                ? assignLoader.Invoke(null, new object[] { manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android })
                : assignLoader.Invoke(null, new object[] { manager, "UnityEngine.XR.OpenXR.OpenXRLoader", "", BuildTargetGroup.Android });

            Debug.Log($"[QuestFlightLab] OpenXR loader assigned for Android: {result}");
        }

        private static void EnableOpenXRFeaturesAndroid()
        {
            Type settingsType = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR")
                                ?? Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, UnityEngine.XR.OpenXR");
            if (settingsType == null)
            {
                Debug.LogWarning("[QuestFlightLab] OpenXR settings unavailable before package restore.");
                return;
            }

            MethodInfo getSettings = settingsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetSettingsForBuildTargetGroup" && m.GetParameters().Length == 1);
            object settings = getSettings?.Invoke(null, new object[] { BuildTargetGroup.Android });
            if (settings == null) return;

            IEnumerable features = null;
            MethodInfo getFeatures = settings.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetFeatures" && !m.IsGenericMethod && m.GetParameters().Length == 0);
            if (getFeatures != null)
            {
                features = getFeatures.Invoke(settings, null) as IEnumerable;
            }

            if (features == null) return;

            foreach (object feature in features)
            {
                if (feature == null) continue;
                string fullName = feature.GetType().FullName ?? feature.GetType().Name;
                PropertyInfo enabled = feature.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
                if (enabled == null || !enabled.CanWrite) continue;

                bool shouldEnable = fullName.IndexOf("OculusTouchControllerProfile", StringComparison.OrdinalIgnoreCase) >= 0
                                    || fullName.IndexOf("MetaQuest", StringComparison.OrdinalIgnoreCase) >= 0
                                    || fullName.IndexOf("HandInteraction", StringComparison.OrdinalIgnoreCase) >= 0;

                if (shouldEnable)
                {
                    enabled.SetValue(feature, true);
                    Debug.Log($"[QuestFlightLab] Enabled OpenXR feature: {fullName}");
                }
            }
        }
    }
}
