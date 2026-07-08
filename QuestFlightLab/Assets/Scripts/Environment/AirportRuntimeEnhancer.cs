using UnityEngine;
using QuestFlightLab.Runtime;

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
            Material asphalt = VisualMaterial("Runway Asphalt Baseline", new Color(0.045f, 0.048f, 0.052f), 0f, 0.18f);
            Material concrete = VisualMaterial("Apron Concrete Baseline", new Color(0.42f, 0.41f, 0.38f), 0f, 0.22f);
            Material grass = VisualMaterial("High Plains Grass Baseline", new Color(0.27f, 0.38f, 0.18f), 0f, 0.12f);
            Material hangar = VisualMaterial("Hangar Painted Metal Baseline", new Color(0.68f, 0.7f, 0.68f), 0.05f, 0.28f);
            Material roof = VisualMaterial("Hangar Roof Baseline", new Color(0.18f, 0.2f, 0.22f), 0.08f, 0.34f);
            Material rubber = VisualMaterial("Runway Rubber Wear Baseline", new Color(0.012f, 0.013f, 0.014f), 0f, 0.12f);
            Material asphaltPatch = VisualMaterial("Runway Asphalt Patch Baseline", new Color(0.075f, 0.078f, 0.08f), 0f, 0.16f);
            Material concreteJoint = VisualMaterial("Concrete Joint Baseline", new Color(0.18f, 0.18f, 0.17f), 0f, 0.14f);
            Material grassLight = VisualMaterial("Dry Grass Variation Baseline", new Color(0.38f, 0.43f, 0.20f), 0f, 0.12f);
            Material grassDark = VisualMaterial("Mowed Grass Variation Baseline", new Color(0.17f, 0.29f, 0.13f), 0f, 0.12f);
            Material coneOrange = VisualMaterial("Airport Cone Orange Baseline", new Color(1f, 0.31f, 0.04f), 0f, 0.18f);
            Material treeTrunk = VisualMaterial("Tree Trunk Baseline", new Color(0.27f, 0.18f, 0.1f), 0f, 0.16f);
            Material treeCanopy = VisualMaterial("Tree Canopy Baseline", new Color(0.12f, 0.3f, 0.12f), 0f, 0.18f);
            Material hill = VisualMaterial("Foothill Baseline", new Color(0.28f, 0.32f, 0.25f), 0f, 0.2f);
            Material lightWarm = VisualMaterial("Warm Runway Light Baseline", new Color(1f, 0.82f, 0.46f), 0f, 0.1f, new Color(1f, 0.65f, 0.24f));
            Material red = VisualMaterial("Red Light Baseline", new Color(0.8f, 0.05f, 0.04f), 0f, 0.12f, new Color(0.9f, 0.02f, 0.01f));

            if (root.transform.Find("RunwayEdgeLineLeft") == null)
            {
                AddRunwayMarkingEnhancements(root.transform, white, yellow);
            }
            if (root.transform.Find("PatternGate_Upwind_08") == null)
            {
                KbduApproxAirport.BuildPatternMarkers(root.transform, blue, green, amber);
            }
            else if (root.transform.Find("ApproachGate_3Deg_Stable300Agl08") == null)
            {
                KbduApproxAirport.BuildApproachMarkers(root.transform, green, amber);
            }
            if (root.transform.Find("AirportLabel") == null)
            {
                AddAirportLabels(root.transform, white);
            }
            if (root.GetComponent<AirportDebugLabelToggle>() == null)
            {
                root.AddComponent<AirportDebugLabelToggle>();
            }
            if (root.transform.Find("VisualBaselineRoot") == null)
            {
                AddVisualBaseline(root.transform, asphalt, concrete, grass, white, yellow, hangar, roof, rubber, asphaltPatch, concreteJoint, grassLight, grassDark, coneOrange, treeTrunk, treeCanopy, hill, lightWarm, green, red);
            }
            if (QuestLaunchOptions.PlaytestHudEnabled())
            {
                ApplyPlayableVisualPresentation(root.transform);
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

        private static void AddVisualBaseline(
            Transform root,
            Material asphalt,
            Material concrete,
            Material grass,
            Material white,
            Material yellow,
            Material hangar,
            Material roof,
            Material rubber,
            Material asphaltPatch,
            Material concreteJoint,
            Material grassLight,
            Material grassDark,
            Material coneOrange,
            Material treeTrunk,
            Material treeCanopy,
            Material hill,
            Material lightWarm,
            Material green,
            Material red)
        {
            GameObject baseline = new GameObject("VisualBaselineRoot");
            baseline.transform.SetParent(root, false);

            Cube(baseline.transform, "EnhancedRunwaySurface", new Vector3(0f, 0.084f, 0f), Quaternion.identity, new Vector3(1250f, 0.012f, 23.4f), asphalt);
            Cube(baseline.transform, "EnhancedTaxiwaySurface", new Vector3(0f, 0.088f, -75f), Quaternion.identity, new Vector3(910f, 0.012f, 11.4f), concrete);
            Cube(baseline.transform, "EnhancedApronSurface", new Vector3(-360f, 0.091f, -145f), Quaternion.identity, new Vector3(174f, 0.012f, 104f), concrete);
            Cube(baseline.transform, "MowedRunwaySafetyArea", new Vector3(0f, 0.066f, 0f), Quaternion.identity, new Vector3(1320f, 0.012f, 74f), grass);

            AddRunwayNumeralApproximation(baseline.transform, white);
            AddRunwayLights(baseline.transform, lightWarm, green, red);
            AddTaxiwaySigns(baseline.transform, yellow, asphalt);
            AddHangarsAndApron(baseline.transform, hangar, roof, concrete);
            AddVisualFidelitySurfaceDetails(baseline.transform, rubber, asphaltPatch, concreteJoint, grassLight, grassDark, coneOrange, white, yellow);
            AddVegetationAndFoothills(baseline.transform, treeTrunk, treeCanopy, hill);
        }

        private static void ApplyPlayableVisualPresentation(Transform root)
        {
            string[] hiddenPrefixes =
            {
                "LeftDownwindMarker",
                "BaseLegMarker",
                "PatternGate",
                "PatternAltitudeBand",
                "PatternBoxBoundary",
                "ApproachGate",
                "ApproachPathPlaceholder",
                "GoAroundClimboutGate",
                "ExtendedCenterline",
                "PapiVasiPlaceholder",
                "Foothill_",
                "BaselineFoothill",
                "LowFoothillShelf"
            };

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (string prefix in hiddenPrefixes)
                {
                    if (child.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        child.gameObject.SetActive(false);
                        break;
                    }
                }
            }

            AirportDebugLabelToggle labelToggle = root.GetComponent<AirportDebugLabelToggle>();
            if (labelToggle != null)
            {
                labelToggle.debugLabelsVisible = false;
            }

            foreach (TextMesh label in root.GetComponentsInChildren<TextMesh>(true))
            {
                if (label.name.Contains("Label"))
                {
                    label.gameObject.SetActive(false);
                }
            }
        }

        private static void AddRunwayNumeralApproximation(Transform parent, Material white)
        {
            Cube(parent, "Runway08NumeralStem0", new Vector3(-575f, 0.105f, 4.7f), Quaternion.identity, new Vector3(2.4f, 0.012f, 8f), white);
            Cube(parent, "Runway08NumeralStem8A", new Vector3(-555f, 0.105f, 4.7f), Quaternion.identity, new Vector3(2.4f, 0.012f, 8f), white);
            Cube(parent, "Runway08NumeralStem8B", new Vector3(-544f, 0.105f, 4.7f), Quaternion.identity, new Vector3(2.4f, 0.012f, 8f), white);
            Cube(parent, "Runway08NumeralBar8Top", new Vector3(-549.5f, 0.106f, 8.2f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.6f), white);
            Cube(parent, "Runway08NumeralBar8Mid", new Vector3(-549.5f, 0.106f, 4.7f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.4f), white);
            Cube(parent, "Runway08NumeralBar8Bottom", new Vector3(-549.5f, 0.106f, 1.2f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.6f), white);

            Cube(parent, "Runway26NumeralStem2", new Vector3(544f, 0.105f, -4.7f), Quaternion.identity, new Vector3(2.4f, 0.012f, 8f), white);
            Cube(parent, "Runway26NumeralBar2Top", new Vector3(549.5f, 0.106f, -1.2f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.6f), white);
            Cube(parent, "Runway26NumeralBar2Mid", new Vector3(549.5f, 0.106f, -4.7f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.4f), white);
            Cube(parent, "Runway26NumeralBar2Bottom", new Vector3(549.5f, 0.106f, -8.2f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.6f), white);
            Cube(parent, "Runway26NumeralStem6A", new Vector3(565f, 0.105f, -4.7f), Quaternion.identity, new Vector3(2.4f, 0.012f, 8f), white);
            Cube(parent, "Runway26NumeralBar6Top", new Vector3(570.5f, 0.106f, -1.2f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.6f), white);
            Cube(parent, "Runway26NumeralBar6Mid", new Vector3(570.5f, 0.106f, -4.7f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.4f), white);
            Cube(parent, "Runway26NumeralBar6Bottom", new Vector3(570.5f, 0.106f, -8.2f), Quaternion.identity, new Vector3(10.5f, 0.012f, 1.6f), white);
        }

        private static void AddRunwayLights(Transform parent, Material lightWarm, Material green, Material red)
        {
            for (int i = -12; i <= 12; i++)
            {
                float x = i * 48f;
                Sphere(parent, $"RunwayLightNorth_{i + 12}", new Vector3(x, 0.22f, 14.7f), new Vector3(1.1f, 0.45f, 1.1f), lightWarm);
                Sphere(parent, $"RunwayLightSouth_{i + 12}", new Vector3(x, 0.22f, -14.7f), new Vector3(1.1f, 0.45f, 1.1f), lightWarm);
            }

            for (int i = 0; i < 5; i++)
            {
                float z = -8f + i * 4f;
                Sphere(parent, $"Runway08ThresholdGreen_{i}", new Vector3(-618f, 0.26f, z), new Vector3(1.35f, 0.55f, 1.35f), green);
                Sphere(parent, $"Runway26EndRed_{i}", new Vector3(-634f, 0.24f, z), new Vector3(1.1f, 0.45f, 1.1f), red);
                Sphere(parent, $"Runway26ThresholdGreen_{i}", new Vector3(618f, 0.26f, z), new Vector3(1.35f, 0.55f, 1.35f), green);
                Sphere(parent, $"Runway08EndRed_{i}", new Vector3(634f, 0.24f, z), new Vector3(1.1f, 0.45f, 1.1f), red);
            }
        }

        private static void AddTaxiwaySigns(Transform parent, Material yellow, Material dark)
        {
            Cube(parent, "TaxiSignRunwayHold08", new Vector3(-330f, 1.6f, -24f), Quaternion.Euler(0f, 90f, 0f), new Vector3(6f, 2f, 0.25f), yellow);
            Cube(parent, "TaxiSignRunwayHold08Back", new Vector3(-330f, 1.58f, -24.18f), Quaternion.Euler(0f, 90f, 0f), new Vector3(6.5f, 2.3f, 0.18f), dark);
            Cube(parent, "TaxiSignApronA", new Vector3(-260f, 1.35f, -83f), Quaternion.identity, new Vector3(7f, 1.8f, 0.25f), yellow);
            Cube(parent, "TaxiSignApronABack", new Vector3(-260f, 1.33f, -83.18f), Quaternion.identity, new Vector3(7.5f, 2.1f, 0.18f), dark);
        }

        private static void AddHangarsAndApron(Transform parent, Material hangar, Material roof, Material concrete)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = -430f + i * 58f;
                Cube(parent, $"BaselineHangar_{i}_Body", new Vector3(x, 7.5f, -214f), Quaternion.identity, new Vector3(44f, 15f, 34f), hangar);
                Cube(parent, $"BaselineHangar_{i}_RoofLeft", new Vector3(x - 9f, 17f, -214f), Quaternion.Euler(0f, 0f, -22f), new Vector3(27f, 2.2f, 38f), roof);
                Cube(parent, $"BaselineHangar_{i}_RoofRight", new Vector3(x + 9f, 17f, -214f), Quaternion.Euler(0f, 0f, 22f), new Vector3(27f, 2.2f, 38f), roof);
                Cube(parent, $"BaselineHangar_{i}_Door", new Vector3(x, 5f, -196.7f), Quaternion.identity, new Vector3(28f, 9f, 0.5f), concrete);
            }

            Cube(parent, "BaselineFuelIsland", new Vector3(-286f, 1.4f, -134f), Quaternion.identity, new Vector3(14f, 2.8f, 9f), roof);
            Cylinder(parent, "BaselineFuelPump", new Vector3(-286f, 2.8f, -128f), Quaternion.identity, new Vector3(1.3f, 2.8f, 1.3f), hangar);
        }

        private static void AddVisualFidelitySurfaceDetails(
            Transform parent,
            Material rubber,
            Material asphaltPatch,
            Material concreteJoint,
            Material grassLight,
            Material grassDark,
            Material coneOrange,
            Material white,
            Material yellow)
        {
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 18f;
                Cube(parent, $"TouchdownRubberNorth_{i + 5}", new Vector3(-510f + x, 0.113f, 3.4f), Quaternion.Euler(0f, 0f, 0f), new Vector3(11f, 0.01f, 0.55f), rubber);
                Cube(parent, $"TouchdownRubberSouth_{i + 5}", new Vector3(-510f + x, 0.114f, -3.4f), Quaternion.Euler(0f, 0f, 0f), new Vector3(10f, 0.01f, 0.5f), rubber);
                Cube(parent, $"OppositeTouchdownRubberNorth_{i + 5}", new Vector3(510f - x, 0.113f, 3.2f), Quaternion.identity, new Vector3(9.5f, 0.01f, 0.5f), rubber);
                Cube(parent, $"OppositeTouchdownRubberSouth_{i + 5}", new Vector3(510f - x, 0.114f, -3.2f), Quaternion.identity, new Vector3(10.5f, 0.01f, 0.55f), rubber);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -420f + i * 120f;
                float z = i % 2 == 0 ? 6.8f : -7.1f;
                Cube(parent, $"RunwayPatch_{i}", new Vector3(x, 0.111f, z), Quaternion.Euler(0f, (i % 3 - 1) * 4f, 0f), new Vector3(34f, 0.01f, 3.8f), asphaltPatch);
            }

            for (int i = 0; i < 14; i++)
            {
                float x = -585f + i * 90f;
                Cube(parent, $"RunwayExpansionJoint_{i}", new Vector3(x, 0.116f, 0f), Quaternion.identity, new Vector3(0.45f, 0.01f, 22.2f), concreteJoint);
            }

            for (int i = 0; i < 10; i++)
            {
                float x = -610f + i * 135f;
                Material mat = i % 2 == 0 ? grassLight : grassDark;
                Cube(parent, $"RunwayGrassVariationNorth_{i}", new Vector3(x, 0.074f, 45f), Quaternion.Euler(0f, i * 7f, 0f), new Vector3(82f, 0.008f, 15f), mat);
                Cube(parent, $"RunwayGrassVariationSouth_{i}", new Vector3(x + 34f, 0.074f, -47f), Quaternion.Euler(0f, -i * 5f, 0f), new Vector3(78f, 0.008f, 13f), mat);
            }

            for (int i = 0; i < 9; i++)
            {
                float x = -430f + i * 20f;
                Cube(parent, $"ApronConcreteJointLong_{i}", new Vector3(x, 0.111f, -145f), Quaternion.identity, new Vector3(0.26f, 0.01f, 96f), concreteJoint);
            }

            for (int i = 0; i < 5; i++)
            {
                float z = -188f + i * 21f;
                Cube(parent, $"ApronConcreteJointLat_{i}", new Vector3(-360f, 0.112f, z), Quaternion.identity, new Vector3(166f, 0.01f, 0.24f), concreteJoint);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -424f + i * 24f;
                Cube(parent, $"TieDownStripe_{i}", new Vector3(x, 0.122f, -117f), Quaternion.Euler(0f, 22f, 0f), new Vector3(7f, 0.01f, 0.36f), white);
                Cylinder(parent, $"TieDownRing_{i}", new Vector3(x, 0.18f, -124f), Quaternion.identity, new Vector3(1.1f, 0.08f, 1.1f), yellow);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -250f + i * 18f;
                Cylinder(parent, $"TaxiCone_{i}", new Vector3(x, 0.42f, -91f), Quaternion.identity, new Vector3(0.8f, 0.8f, 0.8f), coneOrange);
                Cube(parent, $"TaxiConeStripe_{i}", new Vector3(x, 0.61f, -91f), Quaternion.identity, new Vector3(0.82f, 0.06f, 0.82f), white);
            }
        }

        private static void AddVegetationAndFoothills(Transform parent, Material treeTrunk, Material treeCanopy, Material hill)
        {
            for (int i = 0; i < 20; i++)
            {
                float x = -720f + i * 76f;
                AddTree(parent, $"NorthTree_{i}", new Vector3(x, 0f, 310f + Mathf.Sin(i * 0.7f) * 26f), treeTrunk, treeCanopy);
                AddTree(parent, $"SouthTree_{i}", new Vector3(x + 24f, 0f, -390f + Mathf.Cos(i * 0.5f) * 22f), treeTrunk, treeCanopy);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -780f + i * 220f;
                Cube(parent, $"LowFoothillShelf_{i}", new Vector3(x, 12f + i * 1.6f, 820f + Mathf.Sin(i) * 38f), Quaternion.Euler(0f, i * 3f, -3f + i % 3 * 3f), new Vector3(250f, 18f + i * 1.8f, 74f), hill);
            }
        }

        private static void AddTree(Transform parent, string name, Vector3 basePosition, Material trunk, Material canopy)
        {
            GameObject tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            Cylinder(tree.transform, "Trunk", basePosition + new Vector3(0f, 2.3f, 0f), Quaternion.identity, new Vector3(0.9f, 2.3f, 0.9f), trunk);
            Sphere(tree.transform, "CanopyLower", basePosition + new Vector3(0f, 5.3f, 0f), new Vector3(5.2f, 3.0f, 5.2f), canopy);
            Sphere(tree.transform, "CanopyUpper", basePosition + new Vector3(0f, 7.2f, 0f), new Vector3(3.8f, 2.5f, 3.8f), canopy);
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

        private static GameObject Cube(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            return Primitive(PrimitiveType.Cube, parent, name, position, rotation, scale, material);
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Primitive(PrimitiveType.Sphere, parent, name, position, Quaternion.identity, scale, material);
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            return Primitive(PrimitiveType.Cylinder, parent, name, position, rotation, scale, material);
        }

        private static GameObject Primitive(PrimitiveType primitiveType, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            return go;
        }

        private static Material VisualMaterial(string name, Color color, float metallic, float smoothness)
        {
            return VisualMaterial(name, color, metallic, smoothness, Color.black);
        }

        private static Material VisualMaterial(string name, Color color, float metallic, float smoothness, Color emission)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }

            return material;
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
