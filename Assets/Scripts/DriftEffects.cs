using UnityEngine;
using UnityEngine.Rendering;

namespace DriftX
{
    /// <summary>
    /// Tire effects for the rear wheels: skid marks (trail ribbons laid on the ground) and tire smoke
    /// (particles). Both switch on while the car is sliding, using the handbrake, or spinning the wheels.
    /// Materials are created by the scene generator so their shaders are included in the build.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class DriftEffects : MonoBehaviour
    {
        public DriftCar car;
        public Material skidMaterial;
        public Material smokeMaterial;
        public float skidTime = 10f;
        public float skidWidth = 0.28f;
        public float minSlipDeg = 10f;
        public float minSpeedKmh = 12f;
        public float maxSmokeRate = 45f;

        readonly TrailRenderer[] trails = new TrailRenderer[4];
        readonly ParticleSystem[] smoke = new ParticleSystem[4];
        Material smokeInstance;
        Texture2D softCircle;

        void Start()
        {
            if (car == null) car = GetComponent<DriftCar>();
            if (car == null) { enabled = false; return; }

            if (smokeMaterial != null)
            {
                softCircle = MakeSoftCircle(64);
                smokeInstance = new Material(smokeMaterial);
                smokeInstance.mainTexture = softCircle;
                for (int i = 2; i < 4; i++) smoke[i] = CreateSmoke(i); // rear wheels only
            }
        }

        void LateUpdate()
        {
            if (car == null || car.wheelVisuals == null || car.wheelVisuals.Length < 4) return;

            float speed = car.SpeedKmh;
            float slip = Mathf.Abs(car.SlipAngleDeg);
            bool moving = car.Grounded && speed > minSpeedKmh;
            bool sliding = moving && (slip > minSlipDeg || DriftInput.Handbrake);
            bool burnout = car.Grounded && DriftInput.Throttle > 0.8f && DriftInput.Brake < 0.1f && speed < 20f;

            float smokeAmount = 0f;
            if (sliding) smokeAmount = Mathf.Clamp01((slip - 6f) / 30f);
            if (DriftInput.Handbrake && moving) smokeAmount = Mathf.Max(smokeAmount, 0.5f);
            if (burnout) smokeAmount = Mathf.Max(smokeAmount, 0.6f);
            bool marking = sliding || burnout;

            for (int i = 2; i < 4; i++)
            {
                Transform w = car.wheelVisuals[i];
                if (w == null) continue;

                // Point where the tire touches the ground (a little above it so marks don't flicker).
                Vector3 ground = w.position - car.transform.up * (car.wheelRadius - 0.04f);

                if (marking)
                {
                    if (trails[i] == null) trails[i] = StartSkid(ground);
                    trails[i].transform.position = ground;
                }
                else if (trails[i] != null)
                {
                    StopSkid(i);
                }

                var ps = smoke[i];
                if (ps != null)
                {
                    ps.transform.position = ground + Vector3.up * 0.1f;
                    var em = ps.emission;
                    em.rateOverTime = smokeAmount * maxSmokeRate;
                }
            }
        }

        TrailRenderer StartSkid(Vector3 pos)
        {
            var go = new GameObject("Skid");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // Z axis points up, so the ribbon lies flat
            var tr = go.AddComponent<TrailRenderer>();
            if (skidMaterial != null) tr.sharedMaterial = skidMaterial;
            tr.time = skidTime;
            tr.widthMultiplier = skidWidth;
            tr.minVertexDistance = 0.2f;
            tr.alignment = LineAlignment.TransformZ;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.numCapVertices = 0;
            tr.emitting = true;
            return tr;
        }

        void StopSkid(int i)
        {
            var tr = trails[i];
            trails[i] = null;
            if (tr == null) return;
            tr.emitting = false;
            Destroy(tr.gameObject, skidTime + 1f); // let the finished mark fade, then clean up
        }

        ParticleSystem CreateSmoke(int i)
        {
            var go = new GameObject("Smoke" + i);
            go.transform.position = transform.position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);
            main.gravityModifier = -0.03f;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f));

            var colourOverLife = ps.colorOverLifetime;
            colourOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.12f), new GradientAlphaKey(0f, 1f) });
            colourOverLife.color = grad;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            if (smokeInstance != null) rend.sharedMaterial = smokeInstance;

            ps.Play();
            return ps;
        }

        static Texture2D MakeSoftCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}
