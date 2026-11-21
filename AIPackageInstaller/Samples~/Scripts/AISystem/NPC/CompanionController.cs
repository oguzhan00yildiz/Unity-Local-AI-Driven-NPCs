using System;
using UnityEngine;
using UnityEngine.AI;

namespace AISystem
{
    /// <summary>
    /// Moves and animates a companion NPC. NPCActions events call the public methods
    /// (Follow, Stop, GoTo, Walk, Sprint, Jump, Wave, Dance, Fight).
    /// With a MeleeCombatant on the same object she can fight: Fight() attacks enemies near the player
    /// until none are left, then she follows again. She also fights back when she or the player is hit.
    ///
    /// Animator parameters: Speed (float, m/s), Jump (trigger), Wave (trigger), Dance (bool).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionController : MonoBehaviour
    {
        private enum Mode { Idle, Follow, GoTo, Fight }

        [Tooltip("Who to follow. Found by the 'Player' tag if left empty.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("The companion stops this far from the follow target.")]
        [SerializeField] private float followDistance = 2.5f;

        [SerializeField] private float walkSpeed   = 2f;
        [SerializeField] private float sprintSpeed = 5.5f;

        [Tooltip("The path is updated when the target moves this far.")]
        [SerializeField] private float repathDistance = 0.5f;

        [Header("Combat (needs MeleeCombatant)")]
        [Tooltip("Enemies within this distance of the player are attacked in Fight mode.")]
        [SerializeField] private float fightRadius = 15f;
        [Tooltip("Fight back by herself when she or the player is hit by an enemy.")]
        [SerializeField] private bool defendAutomatically = true;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int JumpParam  = Animator.StringToHash("Jump");
        private static readonly int WaveParam  = Animator.StringToHash("Wave");
        private static readonly int DanceParam = Animator.StringToHash("Dance");

        private NavMeshAgent _navAgent;
        private Animator _animator;
        private MeleeCombatant _combat;
        private Health _playerHealth;
        private Mode _mode = Mode.Idle;
        private Mode _modeBeforeFight = Mode.Follow;
        private Health _target;
        private Vector3 _lastTargetPosition;

        public bool IsFollowing => _mode == Mode.Follow || (_mode == Mode.Fight && _modeBeforeFight == Mode.Follow);
        public bool IsFighting  => _mode == Mode.Fight;

        /// <summary>Fires when the companion starts a fight (by command or to defend).</summary>
        public event Action OnFightStarted;

        void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _combat   = GetComponent<MeleeCombatant>();
            _navAgent.speed = walkSpeed;
        }

        void Start()
        {
            if (followTarget == null)
                followTarget = GameObject.FindWithTag("Player").transform;

            if (_combat == null) return;
            _combat.Health.OnDamaged += HandleAttacked;
            _combat.Health.OnDied    += HandleDied;
            _playerHealth = followTarget.GetComponent<Health>();
            if (_playerHealth != null) _playerHealth.OnDamaged += HandleAttacked;
        }

        void OnDestroy()
        {
            if (_combat == null) return;
            _combat.Health.OnDamaged -= HandleAttacked;
            _combat.Health.OnDied    -= HandleDied;
            if (_playerHealth != null) _playerHealth.OnDamaged -= HandleAttacked;
        }

        void Update()
        {
            if (_mode == Mode.Fight)
                UpdateFight();
            else if (_mode == Mode.Follow && (followTarget.position - _lastTargetPosition).sqrMagnitude > repathDistance * repathDistance)
                MoveTo(followTarget.position, followDistance);

            _animator.SetFloat(SpeedParam, _navAgent.velocity.magnitude);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        public void Follow()
        {
            _mode = Mode.Follow;
            MoveTo(followTarget.position, followDistance);
        }

        public void Stop()
        {
            _mode = Mode.Idle;
            _combat?.Disengage();
            _navAgent.ResetPath();
        }

        public void GoTo(Transform destination)
        {
            _mode = Mode.GoTo;
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

        /// <summary>Attacks enemies near the player until none are left, then returns to what she did before.</summary>
        public void Fight()
        {
            if (_combat == null || _combat.Health.IsDead) return;
            if (_mode != Mode.Fight)
            {
                _modeBeforeFight = _mode == Mode.Idle ? Mode.Follow : _mode;
                _mode = Mode.Fight;
                _animator.SetBool(DanceParam, false);
                OnFightStarted?.Invoke();
            }
            _target = null;
        }

        // ── Combat ────────────────────────────────────────────────────────────

        private void UpdateFight()
        {
            if (_target == null || _target.IsDead)
                _target = _combat.FindNearestHostile(followTarget.position, fightRadius);

            if (_target != null && _combat.Engage(_target)) return;

            // No enemies left near the player
            _target = null;
            _navAgent.speed = walkSpeed;
            if (_modeBeforeFight == Mode.Follow) Follow();
            else Stop();
        }

        private void HandleAttacked(float amount, GameObject source)
        {
            if (!defendAutomatically || _mode == Mode.Fight || source == null) return;
            var attacker = source.GetComponent<Health>();
            if (attacker == null || !_combat.Health.Team.IsHostileTo(attacker.Team)) return;
            Fight();
            _target = attacker;
        }

        private void HandleDied()
        {
            _mode = Mode.Idle;
            _navAgent.enabled = false;
            enabled = false;
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
