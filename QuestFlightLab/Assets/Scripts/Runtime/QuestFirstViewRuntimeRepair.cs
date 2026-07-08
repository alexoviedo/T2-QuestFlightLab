using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using QuestFlightLab.Aircraft;
using QuestFlightLab.Flight;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace QuestFlightLab.Runtime
{
    public class QuestFirstViewRuntimeRepair : MonoBehaviour
    {
        private const string LogPrefix = "[QuestFlightLab][FirstView]";
        public const string ImportedC172ResourcePath = "QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172";
        public static readonly Vector3 ImportedC172SeatReferenceLocal = PilotViewpointConfig.ImportedC172SeatReferenceLocal;
        public static readonly Vector3 ImportedC172DefaultPilotViewOffset = PilotViewpointConfig.ImportedC172DefaultPilotViewOffset;
        public static readonly Vector3 ImportedC172PilotEyeLocal = PilotViewpointConfig.ImportedC172DefaultPilotEyeLocal;
        public static readonly Vector3 ImportedC172LocalEuler = new Vector3(-90f, 0f, 0f);
        public static readonly Vector3 ImportedC172CockpitModelEye = new Vector3(-0.28f, -0.45f, 1.69f);
        public static readonly Vector3 ImportedC172CockpitModelEyeEuler = Vector3.zero;

        public static QuestFirstViewRuntimeRepair Instance { get; private set; }

        public Vector3 pilotEyeLocal = ImportedC172PilotEyeLocal;
        public Vector3 importedC172LocalPosition = Vector3.zero;
        public Vector3 importedC172LocalEuler = ImportedC172LocalEuler;
        public Vector3 importedC172LocalScale = Vector3.one;
        public Vector3 importedC172CockpitModelEye = ImportedC172CockpitModelEye;
        public Vector3 importedC172PilotViewOffset = Vector3.zero;
        public float importedC172CockpitYawDeg;
        public bool followAircraft = true;
        public bool applyHeadPoseFromXrDevice = true;
        public float headPositionScale = 1f;
        public float headPositionClampMeters = 0.45f;
        public float headPoseRecenterThresholdMeters = 0.85f;
        public float seatCalibrationSpeedMetersPerSecond = 0.22f;
        public float cockpitYawCalibrationSpeedDegPerSecond = 42f;
        public float seatCalibrationFineScale = 0.25f;

        public bool ImportedC172Loaded { get; private set; }
        public int ImportedExteriorRendererHiddenCount { get; private set; }
        public Vector3 ImportedC172BoundsSize { get; private set; }
        public Vector3 ImportedC172CockpitModelEyeUsed => importedC172CockpitModelEye;
        public Vector3 ImportedC172PilotViewOffsetUsed => importedC172PilotViewOffset;
        public Vector3 PilotEyeLocalUsed => pilotEyeLocal;
        public float ImportedC172CockpitYawDegUsed => importedC172CockpitYawDeg;
        public bool SeatCalibrationEnabled { get; private set; }
        public bool SeatCalibrationAdjustmentActive { get; private set; }
        public bool SeatCalibrationModeActive { get; private set; }
        public string SeatCalibrationCapturePath { get; private set; } = string.Empty;
        public string SeatCalibrationStatus { get; private set; } = string.Empty;
        public bool SavedSeatCalibrationLoaded { get; private set; }
        public string SavedSeatCalibrationPath { get; private set; } = string.Empty;
        public bool ManualHeadPoseApplied { get; private set; }
        public bool HeadDevicePoseValid { get; private set; }
        public bool HeadDeviceTracked { get; private set; }
        public bool HeadUserPresent { get; private set; }
        public float HeadYawDeltaDeg { get; private set; }
        public float HeadPitchDeltaDeg { get; private set; }
        public float HeadPositionDeltaMeters { get; private set; }
        public float HeadAppliedPositionDeltaMeters { get; private set; }
        public int HeadBaselineRecaptureCount { get; private set; }
        public string HeadBaselineStatus { get; private set; } = string.Empty;
        public string HeadPoseMode => applyHeadPoseFromXrDevice ? "manual_device_override" : "native_xr_camera";

        private Camera _camera;
        private Transform _xrOrigin;
        private Transform _aircraft;
        private Vector3 _neutralCameraLocalPosition;
        private Quaternion _neutralCameraLocalRotation = Quaternion.identity;
        private Quaternion _seatForwardLocalRotation = Quaternion.identity;
        private Vector3 _headBaselinePosition;
        private Quaternion _headBaselineRotation = Quaternion.identity;
        private Quaternion _headBaselineYawRotation = Quaternion.identity;
        private bool _headBaselineCaptured;
        private bool _headWasTracked;
        private bool _headWasPresent;
        private bool _forceHeadBaselineRecapture;
        private string _pendingHeadBaselineReason = "requested";
        private bool _ready;
        private Transform _importedC172Visual;
        private bool _rightPrimaryWasDown;
        private bool _rightSecondaryWasDown;
        private bool _leftPrimaryWasDown;
        private float _nextSeatCalibrationLogTime;
        private Vector2 _lastLeftCalibrationAxis;
        private Vector2 _lastRightCalibrationAxis;
        private static readonly List<InputDevice> ControllerDeviceScratch = new List<InputDevice>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!QuestLaunchOptions.PlaytestHudEnabled()) return;
            if (FindFirstObjectByType<QuestFirstViewRuntimeRepair>() != null) return;

            GameObject go = new GameObject("Quest First View Runtime Repair");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestFirstViewRuntimeRepair>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Application.onBeforeRender += ApplyFramePose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyFramePose;
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            yield return null;
            ResolveSceneReferences();
            RepairXrOriginReferences();
            EnsureCameraClipPlanes();

            if (_camera == null || _xrOrigin == null || _aircraft == null)
            {
                Debug.LogWarning($"{LogPrefix} Pilot view repair incomplete. camera={NameOrNull(_camera)} origin={NameOrNull(_xrOrigin)} aircraft={NameOrNull(_aircraft)}");
                yield break;
            }

            CaptureNeutralCameraPose();
            ApplyLaunchOverrides();
            HidePilotSeatOccluders();
            if (!TryAddImportedC172Model())
            {
                EnsureC172ExteriorVisuals();
                EnsurePlaytestCockpitCues();
            }
            ApplyFramePose();
            _ready = true;
            Debug.Log($"{LogPrefix} Pilot view active. camera={_camera.name} origin={_xrOrigin.name} aircraft={_aircraft.name} pilotEyeLocal={pilotEyeLocal}");
        }

        private void LateUpdate()
        {
            if (!_ready) return;

            if (_aircraft == null)
            {
                ResolveSceneReferences();
            }

            UpdateSeatCalibration();
            ApplyFramePose();
        }

        private void ApplyFramePose()
        {
            if (_camera == null || _xrOrigin == null || _aircraft == null) return;

            ApplyManualHeadPoseFromDevice();

            if (followAircraft)
            {
                ApplyPilotSeatPose();
            }
        }

        private void ResolveSceneReferences()
        {
            if (_camera == null) _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (_xrOrigin == null) _xrOrigin = FindXrOriginTransform(_camera != null ? _camera.transform : null);

            if (_aircraft == null)
            {
                SimpleAircraftPhysics physics = FindFirstObjectByType<SimpleAircraftPhysics>();
                if (physics != null) _aircraft = physics.transform;
                if (_aircraft == null)
                {
                    AircraftState state = FindFirstObjectByType<AircraftState>();
                    if (state != null) _aircraft = state.transform;
                }
            }
        }

        private void RepairXrOriginReferences()
        {
            if (_camera == null || _xrOrigin == null) return;

            Type xrOriginType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (xrOriginType == null) return;

            Component origin = _xrOrigin.GetComponent(xrOriginType);
            if (origin == null) return;

            GameObject offset = _camera.transform.parent != null && _camera.transform.parent != _xrOrigin
                ? _camera.transform.parent.gameObject
                : _xrOrigin.gameObject;

            bool assignedCamera = SetMember(xrOriginType, origin, "Camera", _camera) ||
                                  SetMember(xrOriginType, origin, "m_Camera", _camera);
            bool assignedOffset = SetMember(xrOriginType, origin, "CameraFloorOffsetObject", offset) ||
                                  SetMember(xrOriginType, origin, "m_CameraFloorOffsetObject", offset);

            Debug.Log($"{LogPrefix} XR Origin reference repair camera={assignedCamera} offset={assignedOffset} offsetObject={offset.name}");
        }

        private void EnsureCameraClipPlanes()
        {
            if (_camera == null) return;
            _camera.nearClipPlane = Mathf.Min(_camera.nearClipPlane, 0.03f);
            _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, 4000f);
        }

        private void CaptureNeutralCameraPose()
        {
            _neutralCameraLocalPosition = _xrOrigin.InverseTransformPoint(_camera.transform.position);
            _neutralCameraLocalRotation = Quaternion.Inverse(_xrOrigin.rotation) * _camera.transform.rotation;
            _seatForwardLocalRotation = YawOnlyRotation(_neutralCameraLocalRotation);
            Debug.Log($"{LogPrefix} Captured pilot recenter pose. neutralLocalEuler={_neutralCameraLocalRotation.eulerAngles} seatForwardEuler={_seatForwardLocalRotation.eulerAngles}");
        }

        private void ApplyLaunchOverrides()
        {
            bool resetCalibration = QuestLaunchOptions.ResetSeatCalibrationRequested();
            if (resetCalibration)
            {
                DeleteSavedSeatCalibration();
            }

            applyHeadPoseFromXrDevice = QuestLaunchOptions.ReadBool(QuestLaunchOptions.ManualHeadPoseKey, applyHeadPoseFromXrDevice);

            bool hasExplicitCockpitEyeZ = QuestLaunchOptions.TryReadFloat(QuestLaunchOptions.CockpitEyeZKey, out float cockpitEyeZ);
            if (hasExplicitCockpitEyeZ)
            {
                importedC172CockpitModelEye.z = cockpitEyeZ;
            }

            Vector3 offset = importedC172PilotViewOffset;
            bool hasExplicitOffset = false;
            if (QuestLaunchOptions.TryReadFloat(QuestLaunchOptions.PilotViewOffsetXKey, out float offsetX))
            {
                offset.x = offsetX;
                hasExplicitOffset = true;
            }

            if (QuestLaunchOptions.TryReadFloat(QuestLaunchOptions.PilotViewOffsetYKey, out float offsetY))
            {
                offset.y = offsetY;
                hasExplicitOffset = true;
            }

            if (QuestLaunchOptions.TryReadFloat(QuestLaunchOptions.PilotViewOffsetZKey, out float offsetZ))
            {
                offset.z = offsetZ;
                hasExplicitOffset = true;
            }

            if (hasExplicitOffset)
            {
                importedC172PilotViewOffset = ClampPilotViewOffset(offset);
            }

            bool hasExplicitCockpitYaw = QuestLaunchOptions.TryReadFloat(QuestLaunchOptions.CockpitYawDegKey, out float cockpitYawDeg);
            if (hasExplicitCockpitYaw)
            {
                importedC172CockpitYawDeg = NormalizeAngle(cockpitYawDeg);
            }

            if (!resetCalibration && TryLoadSavedSeatCalibration(out CockpitViewpointCalibrationState savedCalibration))
            {
                if (!hasExplicitCockpitEyeZ)
                {
                    importedC172CockpitModelEye = savedCalibration.importedC172CockpitModelEye;
                }

                if (!hasExplicitOffset)
                {
                    importedC172PilotViewOffset = ClampPilotViewOffset(savedCalibration.importedC172PilotViewOffset);
                }

                if (!hasExplicitCockpitYaw)
                {
                    importedC172CockpitYawDeg = NormalizeAngle(savedCalibration.importedC172CockpitYawDeg);
                }

                SavedSeatCalibrationLoaded = true;
                SeatCalibrationStatus = "loaded saved";
            }

            ApplyPilotSeatCalibrationOffset();
            SeatCalibrationEnabled = QuestLaunchOptions.SeatCalibrationEnabled();
            if (resetCalibration)
            {
                SeatCalibrationStatus = "reset clean";
            }

            Debug.Log($"{LogPrefix} Imported C172 cockpit eye calibration={importedC172CockpitModelEye} seatReference={ImportedC172SeatReferenceLocal} defaultPilotOffset={ImportedC172DefaultPilotViewOffset} pilotViewOffset={importedC172PilotViewOffset} pilotEyeLocal={pilotEyeLocal} cockpitYawDeg={importedC172CockpitYawDeg:F1} seatCalibration={SeatCalibrationEnabled} headPoseMode={HeadPoseMode}");
        }

        private void ApplyManualHeadPoseFromDevice()
        {
            ManualHeadPoseApplied = false;
            HeadDevicePoseValid = false;
            if (_camera == null) return;
            if (!TryGetHeadPose(out Vector3 devicePosition, out Quaternion deviceRotation, out bool tracked, out bool userPresent)) return;
            HeadDevicePoseValid = true;
            HeadDeviceTracked = tracked;
            HeadUserPresent = userPresent;

            if (!tracked)
            {
                _headWasTracked = false;
                HeadBaselineStatus = "tracking lost";
                return;
            }

            if (!userPresent)
            {
                _headWasPresent = false;
                HeadBaselineStatus = "waiting for headset";
                return;
            }

            if (!_headBaselineCaptured || !_headWasTracked || !_headWasPresent || _forceHeadBaselineRecapture)
            {
                string reason = !_headBaselineCaptured
                    ? "initial tracked pose"
                    : !_headWasTracked
                        ? "tracking reacquired"
                        : !_headWasPresent
                            ? "headset worn"
                            : _pendingHeadBaselineReason;
                CaptureHeadBaseline(devicePosition, deviceRotation, reason);
            }

            Vector3 positionDelta = devicePosition - _headBaselinePosition;
            if (positionDelta.magnitude > Mathf.Max(headPoseRecenterThresholdMeters, 0.05f))
            {
                CaptureHeadBaseline(devicePosition, deviceRotation, $"large pose jump {positionDelta.magnitude:F2}m");
                positionDelta = Vector3.zero;
            }

            Quaternion rotationDelta = Quaternion.Inverse(_headBaselineYawRotation) * deviceRotation;
            Vector3 appliedPositionDelta = Vector3.ClampMagnitude(positionDelta, Mathf.Max(headPositionClampMeters, 0f));

            Vector3 euler = rotationDelta.eulerAngles;
            HeadYawDeltaDeg = NormalizeAngle(euler.y);
            HeadPitchDeltaDeg = NormalizeAngle(euler.x);
            HeadPositionDeltaMeters = positionDelta.magnitude;
            HeadAppliedPositionDeltaMeters = appliedPositionDelta.magnitude;
            _headWasTracked = true;
            _headWasPresent = true;

            if (!applyHeadPoseFromXrDevice) return;

            _camera.transform.localPosition = _neutralCameraLocalPosition + appliedPositionDelta * headPositionScale;
            _camera.transform.localRotation = _seatForwardLocalRotation * rotationDelta;
            ManualHeadPoseApplied = true;
        }

        private void RequestHeadBaselineRecapture(string reason)
        {
            _forceHeadBaselineRecapture = true;
            _pendingHeadBaselineReason = string.IsNullOrWhiteSpace(reason) ? "requested" : reason;
            HeadBaselineStatus = $"pending recenter: {_pendingHeadBaselineReason}";
        }

        private void RecenterHeadBaselineNowOrNext(string reason)
        {
            if (TryGetHeadPose(out Vector3 devicePosition, out Quaternion deviceRotation, out bool tracked, out bool userPresent) && tracked && userPresent)
            {
                CaptureHeadBaseline(devicePosition, deviceRotation, reason);
            }
            else
            {
                RequestHeadBaselineRecapture(reason);
            }
        }

        private void CaptureHeadBaseline(Vector3 devicePosition, Quaternion deviceRotation, string reason)
        {
            _headBaselinePosition = devicePosition;
            _headBaselineRotation = deviceRotation;
            _headBaselineYawRotation = YawOnlyRotation(deviceRotation);
            _headBaselineCaptured = true;
            _headWasTracked = true;
            _headWasPresent = true;
            _forceHeadBaselineRecapture = false;
            _pendingHeadBaselineReason = string.Empty;
            HeadBaselineRecaptureCount++;
            HeadBaselineStatus = $"recentered: {reason}";
            Debug.Log($"{LogPrefix} Head pose baseline recentered reason={reason} position={devicePosition} yaw={NormalizeAngle(_headBaselineYawRotation.eulerAngles.y):F1} count={HeadBaselineRecaptureCount}");
        }

        private void ApplyPilotSeatPose()
        {
            ComputeOriginPoseForTest(
                _aircraft,
                pilotEyeLocal,
                _neutralCameraLocalPosition,
                _seatForwardLocalRotation,
                out Vector3 originPosition,
                out Quaternion originRotation);

            _xrOrigin.SetPositionAndRotation(originPosition, originRotation);
        }

        public static void ComputeOriginPoseForTest(
            Transform aircraft,
            Vector3 pilotEyeLocal,
            Vector3 neutralCameraLocalPosition,
            Quaternion neutralCameraLocalRotation,
            out Vector3 originPosition,
            out Quaternion originRotation)
        {
            Quaternion aircraftRotation = aircraft != null ? aircraft.rotation : Quaternion.identity;
            Vector3 pilotWorld = aircraft != null ? aircraft.TransformPoint(pilotEyeLocal) : pilotEyeLocal;

            originRotation = aircraftRotation * Quaternion.Inverse(neutralCameraLocalRotation);
            originPosition = pilotWorld - originRotation * neutralCameraLocalPosition;
        }

        private void HidePilotSeatOccluders()
        {
            if (_aircraft == null) return;

            string[] occluderNames =
            {
                "Fuselage",
                "Nose",
                "CabinGlass",
                "LeftWing",
                "RightWing",
                "TailBoom",
                "VerticalStab",
                "HorizontalStab",
                "LeftAileron",
                "RightAileron",
                "LeftFlap",
                "RightFlap",
                "Elevator",
                "Rudder",
                "Control Indicators"
            };

            int hidden = 0;
            foreach (string childName in occluderNames)
            {
                Transform child = _aircraft.Find(childName);
                if (child == null) continue;

                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        hidden++;
                    }
                }
            }

            Transform oldCues = _aircraft.Find("Playtest Cockpit Cues");
            if (oldCues != null) oldCues.gameObject.SetActive(false);

            if (hidden > 0)
            {
                Debug.Log($"{LogPrefix} Hid {hidden} solid placeholder renderer(s) that blocked the pilot view.");
            }
        }

        private bool TryAddImportedC172Model()
        {
            if (_aircraft == null) return false;
            Transform existing = _aircraft.Find("Imported C172 Cockpit Interior Visual");
            if (existing != null)
            {
                _importedC172Visual = existing;
                ApplyImportedCockpitPose(_importedC172Visual);
                return true;
            }

            GameObject prefab = Resources.Load<GameObject>(ImportedC172ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"{LogPrefix} Imported C172 model not available at Resources/{ImportedC172ResourcePath}; using procedural fallback.");
                return false;
            }

            GameObject instance = Instantiate(prefab, _aircraft);
            instance.name = "Imported C172 Cockpit Interior Visual";
            _importedC172Visual = instance.transform;
            ApplyImportedCockpitPose(instance.transform);
            instance.transform.localScale = importedC172LocalScale;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            ConfigureImportedC172Materials(instance);
            ImportedExteriorRendererHiddenCount = HideImportedExteriorForCockpit(instance);
            ImportedC172Loaded = true;

            if (TryGetRendererBounds(instance, out Bounds bounds))
            {
                ImportedC172BoundsSize = bounds.size;
                Debug.Log($"{LogPrefix} Imported C172 model loaded. boundsCenter={bounds.center} boundsSize={bounds.size} resource={ImportedC172ResourcePath}");
            }
            else
            {
                Debug.Log($"{LogPrefix} Imported C172 model loaded without renderer bounds. resource={ImportedC172ResourcePath}");
            }

            return true;
        }

        private void ApplyImportedCockpitPose(Transform cockpit)
        {
            Quaternion modelRotation = Quaternion.Euler(importedC172LocalEuler);
            Quaternion cameraInModelRotation = Quaternion.Euler(ImportedC172CockpitModelEyeEuler);
            Quaternion modelInCameraRotation = Quaternion.Inverse(cameraInModelRotation) * modelRotation;

            Quaternion cockpitYaw = Quaternion.Euler(0f, importedC172CockpitYawDeg, 0f);
            cockpit.localRotation = cockpitYaw * modelInCameraRotation;
            Vector3 baseSeatTarget = ImportedC172SeatReferenceLocal + importedC172LocalPosition;
            cockpit.localPosition = baseSeatTarget - cockpit.localRotation * importedC172CockpitModelEye;
        }

        private void UpdateSeatCalibration()
        {
            if (!SeatCalibrationEnabled || _importedC172Visual == null)
            {
                SeatCalibrationAdjustmentActive = false;
                SeatCalibrationModeActive = false;
                return;
            }

            bool rightPrimaryPressed = ButtonDown(XRNode.RightHand, CommonUsages.primaryButton, ref _rightPrimaryWasDown);
            bool rightSecondaryPressed = ButtonDown(XRNode.RightHand, CommonUsages.secondaryButton, ref _rightSecondaryWasDown);
            bool leftPrimaryPressed = ButtonDown(XRNode.LeftHand, CommonUsages.primaryButton, ref _leftPrimaryWasDown);
            bool gripHeld = ButtonHeld(XRNode.LeftHand, CommonUsages.gripButton) ||
                            ButtonHeld(XRNode.RightHand, CommonUsages.gripButton);

            if (rightPrimaryPressed)
            {
                if (SeatCalibrationModeActive)
                {
                    CaptureSeatCalibration();
                    SeatCalibrationModeActive = false;
                    SeatCalibrationStatus = "saved";
                }
                else
                {
                    SeatCalibrationModeActive = true;
                    SeatCalibrationStatus = "seat adjust on";
                    RecenterHeadBaselineNowOrNext("seat adjustment opened");
                }
            }

            SeatCalibrationAdjustmentActive = SeatCalibrationModeActive || gripHeld;

            if (leftPrimaryPressed && SeatCalibrationAdjustmentActive)
            {
                RecenterHeadBaselineNowOrNext("user forward recenter");
                SeatCalibrationStatus = "forward recentered";
            }

            Vector3 pilotDelta = ReadPilotViewCalibrationDelta();
            float yawDelta = ReadCockpitYawCalibrationDelta();
            if (pilotDelta.sqrMagnitude > 0.0000001f)
            {
                importedC172PilotViewOffset = ClampPilotViewOffset(importedC172PilotViewOffset + pilotDelta);
                ApplyPilotSeatCalibrationOffset();
                SeatCalibrationStatus = "adjusting";
            }

            if (Mathf.Abs(yawDelta) > 0.0001f)
            {
                importedC172CockpitYawDeg = NormalizeAngle(importedC172CockpitYawDeg + yawDelta);
                ApplyImportedCockpitPose(_importedC172Visual);
                SeatCalibrationStatus = "adjusting";
            }

            if (rightSecondaryPressed && SeatCalibrationAdjustmentActive)
            {
                importedC172PilotViewOffset = Vector3.zero;
                importedC172CockpitYawDeg = 0f;
                ApplyPilotSeatCalibrationOffset();
                ApplyImportedCockpitPose(_importedC172Visual);
                RecenterHeadBaselineNowOrNext("seat calibration reset");
                Debug.Log($"{LogPrefix} Seat calibration reset. pilotViewOffset={importedC172PilotViewOffset} pilotEyeLocal={pilotEyeLocal}");
            }

            if (Time.unscaledTime >= _nextSeatCalibrationLogTime)
            {
                _nextSeatCalibrationLogTime = Time.unscaledTime + 1f;
                Debug.Log($"{LogPrefix} Seat calibration available modeActive={SeatCalibrationModeActive} adjustmentActive={SeatCalibrationAdjustmentActive} offset={importedC172PilotViewOffset} pilotEyeLocal={pilotEyeLocal} cockpitYawDeg={importedC172CockpitYawDeg:F1} leftAxis={_lastLeftCalibrationAxis} rightAxis={_lastRightCalibrationAxis} leftDevice={DeviceLabel(XRNode.LeftHand)} rightDevice={DeviceLabel(XRNode.RightHand)}");
            }
        }

        private void ApplyPilotSeatCalibrationOffset()
        {
            pilotEyeLocal = ImportedC172PilotEyeLocal + importedC172PilotViewOffset;
        }

        private Vector3 ReadPilotViewCalibrationDelta()
        {
            Vector2 leftAxis = ReadPrimary2DAxis(XRNode.LeftHand);
            Vector2 rightAxis = ReadPrimary2DAxis(XRNode.RightHand);

            leftAxis = ApplyDeadzone(leftAxis, 0.18f);
            rightAxis = ApplyDeadzone(rightAxis, 0.18f);
            _lastLeftCalibrationAxis = leftAxis;
            _lastRightCalibrationAxis = rightAxis;

            if (!SeatCalibrationAdjustmentActive) return Vector3.zero;
            if (leftAxis == Vector2.zero && rightAxis == Vector2.zero) return Vector3.zero;

            float speed = seatCalibrationSpeedMetersPerSecond;
            if (ButtonHeld(XRNode.LeftHand, CommonUsages.gripButton) &&
                ButtonHeld(XRNode.RightHand, CommonUsages.gripButton))
            {
                speed *= seatCalibrationFineScale;
            }

            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            return new Vector3(leftAxis.x, rightAxis.y, leftAxis.y) * speed * dt;
        }

        private float ReadCockpitYawCalibrationDelta()
        {
            if (!SeatCalibrationAdjustmentActive) return 0f;
            float yawAxis = _lastRightCalibrationAxis.x;
            if (Mathf.Abs(yawAxis) < 0.001f) return 0f;

            float speed = cockpitYawCalibrationSpeedDegPerSecond;
            if (ButtonHeld(XRNode.LeftHand, CommonUsages.gripButton) &&
                ButtonHeld(XRNode.RightHand, CommonUsages.gripButton))
            {
                speed *= seatCalibrationFineScale;
            }

            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            return yawAxis * speed * dt;
        }

        private void CaptureSeatCalibration()
        {
            try
            {
                CockpitViewpointCalibrationState evidence = new CockpitViewpointCalibrationState
                {
                    schemaVersion = CockpitViewpointPersistence.SchemaVersion,
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    sceneryMode = QuestLaunchOptions.SceneryMode(),
                    demoMode = QuestLaunchOptions.DemoMode(),
                    importedC172SeatReferenceLocal = ImportedC172SeatReferenceLocal,
                    importedC172DefaultPilotViewOffset = ImportedC172DefaultPilotViewOffset,
                    importedC172CockpitModelEye = importedC172CockpitModelEye,
                    importedC172PilotViewOffset = importedC172PilotViewOffset,
                    importedC172CockpitYawDeg = importedC172CockpitYawDeg,
                    pilotEyeLocal = pilotEyeLocal,
                    importedC172LocalPosition = importedC172LocalPosition,
                    instructions = "Left stick adjusts the pilot seat left/right and forward/back. Right stick up/down adjusts pilot seat height. Right stick left/right rotates cockpit yaw. A saves. B resets. Hold grip for fine adjustment."
                };

                string currentPath = CockpitViewpointPersistence.SaveCurrent(evidence);
                SeatCalibrationCapturePath = currentPath;
                SavedSeatCalibrationPath = currentPath;
                SavedSeatCalibrationLoaded = true;
                SeatCalibrationStatus = "saved";
                RecenterHeadBaselineNowOrNext("seat calibration saved");
                Debug.Log($"{LogPrefix} Seat calibration captured current={currentPath} cockpitModelEye={importedC172CockpitModelEye} pilotViewOffset={importedC172PilotViewOffset} pilotEyeLocal={pilotEyeLocal} cockpitYawDeg={importedC172CockpitYawDeg:F1}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Seat calibration capture failed: {ex.Message}");
                SeatCalibrationStatus = "save failed";
            }
        }

        private bool TryLoadSavedSeatCalibration(out CockpitViewpointCalibrationState evidence)
        {
            if (CockpitViewpointPersistence.TryLoadCurrent(out evidence, out string path, out string error))
            {
                SavedSeatCalibrationPath = path;
                Debug.Log($"{LogPrefix} Loaded saved seat calibration path={path} cockpitModelEye={evidence.importedC172CockpitModelEye} pilotViewOffset={evidence.importedC172PilotViewOffset} pilotEyeLocal={evidence.pilotEyeLocal} cockpitYawDeg={evidence.importedC172CockpitYawDeg:F1}");
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning($"{LogPrefix} Saved seat calibration load failed from {path}: {error}");
                SeatCalibrationStatus = error.StartsWith("old schema", StringComparison.OrdinalIgnoreCase) ? "old save ignored" : "load failed";
            }

            return false;
        }

        private void DeleteSavedSeatCalibration()
        {
            if (CockpitViewpointPersistence.DeleteCurrent(out string currentPath, out string error))
            {
                Debug.Log($"{LogPrefix} Deleted saved seat calibration {currentPath}");
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning($"{LogPrefix} Saved seat calibration delete failed: {error}");
            }

            SavedSeatCalibrationLoaded = false;
            SavedSeatCalibrationPath = string.Empty;
            SeatCalibrationCapturePath = string.Empty;
        }

        private static Vector3 ClampPilotViewOffset(Vector3 offset)
        {
            return new Vector3(
                Mathf.Clamp(offset.x, -0.8f, 0.8f),
                Mathf.Clamp(offset.y, -0.8f, 0.8f),
                Mathf.Clamp(offset.z, -1.2f, 1.2f));
        }

        private static Vector2 ReadPrimary2DAxis(XRNode node)
        {
            if (!TryGetControllerDevice(node, out InputDevice device)) return Vector2.zero;
            if (!device.isValid) return Vector2.zero;
            return device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value) ? value : Vector2.zero;
        }

        private static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
        {
            return value.magnitude < deadzone ? Vector2.zero : value;
        }

        private static bool ButtonHeld(XRNode node, InputFeatureUsage<bool> usage)
        {
            if (!TryGetControllerDevice(node, out InputDevice device)) return false;
            return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        }

        private static bool ButtonDown(XRNode node, InputFeatureUsage<bool> usage, ref bool wasDown)
        {
            bool pressed = ButtonHeld(node, usage);
            bool down = pressed && !wasDown;
            wasDown = pressed;
            return down;
        }

        private static string DeviceLabel(XRNode node)
        {
            if (!TryGetControllerDevice(node, out InputDevice device)) return "invalid";
            if (!device.isValid) return "invalid";
            return $"{device.name}/{device.characteristics}";
        }

        private static bool TryGetControllerDevice(XRNode node, out InputDevice device)
        {
            device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) return true;

            InputDeviceCharacteristics hand = node == XRNode.LeftHand
                ? InputDeviceCharacteristics.Left
                : InputDeviceCharacteristics.Right;

            ControllerDeviceScratch.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | hand,
                ControllerDeviceScratch);

            for (int i = 0; i < ControllerDeviceScratch.Count; i++)
            {
                if (!ControllerDeviceScratch[i].isValid) continue;
                device = ControllerDeviceScratch[i];
                return true;
            }

            ControllerDeviceScratch.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | hand,
                ControllerDeviceScratch);

            for (int i = 0; i < ControllerDeviceScratch.Count; i++)
            {
                if (!ControllerDeviceScratch[i].isValid) continue;
                device = ControllerDeviceScratch[i];
                return true;
            }

            return false;
        }

        private static int HideImportedExteriorForCockpit(GameObject root)
        {
            int hidden = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled) continue;

                string path = PathFor(renderer.transform);
                if (!path.Contains("Cessna_Exterior_", StringComparison.OrdinalIgnoreCase)) continue;

                renderer.enabled = false;
                hidden++;
            }

            if (hidden > 0)
            {
                Debug.Log($"{LogPrefix} Hid {hidden} imported exterior renderer(s) for first-person cockpit view.");
            }

            return hidden;
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

                    string materialName = material.name.ToLowerInvariant();
                    if (!materialName.Contains("glass")) continue;

                    Color color = material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : material.color;

                    float brightness = Mathf.Max(color.r, color.g, color.b);
                    if (brightness < 0.08f)
                    {
                        color = new Color(0.55f, 0.82f, 0.96f, color.a);
                    }

                    color.a = Mathf.Min(color.a <= 0f ? 0.18f : color.a, 0.22f);
                    SetTransparentMaterial(material, color);
                    changed = true;
                }

                if (changed) renderer.materials = materials;
            }
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

        private void EnsureC172ExteriorVisuals()
        {
            if (_aircraft == null) return;
            if (_aircraft.Find("C172 Playtest Exterior Visuals") != null) return;

            GameObject root = new GameObject("C172 Playtest Exterior Visuals");
            root.transform.SetParent(_aircraft, false);

            Material paint = Material("C172 Exterior Warm White", new Color(0.88f, 0.9f, 0.84f));
            Material lowerPaint = Material("C172 Exterior Lower Blue", new Color(0.08f, 0.18f, 0.34f));
            Material trim = Material("C172 Exterior Trim Gray", new Color(0.46f, 0.49f, 0.5f));
            Material rubber = Material("C172 Tire Rubber", new Color(0.012f, 0.013f, 0.014f));
            Material metal = Material("C172 Brushed Metal", new Color(0.5f, 0.52f, 0.5f));
            Material glass = TransparentMaterial("C172 Transparent Cabin Glass", new Color(0.45f, 0.72f, 0.86f, 0.24f));
            Material surface = Material("C172 Control Surface White", new Color(0.82f, 0.84f, 0.8f));

            // Approximate C172 proportions: high wing, fixed tricycle gear, box cabin, rounded nose, and tapered tail.
            Capsule(root.transform, "C172RoundedFuselage", new Vector3(0f, 0.04f, 0.08f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.58f, 3.35f, 0.52f), paint);
            Cube(root.transform, "C172CabinRoofSkin", new Vector3(0f, 0.98f, 1.0f), Quaternion.identity, new Vector3(1.16f, 0.16f, 1.48f), paint);
            Cube(root.transform, "C172CabinBellySkin", new Vector3(0f, 0.16f, 1.0f), Quaternion.identity, new Vector3(1.16f, 0.18f, 1.48f), paint);
            Cube(root.transform, "C172LeftLowerDoorSkin", new Vector3(-0.59f, 0.42f, 1.02f), Quaternion.identity, new Vector3(0.04f, 0.22f, 1.32f), paint);
            Cube(root.transform, "C172RightLowerDoorSkin", new Vector3(0.59f, 0.42f, 1.02f), Quaternion.identity, new Vector3(0.04f, 0.22f, 1.32f), paint);
            Cube(root.transform, "C172CabinAftFrame", new Vector3(0f, 0.58f, 0.3f), Quaternion.identity, new Vector3(1.1f, 0.78f, 0.04f), trim);
            Cube(root.transform, "C172CabinForwardFrame", new Vector3(0f, 0.58f, 1.73f), Quaternion.identity, new Vector3(1.0f, 0.68f, 0.04f), trim);
            Capsule(root.transform, "C172NoseCowling", new Vector3(0f, 0.03f, 3.85f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.46f, 0.72f, 0.42f), paint);
            Capsule(root.transform, "C172TailCone", new Vector3(0f, 0.08f, -3.05f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.34f, 1.14f, 0.3f), paint);
            Cube(root.transform, "C172BlueBellyStripe", new Vector3(0f, -0.31f, 0.35f), Quaternion.identity, new Vector3(1.03f, 0.08f, 4.85f), lowerPaint);
            Cube(root.transform, "C172BlueTailStripe", new Vector3(0f, 0.13f, -2.65f), Quaternion.identity, new Vector3(0.72f, 0.08f, 1.75f), lowerPaint);

            Cube(root.transform, "C172WindshieldTransparent", new Vector3(0f, 0.78f, 1.86f), Quaternion.Euler(-17f, 0f, 0f), new Vector3(0.96f, 0.42f, 0.026f), glass);
            Cube(root.transform, "C172LeftSideWindowTransparent", new Vector3(-0.59f, 0.72f, 1.02f), Quaternion.identity, new Vector3(0.026f, 0.44f, 0.7f), glass);
            Cube(root.transform, "C172RightSideWindowTransparent", new Vector3(0.59f, 0.72f, 1.02f), Quaternion.identity, new Vector3(0.026f, 0.44f, 0.7f), glass);
            Cube(root.transform, "C172RearSideWindowTransparent", new Vector3(0f, 0.74f, 0.27f), Quaternion.identity, new Vector3(1.08f, 0.34f, 0.026f), glass);
            Cube(root.transform, "C172CabinDoorFrameLeft", new Vector3(-0.62f, 0.51f, 0.95f), Quaternion.identity, new Vector3(0.035f, 0.78f, 1.06f), trim);
            Cube(root.transform, "C172CabinDoorFrameRight", new Vector3(0.62f, 0.51f, 0.95f), Quaternion.identity, new Vector3(0.035f, 0.78f, 1.06f), trim);

            Cube(root.transform, "C172HighWingCenterSection", new Vector3(0f, 1.24f, 0.42f), Quaternion.identity, new Vector3(1.52f, 0.12f, 1.24f), paint);
            Cube(root.transform, "C172LeftHighWing", new Vector3(-3.98f, 1.24f, 0.42f), Quaternion.identity, new Vector3(6.96f, 0.1f, 1.22f), paint);
            Cube(root.transform, "C172RightHighWing", new Vector3(3.98f, 1.24f, 0.42f), Quaternion.identity, new Vector3(6.96f, 0.1f, 1.22f), paint);
            Cube(root.transform, "C172LeftWingBlueTip", new Vector3(-7.55f, 1.23f, 0.42f), Quaternion.identity, new Vector3(0.24f, 0.12f, 1.18f), lowerPaint);
            Cube(root.transform, "C172RightWingBlueTip", new Vector3(7.55f, 1.23f, 0.42f), Quaternion.identity, new Vector3(0.24f, 0.12f, 1.18f), lowerPaint);
            CubeBetween(root.transform, "C172LeftWingStrut", new Vector3(-0.52f, 0.05f, 1.0f), new Vector3(-3.3f, 1.12f, 0.26f), 0.055f, metal);
            CubeBetween(root.transform, "C172RightWingStrut", new Vector3(0.52f, 0.05f, 1.0f), new Vector3(3.3f, 1.12f, 0.26f), 0.055f, metal);

            Transform leftAileron = Cube(root.transform, "C172LeftAileronAnimated", new Vector3(-5.78f, 1.17f, -0.24f), Quaternion.identity, new Vector3(2.5f, 0.06f, 0.26f), surface).transform;
            Transform rightAileron = Cube(root.transform, "C172RightAileronAnimated", new Vector3(5.78f, 1.17f, -0.24f), Quaternion.identity, new Vector3(2.5f, 0.06f, 0.26f), surface).transform;
            Transform leftFlap = Cube(root.transform, "C172LeftFlapAnimated", new Vector3(-2.65f, 1.16f, -0.27f), Quaternion.identity, new Vector3(2.3f, 0.06f, 0.3f), surface).transform;
            Transform rightFlap = Cube(root.transform, "C172RightFlapAnimated", new Vector3(2.65f, 1.16f, -0.27f), Quaternion.identity, new Vector3(2.3f, 0.06f, 0.3f), surface).transform;
            Cube(root.transform, "C172VerticalStabilizer", new Vector3(0f, 0.95f, -3.72f), Quaternion.Euler(0f, 0f, 0f), new Vector3(0.14f, 1.25f, 0.95f), paint);
            Cube(root.transform, "C172HorizontalStabilizer", new Vector3(0f, 0.44f, -3.92f), Quaternion.identity, new Vector3(3.2f, 0.08f, 0.78f), paint);
            Transform elevator = Cube(root.transform, "C172ElevatorAnimated", new Vector3(0f, 0.41f, -4.35f), Quaternion.identity, new Vector3(2.92f, 0.06f, 0.22f), surface).transform;
            Transform rudder = Cube(root.transform, "C172RudderAnimated", new Vector3(0f, 0.93f, -4.22f), Quaternion.identity, new Vector3(0.1f, 0.84f, 0.24f), surface).transform;

            Cylinder(root.transform, "C172LeftMainWheel", new Vector3(-0.78f, -0.58f, 0.48f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.22f, 0.08f, 0.22f), rubber);
            Cylinder(root.transform, "C172RightMainWheel", new Vector3(0.78f, -0.58f, 0.48f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.22f, 0.08f, 0.22f), rubber);
            Cylinder(root.transform, "C172NoseWheel", new Vector3(0f, -0.56f, 3.16f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.18f, 0.07f, 0.18f), rubber);
            CubeBetween(root.transform, "C172LeftMainGear", new Vector3(-0.28f, -0.08f, 0.42f), new Vector3(-0.78f, -0.5f, 0.48f), 0.045f, metal);
            CubeBetween(root.transform, "C172RightMainGear", new Vector3(0.28f, -0.08f, 0.42f), new Vector3(0.78f, -0.5f, 0.48f), 0.045f, metal);
            CubeBetween(root.transform, "C172NoseGear", new Vector3(0f, -0.06f, 3.08f), new Vector3(0f, -0.49f, 3.16f), 0.04f, metal);
            Sphere(root.transform, "C172Spinner", new Vector3(0f, 0.03f, 4.62f), Quaternion.identity, new Vector3(0.22f, 0.22f, 0.22f), lowerPaint);
            Cube(root.transform, "C172PropBladeVertical", new Vector3(0f, 0.03f, 4.7f), Quaternion.identity, new Vector3(0.07f, 1.26f, 0.035f), rubber);
            Cube(root.transform, "C172PropBladeHorizontal", new Vector3(0f, 0.03f, 4.705f), Quaternion.identity, new Vector3(1.26f, 0.07f, 0.035f), rubber);

            ControlSurfaceAnimator animator = _aircraft.GetComponent<ControlSurfaceAnimator>();
            if (animator != null)
            {
                animator.leftAileron = leftAileron;
                animator.rightAileron = rightAileron;
                animator.leftFlap = leftFlap;
                animator.rightFlap = rightFlap;
                animator.elevator = elevator;
                animator.rudder = rudder;
            }

            Debug.Log($"{LogPrefix} Added C172-style high-wing exterior visuals with transparent cabin glass and animated control surfaces.");
        }

        private void EnsurePlaytestCockpitCues()
        {
            if (_xrOrigin == null || _camera == null) return;
            if (_xrOrigin.Find("Playtest Cockpit Frame") != null) return;

            GameObject root = new GameObject("Playtest Cockpit Frame");
            root.transform.SetParent(_xrOrigin, false);

            Material panel = Material("C172 Panel Dark", new Color(0.018f, 0.02f, 0.022f));
            Material panelTrim = Material("C172 Panel Trim", new Color(0.11f, 0.115f, 0.12f));
            Material paint = Material("C172 Interior Warm White", new Color(0.82f, 0.84f, 0.78f));
            Material vinyl = Material("C172 Seat Vinyl", new Color(0.17f, 0.19f, 0.19f));
            Material carpet = Material("C172 Floor Rubber", new Color(0.035f, 0.038f, 0.04f));
            Material leather = Material("C172 Control Yoke Black", new Color(0.015f, 0.016f, 0.018f));
            Material redKnob = Material("C172 Mixture Red Knob", new Color(0.62f, 0.06f, 0.035f));
            Material blackKnob = Material("C172 Throttle Black Knob", new Color(0.025f, 0.025f, 0.025f));
            Material whiteKnob = Material("C172 Carb Heat White Knob", new Color(0.86f, 0.82f, 0.7f));
            Material instrument = Material("C172 Instrument Face", new Color(0.005f, 0.007f, 0.01f));
            Material glassTint = TransparentMaterial("C172 Cockpit Transparent Window Glass", new Color(0.48f, 0.78f, 0.92f, 0.18f));
            Material gaugeNeedle = Material("C172 Gauge Needle", new Color(0.85f, 0.68f, 0.18f));
            Material placard = Material("C172 Placard White", new Color(0.8f, 0.83f, 0.78f));

            const float centerlineX = 0.34f;
            Vector3 seat = _neutralCameraLocalPosition;

            Cube(root.transform, "C172PilotSeatCushion", seat + new Vector3(0f, -1.03f, -0.05f), Quaternion.identity, new Vector3(0.48f, 0.12f, 0.58f), vinyl);
            Cube(root.transform, "C172PilotSeatBack", seat + new Vector3(0f, -0.67f, -0.37f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.5f, 0.72f, 0.12f), vinyl);
            Cube(root.transform, "C172CoPilotSeatCushion", seat + new Vector3(0.7f, -1.03f, -0.05f), Quaternion.identity, new Vector3(0.48f, 0.12f, 0.58f), vinyl);
            Cube(root.transform, "C172CoPilotSeatBack", seat + new Vector3(0.7f, -0.67f, -0.37f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.5f, 0.72f, 0.12f), vinyl);
            Cube(root.transform, "C172CabinFloor", seat + new Vector3(0.34f, -1.13f, 0.68f), Quaternion.identity, new Vector3(1.52f, 0.04f, 1.75f), carpet);

            Cube(root.transform, "C172InstrumentPanel", seat + new Vector3(centerlineX, -0.28f, 0.78f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(1.56f, 0.58f, 0.08f), panel);
            Cube(root.transform, "C172LowerPanelKickPlate", seat + new Vector3(centerlineX, -0.61f, 0.68f), Quaternion.Euler(-4f, 0f, 0f), new Vector3(1.5f, 0.22f, 0.08f), panelTrim);
            Cube(root.transform, "C172GlareShield", seat + new Vector3(centerlineX, -0.06f, 0.98f), Quaternion.Euler(-5f, 0f, 0f), new Vector3(1.62f, 0.1f, 0.4f), panel);
            Cube(root.transform, "C172CompassPod", seat + new Vector3(centerlineX, 0.1f, 0.9f), Quaternion.identity, new Vector3(0.16f, 0.12f, 0.1f), panelTrim);

            Cube(root.transform, "C172CowlingReference", seat + new Vector3(centerlineX, -0.98f, 2.72f), Quaternion.identity, new Vector3(0.7f, 0.07f, 1.42f), paint);
            Cube(root.transform, "C172CowlingLeftCurveApprox", seat + new Vector3(centerlineX - 0.3f, -0.91f, 2.18f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.24f, 0.055f, 0.55f), paint);
            Cube(root.transform, "C172CowlingRightCurveApprox", seat + new Vector3(centerlineX + 0.3f, -0.91f, 2.18f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.24f, 0.055f, 0.55f), paint);

            Cube(root.transform, "C172WindshieldTopBow", seat + new Vector3(centerlineX, 0.26f, 1.02f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(1.56f, 0.06f, 0.08f), panelTrim);
            Cube(root.transform, "C172WindshieldLowerBow", seat + new Vector3(centerlineX, -0.05f, 0.9f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(1.52f, 0.055f, 0.07f), panelTrim);
            Cube(root.transform, "C172WindshieldLeftPost", seat + new Vector3(-0.42f, 0.02f, 1.02f), Quaternion.Euler(-16f, 0f, -13f), new Vector3(0.065f, 0.78f, 0.065f), panelTrim);
            Cube(root.transform, "C172WindshieldRightPost", seat + new Vector3(1.1f, 0.02f, 1.02f), Quaternion.Euler(-16f, 0f, 13f), new Vector3(0.065f, 0.78f, 0.065f), panelTrim);
            Cube(root.transform, "C172WindshieldCenterPost", seat + new Vector3(centerlineX, 0.01f, 1.0f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(0.05f, 0.68f, 0.06f), panelTrim);
            Cube(root.transform, "C172WindshieldGlassLeft", seat + new Vector3(-0.06f, 0.0f, 1.07f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(0.72f, 0.5f, 0.018f), glassTint);
            Cube(root.transform, "C172WindshieldGlassRight", seat + new Vector3(0.74f, 0.0f, 1.07f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(0.72f, 0.5f, 0.018f), glassTint);
            Cube(root.transform, "C172LeftDoorWindowGlass", seat + new Vector3(-0.51f, -0.12f, 0.88f), Quaternion.Euler(0f, 2f, 0f), new Vector3(0.018f, 0.48f, 0.74f), glassTint);
            Cube(root.transform, "C172RightDoorWindowGlass", seat + new Vector3(1.19f, -0.12f, 0.88f), Quaternion.Euler(0f, -2f, 0f), new Vector3(0.018f, 0.48f, 0.74f), glassTint);
            Cube(root.transform, "C172LeftDoorFrame", seat + new Vector3(-0.54f, -0.22f, 0.88f), Quaternion.identity, new Vector3(0.045f, 0.72f, 0.96f), panelTrim);
            Cube(root.transform, "C172RightDoorFrame", seat + new Vector3(1.22f, -0.22f, 0.88f), Quaternion.identity, new Vector3(0.045f, 0.72f, 0.96f), panelTrim);
            Cube(root.transform, "C172HighWingOverheadCue", seat + new Vector3(centerlineX, 0.34f, 0.5f), Quaternion.identity, new Vector3(1.78f, 0.08f, 0.72f), paint);

            AddGaugeAccents(root.transform, seat + new Vector3(-0.05f, 0f, 0f), instrument, panelTrim, gaugeNeedle);
            AddRadioStack(root.transform, seat, panelTrim, placard);
            AddSwitchRow(root.transform, seat, panelTrim, placard);

            GameObject yokeOrigin = new GameObject("C172PilotYokeAnimationOrigin");
            yokeOrigin.transform.SetParent(root.transform, false);
            yokeOrigin.transform.localPosition = seat + new Vector3(0f, -0.15f, 0.78f);
            GameObject yoke = new GameObject("C172PilotYokeAnimated");
            yoke.transform.SetParent(yokeOrigin.transform, false);
            yoke.transform.localPosition = new Vector3(0f, -0.08f, 0.12f);
            AddYokeVisual(yoke.transform, leather, "Pilot");

            GameObject copilotYoke = new GameObject("C172CoPilotYokeVisual");
            copilotYoke.transform.SetParent(root.transform, false);
            copilotYoke.transform.localPosition = seat + new Vector3(0.68f, -0.22f, 0.9f);
            AddYokeVisual(copilotYoke.transform, leather, "CoPilot");
            Cylinder(root.transform, "C172CoPilotYokeColumn", seat + new Vector3(0.68f, -0.58f, 0.92f), Quaternion.Euler(68f, 0f, 0f), new Vector3(0.03f, 0.28f, 0.03f), leather);

            GameObject pedalOrigin = new GameObject("C172RudderPedalsAnimationOrigin");
            pedalOrigin.transform.SetParent(root.transform, false);
            pedalOrigin.transform.localPosition = seat + new Vector3(0f, -0.88f, 1.02f);
            GameObject pedalAnimated = new GameObject("C172RudderPedalsAnimated");
            pedalAnimated.transform.SetParent(pedalOrigin.transform, false);
            Cube(pedalAnimated.transform, "C172LeftRudderPedal", new Vector3(-0.15f, 0f, 0f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.16f, 0.05f, 0.12f), leather);
            Cube(pedalAnimated.transform, "C172RightRudderPedal", new Vector3(0.15f, 0f, 0f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.16f, 0.05f, 0.12f), leather);
            Transform leftBrake = Cube(pedalAnimated.transform, "C172LeftToeBrakeAnimated", new Vector3(-0.15f, 0.05f, 0.04f), Quaternion.identity, new Vector3(0.08f, 0.05f, 0.08f), leather).transform;
            Transform rightBrake = Cube(pedalAnimated.transform, "C172RightToeBrakeAnimated", new Vector3(0.15f, 0.05f, 0.04f), Quaternion.identity, new Vector3(0.08f, 0.05f, 0.08f), leather).transform;

            Cube(root.transform, "C172ThrottleQuadrant", seat + new Vector3(0.58f, -0.64f, 0.8f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.34f, 0.16f, 0.09f), panelTrim);
            Transform throttle = Cylinder(root.transform, "C172ThrottleAnimated", seat + new Vector3(0.49f, -0.53f, 0.76f), Quaternion.identity, new Vector3(0.045f, 0.09f, 0.045f), blackKnob).transform;
            Transform mixture = Cylinder(root.transform, "C172MixtureAnimated", seat + new Vector3(0.61f, -0.53f, 0.76f), Quaternion.identity, new Vector3(0.04f, 0.08f, 0.04f), redKnob).transform;
            Transform carbHeat = Cylinder(root.transform, "C172CarbHeatAnimated", seat + new Vector3(0.73f, -0.53f, 0.76f), Quaternion.identity, new Vector3(0.038f, 0.07f, 0.038f), whiteKnob).transform;
            Transform trimWheel = Cylinder(root.transform, "C172TrimWheelAnimated", seat + new Vector3(-0.48f, -0.66f, 0.82f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.04f, 0.12f), leather).transform;
            GameObject flapOrigin = new GameObject("C172FlapIndicatorAnimationOrigin");
            flapOrigin.transform.SetParent(root.transform, false);
            flapOrigin.transform.localPosition = seat + new Vector3(0.34f, -0.58f, 0.55f);
            Transform flapIndicator = Cube(flapOrigin.transform, "C172FlapIndicatorAnimated", new Vector3(-0.75f, -0.18f, 0.35f), Quaternion.identity, new Vector3(0.08f, 0.08f, 0.08f), placard).transform;

            ControlSurfaceAnimator animator = _aircraft != null ? _aircraft.GetComponent<ControlSurfaceAnimator>() : null;
            if (animator != null)
            {
                animator.yoke = yoke.transform;
                animator.rudderPedals = pedalAnimated.transform;
                animator.leftBrakeBar = leftBrake;
                animator.rightBrakeBar = rightBrake;
                animator.throttleLever = throttle;
                animator.mixtureLever = mixture;
                animator.carbHeatLever = carbHeat;
                animator.trimIndicator = trimWheel;
                animator.flapIndicator = flapIndicator;
            }

            Debug.Log($"{LogPrefix} Added C172-style left-seat cockpit with transparent windows, panel, seats, yokes, rudder pedals, and control knobs.");
        }

        private static void AddGaugeAccents(Transform parent, Vector3 seat, Material instrument, Material trim, Material needle)
        {
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float x = -0.39f + col * 0.26f;
                    float y = -0.22f - row * 0.15f;
                    Vector3 gaugePosition = seat + new Vector3(x, y, 0.7f);
                    Cylinder(parent, $"PlaytestInstrument_{row}_{col}", gaugePosition, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.074f, 0.015f, 0.074f), instrument);
                    Cylinder(parent, $"PlaytestInstrumentTrim_{row}_{col}", gaugePosition + new Vector3(0f, 0f, -0.006f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.086f, 0.008f, 0.086f), trim);
                    Cube(parent, $"PlaytestInstrumentNeedle_{row}_{col}", gaugePosition + new Vector3(0f, 0.004f, -0.022f), Quaternion.Euler(0f, 0f, -35f + col * 22f + row * 11f), new Vector3(0.01f, 0.055f, 0.006f), needle);
                }
            }
        }

        private static void AddRadioStack(Transform parent, Vector3 seat, Material trim, Material placard)
        {
            for (int i = 0; i < 4; i++)
            {
                Cube(parent, $"C172RadioStack_{i}", seat + new Vector3(0.58f, -0.18f - i * 0.095f, 0.7f), Quaternion.identity, new Vector3(0.28f, 0.05f, 0.025f), trim);
                Cube(parent, $"C172RadioDisplay_{i}", seat + new Vector3(0.58f, -0.18f - i * 0.095f, 0.675f), Quaternion.identity, new Vector3(0.18f, 0.024f, 0.012f), placard);
            }
        }

        private static void AddSwitchRow(Transform parent, Vector3 seat, Material trim, Material placard)
        {
            Cube(parent, "C172SwitchPanel", seat + new Vector3(0.1f, -0.52f, 0.66f), Quaternion.identity, new Vector3(0.62f, 0.05f, 0.028f), trim);
            for (int i = 0; i < 7; i++)
            {
                float x = -0.18f + i * 0.075f;
                Cube(parent, $"C172RockerSwitch_{i}", seat + new Vector3(x, -0.515f, 0.625f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.026f, 0.05f, 0.012f), placard);
            }
        }

        private static void AddYokeVisual(Transform yokeRoot, Material leather, string label)
        {
            Cylinder(yokeRoot, $"C172{label}YokeHub", Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.035f, 0.12f), leather);
            Torus(yokeRoot, $"C172{label}YokeWheel", Vector3.zero, Quaternion.identity, 0.215f, 0.02f, leather);
            Cube(yokeRoot, $"C172{label}YokeLeftGrip", new Vector3(-0.18f, 0f, 0f), Quaternion.identity, new Vector3(0.075f, 0.2f, 0.03f), leather);
            Cube(yokeRoot, $"C172{label}YokeRightGrip", new Vector3(0.18f, 0f, 0f), Quaternion.identity, new Vector3(0.075f, 0.2f, 0.03f), leather);
            Cube(yokeRoot, $"C172{label}YokeHorizontalSpoke", Vector3.zero, Quaternion.identity, new Vector3(0.32f, 0.022f, 0.022f), leather);
            Cube(yokeRoot, $"C172{label}YokeVerticalSpoke", new Vector3(0f, -0.08f, 0f), Quaternion.identity, new Vector3(0.025f, 0.17f, 0.022f), leather);
            Cylinder(yokeRoot, $"C172{label}YokeColumn", new Vector3(0f, -0.14f, 0.2f), Quaternion.Euler(62f, 0f, 0f), new Vector3(0.028f, 0.28f, 0.028f), leather);
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
            if (named != null) return named.transform;

            return cameraTransform != null && cameraTransform.parent != null
                ? cameraTransform.parent
                : cameraTransform;
        }

        private static bool SetMember(Type type, object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value);
                return true;
            }

            FieldInfo field = type.GetField(name, flags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return Cube(parent, name, localPosition, Quaternion.identity, localScale, material);
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            return Primitive(PrimitiveType.Cube, parent, name, localPosition, localRotation, localScale, material);
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            return Primitive(PrimitiveType.Cylinder, parent, name, localPosition, localRotation, localScale, material);
        }

        private static GameObject Capsule(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            return Primitive(PrimitiveType.Capsule, parent, name, localPosition, localRotation, localScale, material);
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            return Primitive(PrimitiveType.Sphere, parent, name, localPosition, localRotation, localScale, material);
        }

        private static GameObject CubeBetween(Transform parent, string name, Vector3 start, Vector3 end, float thickness, Material material)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            Quaternion rotation = length > 0.0001f
                ? Quaternion.LookRotation(delta.normalized, Vector3.up)
                : Quaternion.identity;

            return Cube(parent, name, start + delta * 0.5f, rotation, new Vector3(thickness, thickness, length), material);
        }

        private static GameObject Torus(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, float majorRadius, float minorRadius, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateTorusMesh(name + "Mesh", majorRadius, minorRadius, 36, 8);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        private static Mesh CreateTorusMesh(string name, float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            Vector3[] vertices = new Vector3[majorSegments * minorSegments];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[majorSegments * minorSegments * 6];

            for (int i = 0; i < majorSegments; i++)
            {
                float u = i / (float)majorSegments * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);

                for (int j = 0; j < minorSegments; j++)
                {
                    float v = j / (float)minorSegments * Mathf.PI * 2f;
                    Vector3 normal = radial * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                    int index = i * minorSegments + j;
                    vertices[index] = radial * majorRadius + normal * minorRadius;
                    normals[index] = normal.normalized;
                }
            }

            int t = 0;
            for (int i = 0; i < majorSegments; i++)
            {
                int nextI = (i + 1) % majorSegments;
                for (int j = 0; j < minorSegments; j++)
                {
                    int nextJ = (j + 1) % minorSegments;
                    int a = i * minorSegments + j;
                    int b = nextI * minorSegments + j;
                    int c = nextI * minorSegments + nextJ;
                    int d = i * minorSegments + nextJ;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject Primitive(PrimitiveType primitiveType, Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            return go;
        }

        private static Material Material(string name, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }

        private static Material TransparentMaterial(string name, Color color)
        {
            Material material = Material(name, color);
            SetTransparentMaterial(material, color);
            return material;
        }

        private static void SetTransparentMaterial(Material material, Color color)
        {
            Shader standard = Shader.Find("Standard");
            if (standard != null && material.shader != standard)
            {
                material.shader = standard;
            }

            material.color = color;
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static bool TryGetHeadPose(out Vector3 position, out Quaternion rotation, out bool tracked, out bool userPresent)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            tracked = false;
            userPresent = false;

            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return false;

            bool hasTrackedFeature = head.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked);
            if (hasTrackedFeature)
            {
                tracked = isTracked;
            }

            bool hasPosition = head.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool hasRotation = head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            if (!hasTrackedFeature && (hasPosition || hasRotation))
            {
                tracked = true;
            }

            bool hasUserPresence = head.TryGetFeatureValue(CommonUsages.userPresence, out userPresent);
            if (!hasUserPresence && (hasPosition || hasRotation))
            {
                userPresent = true;
            }

            return hasPosition || hasRotation;
        }

        private static Quaternion YawOnlyRotation(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        private static string NameOrNull(UnityEngine.Object value)
        {
            return value != null ? value.name : "null";
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
    }
}
