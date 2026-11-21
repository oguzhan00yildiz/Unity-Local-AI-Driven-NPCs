using System;
using UnityEngine;
using UnityEngine.Events;

namespace AISystem
{
    /// <summary>
    /// Hit points for a character. Plays the Animator "Hit" trigger and sets the "Dead" bool when they exist.
    /// </summary>
    [RequireComponent(typeof(CombatTeam))]
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Animator trigger played when damaged. Ignored if the Animator has no such parameter.")]
        [SerializeField] private string hitTrigger = "Hit";
        [Tooltip("Animator bool set on death. Ignored if the Animator has no such parameter.")]
        [SerializeField] private string deadBool = "Dead";

        [Tooltip("Invoked once when health reaches zero. Use it for game over screens or loot.")]
        public UnityEvent onDied = new UnityEvent();

        /// <summary>Damage amount and the attacker (may be null).</summary>
        public event Action<float, GameObject> OnDamaged;
        public event Action OnDied;
        /// <summary>Fires after any change of current health (damage or heal).</summary>
        public event Action OnHealthChanged;

        private float _current;
        private Animator _animator;
        private int _hitId, _deadId;
        private bool _hasHit, _hasDead;

        public float Current  => _current;
        public float Max      => maxHealth;
        public float Fraction => _current / maxHealth;
        public bool  IsDead   => _current <= 0f;
        public CombatTeam Team { get; private set; }

        void Awake()
        {
            _current = maxHealth;
            Team = GetComponent<CombatTeam>();
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            _hitId  = Animator.StringToHash(hitTrigger);
            _deadId = Animator.StringToHash(deadBool);
            foreach (var p in _animator.parameters)
            {
                if (p.nameHash == _hitId)  _hasHit  = true;
                if (p.nameHash == _deadId) _hasDead = true;
            }
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f) return;

            _current = Mathf.Max(0f, _current - amount);
            OnDamaged?.Invoke(amount, source);
            OnHealthChanged?.Invoke();

            if (IsDead)
            {
                if (_hasDead) _animator.SetBool(_deadId, true);
                OnDied?.Invoke();
                onDied.Invoke();
            }
            else if (_hasHit)
            {
                _animator.SetTrigger(_hitId);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            _current = Mathf.Min(maxHealth, _current + amount);
            OnHealthChanged?.Invoke();
        }
    }
}
