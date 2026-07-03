using System;
using System.Collections;
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

        public static QuestFirstViewRuntimeRepair Instance { get; private set; }

        public Vector3 pilotEyeLocal = new Vector3(-0.34f, 0.86f, 1.1f);
        public bool followAircraft = true;
        public bool applyHeadPoseFromXrDevice = true;
        public float headPositionScale = 1f;

        public bool ManualHeadPoseApplied { get; private set; }
        public bool HeadDevicePoseValid { get; private set; }
        public bool HeadDeviceTracked { get; private set; }
        public float HeadYawDeltaDeg { get; private set; }
        public float HeadPitchDeltaDeg { get; private set; }
        public float HeadPositionDeltaMeters { get; private set; }

        private Camera _camera;
        private Transform _xrOrigin;
        private Transform _aircraft;
        private Vector3 _neutralCameraLocalPosition;
        private Quaternion _neutralCameraLocalRotation = Quaternion.identity;
        private Vector3 _headBaselinePosition;
        private Quaternion _headBaselineRotation = Quaternion.identity;
        private bool _headBaselineCaptured;
        private bool _ready;

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
            HidePilotSeatOccluders();
            EnsureC172ExteriorVisuals();
            EnsurePlaytestCockpitCues();
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
        }

        private void ApplyManualHeadPoseFromDevice()
        {
            ManualHeadPoseApplied = false;
            HeadDevicePoseValid = false;
            if (!applyHeadPoseFromXrDevice || _camera == null) return;
            if (!TryGetHeadPose(out Vector3 devicePosition, out Quaternion deviceRotation, out bool tracked)) return;
            HeadDevicePoseValid = true;

            if (!_headBaselineCaptured)
            {
                _headBaselinePosition = devicePosition;
                _headBaselineRotation = deviceRotation;
                _headBaselineCaptured = true;
            }

            Vector3 positionDelta = devicePosition - _headBaselinePosition;
            Quaternion rotationDelta = Quaternion.Inverse(_headBaselineRotation) * deviceRotation;

            _camera.transform.localPosition = _neutralCameraLocalPosition + positionDelta * headPositionScale;
            _camera.transform.localRotation = _neutralCameraLocalRotation * rotationDelta;

            Vector3 euler = rotationDelta.eulerAngles;
            HeadYawDeltaDeg = NormalizeAngle(euler.y);
            HeadPitchDeltaDeg = NormalizeAngle(euler.x);
            HeadPositionDeltaMeters = positionDelta.magnitude;
            HeadDeviceTracked = tracked;
            ManualHeadPoseApplied = true;
        }

        private void ApplyPilotSeatPose()
        {
            ComputeOriginPoseForTest(
                _aircraft,
                pilotEyeLocal,
                _neutralCameraLocalPosition,
                _neutralCameraLocalRotation,
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
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static bool TryGetHeadPose(out Vector3 position, out Quaternion rotation, out bool tracked)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            tracked = false;

            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return false;

            if (head.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
            {
                tracked = isTracked;
            }

            bool hasPosition = head.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool hasRotation = head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            return hasPosition || hasRotation;
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        private static string NameOrNull(UnityEngine.Object value)
        {
            return value != null ? value.name : "null";
        }
    }
}
