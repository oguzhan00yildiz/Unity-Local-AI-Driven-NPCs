using UnityEngine;

namespace AISystem
{
    public enum Team
    {
        Player,   // player, companion, friendly NPCs
        Enemy
    }

    /// <summary>Marks which side a character fights on. MeleeAttacker only damages the other team.</summary>
    public class CombatTeam : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Player;

        public Team Team => team;

        public bool IsHostileTo(CombatTeam other) => other != null && other.team != team;
    }
}
