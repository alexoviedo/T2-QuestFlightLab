using UnityEngine;

namespace QuestFlightLab.Environment
{
    public static class AirportRuntimeEnhancer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnhanceExistingScene()
        {
            GameObject root = GameObject.Find("KBDU_Approx_Airport_NotForNavigation");
            if (root == null) return;

            Material white = KbduApproxAirport.Material("Runway White Runtime", new Color(0.9f, 0.9f, 0.86f));
            Material yellow = KbduApproxAirport.Material("Taxiway Yellow Runtime", new Color(0.95f, 0.72f, 0.08f));
            Material blue = KbduApproxAirport.Material("Pattern Blue Runtime", new Color(0.1f, 0.35f, 0.8f));
            Material green = KbduApproxAirport.Material("Pattern Gate Green Runtime", new Color(0.1f, 0.75f, 0.35f));
            Material amber = KbduApproxAirport.Material("Pattern Reference Amber Runtime", new Color(1f, 0.62f, 0.08f));
            if (root.transform.Find("RunwayEdgeLineLeft") == null)
            {
                AddRunwayMarkingEnhancements(root.transform, white, yellow);
            }
            if (root.transform.Find("PatternGate_Upwind_08") == null)
            {
                KbduApproxAirport.BuildPatternMarkers(root.transform, blue, green, amber);
            }
            if (root.transform.Find("AirportLabel") == null)
            {
                AddAirportLabels(root.transform, white);
            }
            if (root.GetComponent<AirportDebugLabelToggle>() == null)
            {
                root.AddComponent<AirportDebugLabelToggle>();
            }
        }

        public static void AddRunwayMarkingEnhancements(Transform root, Material white, Material yellow)
        {
            KbduApproxAirport.Cube(root, "RunwayEdgeLineLeft", new Vector3(0f, 0.071f, 10.8f), new Vector3(1210f, 0.01f, 0.7f), white);
            KbduApproxAirport.Cube(root, "RunwayEdgeLineRight", new Vector3(0f, 0.071f, -10.8f), new Vector3(1210f, 0.01f, 0.7f), white);

            for (int i = 0; i < 4; i++)
            {
                float offset = i * 5.2f;
                KbduApproxAirport.Cube(root, $"Runway08Threshold_{i}", new Vector3(-600f, 0.078f, -7.8f + offset), new Vector3(24f, 0.01f, 1.4f), white);
                KbduApproxAirport.Cube(root, $"Runway26Threshold_{i}", new Vector3(600f, 0.078f, -7.8f + offset), new Vector3(24f, 0.01f, 1.4f), white);
            }

            KbduApproxAirport.Cube(root, "HoldShortBars08", new Vector3(-320f, 0.082f, -28f), new Vector3(24f, 0.01f, 1.2f), yellow);
            KbduApproxAirport.Cube(root, "HoldShortBars26", new Vector3(320f, 0.082f, -28f), new Vector3(24f, 0.01f, 1.2f), yellow);
        }

        private static void AddAirportLabels(Transform root, Material material)
        {
            CreateLabel(root, "AirportLabel", "KBDU APPROX - NOT FOR NAVIGATION", new Vector3(-500f, 8f, -118f), material, 8f);
            CreateLabel(root, "Runway08Label", "08", new Vector3(-555f, 1.2f, 15f), material, 5f);
            CreateLabel(root, "Runway26Label", "26", new Vector3(535f, 1.2f, -18f), material, 5f);
            CreateLabel(root, "PatternGateLabel", "TRAINING PATTERN GATES - APPROX", new Vector3(-210f, 95f, -470f), material, 6f);
        }

        private static void CreateLabel(Transform root, string name, string text, Vector3 position, Material material, float characterSize)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(root, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(75f, 90f, 0f);
            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = characterSize;
            mesh.fontSize = 32;
            mesh.color = material.color;
        }
    }

    public class AirportDebugLabelToggle : MonoBehaviour
    {
        public bool debugLabelsVisible = true;

        private bool _lastState;

        private void Awake()
        {
            _lastState = !debugLabelsVisible;
            ApplyIfChanged();
        }

        private void Update()
        {
            ApplyIfChanged();
        }

        private void ApplyIfChanged()
        {
            if (_lastState == debugLabelsVisible) return;
            _lastState = debugLabelsVisible;

            foreach (TextMesh label in GetComponentsInChildren<TextMesh>(true))
            {
                if (label.name.Contains("Label"))
                {
                    label.gameObject.SetActive(debugLabelsVisible);
                }
            }
        }
    }
}
