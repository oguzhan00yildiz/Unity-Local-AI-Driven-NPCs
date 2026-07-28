// ============================================================
// DEPRECATED — Bu script artık kullanılmıyor.
// Yeni modüler sistem: Assets/Scripts/AISystem/NPC/NPCAgent.cs
//   NPCAgent aynı işlevi yapar, AISystemManager.Instance'ı
//   otomatik bulur — Inspector'da bağlantı gerekmez.
// ============================================================

using UnityEngine;
using LLMUnity;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
            
            if (chatUI != null && chatUI.IsChatOpen())
                return;

            if (IsInteractionKeyPressed())
            {
                OpenChat();
            }
        }

        private bool IsInteractionKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb[KeyCodeToInputSystemKey(interactionKey)].wasPressedThisFrame;
#else
            return Input.GetKeyDown(interactionKey);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private Key KeyCodeToInputSystemKey(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                return (Key)((int)Key.A + (keyCode - KeyCode.A));
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
                return (Key)((int)Key.Digit0 + (keyCode - KeyCode.Alpha0));
            switch (keyCode)
            {
                case KeyCode.Space:     return Key.Space;
                case KeyCode.Return:    return Key.Enter;
                case KeyCode.Escape:    return Key.Escape;
                case KeyCode.Tab:       return Key.Tab;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift:return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl:return Key.RightCtrl;
                case KeyCode.F1:        return Key.F1;
                case KeyCode.F2:        return Key.F2;
                case KeyCode.F3:        return Key.F3;
                case KeyCode.F4:        return Key.F4;
                case KeyCode.F5:        return Key.F5;
                case KeyCode.F6:        return Key.F6;
                case KeyCode.F7:        return Key.F7;
                case KeyCode.F8:        return Key.F8;
                case KeyCode.F9:        return Key.F9;
                case KeyCode.F10:       return Key.F10;
                case KeyCode.F11:       return Key.F11;
                case KeyCode.F12:       return Key.F12;
                default:                return Key.E;
            }
        }
#endif

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
