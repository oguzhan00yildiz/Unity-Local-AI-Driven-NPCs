using System;
using System.Text;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Live facts NPCs can talk about: the player's health, enemies defeated, whether the companion
    /// follows. NPCKnowledge adds Describe() to every NPC's system prompt and refreshes it on OnChanged.
    /// OnChanged fires only when a described fact changes, so prompts are not rebuilt on every hit.
    /// </summary>
    public class WorldState : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [Tooltip("Optional. Used to tell NPCs whether the companion is with the player.")]
        [SerializeField] private CompanionController companion;

        private int _enemiesDefeated;
        private string _healthWord;
        private bool _companionFollowing;
        private readonly StringBuilder _sb = new StringBuilder(256);

        public int EnemiesDefeated => _enemiesDefeated;
        public Health PlayerHealth => playerHealth;

        public event Action OnChanged;

        void Start()
        {
            _healthWord = HealthWord();
            playerHealth.OnHealthChanged += HandlePlayerHealth;

            // Count every enemy death in the scene
            foreach (var health in FindObjectsByType<Health>(FindObjectsSortMode.None))
                if (health.Team.Team == Team.Enemy)
                    health.OnDied += HandleEnemyDied;
        }

        void Update()
        {
            if (companion == null || companion.IsFollowing == _companionFollowing) return;
            _companionFollowing = companion.IsFollowing;
            OnChanged?.Invoke();
        }

        public string Describe()
        {
            _sb.Clear();
            _sb.Append("the traveller looks ").Append(_healthWord).Append('.');
            if (_enemiesDefeated > 0)
                _sb.Append(" The traveller has defeated ").Append(_enemiesDefeated).Append(_enemiesDefeated == 1 ? " skeleton." : " skeletons.");
            if (companion != null)
                _sb.Append(_companionFollowing ? " Their companion is travelling with them." : " They are alone right now.");
            return _sb.ToString();
        }

        private void HandlePlayerHealth()
        {
            string word = HealthWord();
            if (word == _healthWord) return;
            _healthWord = word;
            OnChanged?.Invoke();
        }

        private void HandleEnemyDied()
        {
            _enemiesDefeated++;
            OnChanged?.Invoke();
        }

        private string HealthWord()
        {
            float f = playerHealth.Fraction;
            if (f > 0.75f) return "healthy";
            if (f > 0.4f)  return "hurt";
            return "badly wounded";
        }
    }
}
