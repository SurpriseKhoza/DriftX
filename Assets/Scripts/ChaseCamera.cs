using UnityEngine;

namespace DriftX
{
    /// <summary>
    /// Chase camera that blends car heading with velocity direction so drifts stay readable.
    /// At speed it widens the field of view and pulls back a little for a stronger sense of speed.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public Rigidbody body;
        public float distance = 7f;
        public float height = 2.6f;
        public float lookAhead = 1.5f;
        public float followSpeed = 5f;
        public float fovKick = 14f;
        public float pullBack = 1.5f;
        public float speedForMaxEffectKmh = 140f;

        Vector3 heading = Vector3.forward;
        Camera cam;
        float baseFov = 65f;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;

            if (target != null)
            {
                Vector3 f = target.forward; f.y = 0f;
                if (f.sqrMagnitude > 0.001f) heading = f.normalized;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 f = target.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.001f) return;
            f.Normalize();

            Vector3 desired = f;
            if (body != null)
            {
                Vector3 v = body.linearVelocity; v.y = 0f;
                if (v.magnitude > 5f) desired = Vector3.Slerp(f, v.normalized, 0.5f);
            }
            heading = Vector3.Slerp(heading, desired, Time.deltaTime * followSpeed).normalized;

            float speedT = 0f;
            if (body != null) speedT = Mathf.Clamp01(body.linearVelocity.magnitude * 3.6f / speedForMaxEffectKmh);

            float dist = distance + pullBack * speedT;
            transform.position = target.position - heading * dist + Vector3.up * height;
            transform.LookAt(target.position + Vector3.up + heading * lookAhead);

            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + fovKick * speedT, Time.deltaTime * 3f);
        }
    }
}
