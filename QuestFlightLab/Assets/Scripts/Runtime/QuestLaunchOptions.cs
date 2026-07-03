using System;
using UnityEngine;

namespace QuestFlightLab.Runtime
{
    public static class QuestLaunchOptions
    {
        public const string SceneryModeKey = "qfl_scenery_mode";
        public const string DemoModeKey = "qfl_demo_mode";
        public const string PlaytestHudKey = "qfl_playtest_hud";
        public const string VerboseHudKey = "qfl_verbose_hud";
        public const string SplatDiagnosticKey = "qfl_splat_diagnostic";

        private const string LogPrefix = "[QuestFlightLab][LaunchOptions]";

        public static string SceneryMode()
        {
            return Normalize(ReadString(SceneryModeKey, "mesh"));
        }

        public static string DemoMode()
        {
            return Normalize(ReadString(DemoModeKey, string.Empty));
        }

        public static bool ShortPlaytestDemoRequested()
        {
            string mode = DemoMode();
            return mode == "short" || mode == "short_playtest" || mode == "demo" || mode == "demo_pilot";
        }

        public static bool PlaytestHudEnabled()
        {
            string explicitValue = ReadString(PlaytestHudKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(explicitValue))
            {
                return ParseBool(explicitValue, true);
            }

            return Application.platform == RuntimePlatform.Android || ShortPlaytestDemoRequested();
        }

        public static bool VerboseHudEnabled()
        {
            return ReadBool(VerboseHudKey, false);
        }

        public static bool SplatDiagnosticEnabled()
        {
            return ReadBool(SplatDiagnosticKey, false);
        }

        public static bool ReadBool(string key, bool fallback)
        {
            string value = ReadString(key, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : ParseBool(value, fallback);
        }

        public static string ReadString(string key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;

            string value = ReadAndroidIntentExtra(key);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            value = System.Environment.GetEnvironmentVariable(ToEnvironmentKey(key));
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            value = System.Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                if (TryReadCommandLineValue(arg, key, out value))
                {
                    return value.Trim();
                }
            }

            return fallback;
        }

        private static bool TryReadCommandLineValue(string arg, string key, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(arg)) return false;

            string[] prefixes =
            {
                key + "=",
                "-" + key + "=",
                "--" + key + "=",
                ToEnvironmentKey(key) + "=",
                "-" + ToEnvironmentKey(key) + "=",
                "--" + ToEnvironmentKey(key) + "="
            };

            foreach (string prefix in prefixes)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefix.Length);
                    return true;
                }
            }

            return false;
        }

        private static string ReadAndroidIntentExtra(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    return intent.Call<string>("getStringExtra", key);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Could not read Android intent extra {key}: {ex.Message}");
            }
#endif
            return string.Empty;
        }

        private static string ToEnvironmentKey(string key)
        {
            return key.Trim().ToUpperInvariant();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant().Replace('-', '_');
        }

        private static bool ParseBool(string value, bool fallback)
        {
            string normalized = Normalize(value);
            if (normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on")
            {
                return true;
            }

            if (normalized == "0" || normalized == "false" || normalized == "no" || normalized == "off")
            {
                return false;
            }

            return fallback;
        }
    }
}
