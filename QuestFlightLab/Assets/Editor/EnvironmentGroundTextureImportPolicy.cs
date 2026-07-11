#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace QuestFlightLab.Editor
{
    /// <summary>Enforces the fixed Quest import budget for the three attributed CC0 ground maps.</summary>
    public sealed class EnvironmentGroundTextureImportPolicy : AssetPostprocessor
    {
        public const string ManagedFolder = "Assets/Resources/QuestFlightLab/Environment/GroundMaterials/";
        public const int MaximumTextureSize = 1024;
        public const int AnisotropicLevel = 4;
        public const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_6x6;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ManagedFolder, System.StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = AnisotropicLevel;
            importer.maxTextureSize = MaximumTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = MaximumTextureSize;
            android.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            android.format = AndroidFormat;
            android.textureCompression = TextureImporterCompression.CompressedHQ;
            android.compressionQuality = 80;
            android.crunchedCompression = false;
            android.allowsAlphaSplitting = false;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
#endif
