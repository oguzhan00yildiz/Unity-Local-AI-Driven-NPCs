using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Left mouse button attacks with MeleeAttacker. On death it stops player control and input.
    /// Put it on the player next to ThirdPersonPlayer and PlayerInputHandler.
    /// </summary>
    [RequireComponent(typeof(MeleeAttacker), typeof(Health))]
    public class PlayerCombat : MonoBehaviour
    {
        private PlayerInputHandler _input;
        private MeleeAttacker _attacker;
        private ThirdPersonPlayer _movement;
        private Health _health;

        void Awake()
        {
            _input    = GetComponent<PlayerInputHandler>();
            _attacker = GetComponent<MeleeAttacker>();
            _movement = GetComponent<ThirdPersonPlayer>();
            _health   = GetComponent<Health>();
            _health.OnDied += HandleDied;
            MeleeCombatant.Register(_health); // enemies can target the player
        }

        void OnDestroy()
        {
            _health.OnDied -= HandleDied;
            MeleeCombatant.Unregister(_health);
        }

        void Update()
        {
            if (!_input.Attack) return;
            _input.ConsumeAttack();
            _attacker.TryAttack();
        }

        private void HandleDied()
        {
            _movement.SetMovementEnabled(false);
            _input.enabled = false;
            enabled = false;
        }
    }
}
