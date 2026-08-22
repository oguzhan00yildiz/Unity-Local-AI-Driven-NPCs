using UnityEngine;
using UnityEditor;

namespace AISystem
{
    [CustomEditor(typeof(NPCAgent))]
    public class NPCAgentEditor : Editor
    {
        private NPCPersonalityPreset preset;

        public override void OnInspectorGUI()
        {
            NPCAgent agent = (NPCAgent)target;

            GUILayout.Space(10);
            GUILayout.Label("Personality Templates", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                preset = (NPCPersonalityPreset)EditorGUILayout.ObjectField("Load Preset", preset, typeof(NPCPersonalityPreset), false);
                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                {
                    if (preset != null)
                    {
                        Undo.RecordObject(agent, "Apply Personality Preset");
                        agent.npcName = preset.npcName;
                        agent.voiceModelName = preset.voiceModelName;

                        if (agent.llmAgent != null)
                        {
                            Undo.RecordObject(agent.llmAgent, "Apply Personality Preset (LLM)");
                            agent.llmAgent.systemPrompt = preset.systemPrompt;
                            EditorUtility.SetDirty(agent.llmAgent);
                        }
                        
                        EditorUtility.SetDirty(agent);
                        Debug.Log($"[NPCAgentEditor] Applied preset '{preset.name}' to {agent.gameObject.name}");
                    }
                }
            }

            GUILayout.Space(10);
            
            // Draw default inspector
            DrawDefaultInspector();
        }
    }
}
