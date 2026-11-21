using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Orbit camera that follows the player. The mouse rotates it while the cursor is locked.
    /// A sphere cast pulls the camera in front of walls. Put it on the Main Camera.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Tooltip("Object to follow. Found by the 'Player' tag if left empty.")]
        [SerializeField] private Transform target;

        [SerializeField] private float distance    = 4.5f;
        [SerializeField] private float pivotHeight = 1.6f;
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch    = -30f;
        [SerializeField] private float maxPitch    = 70f;

        [Tooltip("Layers the camera cannot pass through.")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField] private float collisionRadius = 0.25f;

        private PlayerInputHandler _input;
        private float _yaw;
        private float _pitch = 15f;

        void Start()
        {
            if (target == null)
                target = GameObject.FindWithTag("Player").transform;

            _input = target.GetComponentInChildren<PlayerInputHandler>();
            _yaw   = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (_input.CursorLocked)
            {
                _yaw   += _input.Look.x * sensitivity;
                _pitch  = Mathf.Clamp(_pitch - _input.Look.y * sensitivity, minPitch, maxPitch);
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot    = target.position + Vector3.up * pivotHeight;
            var back     = rotation * Vector3.back;

            float dist = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
                dist = hit.distance;

            transform.SetPositionAndRotation(pivot + back * dist, rotation);
        }
    }
}
