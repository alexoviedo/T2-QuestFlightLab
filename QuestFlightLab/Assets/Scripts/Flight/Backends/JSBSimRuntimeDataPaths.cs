using System.IO;
using UnityEngine;

namespace QuestFlightLab.Flight.Backends
{
    public static class JSBSimRuntimeDataPaths
    {
        public const string DataFolderName = "JSBSim-1.3.1";

        public static string BundledDataRoot => Path.Combine(Application.streamingAssetsPath, DataFolderName);
        public static string ExtractedDataRoot => Path.Combine(Application.persistentDataPath, DataFolderName);

        public static string DefaultReadableDataRoot
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return ExtractedDataRoot;
#else
                return BundledDataRoot;
#endif
            }
        }
    }
}
