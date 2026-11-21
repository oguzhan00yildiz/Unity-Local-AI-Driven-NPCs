using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Melee attack: plays the Animator "Attack" trigger, then after hitDelay damages every
    /// character of the other team inside a sphere in front of the attacker.
    /// </summary>
    [RequireComponent(typeof(CombatTeam))]
    public class MeleeAttacker : MonoBehaviour
    {
        [SerializeField] private float damage    = 20f;
        [SerializeField] private float cooldown  = 0.9f;
        [Tooltip("Seconds from the start of the swing to the hit, to match the animation.")]
        [SerializeField] private float hitDelay  = 0.35f;
        [Tooltip("Distance of the hit sphere centre in front of the character.")]
        [SerializeField] private float reach     = 1.1f;
        [SerializeField] private float hitRadius = 0.9f;
        [SerializeField] private string attackTrigger = "Attack";

        [Tooltip("Transform that faces the attack direction. Uses this transform if empty.")]
        [SerializeField] private Transform facing;

        private static readonly Collider[] Hits = new Collider[16];
        private readonly List<Health> _damaged = new List<Health>(8);

        private CombatTeam _team;
        private Health _ownHealth;
        private Animator _animator;
        private int _attackId;
        private float _nextAttackTime;

        /// <summary>Distance at which an attack can reach a target's centre.</summary>
        public float Reach => reach + hitRadius * 0.5f;
        public bool  IsReady => Time.time >= _nextAttackTime;

        void Awake()
        {
            _team = GetComponent<CombatTeam>();
            _ownHealth = GetComponent<Health>();
            _animator = GetComponentInChildren<Animator>();
            _attackId = Animator.StringToHash(attackTrigger);
            if (facing == null) facing = transform;
        }

        /// <summary>Fires when a swing starts, e.g. for a sword sound.</summary>
        public event System.Action OnAttackStarted;

        /// <summary>Starts an attack if the cooldown has passed. Returns false when it could not attack.</summary>
        public bool TryAttack()
        {
            if (!IsReady || (_ownHealth != null && _ownHealth.IsDead)) return false;

            _nextAttackTime = Time.time + cooldown;
            _animator.SetTrigger(_attackId);
            OnAttackStarted?.Invoke();
            StartCoroutine(HitAfterDelay());
            return true;
        }

        private IEnumerator HitAfterDelay()
        {
            yield return new WaitForSeconds(hitDelay);
            if (_ownHealth != null && _ownHealth.IsDead) yield break;

            Vector3 centre = transform.position + Vector3.up + facing.forward * reach;
            int count = Physics.OverlapSphereNonAlloc(centre, hitRadius, Hits, ~0, QueryTriggerInteraction.Ignore);

            _damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                var health = Hits[i].GetComponentInParent<Health>();
                if (health == null || health == _ownHealth || health.IsDead || _damaged.Contains(health)) continue;
                if (!_team.IsHostileTo(health.Team)) continue;

                _damaged.Add(health);
                health.TakeDamage(damage, gameObject);
            }
        }

        void OnDrawGizmosSelected()
        {
            var f = facing != null ? facing : transform;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up + f.forward * reach, hitRadius);
        }
    }
}
