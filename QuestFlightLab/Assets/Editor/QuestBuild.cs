using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuestFlightLab.Editor
{
    public static class QuestBuild
    {
        public static void PerformAndroidBuild()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            string scenePath = "Assets/Scenes/InputLab.unity";
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException($"Scene not found: {scenePath}. Run QuestProjectBootstrap.CreateInputLabScene first.");
            }

            string outDir = Path.GetFullPath("Builds/Android");
            Directory.CreateDirectory(outDir);
            string apkPath = Path.Combine(outDir, "QuestFlightLab-v0.1-dev.apk");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Android build failed: {report.summary.result}");
            }

            Debug.Log($"[QuestFlightLab] Build succeeded: {apkPath}");
        }
    }
}

