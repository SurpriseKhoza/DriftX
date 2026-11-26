using UnityEngine;

namespace DriftX
{
    /// <summary>Drift scoring (slip angle x speed x chain multiplier) and a simple HUD.</summary>
    public class DriftScore : MonoBehaviour
    {
        public DriftCar car;
        public float minSpeedKmh = 25f;
        public float minAngleDeg = 12f;
        public float maxAngleDeg = 110f;
        public float graceSeconds = 1f;

        public float Score { get; private set; }
        public float Best { get; private set; }
        public float Chain { get; private set; }
        public float Multiplier { get; private set; }

        float driftTime;
        float grace;
        GUIStyle style;
        GUIStyle small;

        void Awake() { Multiplier = 1f; }

        void Update()
        {
            if (car == null) return;
            float a = Mathf.Abs(car.SlipAngleDeg);
            bool drifting = car.Grounded && car.SpeedKmh > minSpeedKmh && a > minAngleDeg && a < maxAngleDeg;

            if (drifting)
            {
                grace = 0f;
                driftTime += Time.deltaTime;
                Multiplier = Mathf.Min(8f, 1f + driftTime * 0.5f);
                float perSec = car.SpeedKmh * 0.2f * Mathf.Clamp(a / 30f, 0.3f, 2f) * Multiplier;
                Chain += perSec * Time.deltaTime;
            }
            else if (Chain > 0f)
            {
                grace += Time.deltaTime;
                if (grace >= graceSeconds)
                {
                    Score += Chain;
                    if (Score > Best) Best = Score;
                    Chain = 0f;
                    driftTime = 0f;
                    Multiplier = 1f;
                    grace = 0f;
                }
            }
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.fontSize = Mathf.RoundToInt(Screen.height * 0.045f);
                style.normal.textColor = Color.white;

                // Created once (not every frame) so the HUD doesn't generate garbage on phones.
                small = new GUIStyle(style);
                small.fontSize = Mathf.RoundToInt(Screen.height * 0.028f);
            }
            float w = Screen.width * 0.6f;
            float h = Screen.height * 0.06f;
            GUI.Label(new Rect(20, 10, w, h), "Score " + Mathf.RoundToInt(Score), style);
            GUI.Label(new Rect(20, 10 + h, w, h), "Chain " + Mathf.RoundToInt(Chain) + "  x" + Multiplier.ToString("0.0"), style);
            if (car != null)
                GUI.Label(new Rect(20, 10 + h * 2, w, h), Mathf.RoundToInt(car.SpeedKmh) + " km/h", style);
            GUI.Label(new Rect(20, Screen.height * 0.30f, w, h),
                SystemInfo.graphicsDeviceType + " | " + QualityTier.CurrentName + " | res " + Mathf.RoundToInt(AdaptiveQuality.CurrentScale * 100f) + "%", small);
        }
    }
}
