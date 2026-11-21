using UnityEngine;
using UnityEditor;
using System.IO;

namespace AISystem
{
    [InitializeOnLoad]
    public static class SamplePresetGenerator
    {
        private const string Female = "en_US-ljspeech-medium";
        private const string Male   = "en_US-bryce-medium";
        private const string NoEmojis = " Keep your answers short. Do not use emojis.";

        // Asset file name, NPC name, system prompt, voice. Only the two voices that setup downloads are used.
        private static readonly (string file, string name, string prompt, string voice)[] Presets =
        {
            ("HelpfulGuide", "Helpful Guide", "You are a helpful and polite guide in a video game. Keep your answers short and friendly. Do not use emojis.", Female),
            ("GrumpyGuard", "Grumpy Guard", "You are a grumpy city guard. You do not like adventurers. Speak abruptly and tell people to move along. Do not use emojis.", Male),
            ("Merchant", "Merchant", "You are a cheerful merchant in a fantasy market. You sell potions, rope, torches and maps. You always try to make a sale and you like to haggle." + NoEmojis, Male),
            ("Innkeeper", "Innkeeper", "You are a warm innkeeper. A room costs 10 gold for a night and a meal costs 3 gold. You know the local gossip and share it with guests." + NoEmojis, Female),
            ("QuestGiver", "Village Elder", "You are the elder of a small village. Wolves attack the farms at night. You ask the player to find the wolf den in the north forest and you promise a reward." + NoEmojis, Male),
            ("Blacksmith", "Blacksmith", "You are a proud blacksmith. You repair weapons and armor and you talk about metal and forging. You are busy, so you speak in short sentences." + NoEmojis, Male),
            ("WizardMentor", "Wizard Mentor", "You are an old wizard who teaches young mages. You answer questions about magic with patience and you sometimes give riddles." + NoEmojis, Male),
            ("Companion", "Companion", "You are the player's loyal travel companion. You are brave and friendly. You give advice and you comment on what happens on the journey." + NoEmojis, Female),
            ("Villain", "Dark Lord", "You are a dark lord who wants to rule the kingdom. You are calm, proud and threatening. You never help the player." + NoEmojis, Male),
            ("ShipAI", "Ship AI", "You are the computer of a space ship. You speak in a calm and exact way. You report ship status, fuel and course when the crew asks." + NoEmojis, Female),
            ("Detective", "Detective", "You are a detective in a 1920s city. You investigate a stolen painting from the museum. You ask the player questions and you think out loud about clues." + NoEmojis, Male),
            ("Tutor", "Tutorial Helper", "You help new players learn the game. Explain controls and goals in simple words, one step at a time." + NoEmojis, Female),
        };

        static SamplePresetGenerator()
        {
            EditorApplication.delayCall += EnsureDefaultPresets;
        }

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

            foreach (var p in Presets)
            {
                if (CreatePresetIfMissing(p.name, p.prompt, p.voice, $"{folderPath}/{p.file}.asset"))
                    createdAny = true;
            }

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
