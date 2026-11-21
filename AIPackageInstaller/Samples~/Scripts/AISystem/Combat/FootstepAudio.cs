using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Plays a footstep each time the character has walked one stride on the ground.
    /// Works for any mover (CharacterController, NavMeshAgent) without animation events. 3D from the character.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        [Tooltip("Metres walked between two steps.")]
        [SerializeField] private float strideLength = 0.75f;

        [Tooltip("Steps above this height over the ground pivot are skipped (jumping, falling).")]
        [SerializeField] private float maxHeightChangePerStep = 0.3f;

        private AudioSource _source;
        private Vector3 _lastPosition;
        private float _walked;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _lastPosition = transform.position;
        }

        void Update()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;

            // Teleports and big drops are not steps
            if (Mathf.Abs(delta.y) > maxHeightChangePerStep) { _walked = 0f; return; }
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > 2f) { _walked = 0f; return; }

            _walked += distance;
            if (_walked < strideLength) return;

            _walked = 0f;
            _source.pitch = Random.Range(0.9f, 1.1f);
            _source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }
    }
}
