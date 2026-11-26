using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DriftX
{
    /// <summary>Picks Low/Medium/High at startup from system RAM and adjusts the URP asset at runtime.</summary>
    public class QualityTier : MonoBehaviour
    {
        public enum Tier { Low, Medium, High }
        public static string CurrentName = "?";

        void Awake()
        {
            int ramMb = SystemInfo.systemMemorySize;
            Tier t = ramMb <= 3500 ? Tier.Low : (ramMb <= 6500 ? Tier.Medium : Tier.High);
            Apply(t);
            Time.fixedDeltaTime = 0.01f;
            Debug.Log("DriftX: RAM " + ramMb + " MB, tier " + t + ", API " + SystemInfo.graphicsDeviceType);
        }

        public static void Apply(Tier t)
        {
            CurrentName = t.ToString();
            QualitySettings.vSyncCount = 0;
            var urp = UniversalRenderPipeline.asset;
            switch (t)
            {
                case Tier.Low:
                    Application.targetFrameRate = 30;
                    if (urp != null) { urp.renderScale = 0.7f; urp.msaaSampleCount = 1; urp.shadowDistance = 30f; urp.supportsHDR = false; }
                    break;
                case Tier.Medium:
                    Application.targetFrameRate = 60;
                    if (urp != null) { urp.renderScale = 0.9f; urp.msaaSampleCount = 2; urp.shadowDistance = 60f; urp.supportsHDR = true; }
                    break;
                default:
                    Application.targetFrameRate = 60;
                    if (urp != null) { urp.renderScale = 1f; urp.msaaSampleCount = 4; urp.shadowDistance = 100f; urp.supportsHDR = true; }
                    break;
            }
        }
    }
}
