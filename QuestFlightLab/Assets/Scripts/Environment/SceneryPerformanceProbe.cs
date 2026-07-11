using System;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    [Serializable]
    public class SceneryPerformanceSnapshot
    {
        public int sampleCount;
        public int warmupSamplesDiscarded;
        public float averageFrameMs;
        public float minFrameMs;
        public float maxFrameMs;
        public float p95FrameMs;
        public float p99FrameMs;
        public float rawMaxFrameMs;
        public float estimatedFps;
        public int framesOver72HzBudget;
        public float framesOver72HzBudgetRatio;
    }

    public class SceneryPerformanceProbe : MonoBehaviour
    {
        public int maxSamples = 180;
        public int warmupSamples = 30;

        public int SampleCount => sampleCount;

        private int sampleCount;
        private int observedSamples;
        private float totalFrameMs;
        private float minFrameMs = float.MaxValue;
        private float maxFrameMs;
        private float rawMaxFrameMs;
        private int framesOver72HzBudget;
        private float[] samples;

        private void Update()
        {
            if (sampleCount >= maxSamples) return;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            observedSamples++;
            rawMaxFrameMs = Mathf.Max(rawMaxFrameMs, frameMs);
            if (observedSamples <= Mathf.Max(0, warmupSamples)) return;

            EnsureSampleStorage();
            samples[sampleCount] = frameMs;
            sampleCount++;
            totalFrameMs += frameMs;
            minFrameMs = Mathf.Min(minFrameMs, frameMs);
            maxFrameMs = Mathf.Max(maxFrameMs, frameMs);
            if (frameMs > 1000f / 72f) framesOver72HzBudget++;
        }

        public SceneryPerformanceSnapshot Capture()
        {
            float average = sampleCount > 0 ? totalFrameMs / sampleCount : 0f;
            return new SceneryPerformanceSnapshot
            {
                sampleCount = sampleCount,
                warmupSamplesDiscarded = Mathf.Min(observedSamples, Mathf.Max(0, warmupSamples)),
                averageFrameMs = average,
                minFrameMs = sampleCount > 0 ? minFrameMs : 0f,
                maxFrameMs = maxFrameMs,
                p95FrameMs = CalculatePercentile(samples, sampleCount, 0.95f),
                p99FrameMs = CalculatePercentile(samples, sampleCount, 0.99f),
                rawMaxFrameMs = rawMaxFrameMs,
                estimatedFps = average > 0.01f ? 1000f / average : 0f,
                framesOver72HzBudget = framesOver72HzBudget,
                framesOver72HzBudgetRatio = sampleCount > 0 ? framesOver72HzBudget / (float)sampleCount : 0f
            };
        }

        public static float CalculatePercentile(float[] values, int count, float percentile)
        {
            if (values == null || count <= 0) return 0f;
            count = Mathf.Min(count, values.Length);
            float[] sorted = new float[count];
            Array.Copy(values, sorted, count);
            Array.Sort(sorted);
            int index = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(percentile) * count) - 1, 0, count - 1);
            return sorted[index];
        }

        private void EnsureSampleStorage()
        {
            int required = Mathf.Max(1, maxSamples);
            if (samples == null || samples.Length != required) samples = new float[required];
        }

        public static long EstimateGpuBytes(int splatCount)
        {
            return Math.Max(0, splatCount) * 48L;
        }

        public static float EstimateGpuMegabytes(int splatCount)
        {
            return EstimateGpuBytes(splatCount) / (1024f * 1024f);
        }
    }
}
