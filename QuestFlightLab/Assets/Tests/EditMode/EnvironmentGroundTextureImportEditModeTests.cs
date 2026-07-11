#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QuestFlightLab.Tests
{
    public class EnvironmentGroundTextureImportEditModeTests
    {
        private const string ManagedFolder = "Assets/Resources/QuestFlightLab/Environment/GroundMaterials/";
        private const int MaximumTextureSize = 1024;
        private const int AnisotropicLevel = 4;
        private const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_6x6;

        [TestCase("withered_grass_diff_1k.jpg")]
        [TestCase("sparse_grass_diff_1k.jpg")]
        [TestCase("dry_ground_01_diff_1k.jpg")]
        public void GroundTextureImportIsMipFilteredAndAndroidAstc(string fileName)
        {
            string path = ManagedFolder + fileName;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.streamingMipmaps, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.anisoLevel, Is.EqualTo(AnisotropicLevel));
            Assert.That(importer.maxTextureSize, Is.EqualTo(MaximumTextureSize));
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(MaximumTextureSize));
            Assert.That(android.format, Is.EqualTo(AndroidFormat));
        }
    }
}
#endif
