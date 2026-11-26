using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DriftX
{
    /// <summary>
    /// Runtime optimizer: watches frame time and lowers render resolution (and shadow distance)
    /// when the game runs below its target frame rate, then slowly raises them again when the phone
    /// has headroom. It never goes above the tier chosen by QualityTier.
    /// </summary>
    public class AdaptiveQuality : MonoBehaviour
    {
        public float minScale = 0.5f;
        public float step = 0.05f;
        public float checkInterval = 1f;
        public float stableSecondsToRaise = 6f;
        public float relaxSeconds = 30f;

        UniversalRenderPipelineAsset urp;
        float top = 1f;
        float floor = 0.5f;
        float scale = 1f;
        float ceiling = 1f;
        float baseShadow = 50f;
        float timer, frameSum, stable, relax;
        int frames;

        public static float CurrentScale { get; private set; }

        void Start()
        {
            urp = UniversalRenderPipeline.asset;
            if (urp == null) { enabled = false; return; }
            top = urp.renderScale;
            baseShadow = urp.shadowDistance;
            floor = Mathf.Min(minScale, top);
            scale = top;
            ceiling = top;
            CurrentScale = scale;
        }

        void Update()
        {
            if (urp == null) return;
            if (Time.timeSinceLevelLoad < 3f) return; // ignore loading hitches

            frameSum += Time.unscaledDeltaTime;
            frames++;
            timer += Time.unscaledDeltaTime;
            if (timer < checkInterval) return;

            float avg = frameSum / frames;
            float elapsed = timer;
            timer = 0f;
            frameSum = 0f;
            frames = 0;

            float fps = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60f;
            float budget = 1f / fps;

            // Every so often allow the ceiling to creep back up in case the load got lighter.
            relax += elapsed;
            if (relax >= relaxSeconds)
            {
                relax = 0f;
                ceiling = Mathf.Min(top, ceiling + step);
            }

            if (avg > budget * 1.12f)
            {
                // Too slow: drop resolution and remember this level as the new ceiling.
                stable = 0f;
                if (scale > floor)
                {
                    scale = Mathf.Max(floor, scale - step);
                    ceiling = scale;
                    Apply();
                }
            }
            else if (avg <= budget * 1.05f)
            {
                // Holding the target: after a while, try one step higher (up to the ceiling).
                stable += elapsed;
                if (stable >= stableSecondsToRaise && scale < ceiling)
                {
                    scale = Mathf.Min(ceiling, scale + step);
                    stable = 0f;
                    Apply();
                }
            }
        }

        void Apply()
        {
            urp.renderScale = scale;
            float t = (top - floor) > 0.001f ? Mathf.InverseLerp(floor, top, scale) : 1f;
            urp.shadowDistance = baseShadow * Mathf.Lerp(0.5f, 1f, t);
            CurrentScale = scale;
        }
    }
}
