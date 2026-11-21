using UnityEngine;
using UnityEngine.Events;

namespace AISystem
{
    /// <summary>
    /// An item the player picks up with a key while inside the trigger. onPickedUp can advance the quest.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private string itemName = "Amulet";
        [SerializeField] private KeyCode pickupKey = KeyCode.F;
        [Tooltip("Label shown while the player is in range. Optional.")]
        [SerializeField] private TextMesh promptText;
        [Tooltip("Object hidden after pickup (the item model). Uses this GameObject if empty.")]
        [SerializeField] private GameObject itemVisual;

        public UnityEvent onPickedUp = new UnityEvent();

        private bool _playerInRange;
        private bool _taken;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (itemVisual == null) itemVisual = gameObject;
            SetPrompt(false);
        }

        void Update()
        {
            if (_taken || !_playerInRange || !KeyInput.WasPressed(pickupKey)) return;

            _taken = true;
            SetPrompt(false);
            itemVisual.SetActive(false);
            onPickedUp.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (_taken || !other.CompareTag("Player")) return;
            _playerInRange = true;
            SetPrompt(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            SetPrompt(false);
        }

        private void SetPrompt(bool visible)
        {
            if (promptText == null) return;
            promptText.text = $"Press {pickupKey} to take the {itemName}";
            promptText.gameObject.SetActive(visible);
        }
    }
}
