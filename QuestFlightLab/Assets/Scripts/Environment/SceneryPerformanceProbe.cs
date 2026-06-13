using System;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    [Serializable]
    public class SceneryPerformanceSnapshot
    {
        public int sampleCount;
        public float averageFrameMs;
        public float minFrameMs;
        public float maxFrameMs;
        public float estimatedFps;
    }

    public class SceneryPerformanceProbe : MonoBehaviour
    {
        public int maxSamples = 180;

        private int sampleCount;
        private float totalFrameMs;
        private float minFrameMs = float.MaxValue;
        private float maxFrameMs;

        private void Update()
        {
            if (sampleCount >= maxSamples) return;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            sampleCount++;
            totalFrameMs += frameMs;
            minFrameMs = Mathf.Min(minFrameMs, frameMs);
            maxFrameMs = Mathf.Max(maxFrameMs, frameMs);
        }

        public SceneryPerformanceSnapshot Capture()
        {
            float average = sampleCount > 0 ? totalFrameMs / sampleCount : 0f;
            return new SceneryPerformanceSnapshot
            {
                sampleCount = sampleCount,
                averageFrameMs = average,
                minFrameMs = sampleCount > 0 ? minFrameMs : 0f,
                maxFrameMs = maxFrameMs,
                estimatedFps = average > 0.01f ? 1000f / average : 0f
            };
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
