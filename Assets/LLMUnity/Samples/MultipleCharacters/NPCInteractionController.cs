// ============================================================
// DEPRECATED — Bu script artık kullanılmıyor.
// Yeni modüler sistem: Assets/Scripts/AISystem/NPC/NPCAgent.cs
//   NPCAgent aynı işlevi yapar, AISystemManager.Instance'ı
//   otomatik bulur — Inspector'da bağlantı gerekmez.
// ============================================================

using UnityEngine;
using LLMUnity;

namespace LLMUnitySamples
{
    public class NPCInteractionController : MonoBehaviour
    {
        [Header("Interaction Settings")]
        public float interactionRange = 3f;
        public KeyCode interactionKey = KeyCode.E;
        
        [Header("NPC Setup")]
        public LLMAgent llmAgent;
        public string npcName = "NPC";
        
        [SerializeField] private NPCChatUI chatUI;
        private bool isPlayerInRange = false;
        private Camera mainCamera;

        void Start()
        {
            mainCamera = Camera.main;
            
            if (llmAgent == null)
            {
                Debug.LogError($"LLMAgent not assigned for {gameObject.name}");
            }
        }

        void Update()
        {
            if (!isPlayerInRange) return;
            
            // Chat paneli açıksa E tuşu kontrolünü yapma (yazı girmesi için)
            if (chatUI != null && chatUI.IsChatOpen())
                return;

            // Oyuncu NPCye bakıp E tuşuna basarsa
            if (Input.GetKeyDown(interactionKey))
            {
                OpenChat();
            }
        }

        void OnTriggerEnter(Collider collision)
        {
            if (collision.CompareTag("Player"))
            {
                isPlayerInRange = true;
            }
        }

        void OnTriggerExit(Collider collision)
        {
            if (collision.CompareTag("Player"))
            {
                isPlayerInRange = false;
            }
        }

        private void OpenChat()
        {
            if (chatUI != null)
            {
                chatUI.OpenChat(llmAgent, npcName);
            }
            else
            {
                Debug.LogWarning("NPCChatUI not found in scene!");
            }
        }

        // Visual feedback için (opsiyonel)
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
