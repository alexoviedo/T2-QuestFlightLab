using UnityEditor;
using UnityEngine;

namespace QuestFlightLab.Editor
{
    [InitializeOnLoad]
    public static class JSBSimPluginImporterConfigurator
    {
        private static readonly string[] WindowsPlugins =
        {
            "Assets/Plugins/JSBSim/x86_64/JSBSim.dll",
            "Assets/Plugins/JSBSim/x86_64/qfl_jsbsim_native.dll"
        };

        private static readonly string[] AndroidPlugins =
        {
            "Assets/Plugins/Android/libs/arm64-v8a/libJSBSim.so",
            "Assets/Plugins/Android/libs/arm64-v8a/libqfl_jsbsim_native.so"
        };

        static JSBSimPluginImporterConfigurator()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Quest Flight Lab/Configure JSBSim Native Plugins")]
        public static void Configure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in WindowsPlugins)
            {
                ConfigurePlugin(path, editor: true, windows: true, android: false);
            }

            foreach (string path in AndroidPlugins)
            {
                ConfigurePlugin(path, editor: false, windows: false, android: true);
            }
        }

        private static void ConfigurePlugin(string path, bool editor, bool windows, bool android)
        {
            if (!(AssetImporter.GetAtPath(path) is PluginImporter importer))
            {
                Debug.LogWarning($"[QuestFlightLab][JSBSimNative] Plugin importer missing: {path}");
                return;
            }

            bool changed = false;
            changed |= Set(ref changed, importer.GetCompatibleWithAnyPlatform(), false, importer.SetCompatibleWithAnyPlatform);
            changed |= Set(ref changed, importer.GetCompatibleWithEditor(), editor, importer.SetCompatibleWithEditor);
            changed |= Set(ref changed, importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), windows,
                value => importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, value));
            changed |= Set(ref changed, importer.GetCompatibleWithPlatform(BuildTarget.Android), android,
                value => importer.SetCompatibleWithPlatform(BuildTarget.Android, value));

            if (editor)
            {
                changed |= SetData(importer.GetEditorData("OS"), "Windows", value => importer.SetEditorData("OS", value));
                changed |= SetData(importer.GetEditorData("CPU"), "x86_64", value => importer.SetEditorData("CPU", value));
            }

            if (windows)
            {
                changed |= SetData(
                    importer.GetPlatformData(BuildTarget.StandaloneWindows64, "CPU"),
                    "x86_64",
                    value => importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", value));
            }

            if (android)
            {
                changed |= SetData(
                    importer.GetPlatformData(BuildTarget.Android, "CPU"),
                    "ARM64",
                    value => importer.SetPlatformData(BuildTarget.Android, "CPU", value));
            }

            if (changed) importer.SaveAndReimport();
        }

        private static bool Set(ref bool changed, bool current, bool target, System.Action<bool> setter)
        {
            if (current == target) return false;
            setter(target);
            changed = true;
            return true;
        }

        private static bool SetData(string current, string target, System.Action<string> setter)
        {
            if (current == target) return false;
            setter(target);
            return true;
        }
    }
}
