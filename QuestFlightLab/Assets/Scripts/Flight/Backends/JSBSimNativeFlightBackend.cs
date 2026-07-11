using System;
using System.Runtime.InteropServices;
using QuestFlightLab.Runtime;

namespace QuestFlightLab.Flight.Backends
{
    /// <summary>
    /// Managed owner for the compact qfl_jsbsim_native C ABI. No C++ object or
    /// string crosses the boundary, and all state/control structs are blittable.
    /// </summary>
    public sealed class JSBSimNativeFlightBackend : IFlightDynamicsBackend
    {
        public const int ExpectedApiVersion = 1;
        public const string PinnedJsbsimVersion = "1.3.1";
        public const string PinnedJsbsimRevision = "3b25f25e49b42d0489c04ac805674fc1450ca579";

        private IntPtr _instance;
        private FlightDynamicsBackendContext _context;
        private NativeControls _controls;
        private NativeAtmosphere _atmosphere;
        private readonly bool _libraryAvailable;

        public JSBSimNativeFlightBackend()
        {
            _libraryAvailable = ProbeLibrary(out string error);
            LastError = error;
        }

        public FlightDynamicsBackendKind Kind => FlightDynamicsBackendKind.JSBSimNative;
        public string DisplayName => $"JSBSim {PinnedJsbsimVersion} native";
        public bool IsAvailable => _libraryAvailable;
        public bool IsInitialized => _instance != IntPtr.Zero;
        public string LastError { get; private set; } = string.Empty;
        public FlightDynamicsState CurrentState { get; private set; }

