using System;
using System.IO;
using UnityEngine;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class CockpitViewpointCalibrationState
    {
        public int schemaVersion;
        public string generatedUtc;
        public string sceneryMode;
        public string demoMode;
        public Vector3 importedC172CockpitModelEye;
        public Vector3 importedC172PilotViewOffset;
        public float importedC172CockpitYawDeg;
        public Vector3 pilotEyeLocal;
        public Vector3 importedC172LocalPosition;
        public string instructions;
    }

    public static class CockpitViewpointPersistence
    {
        public const int SchemaVersion = 2;
        public const string DirectoryName = "seat_calibration";
        public const string CurrentFileName = "seat_calibration_current.json";

        public static string DirectoryPath(string persistentDataRoot = null)
        {
            string root = string.IsNullOrWhiteSpace(persistentDataRoot)
                ? Application.persistentDataPath
                : persistentDataRoot;
            return Path.Combine(root, "QuestFlightLab", DirectoryName);
        }

        public static string CurrentPath(string persistentDataRoot = null)
        {
            return Path.Combine(DirectoryPath(persistentDataRoot), CurrentFileName);
        }

        public static string SaveCurrent(CockpitViewpointCalibrationState state, string persistentDataRoot = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.schemaVersion <= 0) state.schemaVersion = SchemaVersion;
            if (string.IsNullOrWhiteSpace(state.generatedUtc)) state.generatedUtc = DateTime.UtcNow.ToString("O");

            string dir = DirectoryPath(persistentDataRoot);
            Directory.CreateDirectory(dir);
            string timestampedPath = Path.Combine(dir, $"seat_calibration_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            string currentPath = CurrentPath(persistentDataRoot);
            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(timestampedPath, json);
            File.WriteAllText(currentPath, json);
            return currentPath;
        }

        public static bool TryLoadCurrent(
            out CockpitViewpointCalibrationState state,
            out string path,
            out string error,
            string persistentDataRoot = null)
        {
            state = null;
            error = string.Empty;
            path = CurrentPath(persistentDataRoot);
            if (!File.Exists(path)) return false;

            try
            {
                state = JsonUtility.FromJson<CockpitViewpointCalibrationState>(File.ReadAllText(path));
                if (state == null)
                {
                    error = "empty or invalid calibration JSON";
                    return false;
                }

                if (state.schemaVersion < SchemaVersion)
                {
                    error = $"old schema {state.schemaVersion}; expected {SchemaVersion}";
                    state = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                state = null;
                return false;
            }
        }

        public static bool DeleteCurrent(out string path, out string error, string persistentDataRoot = null)
        {
            path = CurrentPath(persistentDataRoot);
            error = string.Empty;

            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
