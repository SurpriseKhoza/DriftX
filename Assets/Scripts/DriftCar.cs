using UnityEngine;

namespace DriftX
{
    /// <summary>
    /// Raycast-suspension car with a custom tire model: slip-angle lateral force,
    /// friction circle (throttle/brake steal lateral grip), rear-wheel drive and handbrake.
    /// All values are starting points meant to be tuned.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DriftCar : MonoBehaviour
    {
        [Header("Geometry")]
        public float wheelBase = 2.6f;
        public float trackWidth = 1.6f;
        public float wheelRadius = 0.34f;
        public float mountHeight = -0.1f;

        [Header("Suspension")]
        public float restLength = 0.28f;
        public float springK = 45000f;
        public float damper = 4500f;

        [Header("Drivetrain (RWD)")]
        public float maxDriveForce = 7500f;
        public float enginePower = 150000f;
        public float brakeForce = 12000f;

        [Header("Tires")]
        public float frontGrip = 1.15f;
        public float rearGrip = 0.95f;
        public float handbrakeRearGrip = 0.45f;
        public float slipLinearRange = 0.12f;

        [Header("Steering")]
        public float maxSteerDeg = 34f;
        public float steerRateDegPerSec = 160f;
        public float steerAssist = 0.6f;

        public float downforce = 1.5f;
        public Transform[] wheelVisuals; // FL, FR, RL, RR

        Rigidbody rb;
        readonly float[] prevLen = new float[4];
        readonly float[] curLen = new float[4];
        readonly float[] spin = new float[4];
        float steerAngle;
        float flippedTime;

        public float SpeedKmh { get { return rb.linearVelocity.magnitude * 3.6f; } }
        public float SlipAngleDeg { get; private set; }
        public bool Grounded { get; private set; }

        // 0 FL, 1 FR, 2 RL, 3 RR
        public Vector3 MountLocal(int i)
        {
            return new Vector3((i % 2 == 0 ? -1f : 1f) * trackWidth * 0.5f, mountHeight, (i < 2 ? 1f : -1f) * wheelBase * 0.5f);
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 1300f;
            rb.centerOfMass = new Vector3(0f, -0.35f, 0f);
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            for (int i = 0; i < 4; i++) { prevLen[i] = restLength; curLen[i] = restLength; }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 vel = rb.linearVelocity;
            Vector3 lv = transform.InverseTransformDirection(vel);
            float speed = vel.magnitude;

            // Slip angle of the body (drift angle)
            SlipAngleDeg = speed > 2f ? Mathf.Atan2(lv.x, Mathf.Abs(lv.z)) * Mathf.Rad2Deg : 0f;

            // Steering with speed sensitivity and counter-steer assist
            float speedFactor = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(speed / 40f));
            float target = DriftInput.Steer * maxSteerDeg * speedFactor;
            if (lv.z > 0.5f && speed > 3f)
                target += Mathf.Clamp(SlipAngleDeg, -35f, 35f) * steerAssist;
            target = Mathf.Clamp(target, -maxSteerDeg * 1.3f, maxSteerDeg * 1.3f);
            steerAngle = Mathf.MoveTowards(steerAngle, target, steerRateDegPerSec * dt);

            // Drive / brake inputs
            float throttle = DriftInput.Throttle;
            float brake = DriftInput.Brake;
            bool hand = DriftInput.Handbrake;
            float driveTotal = 0f;
            if (throttle > 0f)
                driveTotal = Mathf.Min(maxDriveForce, enginePower / Mathf.Max(speed, 5f)) * throttle;
            if (brake > 0f && lv.z < 1f)
            {
                driveTotal = -maxDriveForce * 0.35f * brake; // reverse
                brake = 0f;
            }

            float wheelMass = rb.mass * 0.25f;
            Grounded = false;

            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                bool rear = !front;
                Vector3 origin = transform.TransformPoint(MountLocal(i));
                float maxLen = restLength + wheelRadius;

                RaycastHit hit;
                if (!Physics.Raycast(origin, -transform.up, out hit, maxLen, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    prevLen[i] = restLength;
                    curLen[i] = restLength;
                    continue;
                }

                Grounded = true;

                // Suspension
                float len = hit.distance - wheelRadius;
                float compression = restLength - len;
                float compVel = (prevLen[i] - len) / dt;
                prevLen[i] = len;
                curLen[i] = Mathf.Max(0f, len);
                float load = Mathf.Max(0f, springK * compression + damper * compVel);
                rb.AddForceAtPosition(hit.normal * load, hit.point);

                // Tire frame
                Vector3 fwd = transform.forward;
                if (front) fwd = Quaternion.AngleAxis(steerAngle, transform.up) * fwd;
                fwd = Vector3.ProjectOnPlane(fwd, hit.normal).normalized;
                Vector3 right = Vector3.Cross(hit.normal, fwd);

                Vector3 pv = rb.GetPointVelocity(hit.point);
                float vLong = Vector3.Dot(pv, fwd);
                float vLat = Vector3.Dot(pv, right);
                spin[i] += vLong / wheelRadius * Mathf.Rad2Deg * dt;

                float mu = front ? frontGrip : rearGrip;
                if (rear && hand) mu = handbrakeRearGrip;
                float maxF = mu * load;

                // Longitudinal force
                float fx = 0f;
                if (rear) fx += driveTotal * 0.5f;
                if (brake > 0f || (rear && hand))
                {
                    float b = (rear && hand) ? 1f : brake;
                    float cap = brakeForce * 0.25f * b;
                    fx += Mathf.Clamp(-vLong * wheelMass / dt, -cap, cap);
                }
                if (throttle <= 0f && brake <= 0f)
                    fx -= Mathf.Clamp(vLong * 40f, -300f, 300f); // engine braking / rolling resistance

                // Lateral force from slip angle (linear range, then saturates and falls off)
                float alpha = Mathf.Atan2(vLat, Mathf.Abs(vLong) + 1f);
                float sat = Mathf.Clamp(alpha / slipLinearRange, -1f, 1f);
                float falloff = Mathf.Lerp(1f, 0.78f, Mathf.InverseLerp(0.17f, 0.6f, Mathf.Abs(alpha)));
                float fy = -maxF * sat * falloff;

                // Friction circle
                float mag = Mathf.Sqrt(fx * fx + fy * fy);
                if (mag > maxF && mag > 0.001f)
                {
                    float k = maxF / mag;
                    fx *= k;
                    fy *= k;
                }

                rb.AddForceAtPosition(fwd * fx + right * fy, hit.point);
            }

            // Aero downforce
            rb.AddForce(-transform.up * downforce * speed * speed);

            // Auto-recover if flipped
            if (transform.up.y < 0.3f)
            {
                flippedTime += dt;
                if (flippedTime > 2f)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    transform.position += Vector3.up * 1.5f;
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    flippedTime = 0f;
                }
            }
            else flippedTime = 0f;
        }

        void LateUpdate()
        {
            if (wheelVisuals == null) return;
            for (int i = 0; i < wheelVisuals.Length && i < 4; i++)
            {
                if (wheelVisuals[i] == null) continue;
                Vector3 origin = transform.TransformPoint(MountLocal(i));
                wheelVisuals[i].position = origin - transform.up * curLen[i];
                float steer = i < 2 ? steerAngle : 0f;
                wheelVisuals[i].rotation = transform.rotation * Quaternion.Euler(0f, steer, 0f) * Quaternion.Euler(spin[i], 0f, 0f);
            }
        }
    }
}
