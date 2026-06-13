using UnityEngine;

namespace QuestFlightLab.Environment
{
    public static class KbduApproxAirport
    {
        public static GameObject Build(Transform parent)
        {
            GameObject root = new GameObject("KBDU_Approx_Airport_NotForNavigation");
            root.transform.SetParent(parent, false);

            Material asphalt = Material("Asphalt", new Color(0.08f, 0.085f, 0.09f));
            Material concrete = Material("Concrete", new Color(0.35f, 0.35f, 0.33f));
            Material grass = Material("High Plains Grass", new Color(0.22f, 0.36f, 0.18f));
            Material white = Material("Runway White", new Color(0.9f, 0.9f, 0.86f));
            Material yellow = Material("Taxiway Yellow", new Color(0.95f, 0.72f, 0.08f));
            Material red = Material("Windsock Red", new Color(0.85f, 0.08f, 0.04f));
            Material blue = Material("Pattern Blue", new Color(0.1f, 0.35f, 0.8f));
            Material green = Material("Pattern Gate Green", new Color(0.1f, 0.75f, 0.35f));
            Material amber = Material("Pattern Reference Amber", new Color(1f, 0.62f, 0.08f));

            Cube(root.transform, "Terrain", new Vector3(0f, -0.04f, 0f), new Vector3(1800f, 0.08f, 1400f), grass);
            Cube(root.transform, "Runway_08_26_Approx_4100x75ft", new Vector3(0f, 0.015f, 0f), new Vector3(1250f, 0.03f, 23f), asphalt);
            Cube(root.transform, "Taxiway", new Vector3(0f, 0.025f, -75f), new Vector3(900f, 0.03f, 11f), concrete);
            Cube(root.transform, "Apron", new Vector3(-360f, 0.03f, -145f), new Vector3(170f, 0.03f, 100f), concrete);
            Cube(root.transform, "TaxiwayConnector", new Vector3(-320f, 0.035f, -38f), new Vector3(14f, 0.03f, 78f), concrete);

            for (int i = -5; i <= 5; i++)
            {
                Cube(root.transform, $"RunwayCenterline_{i}", new Vector3(i * 95f, 0.06f, 0f), new Vector3(28f, 0.01f, 1.2f), white);
            }

            Cube(root.transform, "Runway08_Number_Block", new Vector3(-560f, 0.065f, 0f), new Vector3(22f, 0.01f, 9f), white);
            Cube(root.transform, "Runway26_Number_Block", new Vector3(560f, 0.065f, 0f), new Vector3(22f, 0.01f, 9f), white);
            Cube(root.transform, "TaxiwayCenterline", new Vector3(0f, 0.07f, -75f), new Vector3(760f, 0.01f, 0.8f), yellow);

            AirportRuntimeEnhancer.AddRunwayMarkingEnhancements(root.transform, white, yellow);
            BuildWindsock(root.transform, new Vector3(-210f, 0f, -125f), red, white);
            BuildPatternMarkers(root.transform, blue, green, amber);
            BuildFoothills(root.transform);

            return root;
        }

        private static void BuildWindsock(Transform parent, Vector3 pos, Material red, Material white)
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "WindsockPole";
            pole.transform.SetParent(parent, false);
            pole.transform.position = pos + new Vector3(0f, 3f, 0f);
            pole.transform.localScale = new Vector3(0.25f, 3f, 0.25f);
            pole.GetComponent<Renderer>().sharedMaterial = white;

            GameObject sock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sock.name = "Windsock";
            sock.transform.SetParent(parent, false);
            sock.transform.position = pos + new Vector3(3f, 6f, 0f);
            sock.transform.localScale = new Vector3(6f, 0.8f, 1.2f);
            sock.GetComponent<Renderer>().sharedMaterial = red;
        }

