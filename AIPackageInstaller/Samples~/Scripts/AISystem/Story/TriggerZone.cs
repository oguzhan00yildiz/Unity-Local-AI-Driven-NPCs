using UnityEngine;
using UnityEngine.Events;

namespace AISystem
{
    /// <summary>Invokes onPlayerEnter when the player walks into this trigger, e.g. to start a companion comment.</summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        [SerializeField] private bool onlyOnce = true;

        public UnityEvent onPlayerEnter = new UnityEvent();

        private bool _fired;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || (onlyOnce && _fired)) return;
            _fired = true;
            onPlayerEnter.Invoke();
        }
    }
}
