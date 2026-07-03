using UnityEngine;

namespace QuestFlightLab.Environment
{
    public class QuestSplatRuntimeConfig : ScriptableObject
    {
        public const string ResourcePath = "QuestFlightLab/Splats/QuestSplatRuntimeConfig";
        public const string SyntheticProfile = "synthetic";
        public const string ScenicProfile = "scenic";

        public Object sample5k;
        public Object sample50k;
        public Object sample100k;
        public Object scenicLowSample;
        public Object scenicMediumSample;
        public Object scenicHighSample;

        public Shader renderSplatsShader;
        public Shader compositeShader;
        public Shader debugPointsShader;
        public Shader debugBoxesShader;
        public ComputeShader splatUtilitiesCompute;

        public Vector3 sampleWorldPosition = new Vector3(0f, 3.0f, 18f);
        public Vector3 sampleEulerAngles = Vector3.zero;
        public float splatScale = 1.35f;
        public float opacityScale = 1.0f;
        public int sphericalHarmonicsOrder = 0;
        public int sortNthFrame = 1;

        public Vector3 scenicWorldPosition = Vector3.zero;
        public Vector3 scenicEulerAngles = new Vector3(0f, 90f, 0f);
        public float scenicSplatScale = 1.0f;
        public float scenicOpacityScale = 0.58f;
        public int scenicSortNthFrame = 1;

        public Object AssetForBudget(int budget)
        {
            if (budget <= 5000) return sample5k;
            if (budget <= 50000) return sample50k;
            return sample100k;
        }

        public Object AssetForProfile(string sampleKey, int budget)
        {
            if (IsScenicProfile(sampleKey))
            {
                if (budget <= 25000) return scenicLowSample;
                if (budget <= 50000) return scenicMediumSample;
                return scenicHighSample;
            }

            return AssetForBudget(budget);
        }

        public string SampleNameForBudget(int budget)
        {
            Object sample = AssetForBudget(budget);
            return sample != null ? sample.name : string.Empty;
        }

        public string SampleNameForProfile(string sampleKey, int budget)
        {
            Object sample = AssetForProfile(sampleKey, budget);
            return sample != null ? sample.name : string.Empty;
        }

        public Vector3 WorldPositionForProfile(string sampleKey)
        {
            return IsScenicProfile(sampleKey) ? scenicWorldPosition : sampleWorldPosition;
        }

        public Vector3 EulerAnglesForProfile(string sampleKey)
        {
            return IsScenicProfile(sampleKey) ? scenicEulerAngles : sampleEulerAngles;
        }

        public float SplatScaleForProfile(string sampleKey)
        {
            return IsScenicProfile(sampleKey) ? scenicSplatScale : splatScale;
        }

        public float OpacityScaleForProfile(string sampleKey)
        {
            return IsScenicProfile(sampleKey) ? scenicOpacityScale : opacityScale;
        }

        public int SortNthFrameForProfile(string sampleKey)
        {
            return IsScenicProfile(sampleKey) ? scenicSortNthFrame : sortNthFrame;
        }

        public static string NormalizeProfile(string sampleKey)
        {
            if (string.IsNullOrWhiteSpace(sampleKey)) return SyntheticProfile;
            string normalized = sampleKey.Trim().ToLowerInvariant().Replace("-", "_");
            return normalized == ScenicProfile ? ScenicProfile : SyntheticProfile;
        }

        public static bool IsScenicProfile(string sampleKey)
        {
            return NormalizeProfile(sampleKey) == ScenicProfile;
        }
    }
}
