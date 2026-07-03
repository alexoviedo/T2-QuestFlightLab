using System.Text;
using QuestFlightLab.Environment;
using QuestFlightLab.Flight;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.UI
{
    public class PlaytestHud : MonoBehaviour
    {
        private const string HudName = "Quest Playtest HUD";

        public static PlaytestHud Instance { get; private set; }

        public GameObject Root { get; private set; }
        public int HiddenVerbosePanelCount { get; private set; }
        public int VisibleLineCount { get; private set; }
        public string LastRenderedText { get; private set; } = string.Empty;

        private Camera _camera;
        private TextMesh _text;
        private GamepadInputReader _reader;
        private Usb2BleInputMapper _mapper;
        private FlightTelemetry _flightTelemetry;
        private SceneryModeController _sceneryModeController;
        private readonly StringBuilder _buffer = new StringBuilder(512);
        private int _frames;
        private float _fpsWindowStart;
        private float _currentFps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!QuestLaunchOptions.PlaytestHudEnabled() || QuestLaunchOptions.VerboseHudEnabled()) return;
            if (FindFirstObjectByType<PlaytestHud>() != null) return;

            GameObject go = new GameObject(HudName);
            DontDestroyOnLoad(go);
            go.AddComponent<PlaytestHud>();
        }

        private void Awake()
        {
            Instance = this;
            _fpsWindowStart = Time.unscaledTime;
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            UpdateFps();
            RefreshBindings();
            RenderText();
        }

        public void InitializeForTest(Camera camera)
        {
            _camera = camera;
            Initialize();
            RenderText();
        }

        private void Initialize()
        {
            if (_camera == null) _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (_camera == null) return;

            HiddenVerbosePanelCount = HideVerbosePanels();
            BuildHud();
            RefreshBindings();
            RenderText();
            Debug.Log($"[QuestFlightLab][PlaytestHUD] Compact HUD active. hiddenVerbosePanels={HiddenVerbosePanelCount}");
        }

        private int HideVerbosePanels()
        {
            int hidden = 0;

            foreach (TelemetryPanel panel in FindObjectsOfType<TelemetryPanel>(true))
            {
                panel.enabled = false;
                GameObject root = panel.panelRoot != null ? panel.panelRoot : panel.gameObject;
                if (root.activeSelf)
                {
                    root.SetActive(false);
                    hidden++;
                }
            }

            foreach (CockpitInstrumentPanel panel in FindObjectsOfType<CockpitInstrumentPanel>(true))
            {
                if (panel.gameObject.activeSelf)
                {
                    panel.gameObject.SetActive(false);
                    hidden++;
                }
            }

            foreach (TouchMenuPlaceholder menu in FindObjectsOfType<TouchMenuPlaceholder>(true))
            {
                if (menu.gameObject.activeSelf)
                {
                    menu.gameObject.SetActive(false);
                    hidden++;
                }
            }

            foreach (PerformanceHud hud in FindObjectsOfType<PerformanceHud>(true))
            {
                hud.enabled = false;
                if (hud.text != null) hud.text.gameObject.SetActive(false);
                if (hud.gameObject.activeSelf)
                {
                    hud.gameObject.SetActive(false);
                    hidden++;
                }
            }

            return hidden;
        }

        private void BuildHud()
        {
            if (Root != null) return;

            Root = new GameObject(HudName);
            Root.transform.SetParent(_camera.transform, false);
            Root.transform.localPosition = new Vector3(-0.82f, 0.16f, 1.62f);
            Root.transform.localRotation = Quaternion.Euler(-4f, 16f, 0f);

            Material panelMat = Material("Playtest HUD Panel", new Color(0.018f, 0.02f, 0.024f));
            Material textMat = Material("Playtest HUD Text", new Color(0.78f, 0.96f, 0.88f));

            Cube(Root.transform, "PlaytestHudBackground", new Vector3(0f, 0f, 0.04f), new Vector3(0.94f, 0.3f, 0.025f), panelMat);

            GameObject textObject = new GameObject("PlaytestHudText");
            textObject.transform.SetParent(Root.transform, false);
            textObject.transform.localPosition = new Vector3(-0.44f, 0.12f, -0.02f);
            _text = textObject.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.UpperLeft;
            _text.alignment = TextAlignment.Left;
            _text.fontSize = 22;
            _text.characterSize = 0.0105f;
            _text.lineSpacing = 0.84f;
            _text.color = textMat.color;
        }

        private void RefreshBindings()
        {
            if (_reader == null) _reader = FindFirstObjectByType<GamepadInputReader>();
            if (_mapper == null) _mapper = FindFirstObjectByType<Usb2BleInputMapper>();
            if (_flightTelemetry == null) _flightTelemetry = FindFirstObjectByType<FlightTelemetry>();
            if (_sceneryModeController == null) _sceneryModeController = FindFirstObjectByType<SceneryModeController>();
        }

        private void RenderText()
        {
            if (_text == null) return;

            FlightTelemetrySnapshot f = _flightTelemetry != null ? _flightTelemetry.Current : new FlightTelemetrySnapshot();
            AircraftControlState c = _mapper != null ? _mapper.Current : AircraftControlState.Neutral(0f);
            GamepadInputSnapshot g = _reader != null ? _reader.Current : GamepadInputSnapshot.Disconnected(Time.unscaledTime, 0f);
            ShortPlaytestDemoPilot demo = ShortPlaytestDemoPilot.Instance;
            FirstViewPlaytestDiagnostics diagnostics = FirstViewPlaytestDiagnostics.Instance;

            string mode = QuestLaunchOptions.SceneryMode();
            string demoLabel = demo != null ? "DEMO PILOT MODE" : "LIVE INPUT";
            string input = demo != null ? "deterministic" : g.connected ? "gamepad" : "none";
            string phase = demo != null ? demo.PhaseName : "waiting";
            string scenery = SceneryLabel();
            string tracking = HeadLabel(diagnostics);

            _buffer.Clear();
            _buffer.AppendLine($"QUEST PLAYTEST | {mode} | {demoLabel}");
            _buffer.AppendLine($"SPD {f.airspeedKts:000} kt  ALT {f.altitudeFt:0000} ft  HDG {f.headingDeg:000}  FPS {_currentFps:00}");
            _buffer.AppendLine($"CTL A {c.aileron:+0.00;-0.00;0.00}  E {c.elevator:+0.00;-0.00;0.00}  R {c.rudder:+0.00;-0.00;0.00}  THR {c.throttle:0.00}");
            _buffer.AppendLine($"INPUT {input}  SCENERY {scenery}");
            _buffer.AppendLine($"PHASE {phase}  HEAD {tracking}");
            _buffer.Append("Look around: runway, cockpit, world scenery shift.");

            LastRenderedText = _buffer.ToString();
            VisibleLineCount = LastRenderedText.Split('\n').Length;
            _text.text = LastRenderedText;
        }

        private string SceneryLabel()
        {
            SceneryProviderStatus status = _sceneryModeController != null ? _sceneryModeController.LastStatus : null;
            if (status == null) return QuestLaunchOptions.SceneryMode();
            if (status.warnings.Exists(w => w.Contains(SplatSceneryProvider.QuestXrStereoWorldLockWarning)))
            {
                return "mesh baseline (splat gated)";
            }

            if (status.activeMode == SceneryMode.ExperimentalSplatRenderer.ToString())
            {
                return status.budgetProfile;
            }

            return status.activeMode;
        }

        private static string HeadLabel(FirstViewPlaytestDiagnostics diagnostics)
        {
            QuestFirstViewRuntimeRepair repair = QuestFirstViewRuntimeRepair.Instance;
            if (repair != null && repair.HeadDevicePoseValid)
            {
                string tracked = repair.HeadDeviceTracked ? "tracked" : "untracked";
                return $"Y {repair.HeadYawDeltaDeg:+0;-0;0} P {repair.HeadPitchDeltaDeg:+0;-0;0} Pos {repair.HeadPositionDeltaMeters:0.00} {tracked}";
            }

            return diagnostics != null && diagnostics.EvidencePath.Length > 0 ? "diagnostics on" : "diagnostics starting";
        }

        private void UpdateFps()
        {
            _frames++;
            float elapsed = Time.unscaledTime - _fpsWindowStart;
            if (elapsed < 0.5f) return;

            _currentFps = _frames / Mathf.Max(0.001f, elapsed);
            _frames = 0;
            _fpsWindowStart = Time.unscaledTime;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
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
    }
}
