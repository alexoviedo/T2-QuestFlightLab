using System;
using System.IO;
using QuestFlightLab.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuestFlightLab.Editor
{
    public static class ImportedC172CalibrationBatchRunner
    {
        public static void RenderCandidates()
        {
            string outputDir = System.Environment.GetEnvironmentVariable("QFL_C172_CALIBRATION_DIR");
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    "Dev",
                    "T2",
                    "T2-QuestFlightLab-setup-artifacts",
                    "c172_calibration_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            }

            Directory.CreateDirectory(outputDir);
            GameObject prefab = Resources.Load<GameObject>(QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Imported C172 prefab missing at Resources/" + QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Calibration Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.72f, 0.94f);

            GameObject lightObject = new GameObject("Calibration Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            Vector3[] eyes =
            {
                new Vector3(0.28f, -0.05f, -1.45f),
                new Vector3(0.28f, 1.68f, -0.45f),
                new Vector3(0.28f, -0.45f, 1.68f),
                new Vector3(0.28f, -1.45f, -0.05f),
                new Vector3(0.28f, 0.65f, -0.45f),
                new Vector3(0.28f, -0.45f, -0.65f)
            };

            Vector3[] rotations =
            {
                new Vector3(-90f, 0f, 0f),
                new Vector3(-90f, 180f, 0f),
                new Vector3(90f, 0f, 0f),
                new Vector3(90f, 180f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 180f, 0f)
            };

            foreach (Vector3 rotationEuler in rotations)
            {
                foreach (Vector3 eye in eyes)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    instance.name = "Candidate C172";
                    HideExterior(instance);
                    Quaternion rotation = Quaternion.Euler(rotationEuler);
                    instance.transform.SetPositionAndRotation(-(rotation * eye), rotation);

                    string safeName = $"rot_{rotationEuler.x}_{rotationEuler.y}_{rotationEuler.z}_eye_{eye.x}_{eye.y}_{eye.z}"
                        .Replace("-", "m")
                        .Replace(".", "p");
                    Render(camera, Path.Combine(outputDir, safeName + ".png"));
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            File.WriteAllText(Path.Combine(outputDir, "README.txt"),
                "Camera is at the pilot eye, looking +Z, with +Y up. Pick the frame where the panel is forward/upright and the camera is seated in the left seat.");
            Debug.Log("Imported C172 calibration renders written to " + outputDir);
        }

        public static void RenderRefinedLeftSeatCandidates()
        {
            string outputDir = System.Environment.GetEnvironmentVariable("QFL_C172_CALIBRATION_DIR");
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    "Dev",
                    "T2",
                    "T2-QuestFlightLab-setup-artifacts",
                    "c172_left_seat_calibration_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            }

            Directory.CreateDirectory(outputDir);
            GameObject prefab = Resources.Load<GameObject>(QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Imported C172 prefab missing at Resources/" + QuestFirstViewRuntimeRepair.ImportedC172ResourcePath);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Calibration Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.72f, 0.94f);

            GameObject lightObject = new GameObject("Calibration Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            Vector3 rotationEuler = new Vector3(-90f, 0f, 0f);
            Quaternion rotation = Quaternion.Euler(rotationEuler);
            float[] lateralCandidates = { -0.42f, -0.28f, -0.12f, 0.12f, 0.28f };
            float[] foreAftCandidates = { -1.35f, -1.05f, -0.75f, -0.45f };
            float[] heightCandidates = { 1.48f, 1.62f, 1.76f };

            foreach (float x in lateralCandidates)
            {
                foreach (float y in foreAftCandidates)
                {
                    foreach (float z in heightCandidates)
                    {
                        Vector3 eye = new Vector3(x, y, z);
                        GameObject instance = UnityEngine.Object.Instantiate(prefab);
                        instance.name = "Candidate C172";
                        HideExterior(instance);
                        instance.transform.SetPositionAndRotation(-(rotation * eye), rotation);

                        string safeName = $"leftseat_x{x}_y{y}_z{z}"
                            .Replace("-", "m")
                            .Replace(".", "p");
                        Render(camera, Path.Combine(outputDir, safeName + ".png"));
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }

            File.WriteAllText(Path.Combine(outputDir, "README.txt"),
                "Rotation is fixed at (-90, 0, 0). Camera is at the candidate pilot eye, looking +Z with +Y up. Pick the frame that looks like a left-seat C172 pilot view with readable panel, runway ahead, and no exterior shell blocking the camera.");
            Debug.Log("Imported C172 refined left-seat calibration renders written to " + outputDir);
        }

        private static void HideExterior(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string path = PathFor(renderer.transform);
                if (path.IndexOf("Cessna_Exterior_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    renderer.enabled = false;
                }
            }
        }

        private static void Render(Camera camera, string path)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static string PathFor(Transform transform)
        {
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
