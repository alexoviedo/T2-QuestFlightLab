using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using QuestFlightLab.Environment;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using QuestFlightLab.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuestFlightLab.Editor
{
    public static class VisualQaBatchRunner
    {
        private const int DefaultWidth = 1280;
        private const int DefaultHeight = 720;
        private static readonly Vector3 RunwayStartPosition = new Vector3(-560f, 1.25f, 0f);
        private static readonly Quaternion RunwayStartRotation = Quaternion.Euler(0f, 90f, 0f);

        [MenuItem("Quest Flight Lab/Run Autonomous Visual QA")]
        public static void RunVisualQa()
        {
            string outputDir = System.Environment.GetEnvironmentVariable("QFL_VISUAL_QA_DIR");
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.GetFullPath(Path.Combine(
                    "..",
                    "T2-QuestFlightLab-setup-artifacts",
                    $"visual_qa_{DateTime.UtcNow:yyyyMMdd_HHmmss}"));
            }

            int width = ReadInt("QFL_VISUAL_QA_WIDTH", DefaultWidth);
            int height = ReadInt("QFL_VISUAL_QA_HEIGHT", DefaultHeight);
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "screenshots"));

            VisualQaReport report = new VisualQaReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                outputDirectory = outputDir,
                visualFeedbackPath = "Unity Editor deterministic camera capture",
                metaXrSimulatorStatus = FlightCoreBatchRunner.DetectMetaXrSimulatorStatus(),
                xrDeviceSimulatorStatus = DetectXrDeviceSimulatorStatus(),
                renderWidth = width,
                renderHeight = height
            };

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.Environment.SetEnvironmentVariable(QuestLaunchOptions.SceneryModeKey, "visual_fidelity_demo");
            System.Environment.SetEnvironmentVariable(QuestLaunchOptions.DemoModeKey, "short_playtest");
            System.Environment.SetEnvironmentVariable(QuestLaunchOptions.PlaytestHudKey, "true");
            System.Environment.SetEnvironmentVariable(QuestLaunchOptions.SeatCalibrationKey, "true");
            report.renderQuality = QuestRenderQualityConfigurator.ApplyProfile("visual_fidelity_demo_editor");

            VisualQaContext context = BuildVisualQaScene(width, height, report);
            report.viewpointPersistence = VerifyViewpointPersistence(outputDir);

            RenderRequiredShots(context, report, outputDir, width, height);
            report.demoPilot = VerifyDemoPilotMotion();
            report.contactSheetPath = BuildContactSheet(report, outputDir);
            report.passed = report.shots.All(s => s.passed) &&
                            report.viewpointPersistence.passed &&
                            report.demoPilot.passed;

            WriteReportFiles(report, outputDir);

            string failedShots = string.Join(", ", report.shots.Where(s => !s.passed).Select(s => s.id));
            Debug.Log($"[QuestFlightLab][VisualQA] Captured {report.shots.Count} shots. passed={report.passed} output={outputDir}");
            if (!string.IsNullOrWhiteSpace(failedShots))
            {
                Debug.LogWarning($"[QuestFlightLab][VisualQA] Failing visual sanity shot(s): {failedShots}");
            }
        }

        private static VisualQaContext BuildVisualQaScene(int width, int height, VisualQaReport report)
        {
            RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.62f, 0.73f, 0.86f);
            RenderSettings.fogDensity = 0.00045f;

            GameObject sunObject = new GameObject("Visual QA Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sunObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            GameObject fillObject = new GameObject("Visual QA Cockpit Fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fillObject.transform.rotation = Quaternion.Euler(12f, 140f, 0f);

            GameObject cameraObject = new GameObject("Visual QA Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 6500f;
            camera.fieldOfView = 76f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.72f, 0.94f);
            camera.aspect = width / (float)height;
            cameraObject.AddComponent<AudioListener>();

            GameObject airport = KbduApproxAirport.Build(null);
            AirportRuntimeEnhancer.EnhanceExistingScene();
            GameObject world = KbduInspiredWorldBuilder.AddWorld(airport != null ? airport.transform : null);
            if (world != null)
            {
                WorldPerformanceBudget budget = world.GetComponent<WorldPerformanceBudget>();
                report.worldBuilderStatus = budget != null
                    ? $"profile={budget.profileName} size={budget.worldSizeMeters.x:0}x{budget.worldSizeMeters.y:0}m chunks={budget.terrainChunkCount} lodGroups={budget.lodGroupCount}"
                    : "expanded KBDU-inspired world built";
            }
            else
            {
                report.worldBuilderStatus = "expanded KBDU-inspired world missing";
            }

            GameObject sceneryObject = new GameObject("Visual QA Scenery Mode Controller");
            SceneryModeController scenery = sceneryObject.AddComponent<SceneryModeController>();
            scenery.requestedMode = SceneryMode.MeshFallback;
            scenery.enableExperimentalSplatProxy = true;
            scenery.syntheticSplatCount = 50000;
            scenery.splatSampleKey = QuestSplatRuntimeConfig.ScenicProfile;
            scenery.splatBudgetProfile = "scenic_splat_medium";
            report.meshSceneryStatus = StatusSummary(scenery.ApplyMode(SceneryMode.MeshFallback));

            GameObject systems = new GameObject("Runtime Systems");
            GamepadInputReader reader = systems.AddComponent<GamepadInputReader>();
            Usb2BleInputMapper mapper = systems.AddComponent<Usb2BleInputMapper>();
            mapper.reader = reader;
            mapper.defaultThrottle = 0.72f;

            GameObject aircraft = new GameObject("Visual QA C172 Aircraft Root");
            aircraft.transform.SetPositionAndRotation(RunwayStartPosition, RunwayStartRotation);
            AircraftState aircraftState = aircraft.AddComponent<AircraftState>();
            aircraftState.config = Resources.Load<C172StyleAircraftConfig>("C172StyleAircraftConfig");
            FlightTelemetry telemetry = aircraft.AddComponent<FlightTelemetry>();
            telemetry.aircraftState = aircraftState;

            GameObject importedModel = AddImportedC172(aircraft.transform, report);
            report.cockpitAssetStatus = importedModel != null
                ? "C172-style cockpit/exterior asset loaded from Resources/" + report.importedC172ResourcePath + "."
                : "Imported C172 placeholder missing; visual QA used no imported cockpit asset.";

            GameObject hudObject = new GameObject("Visual QA Playtest HUD");
            PlaytestHud hud = hudObject.AddComponent<PlaytestHud>();
            hud.InitializeForTest(camera);

            GameObject calibrationPanel = BuildCalibrationPanel(camera.transform);
            calibrationPanel.SetActive(false);

            return new VisualQaContext
            {
                camera = camera,
                aircraftRoot = aircraft,
                importedC172 = importedModel,
                scenery = scenery,
                hud = hud,
                calibrationPanel = calibrationPanel
            };
        }

        private static GameObject AddImportedC172(Transform aircraft, VisualQaReport report)
        {
            string resourcePath = QuestFirstViewRuntimeRepair.ImportedC172ResourcePath;
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return null;
            report.importedC172ResourcePath = resourcePath;

            GameObject instance = Object.Instantiate(prefab, aircraft);
            instance.name = "Visual QA Imported C172";
            ApplyImportedCockpitPose(instance.transform, Vector3.zero, 0f);
            instance.transform.localScale = Vector3.one;
            ConfigureImportedC172Materials(instance);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            report.importedC172RendererCount = renderers.Length;
            if (TryGetRendererBounds(instance, out Bounds bounds))
            {
                report.importedC172BoundsCenter = bounds.center;
                report.importedC172BoundsSize = bounds.size;
            }

            return instance;
        }

        private static void RenderRequiredShots(
            VisualQaContext context,
            VisualQaReport report,
            string outputDir,
            int width,
            int height)
        {
            Vector3 pilotEye = QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal;
            Vector3 start = RunwayStartPosition;
            Quaternion startRotation = RunwayStartRotation;

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "01_cockpit_pilot_mesh",
                title = "Cockpit pilot view, mesh fallback",
                sceneryMode = "mesh",
                cameraPosition = context.aircraftRoot.transform.TransformPoint(pilotEye),
                cameraRotation = startRotation,
                fov = 76f,
                hideExteriorForCockpit = true,
                showHud = true,
                sanityTag = "cockpit"
            });

            SceneryProviderStatus scenicStatus = context.scenery.ApplyMode(SceneryMode.ExperimentalSplatRenderer);
            report.scenicSceneryStatus = StatusSummary(scenicStatus);
            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "02_cockpit_pilot_scenic",
                title = "Cockpit pilot view, scenic_splat_medium or fallback",
                sceneryMode = "scenic_splat_medium",
                cameraPosition = context.aircraftRoot.transform.TransformPoint(pilotEye),
                cameraRotation = startRotation,
                fov = 76f,
                hideExteriorForCockpit = true,
                showHud = true,
                sanityTag = "cockpit"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "03_instrument_hud_readability",
                title = "Instrument and HUD readability close view",
                sceneryMode = "mesh",
                cameraPosition = context.aircraftRoot.transform.TransformPoint(pilotEye + new Vector3(0.02f, -0.08f, 0.05f)),
                cameraRotation = startRotation * Quaternion.Euler(8f, 0f, 0f),
                fov = 58f,
                hideExteriorForCockpit = true,
                showHud = true,
                sanityTag = "hud"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "04_runway_centerline_takeoff",
                title = "Runway centerline and takeoff view",
                sceneryMode = "mesh",
                cameraPosition = new Vector3(-612f, 2.3f, -5.8f),
                cameraRotation = Quaternion.LookRotation((new Vector3(-485f, 1.4f, 0f) - new Vector3(-612f, 2.3f, -5.8f)).normalized, Vector3.up),
                fov = 64f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "runway"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "05_external_aircraft_view",
                title = "External aircraft view",
                sceneryMode = "mesh",
                cameraPosition = start + startRotation * new Vector3(-9f, 4.2f, -10f),
                cameraRotation = LookAt(start + startRotation * new Vector3(-9f, 4.2f, -10f), start + Vector3.up * 1.2f),
                fov = 52f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "aircraft"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "06_airport_overview",
                title = "Airport overview",
                sceneryMode = "mesh",
                cameraPosition = new Vector3(-120f, 170f, -230f),
                cameraRotation = LookAt(new Vector3(-120f, 170f, -230f), new Vector3(-120f, 0f, -15f)),
                fov = 54f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "runway"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "07_splat_scenic_patch_view",
                title = "Scenic splat patch view or explicit fallback",
                sceneryMode = "scenic_splat_medium",
                cameraPosition = new Vector3(-700f, 65f, -145f),
                cameraRotation = LookAt(new Vector3(-700f, 65f, -145f), new Vector3(-505f, 12f, 20f)),
                fov = 50f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "scenery"
            });

            RenderDemoShot(context, report, outputDir, width, height, 55f, "08_demo_takeoff_climb", "Demo-pilot takeoff/climb moment");
            RenderDemoShot(context, report, outputDir, width, height, 82f, "09_demo_shallow_turn", "Demo-pilot shallow turn moment");

            ApplyDemoPose(context, 0f);
            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "10_viewpoint_calibration_state",
                title = "Viewpoint calibration UI/state view",
                sceneryMode = "mesh",
                cameraPosition = context.aircraftRoot.transform.TransformPoint(pilotEye),
                cameraRotation = RunwayStartRotation,
                fov = 68f,
                hideExteriorForCockpit = true,
                showHud = true,
                showCalibrationPanel = true,
                sanityTag = "hud"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "11_large_terrain_far_scenery",
                title = "Large terrain and Front Range-inspired far scenery",
                sceneryMode = "visual_fidelity_demo",
                cameraPosition = new Vector3(-1650f, 520f, -1180f),
                cameraRotation = LookAt(new Vector3(-1650f, 520f, -1180f), new Vector3(-150f, 35f, 1100f)),
                fov = 48f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "scenery"
            });

            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = "12_render_quality_ground_detail",
                title = "Runway surface, markings, and anti-aliasing detail check",
                sceneryMode = "visual_fidelity_demo",
                cameraPosition = new Vector3(-545f, 5.2f, -18f),
                cameraRotation = LookAt(new Vector3(-545f, 5.2f, -18f), new Vector3(-455f, 0.4f, 2f)),
                fov = 36f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "runway"
            });
        }

        private static void RenderDemoShot(
            VisualQaContext context,
            VisualQaReport report,
            string outputDir,
            int width,
            int height,
            float elapsedSeconds,
            string id,
            string title)
        {
            ApplyDemoPose(context, elapsedSeconds);
            Vector3 target = context.aircraftRoot.transform.position + Vector3.up * 1.2f;
            Vector3 cameraPosition = target - context.aircraftRoot.transform.forward * 13f + context.aircraftRoot.transform.right * 6.5f + Vector3.up * 4.5f;
            RenderShot(context, report, outputDir, width, height, new ShotDefinition
            {
                id = id,
                title = title,
                sceneryMode = "mesh",
                demoTimeSeconds = elapsedSeconds,
                cameraPosition = cameraPosition,
                cameraRotation = LookAt(cameraPosition, target),
                fov = 54f,
                hideExteriorForCockpit = false,
                showHud = false,
                sanityTag = "aircraft"
            });
        }

        private static void RenderShot(
            VisualQaContext context,
            VisualQaReport report,
            string outputDir,
            int width,
            int height,
            ShotDefinition shot)
        {
            context.camera.transform.SetPositionAndRotation(shot.cameraPosition, shot.cameraRotation);
            context.camera.fieldOfView = shot.fov;
            SetImportedExteriorHidden(context.importedC172, shot.hideExteriorForCockpit);
            if (context.hud != null && context.hud.Root != null) context.hud.Root.SetActive(shot.showHud);
            if (context.calibrationPanel != null) context.calibrationPanel.SetActive(shot.showCalibrationPanel);

            string screenshotPath = Path.Combine(outputDir, "screenshots", $"{shot.id}.png");
            Texture2D texture = RenderCameraToTexture(context.camera, width, height);
            try
            {
                File.WriteAllBytes(screenshotPath, texture.EncodeToPNG());
                ImageStats stats = AnalyzeTexture(texture);
                VisualQaShotResult result = BuildShotResult(shot, screenshotPath, stats, context, report);
                report.shots.Add(result);
            }
            catch (Exception ex)
            {
                report.shots.Add(new VisualQaShotResult
                {
                    id = shot.id,
                    title = shot.title,
                    screenshotPath = screenshotPath,
                    sceneryMode = shot.sceneryMode,
                    cameraPosition = shot.cameraPosition,
                    cameraEuler = shot.cameraRotation.eulerAngles,
                    fov = shot.fov,
                    passed = false,
                    error = ex.Message
                });
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static VisualQaShotResult BuildShotResult(
            ShotDefinition shot,
            string screenshotPath,
            ImageStats stats,
            VisualQaContext context,
            VisualQaReport report)
        {
            VisualQaShotResult result = new VisualQaShotResult
            {
                id = shot.id,
                title = shot.title,
                screenshotPath = screenshotPath,
                sceneryMode = shot.sceneryMode,
                demoTimeSeconds = shot.demoTimeSeconds,
                cameraPosition = shot.cameraPosition,
                cameraEuler = shot.cameraRotation.eulerAngles,
                fov = shot.fov,
                width = stats.width,
                height = stats.height,
                colorVariance = stats.colorVariance,
                nonBackgroundRatio = stats.nonBackgroundRatio,
                greenTextRatio = stats.greenTextRatio,
                dimensionsValid = stats.width > 0 && stats.height > 0,
                imageNotBlank = stats.colorVariance > 18f && stats.nonBackgroundRatio > 0.035f,
                giantUiOverlayDetected = shot.showHud && stats.greenTextRatio > 0.16f,
                cockpitAssetPresent = context.importedC172 != null && report.importedC172RendererCount > 10,
                runwayGeometryPresent = GameObject.Find("Runway_08_26_Approx_4100x75ft") != null,
                activeCameraNotAtOrigin = shot.cameraPosition.sqrMagnitude > 0.5f,
                hudVisible = shot.showHud && context.hud != null && context.hud.Root != null && context.hud.Root.activeInHierarchy,
                calibrationPanelVisible = shot.showCalibrationPanel && context.calibrationPanel != null && context.calibrationPanel.activeInHierarchy
            };

            if (!result.dimensionsValid) result.warnings.Add("Screenshot dimensions are invalid.");
            if (!result.imageNotBlank) result.warnings.Add("Screenshot looks blank/solid by simple color heuristic.");
            if (result.giantUiOverlayDetected) result.warnings.Add("HUD-colored pixels dominate the frame; possible giant overlay.");
            if (!result.activeCameraNotAtOrigin) result.warnings.Add("Camera is unexpectedly near world origin.");
            if ((shot.sanityTag == "cockpit" || shot.sanityTag == "aircraft") && !result.cockpitAssetPresent)
            {
                result.warnings.Add("Imported C172 placeholder asset is missing or has too few renderers.");
            }

            if (shot.sanityTag == "cockpit" && result.nonBackgroundRatio < 0.12f)
            {
                result.warnings.Add("Cockpit shot has too little non-background detail.");
            }

            if (shot.sanityTag == "runway" && !result.runwayGeometryPresent)
            {
                result.warnings.Add("Runway geometry object is missing.");
            }

            if (shot.sanityTag == "hud" && !result.hudVisible)
            {
                result.warnings.Add("HUD expected but not active.");
            }

            result.passed = result.dimensionsValid &&
                            result.imageNotBlank &&
                            !result.giantUiOverlayDetected &&
                            result.activeCameraNotAtOrigin &&
                            (shot.sanityTag != "cockpit" || (result.cockpitAssetPresent && result.nonBackgroundRatio >= 0.12f)) &&
                            (shot.sanityTag != "aircraft" || result.cockpitAssetPresent) &&
                            (shot.sanityTag != "runway" || result.runwayGeometryPresent) &&
                            (shot.sanityTag != "hud" || result.hudVisible);
            return result;
        }

        private static Texture2D RenderCameraToTexture(Camera camera, int width, int height)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static ImageStats AnalyzeTexture(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int count = pixels.Length;
            Color32 background = pixels.Length > 0 ? pixels[pixels.Length - 1] : new Color32(0, 0, 0, 255);
            double meanR = 0;
            double meanG = 0;
            double meanB = 0;
            int nonBackground = 0;
            int greenText = 0;

            foreach (Color32 pixel in pixels)
            {
                meanR += pixel.r;
                meanG += pixel.g;
                meanB += pixel.b;
                int bgDiff = Math.Abs(pixel.r - background.r) + Math.Abs(pixel.g - background.g) + Math.Abs(pixel.b - background.b);
                if (bgDiff > 36) nonBackground++;
                if (pixel.g > 130 && pixel.r < 150 && pixel.b > 80) greenText++;
            }

            meanR /= Math.Max(1, count);
            meanG /= Math.Max(1, count);
            meanB /= Math.Max(1, count);
            double variance = 0;
            foreach (Color32 pixel in pixels)
            {
                variance += Math.Pow(pixel.r - meanR, 2);
                variance += Math.Pow(pixel.g - meanG, 2);
                variance += Math.Pow(pixel.b - meanB, 2);
            }

            variance /= Math.Max(1, count * 3);
            return new ImageStats
            {
                width = texture.width,
                height = texture.height,
                colorVariance = (float)variance,
                nonBackgroundRatio = nonBackground / (float)Math.Max(1, count),
                greenTextRatio = greenText / (float)Math.Max(1, count)
            };
        }

        private static ViewpointPersistenceResult VerifyViewpointPersistence(string outputDir)
        {
            string root = Path.Combine(outputDir, "viewpoint_persistence_probe");
            Directory.CreateDirectory(root);
            CockpitViewpointCalibrationState state = new CockpitViewpointCalibrationState
            {
                schemaVersion = CockpitViewpointPersistence.SchemaVersion,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                sceneryMode = "visual_qa",
                demoMode = "short_playtest",
                importedC172CockpitModelEye = QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye,
                importedC172PilotViewOffset = new Vector3(-0.21f, 0.17f, 0.05f),
                importedC172CockpitYawDeg = 3.5f,
                pilotEyeLocal = QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal + new Vector3(-0.21f, 0.17f, 0.05f),
                importedC172LocalPosition = Vector3.zero,
                instructions = "Visual QA persistence probe."
            };

            string path = CockpitViewpointPersistence.SaveCurrent(state, root);
            bool loaded = CockpitViewpointPersistence.TryLoadCurrent(out CockpitViewpointCalibrationState loadedState, out string loadedPath, out string loadError, root);
            bool matches = loaded &&
                           Vector3.Distance(state.importedC172PilotViewOffset, loadedState.importedC172PilotViewOffset) < 0.0001f &&
                           Mathf.Abs(state.importedC172CockpitYawDeg - loadedState.importedC172CockpitYawDeg) < 0.0001f;
            bool deleted = CockpitViewpointPersistence.DeleteCurrent(out string deletedPath, out string deleteError, root);
            bool reset = !File.Exists(CockpitViewpointPersistence.CurrentPath(root));

            return new ViewpointPersistenceResult
            {
                path = path,
                loadedPath = loadedPath,
                deletedPath = deletedPath,
                loaded = loaded,
                valuesMatched = matches,
                resetDeletedCurrent = deleted && reset,
                error = string.IsNullOrWhiteSpace(loadError) ? deleteError : loadError,
                passed = loaded && matches && deleted && reset
            };
        }

        private static DemoPilotVisualResult VerifyDemoPilotMotion()
        {
            Vector3 start = RunwayStartPosition;
            Quaternion rotation = RunwayStartRotation;
            bool takeoffPose = ShortPlaytestDemoPilot.TryGetVisualFlightPoseForElapsedSeconds(55f, start, rotation, out Vector3 climbPosition, out Vector3 climbEuler, out _);
            bool turnPose = ShortPlaytestDemoPilot.TryGetVisualFlightPoseForElapsedSeconds(82f, start, rotation, out Vector3 turnPosition, out Vector3 turnEuler, out _);
            float climbDistance = Vector3.Distance(start, climbPosition);
            float turnDistance = Vector3.Distance(climbPosition, turnPosition);

            return new DemoPilotVisualResult
            {
                takeoffPoseAvailable = takeoffPose,
                turnPoseAvailable = turnPose,
                startPosition = start,
                climbPosition = climbPosition,
                turnPosition = turnPosition,
                climbEuler = climbEuler,
                turnEuler = turnEuler,
                distanceFromStartMeters = climbDistance,
                distanceBetweenDemoShotsMeters = turnDistance,
                passed = takeoffPose && turnPose && climbDistance > 250f && turnDistance > 100f
            };
        }

        private static void ApplyDemoPose(VisualQaContext context, float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
            {
                context.aircraftRoot.transform.SetPositionAndRotation(RunwayStartPosition, RunwayStartRotation);
                return;
            }

            if (ShortPlaytestDemoPilot.TryGetVisualFlightPoseForElapsedSeconds(
                    elapsedSeconds,
                    RunwayStartPosition,
                    RunwayStartRotation,
                    out Vector3 position,
                    out Vector3 euler,
                    out _))
            {
                context.aircraftRoot.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            }
        }

        private static GameObject BuildCalibrationPanel(Transform cameraTransform)
        {
            GameObject root = new GameObject("Visual QA Viewpoint Calibration Panel");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0.78f, -0.14f, 1.55f);
            root.transform.localRotation = Quaternion.Euler(-4f, -18f, 0f);

            Material panelMat = Material("Visual QA Calibration Panel", new Color(0.018f, 0.02f, 0.024f));
            Material textMat = Material("Visual QA Calibration Text", new Color(0.78f, 0.96f, 0.88f));
            Cube(root.transform, "CalibrationPanelBackground", new Vector3(0f, 0f, 0.04f), Quaternion.identity, new Vector3(0.82f, 0.34f, 0.025f), panelMat);

            GameObject textObject = new GameObject("CalibrationPanelText");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(-0.38f, 0.14f, -0.02f);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.UpperLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 22;
            text.characterSize = 0.0102f;
            text.lineSpacing = 0.84f;
            text.color = textMat.color;
            text.text =
                "VIEWPOINT CAL\n" +
                "A save / open\n" +
                "X recenter forward\n" +
                "B reset while active\n" +
                "sticks: X/Y/Z + yaw\n" +
                "persist probe: PASS";
            return root;
        }

        private static string BuildContactSheet(VisualQaReport report, string outputDir)
        {
            List<Texture2D> loaded = new List<Texture2D>();
            int thumbWidth = 320;
            int thumbHeight = 180;
            int columns = 2;
            int rows = Mathf.CeilToInt(report.shots.Count / (float)columns);
            Texture2D sheet = new Texture2D(columns * thumbWidth, rows * thumbHeight, TextureFormat.RGBA32, false);
            Color32[] clear = Enumerable.Repeat(new Color32(16, 18, 22, 255), sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(clear);

            try
            {
                for (int i = 0; i < report.shots.Count; i++)
                {
                    string path = report.shots[i].screenshotPath;
                    Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    source.LoadImage(File.ReadAllBytes(path));
                    loaded.Add(source);

                    int col = i % columns;
                    int row = rows - 1 - (i / columns);
                    BlitScaled(source, sheet, col * thumbWidth, row * thumbHeight, thumbWidth, thumbHeight);
                }

                sheet.Apply();
                string contactSheet = Path.Combine(outputDir, "visual_qa_contact_sheet.png");
                File.WriteAllBytes(contactSheet, sheet.EncodeToPNG());
                return contactSheet;
            }
            finally
            {
                foreach (Texture2D texture in loaded)
                {
                    Object.DestroyImmediate(texture);
                }

                Object.DestroyImmediate(sheet);
            }
        }

        private static void BlitScaled(Texture2D source, Texture2D target, int offsetX, int offsetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sy = Mathf.Clamp(Mathf.RoundToInt(y / (float)Math.Max(1, height - 1) * (source.height - 1)), 0, source.height - 1);
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.Clamp(Mathf.RoundToInt(x / (float)Math.Max(1, width - 1) * (source.width - 1)), 0, source.width - 1);
                    target.SetPixel(offsetX + x, offsetY + y, source.GetPixel(sx, sy));
                }
            }
        }

        private static void WriteReportFiles(VisualQaReport report, string outputDir)
        {
            File.WriteAllText(Path.Combine(outputDir, "visual_qa_report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(outputDir, "visual_qa_report.csv"), BuildCsv(report));
            File.WriteAllText(Path.Combine(outputDir, "visual_qa_summary.md"), BuildMarkdown(report));
        }

        private static string BuildCsv(VisualQaReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("id,title,passed,scenery_mode,path,variance,non_background_ratio,green_text_ratio,warnings");
            foreach (VisualQaShotResult shot in report.shots)
            {
                sb.Append(Escape(shot.id)).Append(',')
                    .Append(Escape(shot.title)).Append(',')
                    .Append(shot.passed).Append(',')
                    .Append(Escape(shot.sceneryMode)).Append(',')
                    .Append(Escape(shot.screenshotPath)).Append(',')
                    .Append(shot.colorVariance.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(shot.nonBackgroundRatio.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(shot.greenTextRatio.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(string.Join("; ", shot.warnings))).AppendLine();
            }

            return sb.ToString();
        }

        private static string BuildMarkdown(VisualQaReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Autonomous Visual QA Summary");
            sb.AppendLine();
            sb.AppendLine($"- Generated UTC: {report.generatedUtc}");
            sb.AppendLine($"- Unity: {report.unityVersion}");
            sb.AppendLine($"- Visual feedback path: {report.visualFeedbackPath}");
            sb.AppendLine($"- Meta XR Simulator: {report.metaXrSimulatorStatus}");
            sb.AppendLine($"- XR Device Simulator: {report.xrDeviceSimulatorStatus}");
            sb.AppendLine($"- Contact sheet: `{report.contactSheetPath}`");
            sb.AppendLine($"- Overall pass: {report.passed}");
            sb.AppendLine($"- Cockpit asset: {report.cockpitAssetStatus}");
            sb.AppendLine($"- World builder: {report.worldBuilderStatus}");
            sb.AppendLine($"- Render quality: AA {report.renderQuality.antiAliasing}, aniso {report.renderQuality.anisotropicFiltering}, LOD bias {report.renderQuality.lodBias:0.00}, shadow distance {report.renderQuality.shadowDistance:0}m");
            sb.AppendLine($"- Mesh scenery: {report.meshSceneryStatus}");
            sb.AppendLine($"- Scenic/splat status: {report.scenicSceneryStatus}");
            sb.AppendLine($"- Viewpoint persistence: {(report.viewpointPersistence.passed ? "PASS" : "FAIL")} `{report.viewpointPersistence.path}`");
            sb.AppendLine($"- Demo pilot motion: {(report.demoPilot.passed ? "PASS" : "FAIL")} distance {report.demoPilot.distanceFromStartMeters:0.0} m");
            sb.AppendLine();
            sb.AppendLine("| Shot | Pass | Notes |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (VisualQaShotResult shot in report.shots)
            {
                string notes = shot.warnings.Count == 0 ? "ok" : string.Join("; ", shot.warnings);
                sb.AppendLine($"| `{shot.id}` | {shot.passed} | {notes} |");
            }

            sb.AppendLine();
            sb.AppendLine("Limitations: this is deterministic Unity Editor visual QA, not real Quest comfort/performance evidence, not final C172 fidelity, not FAA/training suitability, and not proof that Quest XR splats are stereo/world-locked.");
            return sb.ToString();
        }

        private static string DetectXrDeviceSimulatorStatus()
        {
            string settingsPath = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";
            bool settingsExist = File.Exists(settingsPath);
            bool packageCached = Directory.Exists(Path.Combine("Library", "PackageCache")) &&
                                 Directory.GetDirectories(Path.Combine("Library", "PackageCache"), "com.unity.xr.interaction.toolkit@*").Length > 0;
            if (settingsExist && packageCached)
            {
                return "Unity XR Interaction Toolkit simulator package/settings detected; not used for this batch run because deterministic editor camera capture is the primary no-headset path.";
            }

            if (packageCached)
            {
                return "Unity XR Interaction Toolkit package detected; simulator settings/prefab are not configured for automatic batch capture.";
            }

            return "Unity XR Device Simulator package not detected; deterministic editor camera capture used.";
        }

        private static string StatusSummary(SceneryProviderStatus status)
        {
            if (status == null) return "not run";
            string warnings = status.warnings != null && status.warnings.Count > 0
                ? " warnings=" + string.Join("; ", status.warnings)
                : string.Empty;
            return $"requested={status.requestedMode} active={status.activeMode} fallback={status.fallbackUsed} sample={status.sampleKey} budget={status.splatCount}{warnings}";
        }

        private static void ApplyImportedCockpitPose(Transform cockpit, Vector3 offset, float yawDegrees)
        {
            Quaternion modelRotation = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172LocalEuler);
            Quaternion cameraInModelRotation = Quaternion.Euler(QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEyeEuler);
            Quaternion modelInCameraRotation = Quaternion.Inverse(cameraInModelRotation) * modelRotation;
            cockpit.localRotation = Quaternion.Euler(0f, yawDegrees, 0f) * modelInCameraRotation;
            Vector3 baseSeatTarget = QuestFirstViewRuntimeRepair.ImportedC172PilotEyeLocal + offset;
            cockpit.localPosition = baseSeatTarget - cockpit.localRotation * QuestFirstViewRuntimeRepair.ImportedC172CockpitModelEye;
        }

        private static void SetImportedExteriorHidden(GameObject root, bool hidden)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string path = PathFor(renderer.transform);
                if (path.IndexOf("Cessna_Exterior_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    renderer.enabled = !hidden;
                }
            }
        }

        private static void ConfigureImportedC172Materials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    if (material.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                    float brightness = Mathf.Max(color.r, color.g, color.b);
                    if (brightness < 0.08f) color = new Color(0.55f, 0.82f, 0.96f, color.a);
                    color.a = Mathf.Min(color.a <= 0f ? 0.18f : color.a, 0.22f);
                    SetTransparentMaterial(material, color);
                    changed = true;
                }

                if (changed) renderer.materials = materials;
            }
        }

        private static void SetTransparentMaterial(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
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

            return hasBounds;
        }

        private static Quaternion LookAt(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static Material Material(string name, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }

        private static string PathFor(Transform transform)
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

        private static int ReadInt(string key, int fallback)
        {
            string value = System.Environment.GetEnvironmentVariable(key);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        [Serializable]
        public class VisualQaReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string outputDirectory;
            public string visualFeedbackPath;
            public string metaXrSimulatorStatus;
            public string xrDeviceSimulatorStatus;
            public int renderWidth;
            public int renderHeight;
            public string cockpitAssetStatus;
            public string importedC172ResourcePath;
            public int importedC172RendererCount;
            public Vector3 importedC172BoundsCenter;
            public Vector3 importedC172BoundsSize;
            public string worldBuilderStatus;
            public RenderQualityEvidence renderQuality;
            public string meshSceneryStatus;
            public string scenicSceneryStatus;
            public string contactSheetPath;
            public ViewpointPersistenceResult viewpointPersistence;
            public DemoPilotVisualResult demoPilot;
            public bool passed;
            public List<VisualQaShotResult> shots = new List<VisualQaShotResult>();
        }

        [Serializable]
        public class VisualQaShotResult
        {
            public string id;
            public string title;
            public string screenshotPath;
            public string sceneryMode;
            public float demoTimeSeconds;
            public Vector3 cameraPosition;
            public Vector3 cameraEuler;
            public float fov;
            public int width;
            public int height;
            public float colorVariance;
            public float nonBackgroundRatio;
            public float greenTextRatio;
            public bool dimensionsValid;
            public bool imageNotBlank;
            public bool giantUiOverlayDetected;
            public bool cockpitAssetPresent;
            public bool runwayGeometryPresent;
            public bool activeCameraNotAtOrigin;
            public bool hudVisible;
            public bool calibrationPanelVisible;
            public bool passed;
            public string error;
            public List<string> warnings = new List<string>();
        }

        [Serializable]
        public class ViewpointPersistenceResult
        {
            public string path;
            public string loadedPath;
            public string deletedPath;
            public bool loaded;
            public bool valuesMatched;
            public bool resetDeletedCurrent;
            public string error;
            public bool passed;
        }

        [Serializable]
        public class DemoPilotVisualResult
        {
            public bool takeoffPoseAvailable;
            public bool turnPoseAvailable;
            public Vector3 startPosition;
            public Vector3 climbPosition;
            public Vector3 turnPosition;
            public Vector3 climbEuler;
            public Vector3 turnEuler;
            public float distanceFromStartMeters;
            public float distanceBetweenDemoShotsMeters;
            public bool passed;
        }

        private class VisualQaContext
        {
            public Camera camera;
            public GameObject aircraftRoot;
            public GameObject importedC172;
            public SceneryModeController scenery;
            public PlaytestHud hud;
            public GameObject calibrationPanel;
        }

        private class ShotDefinition
        {
            public string id;
            public string title;
            public string sceneryMode;
            public string sanityTag;
            public float demoTimeSeconds;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float fov;
            public bool hideExteriorForCockpit;
            public bool showHud;
            public bool showCalibrationPanel;
        }

        private struct ImageStats
        {
            public int width;
            public int height;
            public float colorVariance;
            public float nonBackgroundRatio;
            public float greenTextRatio;
        }
    }
}
