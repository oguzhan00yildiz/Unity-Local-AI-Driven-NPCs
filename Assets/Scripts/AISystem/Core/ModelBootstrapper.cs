using UnityEngine;
using LLMUnity;
using System.Collections.Generic;
using System.Threading.Tasks;
using Whisper;

namespace AISystem
{
    /// <summary>
    /// Warms up all models in the background on startup.
    /// Attach to a child of the AI System prefab.
    /// Hides the loading overlay on ChatUIController when ready.
    /// </summary>
    public class ModelBootstrapper : MonoBehaviour
    {
        [Header("Whisper (optional  already referenced by VoiceInputService)")]
        [Tooltip("If left empty, resolved via FindAnyObjectByType in the scene.")]
        public WhisperManager whisperManager;

        [Header("UI Notification")]
        [Tooltip("If left empty, resolved via FindAnyObjectByType in the scene.")]
        public ChatUIController chatUI;

        public bool AllModelsReady { get; private set; }

        void Start()
        {
            if (chatUI == null)
                chatUI = GetComponentInParent<ChatUIController>(true)
                      ?? FindAnyObjectByType<ChatUIController>();

            if (whisperManager == null)
                whisperManager = FindAnyObjectByType<WhisperManager>();

            chatUI?.SetLoadingOverlay(true, "Loading AI models...");
            _ = WarmupAll();
        }

        private async Task WarmupAll()
        {
            var tasks = new List<Task>();

            // Warm up all LLMAgents in the scene
            var agents = FindObjectsByType<LLMAgent>(FindObjectsSortMode.None);
            foreach (var agent in agents)
                tasks.Add(agent.Warmup());

            // Warm up Whisper model
            if (whisperManager != null && !whisperManager.IsLoaded)
                tasks.Add(InitWhisper());

            try
            {
                await Task.WhenAll(tasks);
                Debug.Log($"[ModelBootstrapper] All models ready ({agents.Length} LLMAgent(s)).");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ModelBootstrapper] Warmup error: {ex.Message}");
            }
            finally
            {
                AllModelsReady = true;
                chatUI?.SetLoadingOverlay(false);
            }
        }

        private async Task InitWhisper()
        {
            if (whisperManager == null || whisperManager.IsLoaded) return;
            await whisperManager.InitModel();
            Debug.Log("[ModelBootstrapper] Whisper model ready.");
        }
    }
}
