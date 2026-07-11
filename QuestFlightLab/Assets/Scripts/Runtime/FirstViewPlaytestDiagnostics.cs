using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using QuestFlightLab.UI;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class FirstViewPlaytestEvidence
    {
        public string generatedUtc;
        public string platform;
        public string unityVersion;
        public string deviceModel;
        public string sceneryMode;
        public string demoMode;
        public string activeCameraName;
        public string activeCameraPath;
        public string xrOriginName;
        public string aircraftSimulationRootPath;
        public string aircraftVisualRootPath;
        public string pilotSeatAnchorPath;
        public string userViewCalibrationOffsetPath;
        public string aircraftRotationPivotPath;
        public string centerOfGravityReferencePath;
        public Vector3 centerOfGravityLocalOffset;
        public string cameraPoseOwner;
        public string trackedPosePositionBinding;
        public string trackedPoseRotationBinding;
        public bool referenceFrameHierarchyValid;
        public bool trackedCameraUnderXrOrigin;
        public bool trackedPoseDriverConfigured;
        public int trackedPoseResolvedControlCount;
        public int enabledXrOriginCount;
        public string leftControllerPath;
        public string rightControllerPath;
        public bool controllerPoseDriversConfigured;
        public int leftControllerResolvedControlCount;
        public int rightControllerResolvedControlCount;
        public Vector3 xrOriginLocalPosition;
        public Vector3 xrOriginLocalEuler;
        public Vector3 trackedCameraLocalPosition;
        public Vector3 trackedCameraLocalEuler;
        public Vector3 cameraSeatRelativeEuler;
        public float cameraToAircraftForwardAngleDeg;
        public bool xrSettingsEnabled;
        public bool xrDeviceActive;
        public bool xrLoaderActive;
        public bool headDeviceValid;
        public bool headDeviceTracked;
        public bool manualHeadPoseApplied;
        public bool manualHeadDevicePoseValid;
        public bool manualHeadDeviceTracked;
        public bool manualHeadUserPresent;
        public float manualHeadYawDeltaDeg;
        public float manualHeadPitchDeltaDeg;
        public float manualHeadPositionDeltaMeters;
        public float manualHeadAppliedPositionDeltaMeters;
        public int manualHeadBaselineRecaptureCount;
        public string manualHeadBaselineStatus;
        public string headPoseMode;
        public bool startupSeatAlignmentPending;
        public bool startupSeatAlignmentCompleted;
        public int startupSeatStableFrameCount;
        public int startupSeatStableFramesRequired;
        public int startupSeatRecenterCount;
        public string startupSeatAlignmentStatus;
        public float startupSeatPositionErrorMeters;
        public float startupSeatYawErrorDegrees;
        public bool importedC172Loaded;
        public int importedExteriorRendererHiddenCount;
        public CockpitLightingReport importedC172Lighting;
        public Vector3 importedC172BoundsSize;
        public Vector3 importedC172CockpitModelEye;
        public Vector3 importedC172PilotViewOffset;
        public float importedC172CockpitYawDeg;
        public Vector3 pilotEyeLocal;
        public bool seatCalibrationEnabled;
        public bool seatCalibrationAdjustmentActive;
        public bool seatCalibrationModeActive;
        public string seatCalibrationCapturePath;
        public string seatCalibrationStatus;
        public bool savedSeatCalibrationLoaded;
        public string savedSeatCalibrationPath;
        public bool playtestHudActive;
        public int hiddenVerbosePanelCount;
        public int hudLineCount;
        public int sampleCount;
        public float sampleSeconds;
        public float localYawDeltaDeg;
        public float localPitchDeltaDeg;
        public float localPositionDeltaMeters;
        public bool headPoseChanged;
        public string evidencePath;
        public List<string> screenshotPaths = new List<string>();
        public List<string> warnings = new List<string>();
    }

    public class FirstViewPlaytestDiagnostics : MonoBehaviour
    {
        private const string LogPrefix = "[QuestFlightLab][FirstViewDiagnostics]";
        public const float HeavyRefreshIntervalSeconds = 0.25f;

        public static FirstViewPlaytestDiagnostics Instance { get; private set; }

        public float maxSampleSeconds = 180f;
        public float writeIntervalSeconds = 5f;
        public string EvidencePath => _evidence != null ? _evidence.evidencePath : string.Empty;
        public bool InitialDiagnosticScreenshotsComplete { get; private set; }

        private Camera _camera;
        private Transform _xrOrigin;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation = Quaternion.identity;
        private float _maxYawAbs;
        private float _maxPitchAbs;
        private float _maxPositionDelta;
        private float _startTime;
        private float _nextHeavyRefreshTime;
        private float _nextWriteTime;
        private FirstViewPlaytestEvidence _evidence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!QuestLaunchOptions.PlaytestHudEnabled()) return;
            if (FindFirstObjectByType<FirstViewPlaytestDiagnostics>() != null) return;

            GameObject go = new GameObject("First View Playtest Diagnostics");
            DontDestroyOnLoad(go);
            go.AddComponent<FirstViewPlaytestDiagnostics>();
        }

        private IEnumerator Start()
        {
            Instance = this;
            yield return null;
            ResolveCamera();
            _startTime = Time.unscaledTime;
            _nextHeavyRefreshTime = Time.unscaledTime + HeavyRefreshIntervalSeconds;
            _nextWriteTime = Time.unscaledTime + writeIntervalSeconds;

            if (_camera != null)
            {
                _initialLocalPosition = _camera.transform.localPosition;
                _initialLocalRotation = _camera.transform.localRotation;
            }

            _evidence = CreateEvidence();
            WriteEvidence();
            Debug.Log($"{LogPrefix} Started. camera={_evidence.activeCameraName} origin={_evidence.xrOriginName} path={_evidence.evidencePath}");
            StartCoroutine(CaptureFirstViewScreenshots());
        }

        private void Update()
        {
            if (_evidence == null) return;
            if (_camera == null) ResolveCamera();
            if (_camera == null) return;

            float elapsed = Time.unscaledTime - _startTime;
            if (elapsed <= maxSampleSeconds)
            {
                // Pose maxima remain frame-accurate. Scene-wide hierarchy/input
                // inspection is evidence work and only needs a human-readable 4 Hz cadence.
                SamplePose(elapsed);
                if (Time.unscaledTime >= _nextHeavyRefreshTime)
                {
                    RefreshEvidence(_evidence, elapsed);
                    _nextHeavyRefreshTime = Time.unscaledTime + HeavyRefreshIntervalSeconds;
                }
            }

            if (Time.unscaledTime >= _nextWriteTime)
            {
                WriteEvidence();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) WriteEvidence();
        }

        private void OnApplicationQuit()
        {
            WriteEvidence();
        }

        private void ResolveCamera()
        {
            _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            _xrOrigin = FindXrOriginTransform(_camera != null ? _camera.transform : null);
        }

        private FirstViewPlaytestEvidence CreateEvidence()
        {
            EnsureEvidenceDirectory(out string path);

            FirstViewPlaytestEvidence evidence = new FirstViewPlaytestEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel,
                sceneryMode = QuestLaunchOptions.SceneryMode(),
                demoMode = QuestLaunchOptions.DemoMode(),
                evidencePath = path
            };

            RefreshEvidence(evidence, 0f);
            return evidence;
        }

        private void SamplePose(float elapsed)
        {
            Quaternion deltaRotation = Quaternion.Inverse(_initialLocalRotation) * _camera.transform.localRotation;
            Vector3 deltaEuler = deltaRotation.eulerAngles;
            float yaw = Mathf.Abs(NormalizeAngle(deltaEuler.y));
            float pitch = Mathf.Abs(NormalizeAngle(deltaEuler.x));
            float position = Vector3.Distance(_initialLocalPosition, _camera.transform.localPosition);

            _maxYawAbs = Mathf.Max(_maxYawAbs, yaw);
            _maxPitchAbs = Mathf.Max(_maxPitchAbs, pitch);
            _maxPositionDelta = Mathf.Max(_maxPositionDelta, position);

            _evidence.sampleCount++;
            UpdatePoseSummary(_evidence, elapsed);
        }

        private void UpdatePoseSummary(FirstViewPlaytestEvidence evidence, float elapsed)
        {
            evidence.sampleSeconds = elapsed;
            evidence.localYawDeltaDeg = _maxYawAbs;
            evidence.localPitchDeltaDeg = _maxPitchAbs;
            evidence.localPositionDeltaMeters = _maxPositionDelta;
            evidence.headPoseChanged = _maxYawAbs > 1.5f ||
                                       _maxPitchAbs > 1.5f ||
                                       _maxPositionDelta > 0.025f ||
                                       evidence.manualHeadYawDeltaDeg > 1.5f ||
                                       evidence.manualHeadPitchDeltaDeg > 1.5f ||
                                       evidence.manualHeadPositionDeltaMeters > 0.025f;
        }

        private void RefreshEvidence(FirstViewPlaytestEvidence evidence, float elapsed)
        {
            ResolveCamera();

            evidence.generatedUtc = DateTime.UtcNow.ToString("O");
            evidence.sceneryMode = QuestLaunchOptions.SceneryMode();
            evidence.demoMode = QuestLaunchOptions.DemoMode();
            evidence.activeCameraName = _camera != null ? _camera.name : string.Empty;
            evidence.activeCameraPath = _camera != null ? PathFor(_camera.transform) : string.Empty;
            evidence.xrOriginName = _xrOrigin != null ? _xrOrigin.name : string.Empty;
            evidence.xrSettingsEnabled = XRSettings.enabled;
            evidence.xrDeviceActive = XRSettings.isDeviceActive;
            evidence.xrLoaderActive = XRGeneralSettings.Instance != null &&
                                      XRGeneralSettings.Instance.Manager != null &&
                                      XRGeneralSettings.Instance.Manager.activeLoader != null;
            XRNodeState(evidence);
            QuestFirstViewRuntimeRepair repair = QuestFirstViewRuntimeRepair.Instance;
            if (repair != null)
            {
                evidence.manualHeadPoseApplied = repair.ManualHeadPoseApplied;
                evidence.manualHeadDevicePoseValid = repair.HeadDevicePoseValid;
                evidence.manualHeadDeviceTracked = repair.HeadDeviceTracked;
                evidence.manualHeadUserPresent = repair.HeadUserPresent;
                evidence.manualHeadYawDeltaDeg = Mathf.Abs(repair.HeadYawDeltaDeg);
                evidence.manualHeadPitchDeltaDeg = Mathf.Abs(repair.HeadPitchDeltaDeg);
                evidence.manualHeadPositionDeltaMeters = repair.HeadPositionDeltaMeters;
                evidence.manualHeadAppliedPositionDeltaMeters = repair.HeadAppliedPositionDeltaMeters;
                evidence.manualHeadBaselineRecaptureCount = repair.HeadBaselineRecaptureCount;
                evidence.manualHeadBaselineStatus = repair.HeadBaselineStatus;
                evidence.headPoseMode = repair.HeadPoseMode;
                evidence.startupSeatAlignmentPending = repair.StartupSeatAlignmentPending;
                evidence.startupSeatAlignmentCompleted = repair.StartupSeatAlignmentCompleted;
                evidence.startupSeatStableFrameCount = repair.StartupSeatStableFrameCount;
                evidence.startupSeatStableFramesRequired = Mathf.Max(2, repair.startupSeatStableFramesRequired);
                evidence.startupSeatRecenterCount = repair.StartupSeatRecenterCount;
                evidence.startupSeatAlignmentStatus = repair.StartupSeatAlignmentStatus;
                evidence.startupSeatPositionErrorMeters = repair.StartupSeatPositionErrorMeters;
                evidence.startupSeatYawErrorDegrees = repair.StartupSeatYawErrorDegrees;
                AircraftReferenceFrameRig rig = repair.ReferenceFrameRig;
                evidence.aircraftSimulationRootPath = rig != null && rig.AircraftSimulationRoot != null ? PathFor(rig.AircraftSimulationRoot) : string.Empty;
                evidence.aircraftVisualRootPath = rig != null && rig.AircraftVisualRoot != null ? PathFor(rig.AircraftVisualRoot) : string.Empty;
                evidence.pilotSeatAnchorPath = rig != null && rig.PilotSeatAnchor != null ? PathFor(rig.PilotSeatAnchor) : string.Empty;
                evidence.userViewCalibrationOffsetPath = rig != null && rig.UserViewCalibrationOffset != null ? PathFor(rig.UserViewCalibrationOffset) : string.Empty;
                // Rotation is applied to AircraftSimulationRoot; the CG child is
                // a zero-offset reference marker, not a substitute pivot owner.
                evidence.aircraftRotationPivotPath = rig != null && rig.AircraftSimulationRoot != null ? PathFor(rig.AircraftSimulationRoot) : string.Empty;
                evidence.centerOfGravityReferencePath = rig != null && rig.CenterOfGravityReference != null ? PathFor(rig.CenterOfGravityReference) : string.Empty;
                evidence.centerOfGravityLocalOffset = rig != null && rig.CenterOfGravityReference != null ? rig.CenterOfGravityReference.localPosition : Vector3.zero;
                evidence.trackedPoseDriverConfigured = TrackedXrCameraPoseDriver.HasRequiredBindings(_camera);
                evidence.trackedPosePositionBinding = TrackedXrCameraPoseDriver.PositionBindingPath(_camera);
                evidence.trackedPoseRotationBinding = TrackedXrCameraPoseDriver.RotationBindingPath(_camera);
                evidence.trackedPoseResolvedControlCount = TrackedXrCameraPoseDriver.ResolvedControlCount(_camera);
                evidence.enabledXrOriginCount = CountEnabledXrOrigins();
                evidence.leftControllerPath = rig != null && rig.LeftController != null ? PathFor(rig.LeftController) : string.Empty;
                evidence.rightControllerPath = rig != null && rig.RightController != null ? PathFor(rig.RightController) : string.Empty;
                evidence.controllerPoseDriversConfigured = rig != null && TrackedXrControllerPoseDrivers.HasRequiredHierarchy(rig.XrOrigin);
                evidence.leftControllerResolvedControlCount = rig != null ? TrackedXrControllerPoseDrivers.ResolvedControlCount(rig.LeftController) : 0;
                evidence.rightControllerResolvedControlCount = rig != null ? TrackedXrControllerPoseDrivers.ResolvedControlCount(rig.RightController) : 0;
                evidence.xrOriginLocalPosition = rig != null && rig.XrOrigin != null ? rig.XrOrigin.localPosition : Vector3.zero;
                evidence.xrOriginLocalEuler = rig != null && rig.XrOrigin != null ? SignedEuler(rig.XrOrigin.localRotation) : Vector3.zero;
                evidence.trackedCameraLocalPosition = _camera != null ? _camera.transform.localPosition : Vector3.zero;
                evidence.trackedCameraLocalEuler = _camera != null ? SignedEuler(_camera.transform.localRotation) : Vector3.zero;
                if (rig != null && rig.PilotSeatAnchor != null && _camera != null)
                {
                    Quaternion cameraInSeat = Quaternion.Inverse(rig.PilotSeatAnchor.rotation) * _camera.transform.rotation;
                    evidence.cameraSeatRelativeEuler = SignedEuler(cameraInSeat);
                    evidence.cameraToAircraftForwardAngleDeg = Vector3.Angle(
                        _camera.transform.forward,
                        rig.AircraftVisualRoot.forward);
                }
                bool trackedPoseResolved = Application.platform != RuntimePlatform.Android || evidence.trackedPoseResolvedControlCount >= 2;
                evidence.cameraPoseOwner = evidence.trackedPoseDriverConfigured && trackedPoseResolved
                    ? "OpenXR TrackedPoseDriver"
                    : "INVALID: tracked pose bindings or resolved HMD controls missing";
                evidence.referenceFrameHierarchyValid = rig != null &&
                                                        rig.ValidateHierarchy() &&
                                                        evidence.enabledXrOriginCount == 1 &&
                                                        evidence.controllerPoseDriversConfigured;
                evidence.trackedCameraUnderXrOrigin = rig != null && _camera != null && _camera.transform.IsChildOf(rig.XrOrigin);
                evidence.importedC172Loaded = repair.ImportedC172Loaded;
                evidence.importedExteriorRendererHiddenCount = repair.ImportedExteriorRendererHiddenCount;
                evidence.importedC172Lighting = repair.ImportedC172Lighting;
                evidence.importedC172BoundsSize = repair.ImportedC172BoundsSize;
                evidence.importedC172CockpitModelEye = repair.ImportedC172CockpitModelEyeUsed;
                evidence.importedC172PilotViewOffset = repair.ImportedC172PilotViewOffsetUsed;
                evidence.importedC172CockpitYawDeg = repair.ImportedC172CockpitYawDegUsed;
                evidence.pilotEyeLocal = repair.PilotEyeLocalUsed;
                evidence.seatCalibrationEnabled = repair.SeatCalibrationEnabled;
                evidence.seatCalibrationAdjustmentActive = repair.SeatCalibrationAdjustmentActive;
                evidence.seatCalibrationModeActive = repair.SeatCalibrationModeActive;
                evidence.seatCalibrationCapturePath = repair.SeatCalibrationCapturePath;
                evidence.seatCalibrationStatus = repair.SeatCalibrationStatus;
                evidence.savedSeatCalibrationLoaded = repair.SavedSeatCalibrationLoaded;
                evidence.savedSeatCalibrationPath = repair.SavedSeatCalibrationPath;
            }
            evidence.playtestHudActive = PlaytestHud.Instance != null && PlaytestHud.Instance.Root != null;
            evidence.hiddenVerbosePanelCount = PlaytestHud.Instance != null ? PlaytestHud.Instance.HiddenVerbosePanelCount : 0;
            evidence.hudLineCount = PlaytestHud.Instance != null ? PlaytestHud.Instance.VisibleLineCount : 0;
            UpdatePoseSummary(evidence, elapsed);
        }

        private void WriteEvidence()
        {
            if (_evidence == null) return;
            float now = Time.unscaledTime;
            RefreshEvidence(_evidence, Mathf.Max(0f, now - _startTime));
            // Screenshot-triggered writes also restart the periodic interval so
            // evidence I/O cannot cluster immediately after a diagnostic capture.
            _nextWriteTime = now + writeIntervalSeconds;

            try
            {
                File.WriteAllText(_evidence.evidencePath, JsonUtility.ToJson(_evidence, true));
                Debug.Log($"{LogPrefix} Evidence written: {_evidence.evidencePath} poseChanged={_evidence.headPoseChanged} yawDelta={_evidence.localYawDeltaDeg:F1} pitchDelta={_evidence.localPitchDeltaDeg:F1} posDelta={_evidence.localPositionDeltaMeters:F3}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Evidence write failed: {ex.Message}");
            }
        }

        private static int CountEnabledXrOrigins()
        {
            Type xrOriginType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (xrOriginType == null) return 0;

            int count = 0;
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.enabled && behaviour.gameObject.activeInHierarchy && xrOriginType.IsInstanceOfType(behaviour))
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerator CaptureFirstViewScreenshots()
        {
            yield return new WaitForSecondsRealtime(2f);
            yield return new WaitForEndOfFrame();
            CaptureFirstViewScreenshot("startup");

            yield return new WaitForSecondsRealtime(5f);
            yield return new WaitForEndOfFrame();
            CaptureFirstViewScreenshot("demo");
            WriteEvidence();

            float waitStart = Time.unscaledTime;
            while (Time.unscaledTime - waitStart < 30f)
            {
                QuestFirstViewRuntimeRepair repair = QuestFirstViewRuntimeRepair.Instance;
                if (repair != null && repair.ImportedC172Loaded) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }

            yield return new WaitForEndOfFrame();
            CaptureFirstViewScreenshot("imported_c172");
            WriteEvidence();
            InitialDiagnosticScreenshotsComplete = true;
            Debug.Log($"{LogPrefix} Initial diagnostic screenshot sequence complete.");
        }

        private void CaptureFirstViewScreenshot(string label)
        {
            if (_evidence == null) return;
            RefreshEvidence(_evidence, Mathf.Max(0f, Time.unscaledTime - _startTime));

            try
            {
                string path = Path.Combine(EvidenceDirectory(), $"quest_first_view_{label}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                if (CaptureDiagnosticCameraPng(path))
                {
                    _evidence.screenshotPaths.Add(path);
                    Debug.Log($"{LogPrefix} First-view diagnostic camera screenshot written: {path}");
                    return;
                }

                Type screenCaptureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
                MethodInfo captureMethod = screenCaptureType?.GetMethod("CaptureScreenshot", new[] { typeof(string) });
                if (captureMethod == null)
                {
                    _evidence.warnings.Add($"First-view screenshot '{label}' unavailable: Unity ScreenCapture API missing.");
                    Debug.LogWarning($"{LogPrefix} First-view screenshot '{label}' unavailable: Unity ScreenCapture API missing.");
                    return;
                }

                captureMethod.Invoke(null, new object[] { path });
                _evidence.screenshotPaths.Add(path);
                Debug.Log($"{LogPrefix} First-view screenshot requested: {path}");
            }
            catch (Exception ex)
            {
                _evidence.warnings.Add($"First-view screenshot '{label}' failed: {ex.Message}");
                Debug.LogWarning($"{LogPrefix} First-view screenshot '{label}' failed: {ex.Message}");
            }
        }

        private bool CaptureDiagnosticCameraPng(string path)
        {
            Camera source = _camera != null ? _camera : Camera.main;
            if (source == null)
            {
                _evidence.warnings.Add("Diagnostic camera screenshot unavailable: no active camera.");
                return false;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            GameObject captureObject = null;

            try
            {
                renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1
                };
                renderTexture.Create();

                captureObject = new GameObject("First View Diagnostic Capture Camera");
                Camera captureCamera = captureObject.AddComponent<Camera>();
                captureCamera.CopyFrom(source);
                captureCamera.enabled = false;
                captureCamera.aspect = 16f / 9f;
                captureCamera.targetTexture = renderTexture;
                captureCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

                captureCamera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (Exception ex)
            {
                _evidence.warnings.Add($"Diagnostic camera screenshot failed: {ex.Message}");
                Debug.LogWarning($"{LogPrefix} Diagnostic camera screenshot failed: {ex.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (texture != null) Destroy(texture);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Destroy(renderTexture);
                }

                if (captureObject != null) Destroy(captureObject);
            }
        }

        private static void XRNodeState(FirstViewPlaytestEvidence evidence)
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            evidence.headDeviceValid = head.isValid;
            evidence.headDeviceTracked = false;
            if (head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
            {
                evidence.headDeviceTracked = tracked;
            }
        }

        private static void EnsureEvidenceDirectory(out string path)
        {
            path = Path.Combine(EvidenceDirectory(), $"quest_first_view_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
        }

        private static string EvidenceDirectory()
        {
            string dir = Path.Combine(Application.persistentDataPath, "QuestFlightLab", "first_view_diagnostics");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static Transform FindXrOriginTransform(Transform cameraTransform)
        {
            Transform current = cameraTransform;
            while (current != null)
            {
                if (current.name == "XR Origin") return current;
                current = current.parent;
            }

            GameObject named = GameObject.Find("XR Origin");
            return named != null ? named.transform : null;
        }

        private static string PathFor(Transform transform)
        {
            if (transform == null) return string.Empty;
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        private static Vector3 SignedEuler(Quaternion rotation)
        {
            Vector3 euler = rotation.eulerAngles;
            return new Vector3(
                NormalizeAngle(euler.x),
                NormalizeAngle(euler.y),
                NormalizeAngle(euler.z));
        }
    }
}
