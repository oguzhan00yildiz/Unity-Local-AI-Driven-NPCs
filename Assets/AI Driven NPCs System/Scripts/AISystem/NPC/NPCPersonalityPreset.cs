using UnityEngine;

namespace AISystem
{
    [CreateAssetMenu(fileName = "NewPersonalityPreset", menuName = "AI System/Personality Preset")]
    public class NPCPersonalityPreset : ScriptableObject
    {
        [Tooltip("The name of the NPC")]
        public string npcName = "NPC";

        [Tooltip("The system prompt that dictates the NPC's personality and rules")]
        [TextArea(5, 15)]
        public string systemPrompt = "You are a helpful assistant.";

        [Tooltip("Piper voice for this NPC. Download more voices in Tools > AI Packages > Voice Browser.")]
        [VoiceName]
        public string voiceModelName = "en_US-ljspeech-medium";
    }
}
