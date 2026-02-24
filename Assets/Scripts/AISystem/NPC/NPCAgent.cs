using UnityEngine;
using LLMUnity;

namespace AISystem
{
    /// <summary>
    /// Attach one per NPC  holds the LLMAgent reference and interaction settings.
    /// Locates AISystemManager automatically; no cross-prefab Inspector wiring required.
    ///
    /// Prefab layout:
    ///   [NPC]
    ///      NPCAgent        (this script)
    ///      LLMAgent        (or LLMCharacter)
    ///      SphereCollider  (Is Trigger = true, Radius = interactionRange)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCAgent : MonoBehaviour
    {
        //  Inspector 
        [Header("NPC Identity")]
        public string npcName = "NPC";

        [Header("AI Component")]
        [Tooltip("LLMAgent or LLMCharacter on the same prefab.")]
        public LLMAgent llmAgent;

        [Header("Interaction")]
        public float   interactionRange = 3f;
        public KeyCode interactionKey   = KeyCode.E;

        [Header("Interaction Prompt (optional)")]
        [Tooltip("World-space UI object shown when the player enters range.")]
        public GameObject interactionPrompt;

        //  Internal 
        private bool _playerInRange;

        /// <summary>Read by AISystemManager.</summary>
        public LLMAgent Agent   => llmAgent;
        public string   NPCName => npcName;

        //  Lifecycle 
        void Awake()
        {
            // Fall back to same-object LLMAgent if not assigned in Inspector
            if (llmAgent == null)
                llmAgent = GetComponent<LLMAgent>();

            // Sync trigger collider radius to interactionRange
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is SphereCollider sphere)
                    sphere.radius = interactionRange;
            }

            // Hide interaction prompt at startup
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }

        void Start()
        {
            if (llmAgent == null)
                Debug.LogError($"[NPCAgent] No LLMAgent found on '{gameObject.name}'!");

            if (AISystemManager.Instance == null)
                Debug.LogWarning("[NPCAgent] AISystemManager not found in scene. Add the 'AI System' prefab.");
        }

        void Update()
        {
            if (!_playerInRange) return;

            // Skip interaction key while chat panel is already open
            if (AISystemManager.Instance != null && AISystemManager.Instance.IsChatOpen()) return;

            if (Input.GetKeyDown(interactionKey))
                TriggerChat();
        }

        //  Trigger 

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }

        //  Internal 

        private void TriggerChat()
        {
            var manager = AISystemManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[NPCAgent] AISystemManager.Instance is null  add the 'AI System' prefab to the scene.");
                return;
            }
            manager.OpenChat(this);
        }

        //  Editor Gizmo 
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
