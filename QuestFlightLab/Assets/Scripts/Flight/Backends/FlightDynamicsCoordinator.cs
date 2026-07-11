using System;
using System.Collections;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Flight.Backends
{
    /// <summary>
    /// Selects exactly one authoritative backend, advances it at 120 Hz by
    /// default, and interpolates only the separate presentation root.
    /// </summary>
    public sealed class FlightDynamicsCoordinator : MonoBehaviour
    {
        [Header("Authority")]
        public FlightDynamicsBackendKind requestedBackend = FlightDynamicsBackendKind.UnityPrototype;
        public bool allowUnityFallback = true;
        public Transform simulationRoot;
        public Transform presentationRoot;
        public AircraftState aircraftState;
        public SimpleAircraftPhysics unityPrototype;
        public Rigidbody authoritativeRigidbody;
        public Usb2BleInputMapper controls;

        [Header("JSBSim")]
        public string jsbsimAircraft = "c172x";
        public string nativeDataRootOverride = string.Empty;
        public string editorSidecarScriptOverride = string.Empty;

        [Header("Fixed Step")]
        [Min(30f)] public float simulationRateHz = 120f;
        [Range(1, 16)] public int maxStepsPerFixedUpdate = 8;

        public FlightDynamicsBackendKind ActiveBackendKind => _backend?.Kind ?? requestedBackend;
        public bool IsInitialized => _backend != null && _backend.IsInitialized;
        public bool CoordinatorOwnsPresentationInterpolation => ShouldCoordinatorInterpolate(simulationRoot, presentationRoot);
        public string LastError { get; private set; } = string.Empty;
        public string FallbackReason { get; private set; } = string.Empty;
        public FlightDynamicsState CurrentState => _currentState;
        public double DroppedSimulationSeconds => _accumulator?.DroppedSeconds ?? 0.0;

        private IFlightDynamicsBackend _backend;
        private FlightDynamicsAuthorityLease _authorityLease;
        private FixedStepAccumulator _accumulator;
        private FlightDynamicsState _previousState;
        private FlightDynamicsState _currentState;
        private bool _prototypeWasEnabled;
        private bool _rigidbodyWasKinematic;
        private bool _ownsPrototypeDisable;
        private bool _ownsRigidbodyKinematic;
        private FlightBackendRuntimeEvidenceReporter _runtimeEvidence;
        private bool _allocationCounterAvailable = true;
        private Action<double> _stepBackendAction;

        private void Awake()
        {
            if (simulationRoot == null) simulationRoot = transform;
            if (presentationRoot == null) presentationRoot = simulationRoot;
            if (aircraftState == null) aircraftState = simulationRoot.GetComponent<AircraftState>();
            if (unityPrototype == null) unityPrototype = simulationRoot.GetComponent<SimpleAircraftPhysics>();
            if (authoritativeRigidbody == null) authoritativeRigidbody = simulationRoot.GetComponent<Rigidbody>();
            if (controls == null) controls = FindFirstObjectByType<Usb2BleInputMapper>();
            _runtimeEvidence = GetComponent<FlightBackendRuntimeEvidenceReporter>();
            if (_runtimeEvidence == null) _runtimeEvidence = gameObject.AddComponent<FlightBackendRuntimeEvidenceReporter>();
            _stepBackendAction = StepBackendUnchecked;
            EnforceSinglePresentationOwner();
        }

        private IEnumerator Start()
        {
            if (requestedBackend == FlightDynamicsBackendKind.JSBSimNative &&
                string.IsNullOrWhiteSpace(nativeDataRootOverride))
            {
                bool dataReady = false;
                string dataResult = string.Empty;
                yield return JSBSimRuntimeDataInstaller.EnsureReadableData((success, result) =>
                {
                    dataReady = success;
                    dataResult = result;
                });
                if (!dataReady)
                {
                    Debug.LogWarning($"[QuestFlightLab][FlightBackend] Native runtime data unavailable: {dataResult}");
                }
            }

            InitializeSelectedBackend();
        }

        public bool InitializeSelectedBackend()
        {
            ShutdownBackend();
            if (!FlightDynamicsAuthority.TryAcquire(simulationRoot, this, out _authorityLease, out string authorityError))
            {
                return Fail(authorityError);
            }

            double fixedStep = 1.0 / Mathf.Max(30f, simulationRateHz);
            _accumulator = new FixedStepAccumulator(fixedStep, maxStepsPerFixedUpdate);
            if (TryInitializeBackend(requestedBackend, fixedStep)) return true;

            string selectedError = LastError;
            if (allowUnityFallback && requestedBackend != FlightDynamicsBackendKind.UnityPrototype)
            {
                RestoreNonUnityPhysicsState();
                if (TryInitializeBackend(FlightDynamicsBackendKind.UnityPrototype, fixedStep))
                {
                    FallbackReason = selectedError;
                    _runtimeEvidence?.Configure(requestedBackend, _backend.Kind, FallbackReason, string.Empty);
                    Debug.LogWarning($"[QuestFlightLab][FlightBackend] {requestedBackend} unavailable ({selectedError}); using explicit Unity prototype fallback.");
                    return true;
                }
            }

            _authorityLease?.Dispose();
            _authorityLease = null;
            return Fail($"Could not initialize {requestedBackend}. {selectedError} {LastError}".Trim());
        }

        private bool TryInitializeBackend(FlightDynamicsBackendKind kind, double fixedStep)
        {
            PreparePhysicsFor(kind);
            _backend = CreateBackend(kind);
            FlightDynamicsBackendContext context = new FlightDynamicsBackendContext
            {
                simulationRoot = simulationRoot,
                aircraftState = aircraftState,
                unityPrototype = unityPrototype,
                localOrigin = GeodeticReference.Kbdu,
                jsbsimAircraft = jsbsimAircraft,
                jsbsimDataRoot = string.IsNullOrWhiteSpace(nativeDataRootOverride)
                    ? JSBSimRuntimeDataPaths.DefaultReadableDataRoot
                    : nativeDataRootOverride,
                sidecarScriptPath = editorSidecarScriptOverride,
                fixedDeltaTimeSeconds = fixedStep
            };
            if (!_backend.Initialize(context))
            {
                LastError = _backend.LastError;
                _backend.Dispose();
                _backend = null;
                return false;
            }

            _backend.SetAtmosphere(FlightDynamicsAtmosphere.Calm);
            if (!_backend.Reset(FlightDynamicsInitialConditions.KbduRunway()))
            {
                LastError = _backend.LastError;
                _backend.Dispose();
                _backend = null;
                return false;
            }

            _currentState = _backend.CurrentState;
            _previousState = _currentState;
            ApplyAuthoritativeState(_currentState);
            ApplyLegacyState(_currentState);
            LastError = string.Empty;
            FallbackReason = string.Empty;
            _runtimeEvidence?.Configure(requestedBackend, _backend.Kind, FallbackReason, LastError);
            Debug.Log($"[QuestFlightLab][FlightBackend] Authority={_backend.DisplayName}; fixed step={fixedStep * 1000.0:0.###} ms.");
            return true;
        }

        private void FixedUpdate()
        {
            if (!IsInitialized) return;
            if (controls != null && controls.ConsumeResetRequest())
            {
                _backend.Reset(FlightDynamicsInitialConditions.KbduRunway());
                _currentState = _backend.CurrentState;
                _previousState = _currentState;
                _accumulator.Reset();
                ApplyAuthoritativeState(_currentState);
                ApplyLegacyState(_currentState);
                return;
            }

            if (controls != null && controls.Paused) return;
            AircraftControlState controlState = controls != null ? controls.Current : AircraftControlState.Neutral();
            _backend.SetControls(controlState);
            _accumulator.Consume(Time.fixedDeltaTime, _stepBackendAction);
        }

        private void LateUpdate()
        {
            EnforceSinglePresentationOwner();
            if (!IsInitialized || !CoordinatorOwnsPresentationInterpolation) return;
            float alpha = (float)_accumulator.InterpolationAlpha;
            Vector3 position = Vector3.LerpUnclamped(_previousState.positionUnityMeters, _currentState.positionUnityMeters, alpha);
            Quaternion rotation = Quaternion.SlerpUnclamped(_previousState.rotationUnity, _currentState.rotationUnity, alpha);
            presentationRoot.SetPositionAndRotation(position, rotation);
        }

        public bool StepForTest(AircraftControlState controlState, double fixedDeltaTimeSeconds)
        {
            if (!IsInitialized) return false;
            _backend.SetControls(controlState);
            return StepBackend(fixedDeltaTimeSeconds);
        }

        private bool StepBackend(double fixedDeltaTimeSeconds)
        {
            _previousState = _currentState;
            long allocatedBefore = ReadAllocatedBytes();
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            bool advanced = _backend.Advance(fixedDeltaTimeSeconds);
            long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            long allocatedAfter = ReadAllocatedBytes();
            _runtimeEvidence?.RecordStep(
                (endTicks - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
                allocatedBefore >= 0 && allocatedAfter >= allocatedBefore ? allocatedAfter - allocatedBefore : 0,
                _allocationCounterAvailable);
            if (!advanced)
            {
                return Fail($"{_backend.DisplayName} failed: {_backend.LastError}");
            }

            _currentState = _backend.CurrentState;
            ApplyAuthoritativeState(_currentState);
            ApplyLegacyState(_currentState);
            return true;
        }

        private void StepBackendUnchecked(double fixedDeltaTimeSeconds)
        {
            StepBackend(fixedDeltaTimeSeconds);
        }

        private void ApplyAuthoritativeState(FlightDynamicsState state)
        {
            if (_backend.Kind != FlightDynamicsBackendKind.UnityPrototype)
            {
                simulationRoot.SetPositionAndRotation(state.positionUnityMeters, state.rotationUnity);
            }

            if (presentationRoot == simulationRoot)
            {
                presentationRoot.SetPositionAndRotation(state.positionUnityMeters, state.rotationUnity);
            }
        }

        private void ApplyLegacyState(FlightDynamicsState state)
        {
            if (aircraftState == null) return;
            aircraftState.velocityWorld = state.velocityUnityMetersPerSecond;
            aircraftState.angularVelocityDeg = state.angularVelocityBodyDegreesPerSecond;
            aircraftState.airspeedKts = (float)state.calibratedAirspeedKnots;
            aircraftState.altitudeFt = (float)(state.altitudeAglMeters * FlightFrameConversions.MetersToFeet);
            aircraftState.verticalSpeedFpm = (float)state.verticalSpeedFeetPerMinute;
            aircraftState.headingDeg = (float)state.headingDegrees;
            aircraftState.pitchDeg = (float)state.pitchDegrees;
            aircraftState.bankDeg = (float)state.bankDegrees;
            aircraftState.angleOfAttackDeg = (float)state.angleOfAttackDegrees;
            aircraftState.slipSkid = (float)state.sideslipDegrees;
            aircraftState.engineRpm = (float)state.engineRpm;
            aircraftState.loadFactorG = (float)state.loadFactorG;
            aircraftState.onGround = state.weightOnWheels;
        }

        private void PreparePhysicsFor(FlightDynamicsBackendKind kind)
        {
            if (kind == FlightDynamicsBackendKind.UnityPrototype) return;
            if (unityPrototype != null && !_ownsPrototypeDisable)
            {
                _prototypeWasEnabled = unityPrototype.enabled;
                unityPrototype.enabled = false;
                _ownsPrototypeDisable = true;
            }

            if (authoritativeRigidbody != null && !_ownsRigidbodyKinematic)
            {
                _rigidbodyWasKinematic = authoritativeRigidbody.isKinematic;
                authoritativeRigidbody.isKinematic = true;
                _ownsRigidbodyKinematic = true;
            }
        }

        private void RestoreNonUnityPhysicsState()
        {
            if (_ownsPrototypeDisable && unityPrototype != null) unityPrototype.enabled = _prototypeWasEnabled;
            if (_ownsRigidbodyKinematic && authoritativeRigidbody != null) authoritativeRigidbody.isKinematic = _rigidbodyWasKinematic;
            _ownsPrototypeDisable = false;
            _ownsRigidbodyKinematic = false;
        }

        private static IFlightDynamicsBackend CreateBackend(FlightDynamicsBackendKind kind)
        {
            return kind switch
            {
                FlightDynamicsBackendKind.JSBSimEditorSidecar => new JSBSimEditorSidecarBackend(),
                FlightDynamicsBackendKind.JSBSimNative => new JSBSimNativeFlightBackend(),
                _ => new UnityPrototypeFlightBackend()
            };
        }

        public static bool ShouldCoordinatorInterpolate(Transform candidateSimulationRoot, Transform candidatePresentationRoot)
        {
            if (candidateSimulationRoot == null || candidatePresentationRoot == null ||
                candidatePresentationRoot == candidateSimulationRoot)
            {
                return false;
            }

            AircraftReferenceFrameRig rig = candidateSimulationRoot.GetComponent<AircraftReferenceFrameRig>();
            return rig == null || !rig.enabled;
        }

        private void EnforceSinglePresentationOwner()
        {
            if (simulationRoot == null) return;
            AircraftReferenceFrameRig rig = simulationRoot.GetComponent<AircraftReferenceFrameRig>();
            if (rig == null || !rig.enabled) return;
            // AircraftReferenceFrameRig has the late presentation pass. Keeping
            // this coordinator on the simulation root prevents double writes to
            // AircraftVisualRoot even if the rig is created after our Awake.
            presentationRoot = simulationRoot;
        }

        private bool Fail(string message)
        {
            LastError = message;
            _runtimeEvidence?.RecordError(message);
            Debug.LogError($"[QuestFlightLab][FlightBackend] {message}");
            return false;
        }

        private long ReadAllocatedBytes()
        {
            if (!_allocationCounterAvailable) return -1;
            try
            {
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch (Exception)
            {
                // Some IL2CPP/runtime profiles expose the API at compile time
                // but throw a platform-specific exception when queried.
                _allocationCounterAvailable = false;
                return -1;
            }
        }

        private void OnDestroy()
        {
            ShutdownBackend();
        }

        private void ShutdownBackend()
        {
            _backend?.Dispose();
            _backend = null;
            RestoreNonUnityPhysicsState();
            _authorityLease?.Dispose();
            _authorityLease = null;
        }
    }
}
