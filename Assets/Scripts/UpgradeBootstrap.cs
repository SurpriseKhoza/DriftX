using UnityEngine;

namespace DriftX
{
    /// <summary>
    /// Attaches the optional upgrade systems when the game starts, so the scene generator
    /// (CIBuild) does not need to know about them.
    /// </summary>
    public static class UpgradeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var car = Object.FindFirstObjectByType<DriftCar>();
            if (car == null) return;

            var holder = new GameObject("Upgrades");
            holder.AddComponent<AdaptiveQuality>();

            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener != null)
            {
                var engine = listener.gameObject.AddComponent<EngineSound>();
                engine.car = car;
            }
        }
    }
}
