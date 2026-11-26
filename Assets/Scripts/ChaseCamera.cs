using UnityEngine;

namespace DriftX
{
    /// <summary>Chase camera that blends car heading with velocity direction so drifts stay readable.</summary>
    public class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public Rigidbody body;
        public float distance = 7f;
        public float height = 2.6f;
        public float lookAhead = 1.5f;
        public float followSpeed = 5f;

        Vector3 heading = Vector3.forward;

        void Start()
        {
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

            transform.position = target.position - heading * distance + Vector3.up * height;
            transform.LookAt(target.position + Vector3.up + heading * lookAhead);
        }
    }
}
