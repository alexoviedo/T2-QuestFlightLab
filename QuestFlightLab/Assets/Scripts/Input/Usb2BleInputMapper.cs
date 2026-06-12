using QuestFlightLab.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuestFlightLab.Input
{
    public class Usb2BleInputMapper : MonoBehaviour
    {
        public GamepadInputReader reader;
        public bool invertElevator = true;
        public float defaultThrottle = 0.72f;
        public float keyboardThrottleRate = 0.35f;
        public float trimRate = 0.35f;
        public float flapStep = 0.25f;

        public AircraftControlState Current { get; private set; } = AircraftControlState.Neutral();
        public int MarkerCount { get; private set; }
        public bool Paused { get; private set; }
        public bool TelemetryVisible { get; private set; } = true;

        private GamepadInputSnapshot _previous;
        private float _throttle;
        private float _trim;
        private float _flaps;
        private bool _resetQueued;

        private void Awake()
        {
            if (reader == null) reader = FindFirstObjectByType<GamepadInputReader>();
            _throttle = Mathf.Clamp01(defaultThrottle);
        }

        private void Update()
        {
            GamepadInputSnapshot snapshot = reader != null ? reader.Current : GamepadInputSnapshot.Disconnected(Time.unscaledTime, 0f);
            ApplyKeyboardPlaceholders();

            bool xPressed = WasPressed(snapshot.buttonWest, _previous?.buttonWest ?? false);
            bool yPressed = WasPressed(snapshot.buttonNorth, _previous?.buttonNorth ?? false);
            bool aPressed = WasPressed(snapshot.buttonSouth, _previous?.buttonSouth ?? false);
            bool bPressed = WasPressed(snapshot.buttonEast, _previous?.buttonEast ?? false);
            bool startPressed = WasPressed(snapshot.startButton, _previous?.startButton ?? false);
            bool selectPressed = WasPressed(snapshot.selectButton, _previous?.selectButton ?? false);

            if (xPressed) _flaps = Mathf.Clamp01(_flaps + flapStep);
            if (yPressed) _flaps = Mathf.Clamp01(_flaps - flapStep);
            if (aPressed) MarkerCount++;
            if (bPressed) _resetQueued = true;
            if (startPressed) Paused = !Paused;
            if (selectPressed) TelemetryVisible = !TelemetryVisible;

            _trim = Mathf.Clamp(_trim + snapshot.dpadY * trimRate * Time.unscaledDeltaTime, -1f, 1f);

            Current = MapSnapshotForTest(snapshot, invertElevator, _throttle, _trim, _flaps);
            Current.markerPressed = aPressed;
            Current.resetPressed = bPressed;
            Current.pausePressed = startPressed;
            Current.telemetryTogglePressed = selectPressed;

            _previous = snapshot;
        }

        public bool ConsumeResetRequest()
        {
            if (!_resetQueued) return false;
            _resetQueued = false;
            return true;
        }

        public static AircraftControlState MapSnapshotForTest(
            GamepadInputSnapshot snapshot,
            bool invertElevator = true,
            float throttle = 0.72f,
            float trim = 0f,
            float flaps = 0f)
        {
            if (snapshot == null)
            {
                return AircraftControlState.Neutral(throttle);
            }

            return new AircraftControlState
            {
                aileron = Mathf.Clamp(snapshot.leftStickX, -1f, 1f),
                elevator = Mathf.Clamp(invertElevator ? -snapshot.leftStickY : snapshot.leftStickY, -1f, 1f),
                rudder = Mathf.Clamp(snapshot.rightStickX, -1f, 1f),
                throttle = Mathf.Clamp01(throttle),
                mixture = 1f,
                trim = Mathf.Clamp(trim, -1f, 1f),
                flaps = Mathf.Clamp01(flaps),
                leftToeBrake = Mathf.Clamp01(snapshot.leftTrigger),
                rightToeBrake = Mathf.Clamp01(snapshot.rightTrigger)
            };
        }

        private void ApplyKeyboardPlaceholders()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            float delta = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) delta += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) delta -= 1f;
            _throttle = Mathf.Clamp01(_throttle + delta * keyboardThrottleRate * Time.unscaledDeltaTime);

            if (keyboard.rKey.wasPressedThisFrame) _resetQueued = true;
            if (keyboard.spaceKey.wasPressedThisFrame) MarkerCount++;
        }

        private static bool WasPressed(bool now, bool previous)
        {
            return now && !previous;
        }
    }
}

