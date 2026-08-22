using UnityEngine;
using UnityEditor;
using System.IO;

namespace AISystem
{
    public static class SamplePresetGenerator
    {
        [MenuItem("Tools/AI Packages/Generate Sample Presets")]
        public static void GeneratePresets()
        {
            string folderPath = "Assets/Resources/PersonalityPresets";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            CreatePreset("Helpful Guide", "You are a helpful and polite guide in a video game. Keep your answers short and friendly.", "en_US-amy-low", $"{folderPath}/HelpfulGuide.asset");
            CreatePreset("Grumpy Guard", "You are a grumpy city guard. You do not like adventurers. Speak abruptly and tell people to move along.", "en_US-reza_ibrahim-medium", $"{folderPath}/GrumpyGuard.asset");

            AssetDatabase.SaveAssets();
            Debug.Log("[SamplePresetGenerator] Sample presets generated in " + folderPath);
        }

        private static void CreatePreset(string npcName, string prompt, string voice, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<NPCPersonalityPreset>(path) != null) return;

            var preset = ScriptableObject.CreateInstance<NPCPersonalityPreset>();
            preset.npcName = npcName;
            preset.systemPrompt = prompt;
            preset.voiceModelName = voice;

            AssetDatabase.CreateAsset(preset, path);
        }
    }
}
