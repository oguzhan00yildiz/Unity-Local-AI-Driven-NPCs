using UnityEngine;
using UnityEngine.AI;

namespace AISystem
{
    /// <summary>
    /// Walks a background villager between waypoints on the NavMesh and waits at each one.
    /// Drives the Animator's Speed float (m/s) like EnemyAI and CompanionController.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class VillagerWander : MonoBehaviour
    {
        [Tooltip("Places the villager walks between, e.g. the well, the market, house doors. One is picked at random each time.")]
        [SerializeField] private Transform[] waypoints;

        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private Vector2 waitSeconds = new Vector2(3f, 9f);

        [Tooltip("Villagers stop at a random spot this far from the waypoint, so several can share one place.")]
        [SerializeField] private float spread = 1.5f;

        [Tooltip("Seconds without moving before the villager gives up and counts as arrived.")]
        [SerializeField] private float stuckSeconds = 3f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");

        private NavMeshAgent _nav;
        private Animator _animator;
        private float _waitUntil;
        private float _stuckTime;
        private int _current = -1;

        void Awake()
        {
            _nav = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _nav.speed = walkSpeed;
        }

        void Start()
        {
            // Stagger the first walk so villagers do not all leave at once
            _waitUntil = Time.time + Random.Range(0f, waitSeconds.y);
        }

        void Update()
        {
            float speed = _nav.velocity.magnitude;
            _animator.SetFloat(SpeedId, speed);

            if (_nav.pathPending) return;
            if (_nav.hasPath && _nav.remainingDistance > _nav.stoppingDistance)
            {
                // Another villager can stand on the spot; stop trying after a while
                _stuckTime = speed < 0.1f ? _stuckTime + Time.deltaTime : 0f;
                if (_stuckTime < stuckSeconds) return;
                _nav.ResetPath();
            }

            if (_waitUntil < 0f)
            {
                _stuckTime = 0f;
                _waitUntil = Time.time + Random.Range(waitSeconds.x, waitSeconds.y);
                return;
            }
            if (Time.time < _waitUntil) return;

            _waitUntil = -1f;
            _current = NextWaypoint();
            var target = waypoints[_current].position;
            var offset = Random.insideUnitCircle * spread;
            target += new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(target, out var hit, spread, NavMesh.AllAreas))
                target = hit.position;
            _nav.SetDestination(target);
        }

        private int NextWaypoint()
        {
            if (waypoints.Length < 2) return 0;
            int next = Random.Range(0, waypoints.Length - 1);
            return next >= _current ? next + 1 : next;
        }
    }
}