        public static bool ProbeLibrary(out string error)
        {
            try
            {
                int version = NativeMethods.ApiVersion();
                if (version != ExpectedApiVersion)
                {
                    error = $"Native JSBSim ABI version {version} does not match expected version {ExpectedApiVersion}.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is DllNotFoundException ||
                exception is EntryPointNotFoundException ||
                exception is BadImageFormatException)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool Initialize(FlightDynamicsBackendContext context)
        {
            if (!IsAvailable)
            {
                if (string.IsNullOrEmpty(LastError)) LastError = "Native JSBSim plugin is unavailable on this platform.";
                return false;
            }

            if (context == null || context.simulationRoot == null)
            {
                LastError = "Native JSBSim backend requires an aircraft simulation root.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.jsbsimDataRoot))
            {
                LastError = "Native JSBSim backend requires an extracted JSBSim data root.";
                return false;
            }

            Dispose();
            _context = context;
            _instance = NativeMethods.Create();
            if (_instance == IntPtr.Zero)
            {
                LastError = ReadLastError(IntPtr.Zero, "Could not create JSBSim native instance.");
                return false;
            }

            int loaded = NativeMethods.LoadAircraft(
                _instance,
                context.jsbsimDataRoot,
                string.IsNullOrWhiteSpace(context.jsbsimAircraft) ? "c172x" : context.jsbsimAircraft);
            if (loaded == 0)
            {
                LastError = ReadLastError(_instance, "Could not load JSBSim aircraft.");
                Dispose();
                return false;
            }

            LastError = string.Empty;
            return true;
        }

        public bool Reset(FlightDynamicsInitialConditions initialConditions)
        {
            if (!RequireInitialized()) return false;
            NativeInitialConditions native = new NativeInitialConditions
            {
                latitudeDegrees = initialConditions.latitudeDegrees,
                longitudeDegrees = initialConditions.longitudeDegrees,
                altitudeMslFeet = initialConditions.altitudeMslMeters * FlightFrameConversions.MetersToFeet,
                terrainElevationMslFeet = initialConditions.terrainElevationMslMeters * FlightFrameConversions.MetersToFeet,
                calibratedAirspeedKnots = initialConditions.calibratedAirspeedKnots,
                headingDegrees = initialConditions.headingDegrees,
                pitchDegrees = initialConditions.pitchDegrees,
                bankDegrees = initialConditions.bankDegrees,
                flightPathAngleDegrees = initialConditions.flightPathAngleDegrees,
                engineRunning = initialConditions.engineRunning ? 1 : 0
            };
            if (NativeMethods.Reset(_instance, ref native) == 0)
            {
                LastError = ReadLastError(_instance, "JSBSim reset failed.");
                return false;
            }

            NativeMethods.SetControls(_instance, ref _controls);
            NativeMethods.SetAtmosphere(_instance, ref _atmosphere);
            return CaptureState();
        }

        public void SetControls(AircraftControlState controls)
        {
            controls ??= AircraftControlState.Neutral();
            _controls = new NativeControls
            {
                aileron = Clamp(controls.aileron, -1.0, 1.0),
                // AircraftControlState and the pinned c172x FCS both use the
                // project convention that positive pitch input is nose-up.
                elevator = ContractPitchToJsbsim(controls.elevator),
                rudder = Clamp(controls.rudder, -1.0, 1.0),
                throttle = Clamp(controls.throttle, 0.0, 1.0),
                mixture = Clamp(controls.mixture, 0.0, 1.0),
                carbHeat = Clamp(controls.carbHeat, 0.0, 1.0),
                elevatorTrim = ContractPitchToJsbsim(controls.trim),
                flaps = Clamp(controls.flaps, 0.0, 1.0),
                leftBrake = Clamp(controls.leftToeBrake, 0.0, 1.0),
                rightBrake = Clamp(controls.rightToeBrake, 0.0, 1.0)
            };
            if (IsInitialized && NativeMethods.SetControls(_instance, ref _controls) == 0)
            {
                LastError = ReadLastError(_instance, "Could not set JSBSim controls.");
            }
        }

        public void SetAtmosphere(FlightDynamicsAtmosphere atmosphere)
        {
            _atmosphere = new NativeAtmosphere
            {
                windNorthFeetPerSecond = atmosphere.windNorthMetersPerSecond * FlightFrameConversions.MetersToFeet,
                windEastFeetPerSecond = atmosphere.windEastMetersPerSecond * FlightFrameConversions.MetersToFeet,
                windDownFeetPerSecond = atmosphere.windDownMetersPerSecond * FlightFrameConversions.MetersToFeet
            };
            if (IsInitialized && NativeMethods.SetAtmosphere(_instance, ref _atmosphere) == 0)
            {
                LastError = ReadLastError(_instance, "Could not set JSBSim atmosphere.");
            }
        }

        public bool Advance(double fixedDeltaTimeSeconds)
        {
            if (!RequireInitialized()) return false;
            if (fixedDeltaTimeSeconds <= 0.0)
            {
                LastError = "Fixed timestep must be positive.";
                return false;
            }

            if (NativeMethods.SetControls(_instance, ref _controls) == 0 ||
                NativeMethods.Advance(_instance, fixedDeltaTimeSeconds) == 0)
            {
                LastError = ReadLastError(_instance, "JSBSim fixed step failed.");
                return false;
            }

            return CaptureState();
        }

        public void Dispose()
        {
            if (_instance != IntPtr.Zero)
            {
                NativeMethods.Destroy(_instance);
                _instance = IntPtr.Zero;
            }

            _context = null;
        }

        private bool CaptureState()
        {
            if (NativeMethods.GetState(_instance, out NativeState native) == 0)
            {
                LastError = ReadLastError(_instance, "Could not read JSBSim state.");
                return false;
            }

            CurrentState = new FlightDynamicsState
            {
                simulationTimeSeconds = native.simulationTimeSeconds,
                latitudeDegrees = native.latitudeDegrees,
                longitudeDegrees = native.longitudeDegrees,
                altitudeMslMeters = native.altitudeMslFeet * FlightFrameConversions.FeetToMeters,
                altitudeAglMeters = native.altitudeAglFeet * FlightFrameConversions.FeetToMeters,
                calibratedAirspeedKnots = native.calibratedAirspeedKnots,
                groundSpeedKnots = native.groundSpeedKnots,
                verticalSpeedFeetPerMinute = native.verticalSpeedFeetPerMinute,
                headingDegrees = native.headingDegrees,
                pitchDegrees = native.pitchDegrees,
                bankDegrees = native.bankDegrees,
                angleOfAttackDegrees = native.angleOfAttackDegrees,
                sideslipDegrees = native.sideslipDegrees,
                engineRpm = native.engineRpm,
                loadFactorG = native.loadFactorG,
                weightOnWheels = native.weightOnWheels != 0,
                positionUnityMeters = FlightFrameConversions.GeodeticToUnity(
                    native.latitudeDegrees,
                    native.longitudeDegrees,
                    native.altitudeMslFeet * FlightFrameConversions.FeetToMeters,
                    _context.localOrigin),
                rotationUnity = FlightFrameConversions.JsbsimAttitudeToUnity(
                    native.headingDegrees,
                    native.pitchDegrees,
                    native.bankDegrees),
                velocityUnityMetersPerSecond = FlightFrameConversions.NedFeetPerSecondToUnityMetersPerSecond(
                    native.velocityNorthFeetPerSecond,
                    native.velocityEastFeetPerSecond,
                    native.velocityDownFeetPerSecond),
                angularVelocityBodyDegreesPerSecond = FlightFrameConversions.BodyRatesRadiansToUnityDegrees(
                    native.rollRateRadiansPerSecond,
                    native.pitchRateRadiansPerSecond,
                    native.yawRateRadiansPerSecond)
            };
            if (!CurrentState.IsFinite)
            {
                LastError = "JSBSim returned non-finite state.";
                return false;
            }

            LastError = string.Empty;
            return true;
        }

        private bool RequireInitialized()
        {
            if (IsInitialized) return true;
            LastError = "Native JSBSim backend is not initialized.";
            return false;
        }

        private static string ReadLastError(IntPtr instance, string fallback)
        {
            IntPtr pointer = NativeMethods.LastError(instance);
            string message = pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer);
            return string.IsNullOrWhiteSpace(message) ? fallback : message;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        internal static double ContractPitchToJsbsim(double value)
        {
            return Clamp(value, -1.0, 1.0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInitialConditions
        {
            public double latitudeDegrees;
            public double longitudeDegrees;
            public double altitudeMslFeet;
            public double terrainElevationMslFeet;
            public double calibratedAirspeedKnots;
            public double headingDegrees;
            public double pitchDegrees;
            public double bankDegrees;
            public double flightPathAngleDegrees;
            public int engineRunning;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeControls
        {
            public double aileron;
            public double elevator;
            public double rudder;
            public double throttle;
            public double mixture;
            public double carbHeat;
            public double elevatorTrim;
            public double flaps;
            public double leftBrake;
            public double rightBrake;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeAtmosphere
        {
            public double windNorthFeetPerSecond;
            public double windEastFeetPerSecond;
            public double windDownFeetPerSecond;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeState
        {
            public double simulationTimeSeconds;
            public double latitudeDegrees;
            public double longitudeDegrees;
            public double altitudeMslFeet;
            public double altitudeAglFeet;
            public double calibratedAirspeedKnots;
            public double groundSpeedKnots;
            public double verticalSpeedFeetPerMinute;
            public double headingDegrees;
            public double pitchDegrees;
            public double bankDegrees;
            public double angleOfAttackDegrees;
            public double sideslipDegrees;
            public double velocityNorthFeetPerSecond;
            public double velocityEastFeetPerSecond;
            public double velocityDownFeetPerSecond;
            public double rollRateRadiansPerSecond;
            public double pitchRateRadiansPerSecond;
            public double yawRateRadiansPerSecond;
            public double engineRpm;
            public double loadFactorG;
            public int weightOnWheels;
        }

        private static class NativeMethods
        {
            private const string Library = "qfl_jsbsim_native";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_api_version")]
            internal static extern int ApiVersion();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_create")]
            internal static extern IntPtr Create();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_destroy")]
            internal static extern void Destroy(IntPtr instance);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "qfl_jsbsim_load_aircraft")]
            internal static extern int LoadAircraft(IntPtr instance, string dataRoot, string aircraftName);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_reset")]
            internal static extern int Reset(IntPtr instance, ref NativeInitialConditions initialConditions);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_set_controls")]
            internal static extern int SetControls(IntPtr instance, ref NativeControls controls);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_set_atmosphere")]
            internal static extern int SetAtmosphere(IntPtr instance, ref NativeAtmosphere atmosphere);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_advance")]
            internal static extern int Advance(IntPtr instance, double fixedDeltaTimeSeconds);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_get_state")]
            internal static extern int GetState(IntPtr instance, out NativeState state);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "qfl_jsbsim_last_error")]
            internal static extern IntPtr LastError(IntPtr instance);
        }
    }
}
