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
        public bool xrSettingsEnabled;
        public bool xrDeviceActive;
        public bool xrLoaderActive;
        public bool headDeviceValid;
        public bool headDeviceTracked;
        public bool manualHeadPoseApplied;
        public bool manualHeadDevicePoseValid;
        public bool manualHeadDeviceTracked;
        public float manualHeadYawDeltaDeg;
        public float manualHeadPitchDeltaDeg;
        public float manualHeadPositionDeltaMeters;
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

        public static FirstViewPlaytestDiagnostics Instance { get; private set; }

        public float maxSampleSeconds = 180f;
        public float writeIntervalSeconds = 5f;
        public string EvidencePath => _evidence != null ? _evidence.evidencePath : string.Empty;

        private Camera _camera;
        private Transform _xrOrigin;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation = Quaternion.identity;
        private float _maxYawAbs;
        private float _maxPitchAbs;
        private float _maxPositionDelta;
        private float _startTime;
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
                SamplePose(elapsed);
            }

            if (Time.unscaledTime >= _nextWriteTime)
            {
                WriteEvidence();
                _nextWriteTime = Time.unscaledTime + writeIntervalSeconds;
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
            RefreshEvidence(_evidence, elapsed);
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
                evidence.manualHeadYawDeltaDeg = Mathf.Abs(repair.HeadYawDeltaDeg);
                evidence.manualHeadPitchDeltaDeg = Mathf.Abs(repair.HeadPitchDeltaDeg);
                evidence.manualHeadPositionDeltaMeters = repair.HeadPositionDeltaMeters;
            }
            evidence.playtestHudActive = PlaytestHud.Instance != null && PlaytestHud.Instance.Root != null;
            evidence.hiddenVerbosePanelCount = PlaytestHud.Instance != null ? PlaytestHud.Instance.HiddenVerbosePanelCount : 0;
            evidence.hudLineCount = PlaytestHud.Instance != null ? PlaytestHud.Instance.VisibleLineCount : 0;
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

        private void WriteEvidence()
        {
            if (_evidence == null) return;
            RefreshEvidence(_evidence, Mathf.Max(0f, Time.unscaledTime - _startTime));

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

        private IEnumerator CaptureFirstViewScreenshots()
        {
            yield return new WaitForSecondsRealtime(2f);
            yield return new WaitForEndOfFrame();
            CaptureFirstViewScreenshot("startup");

            yield return new WaitForSecondsRealtime(5f);
            yield return new WaitForEndOfFrame();
            CaptureFirstViewScreenshot("demo");
            WriteEvidence();
        }

        private void CaptureFirstViewScreenshot(string label)
        {
            if (_evidence == null) return;

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
    }
}
