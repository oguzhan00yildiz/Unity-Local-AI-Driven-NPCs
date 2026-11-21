using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Turns a world-space label (TextMesh, health bar) to face the main camera every frame,
    /// so it never shows mirrored. Only the yaw follows the camera when lockPitch is on.
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        [Tooltip("Keep the label upright and only turn it around the vertical axis.")]
        [SerializeField] private bool lockPitch = true;

        private Transform _cam;

        void Start()
        {
            _cam = Camera.main.transform;
        }

        void LateUpdate()
        {
            // Text reads correctly when its forward points away from the camera
            Vector3 dir = transform.position - _cam.position;
            if (lockPitch) dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
