using UnityEngine;
using LLMUnity;
using System.Collections.Generic;
using System.Linq;
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
            // Ensure the LLM service process is fully initialized before requesting agent warmups
            var llm = FindAnyObjectByType<LLM>();
            if (llm != null)
            {
                try
                {
                    chatUI?.SetLoadingOverlay(true, "Initializing LLM runtime...");
                    await llm.WaitUntilReady();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ModelBootstrapper] LLM runtime startup check: {ex.Message}");
                }
            }

            chatUI?.SetLoadingOverlay(true, "Loading AI models...");

            var tasks = new List<Task>();

            // Warm up all LLMAgents in the scene
            var agents = FindObjectsByType<LLMAgent>(FindObjectsSortMode.None);

            // Save each agent's numPredict BEFORE warmup.
            // Warmup() temporarily sets numPredict = 0.  If another script
            // (e.g. the legacy NPCChatUI) also calls Warmup() on the same
            // agent concurrently, a race condition can leave numPredict = 0
            // permanently, causing the LLM to emit only ~1 token.
            var savedNumPredict = agents.ToDictionary(a => a, a => a.numPredict);

            foreach (var agent in agents)
                tasks.Add(agent.Warmup());

            // Warm up Whisper model
            if (whisperManager != null && !whisperManager.IsLoaded)
                tasks.Add(InitWhisper());

            try
            {
                await Task.WhenAll(tasks);

                // Restore numPredict — guards against the concurrent-warmup race.
                foreach (var agent in agents)
                {
                    if (agent.numPredict != savedNumPredict[agent])
                    {
                        Debug.LogWarning($"[ModelBootstrapper] {agent.name}.numPredict was corrupted "
                            + $"({agent.numPredict}), restoring to {savedNumPredict[agent]}");
                        agent.numPredict = savedNumPredict[agent];
                    }
                }

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
            if (whisperManager.IsLoaded)
                Debug.Log("[ModelBootstrapper] Whisper model ready.");
            else
                Debug.LogWarning("[ModelBootstrapper] Whisper InitModel completed but IsLoaded=false. Check libwhisper.dll and its dependencies.");
        }
    }
}
