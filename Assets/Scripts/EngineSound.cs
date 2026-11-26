using UnityEngine;

namespace DriftX
{
    /// <summary>
    /// Procedural engine and tire-squeal sound, generated in code (no audio files needed).
    /// Lives next to the AudioListener and mixes into the output in OnAudioFilterRead.
    /// A fake gearbox makes the pitch rise and drop as the car speeds up.
    /// </summary>
    public class EngineSound : MonoBehaviour
    {
        public DriftCar car;
        public float idleHz = 40f;
        public float maxHz = 200f;
        public float volume = 0.12f;

        static readonly float[] GearTop = { 45f, 80f, 115f, 150f, 220f };

        // Written on the main thread, read on the audio thread.
        volatile float targetHz = 40f;
        volatile float targetGain;
        volatile float targetSqueal;

        // Audio-thread state.
        float hz, gain, squeal, phase, sqPhase, lp;
        uint seed = 12345u;
        int sampleRate = 48000;

        // Main-thread state.
        float rpm01;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            hz = idleHz;
        }

        void Update()
        {
            if (car == null) return;

            float speed = car.SpeedKmh;
            float throttle = DriftInput.Throttle;

            // Pick a gear from speed, then work out how high the revs are inside that gear.
            int g = 0;
            while (g < GearTop.Length - 1 && speed > GearTop[g]) g++;
            float lo = g == 0 ? 0f : GearTop[g - 1] * 0.7f;
            float target = Mathf.Clamp01((speed - lo) / (GearTop[g] - lo));
            target = Mathf.Max(target, throttle * 0.35f); // revving while wheelspinning
            rpm01 = Mathf.MoveTowards(rpm01, target, Time.deltaTime * 2.5f);

            targetHz = Mathf.Lerp(idleHz, maxHz, rpm01);
            targetGain = volume * (0.45f + 0.55f * Mathf.Max(throttle, rpm01 * 0.6f));

            float a = Mathf.Abs(car.SlipAngleDeg);
            targetSqueal = (car.Grounded && speed > 20f) ? Mathf.Clamp01((a - 8f) / 25f) : 0f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float tHz = targetHz;
            float tG = targetGain;
            float tS = targetSqueal;
            float inv = 1f / sampleRate;

            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                hz += (tHz - hz) * 0.0005f;
                gain += (tG - gain) * 0.0005f;
                squeal += (tS - squeal) * 0.001f;

                phase += hz * inv;
                if (phase >= 1f) phase -= 1f;

                float saw = phase * 2f - 1f;
                float sq = phase < 0.5f ? 1f : -1f;
                float tone = saw * 0.55f + sq * 0.2f + Mathf.Sin(phase * 6.2831853f * 2f) * 0.3f;
                lp += (tone - lp) * 0.3f; // soften the harsh edges
                float s = lp * gain;

                if (squeal > 0.01f)
                {
                    seed = seed * 1664525u + 1013904223u;
                    float noise = (seed >> 8) * (1f / 8388608f) - 1f;
                    sqPhase += (850f + 250f * squeal) * inv;
                    if (sqPhase >= 1f) sqPhase -= 1f;
                    s += (Mathf.Sin(sqPhase * 6.2831853f) * 0.5f + noise * 0.25f) * squeal * 0.05f;
                }

                for (int c = 0; c < channels; c++) data[i + c] += s;
            }
        }
    }
}
