using UnityEngine;
using UnityEditor;

namespace AISystem
{
    [CustomEditor(typeof(NPCAgent))]
    public class NPCAgentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            NPCAgent agent = (NPCAgent)target;

            GUILayout.Space(5);
            GUILayout.Label("Personality Template", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                var newPreset = (NPCPersonalityPreset)EditorGUILayout.ObjectField("Template", agent.personalityPreset, typeof(NPCPersonalityPreset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newPreset != null)
                    {
                        Undo.RecordObject(agent, "Apply Personality Template");
                        if (agent.llmAgent != null)
                            Undo.RecordObject(agent.llmAgent, "Apply Personality Template (LLM)");

                        agent.ApplyPreset(newPreset);
                        
                        EditorUtility.SetDirty(agent);
                        if (agent.llmAgent != null)
                            EditorUtility.SetDirty(agent.llmAgent);

                        Debug.Log($"[NPCAgentEditor] ✅ Applied template '{newPreset.name}' to {agent.gameObject.name}");
                    }
                    else
                    {
                        Undo.RecordObject(agent, "Clear Personality Template");
                        agent.personalityPreset = null;
                        EditorUtility.SetDirty(agent);
                    }
                }

                if (agent.personalityPreset != null)
                {
                    if (GUILayout.Button("Re-apply Current Template", GUILayout.Height(22)))
                    {
                        Undo.RecordObject(agent, "Apply Personality Template");
                        if (agent.llmAgent != null)
                            Undo.RecordObject(agent.llmAgent, "Apply Personality Template (LLM)");

                        agent.ApplyPreset(agent.personalityPreset);

                        EditorUtility.SetDirty(agent);
                        if (agent.llmAgent != null)
                            EditorUtility.SetDirty(agent.llmAgent);

                        Debug.Log($"[NPCAgentEditor] ✅ Re-applied template '{agent.personalityPreset.name}' to {agent.gameObject.name}");
                    }
                }
            }

            GUILayout.Space(10);
            
            // Draw default inspector for remaining fields
            DrawDefaultInspector();
        }
    }
}
