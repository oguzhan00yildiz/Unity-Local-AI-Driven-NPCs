using UnityEngine;
using UnityEditor;
using System.IO;

namespace AISystem
{
    [InitializeOnLoad]
    public static class SamplePresetGenerator
    {
        static SamplePresetGenerator()
        {
            EditorApplication.delayCall += EnsureDefaultPresets;
        }

        [MenuItem("Tools/AI Packages/Generate Sample Presets")]
        public static void GeneratePresets()
        {
            EnsureDefaultPresets(true);
        }

        public static void EnsureDefaultPresets()
        {
            EnsureDefaultPresets(false);
        }

        public static void EnsureDefaultPresets(bool logIfAlreadyExists)
        {
            string folderPath = Directory.Exists("Assets/AI Driven NPCs System")
                ? "Assets/AI Driven NPCs System/Resources/PersonalityPresets"
                : "Assets/Resources/PersonalityPresets";
            bool createdAny = false;

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            if (CreatePresetIfMissing("Helpful Guide", "You are a helpful and polite guide in a video game. Keep your answers short and friendly.", "en_US-amy-low", $"{folderPath}/HelpfulGuide.asset"))
                createdAny = true;

            if (CreatePresetIfMissing("Grumpy Guard", "You are a grumpy city guard. You do not like adventurers. Speak abruptly and tell people to move along.", "en_US-reza_ibrahim-medium", $"{folderPath}/GrumpyGuard.asset"))
                createdAny = true;

            if (createdAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[PersonalityPresets] ✅ Default personality templates created in " + folderPath);
            }
            else if (logIfAlreadyExists)
            {
                Debug.Log("[PersonalityPresets] Default presets already exist in " + folderPath);
            }
        }

        private static bool CreatePresetIfMissing(string npcName, string prompt, string voice, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<NPCPersonalityPreset>(path) != null) return false;

            var preset = ScriptableObject.CreateInstance<NPCPersonalityPreset>();
            preset.npcName = npcName;
            preset.systemPrompt = prompt;
            preset.voiceModelName = voice;

            AssetDatabase.CreateAsset(preset, path);
            return true;
        }
    }
}
