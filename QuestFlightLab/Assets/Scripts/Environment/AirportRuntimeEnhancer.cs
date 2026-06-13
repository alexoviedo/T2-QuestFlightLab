using UnityEngine;

namespace QuestFlightLab.Environment
{
    public static class AirportRuntimeEnhancer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnhanceExistingScene()
        {
            GameObject root = GameObject.Find("KBDU_Approx_Airport_NotForNavigation");
            if (root == null || root.transform.Find("RunwayEdgeLineLeft") != null) return;

            Material white = KbduApproxAirport.Material("Runway White Runtime", new Color(0.9f, 0.9f, 0.86f));
            Material yellow = KbduApproxAirport.Material("Taxiway Yellow Runtime", new Color(0.95f, 0.72f, 0.08f));
            AddRunwayMarkingEnhancements(root.transform, white, yellow);
            AddAirportLabels(root.transform, white);
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
}
