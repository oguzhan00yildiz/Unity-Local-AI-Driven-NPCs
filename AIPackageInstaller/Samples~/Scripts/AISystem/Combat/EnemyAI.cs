using UnityEngine;
using UnityEngine.AI;

namespace AISystem
{
    /// <summary>
    /// Simple melee enemy: waits at its post, chases the nearest player-side character in aggro range,
    /// attacks in reach, walks back home when it loses the target. Fights back when hit.
    /// </summary>
    [RequireComponent(typeof(MeleeCombatant))]
    public class EnemyAI : MonoBehaviour
    {
        [SerializeField] private float aggroRange = 12f;
        [Tooltip("The enemy gives up when the target gets this far from its home position.")]
        [SerializeField] private float leashRange = 25f;
        [SerializeField] private float walkSpeed  = 1.6f;
        [Tooltip("Seconds between target searches.")]
        [SerializeField] private float searchInterval = 0.4f;
        [Tooltip("Seconds the body stays after death.")]
        [SerializeField] private float corpseTime = 8f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");

        private MeleeCombatant _combat;
        private NavMeshAgent _nav;
        private Animator _animator;
        private Vector3 _home;
        private Health _target;
        private float _nextSearch;

        void Awake()
        {
            _combat   = GetComponent<MeleeCombatant>();
            _nav      = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _home     = transform.position;
        }

        void Start()
        {
            _combat.Health.OnDamaged += HandleDamaged;
            _combat.Health.OnDied    += HandleDied;
        }

        void OnDestroy()
        {
            _combat.Health.OnDamaged -= HandleDamaged;
            _combat.Health.OnDied    -= HandleDied;
        }

        void Update()
        {
            if (Time.time >= _nextSearch)
            {
                _nextSearch = Time.time + searchInterval;
                if (_target == null || _target.IsDead || (_target.transform.position - _home).sqrMagnitude > leashRange * leashRange)
                    _target = _combat.FindNearestHostile(transform.position, aggroRange);
            }

            if (_target != null && !_combat.Engage(_target))
                _target = null;

            if (_target == null && (transform.position - _home).sqrMagnitude > 1f)
            {
                _nav.speed = walkSpeed;
                _nav.stoppingDistance = 0.2f;
                if (!_nav.hasPath) _nav.SetDestination(_home);
            }

            _animator.SetFloat(SpeedId, _nav.velocity.magnitude);
        }

        private void HandleDamaged(float amount, GameObject source)
        {
            if (source == null) return;
            var attacker = source.GetComponent<Health>();
            if (attacker != null && !attacker.IsDead) _target = attacker;
        }

        private void HandleDied()
        {
            _nav.enabled = false;
            foreach (var col in GetComponents<Collider>()) col.enabled = false;
            _animator.SetFloat(SpeedId, 0f);
            enabled = false;
            Destroy(gameObject, corpseTime);
        }
    }
}
