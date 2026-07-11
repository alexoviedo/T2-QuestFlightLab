using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace QuestFlightLab.Flight.Backends
{
    /// <summary>
    /// JSBSim reads XML with native file APIs. Android StreamingAssets live
    /// inside the APK, so an explicitly selected native backend extracts the
    /// small pinned c172x runtime set to persistentDataPath before startup.
    /// </summary>
    public static class JSBSimRuntimeDataInstaller
    {
        private const string ManifestFile = "runtime_manifest.txt";

        public static IEnumerator EnsureReadableData(Action<bool, string> completed)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string destinationRoot = JSBSimRuntimeDataPaths.ExtractedDataRoot;
            string revisionFile = Path.Combine(destinationRoot, "SOURCE_REVISION.txt");
            if (File.Exists(revisionFile) &&
                File.ReadAllText(revisionFile).Contains(JSBSimNativeFlightBackend.PinnedJsbsimRevision))
            {
                completed?.Invoke(true, destinationRoot);
                yield break;
            }

            string manifestUrl = StreamingAssetUrl(ManifestFile);
            using UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestUrl);
            yield return manifestRequest.SendWebRequest();
            if (manifestRequest.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(false, $"Could not read JSBSim runtime manifest: {manifestRequest.error}");
                yield break;
            }

            string[] files = manifestRequest.downloadHandler.text.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawRelative in files)
            {
                string relative = rawRelative.Trim();
                if (string.IsNullOrEmpty(relative)) continue;
                using UnityWebRequest fileRequest = UnityWebRequest.Get(StreamingAssetUrl(relative));
                yield return fileRequest.SendWebRequest();
                if (fileRequest.result != UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(false, $"Could not extract JSBSim runtime file {relative}: {fileRequest.error}");
                    yield break;
                }

                string destination = Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                string directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = destination + ".tmp";
                File.WriteAllBytes(temporary, fileRequest.downloadHandler.data);
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(temporary, destination);
            }

            if (!File.Exists(revisionFile))
            {
                completed?.Invoke(false, "JSBSim extraction completed without its pinned revision marker.");
                yield break;
            }

            completed?.Invoke(true, destinationRoot);
#else
            string root = JSBSimRuntimeDataPaths.BundledDataRoot;
            bool available = File.Exists(Path.Combine(root, "SOURCE_REVISION.txt"));
            completed?.Invoke(available, available ? root : $"JSBSim runtime data is missing: {root}");
            yield break;
#endif
        }

        private static string StreamingAssetUrl(string relative)
        {
            string root = Application.streamingAssetsPath.TrimEnd('/', '\\');
            string suffix = $"{JSBSimRuntimeDataPaths.DataFolderName}/{relative}".Replace('\\', '/');
            return $"{root}/{suffix}";
        }
    }
}
