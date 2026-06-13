using UnityEngine;

namespace QuestFlightLab.Environment
{
    public class QuestSplatRuntimeConfig : ScriptableObject
    {
        public const string ResourcePath = "QuestFlightLab/Splats/QuestSplatRuntimeConfig";

        public Object sample5k;
        public Object sample50k;
        public Object sample100k;

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

        public Object AssetForBudget(int budget)
        {
            if (budget <= 5000) return sample5k;
            if (budget <= 50000) return sample50k;
            return sample100k;
        }

        public string SampleNameForBudget(int budget)
        {
            Object sample = AssetForBudget(budget);
            return sample != null ? sample.name : string.Empty;
        }
    }
}
