using UnityEngine;
using UnityEngine.AI;

namespace AISystem
{
    /// <summary>
    /// Moves and animates a companion NPC. NPCActions events call the public methods
    /// (Follow, Stop, GoTo, Walk, Sprint, Jump, Wave, Dance).
    ///
    /// Animator parameters: Speed (float, m/s), Jump (trigger), Wave (trigger), Dance (bool).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionController : MonoBehaviour
    {
        [Tooltip("Who to follow. Found by the 'Player' tag if left empty.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("The companion stops this far from the follow target.")]
        [SerializeField] private float followDistance = 2.5f;

        [SerializeField] private float walkSpeed   = 2f;
        [SerializeField] private float sprintSpeed = 5.5f;

        [Tooltip("The path is updated when the target moves this far.")]
        [SerializeField] private float repathDistance = 0.5f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int JumpParam  = Animator.StringToHash("Jump");
        private static readonly int WaveParam  = Animator.StringToHash("Wave");
        private static readonly int DanceParam = Animator.StringToHash("Dance");

        private NavMeshAgent _navAgent;
        private Animator _animator;
        private bool _following;
        private Vector3 _lastTargetPosition;

        public bool IsFollowing => _following;

        void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _navAgent.speed = walkSpeed;
        }

        void Start()
        {
            if (followTarget == null)
                followTarget = GameObject.FindWithTag("Player").transform;
        }

        void Update()
        {
            if (_following && (followTarget.position - _lastTargetPosition).sqrMagnitude > repathDistance * repathDistance)
                MoveTo(followTarget.position, followDistance);

            _animator.SetFloat(SpeedParam, _navAgent.velocity.magnitude);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        public void Follow()
        {
            _following = true;
            MoveTo(followTarget.position, followDistance);
        }

        public void Stop()
        {
            _following = false;
            _navAgent.ResetPath();
        }

        public void GoTo(Transform destination)
        {
            _following = false;
            MoveTo(destination.position, 0.5f);
        }

        public void Walk()   => _navAgent.speed = walkSpeed;
        public void Sprint() => _navAgent.speed = sprintSpeed;

        public void Jump()
        {
            _animator.SetBool(DanceParam, false);
            _animator.SetTrigger(JumpParam);
        }

        public void Wave()
        {
            _animator.SetBool(DanceParam, false);
            _animator.SetTrigger(WaveParam);
        }

        public void Dance()
        {
            Stop();
            _animator.SetBool(DanceParam, true);
        }

        private void MoveTo(Vector3 position, float stoppingDistance)
        {
            _animator.SetBool(DanceParam, false);
            _navAgent.stoppingDistance = stoppingDistance;
            _navAgent.SetDestination(position);
            _lastTargetPosition = position;
        }
    }
}
