using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AISystem
{
    /// <summary>
    /// Shared NavMesh melee behaviour: walk to a target, face it, attack when in reach.
    /// Used by EnemyAI and CompanionController.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(MeleeAttacker), typeof(Health))]
    public class MeleeCombatant : MonoBehaviour
    {
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float turnSpeed  = 540f;
        [Tooltip("The path is updated when the target moves this far.")]
        [SerializeField] private float repathDistance = 0.5f;

        private static readonly List<Health> AllHealth = new List<Health>(32);

        private NavMeshAgent _nav;
        private MeleeAttacker _attacker;
        private Health _health;
        private Vector3 _lastTargetPos;

        public Health Health => _health;

        void Awake()
        {
            _nav      = GetComponent<NavMeshAgent>();
            _attacker = GetComponent<MeleeAttacker>();
            _health   = GetComponent<Health>();
        }

        void OnEnable()  => Register(_health);
        void OnDisable() => AllHealth.Remove(_health);

        /// <summary>Registers a character that is not a MeleeCombatant (the player) as a possible target.</summary>
        public static void Register(Health health)   { if (!AllHealth.Contains(health)) AllHealth.Add(health); }
        public static void Unregister(Health health) => AllHealth.Remove(health);

        /// <summary>
        /// Moves toward the target and attacks when in reach. Returns false when the target is dead,
        /// so the caller can pick another one.
        /// </summary>
        public bool Engage(Health target)
        {
            if (target == null || target.IsDead || _health.IsDead) return false;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist > _attacker.Reach * 0.9f)
            {
                _nav.speed = chaseSpeed;
                _nav.stoppingDistance = _attacker.Reach * 0.8f;
                if ((target.transform.position - _lastTargetPos).sqrMagnitude > repathDistance * repathDistance || !_nav.hasPath)
                {
                    _nav.SetDestination(target.transform.position);
                    _lastTargetPos = target.transform.position;
                }
                return true;
            }

            if (_nav.hasPath) _nav.ResetPath();
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(to), turnSpeed * Time.deltaTime);
            _attacker.TryAttack();
            return true;
        }

        /// <summary>Stops moving toward a target.</summary>
        public void Disengage()
        {
            if (_nav.enabled && _nav.hasPath) _nav.ResetPath();
        }

        /// <summary>Nearest living character hostile to this one within range of a point.</summary>
        public Health FindNearestHostile(Vector3 around, float range)
        {
            Health best = null;
            float bestSqr = range * range;
            foreach (var h in AllHealth)
            {
                if (h == _health || h.IsDead || !_health.Team.IsHostileTo(h.Team)) continue;
                float sqr = (h.transform.position - around).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = h;
            }
            return best;
        }
    }
}
