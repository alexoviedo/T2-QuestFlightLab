using UnityEngine;

namespace QuestFlightLab.Flight
{
    public class PerformanceHud : MonoBehaviour
    {
        public TextMesh text;
        public float refreshIntervalSeconds = 0.5f;
        public float CurrentFps { get; private set; }

        private int _frames;
        private float _windowStart;

        private void Start()
        {
            _windowStart = Time.unscaledTime;
        }

        private void Update()
        {
            _frames++;
            float elapsed = Time.unscaledTime - _windowStart;
            if (elapsed < refreshIntervalSeconds) return;

            CurrentFps = _frames / Mathf.Max(0.001f, elapsed);
            _frames = 0;
            _windowStart = Time.unscaledTime;

            if (text != null)
            {
                text.text = $"FPS {CurrentFps:F0}";
            }
        }
    }
}