        public static void BuildPatternMarkers(Transform parent, Material material, Material gateMaterial, Material referenceMaterial)
        {
            Cube(parent, "LeftDownwindMarker", new Vector3(0f, 90f, -430f), new Vector3(620f, 2f, 8f), material);
            Cube(parent, "BaseLegMarker08", new Vector3(-620f, 90f, -220f), new Vector3(8f, 2f, 320f), material);
            Cube(parent, "BaseLegMarker26", new Vector3(620f, 90f, -220f), new Vector3(8f, 2f, 320f), material);

            Cube(parent, "PatternGate_Upwind_08", new Vector3(-300f, 150f, 25f), new Vector3(70f, 26f, 6f), gateMaterial);
            Cube(parent, "PatternGate_Crosswind_08", new Vector3(-610f, 235f, -150f), new Vector3(6f, 34f, 88f), gateMaterial);
            Cube(parent, "PatternGate_Downwind_Midfield", new Vector3(0f, 300f, -430f), new Vector3(115f, 36f, 8f), gateMaterial);
            Cube(parent, "PatternGate_Abeam_Touchdown_08", new Vector3(-360f, 300f, -430f), new Vector3(90f, 36f, 8f), gateMaterial);
            Cube(parent, "PatternGate_Base_08", new Vector3(-620f, 230f, -270f), new Vector3(8f, 34f, 92f), gateMaterial);
            Cube(parent, "PatternGate_Final_08", new Vector3(-420f, 105f, 0f), new Vector3(78f, 30f, 7f), gateMaterial);
            Cube(parent, "PatternAltitudeBandPlaceholder", new Vector3(0f, 305f, -430f), new Vector3(1250f, 4f, 18f), referenceMaterial);
            Cube(parent, "TouchdownZoneMarker08", new Vector3(-470f, 0.09f, 0f), new Vector3(80f, 0.015f, 11f), referenceMaterial);
            Cube(parent, "ApproachPathPlaceholder08", new Vector3(-550f, 18f, 0f), new Vector3(160f, 2f, 5f), referenceMaterial);
            BuildApproachMarkers(parent, gateMaterial, referenceMaterial);
            Cube(parent, "PatternBoxBoundaryNorth", new Vector3(0f, 85f, 255f), new Vector3(1240f, 3f, 6f), material);
            Cube(parent, "PatternBoxBoundarySouth", new Vector3(0f, 85f, -560f), new Vector3(1240f, 3f, 6f), material);
        }

        public static void BuildApproachMarkers(Transform parent, Material gateMaterial, Material referenceMaterial)
        {
            Cube(parent, "ExtendedCenterline08", new Vector3(-760f, 2f, 0f), new Vector3(430f, 1.5f, 2f), referenceMaterial);
            Cube(parent, "ApproachGate_3Deg_Outer08", new Vector3(-820f, 78f, 0f), new Vector3(88f, 26f, 8f), gateMaterial);
            Cube(parent, "ApproachGate_3Deg_Mid08", new Vector3(-660f, 48f, 0f), new Vector3(78f, 24f, 7f), gateMaterial);
            Cube(parent, "ApproachGate_3Deg_Stable300Agl08", new Vector3(-515f, 91f, 0f), new Vector3(72f, 22f, 7f), gateMaterial);
            Cube(parent, "GoAroundClimboutGate08", new Vector3(-210f, 135f, 35f), new Vector3(80f, 28f, 9f), gateMaterial);
            Cube(parent, "PapiVasiPlaceholder08", new Vector3(-430f, 0.11f, 18f), new Vector3(36f, 0.02f, 3f), referenceMaterial);
        }

        private static void BuildFoothills(Transform parent)
        {
            Material hill = Material("Foothill Placeholder", new Color(0.24f, 0.27f, 0.25f));
            for (int i = 0; i < 7; i++)
            {
                GameObject h = GameObject.CreatePrimitive(PrimitiveType.Cube);
                h.name = $"Foothill_{i}";
                h.transform.SetParent(parent, false);
                h.transform.position = new Vector3(-760f + i * 150f, 30f + i * 4f, 520f);
                h.transform.localScale = new Vector3(180f, 55f + i * 8f, 140f);
                h.transform.rotation = Quaternion.Euler(0f, i * 7f, 0f);
                h.GetComponent<Renderer>().sharedMaterial = hill;
            }
        }

        public static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static Material Material(string name, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }
    }
}
