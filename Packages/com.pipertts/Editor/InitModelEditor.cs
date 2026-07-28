using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InitModel))]
public class InitModelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        InitModel initModel = (InitModel)target;

        using (new EditorGUI.DisabledScope(initModel == null || initModel.tts == null))
        {
            if (GUILayout.Button("Use Amy"))
            {
                ApplyPreset(initModel, initModel.ApplyAmyPreset);
            }

            if (GUILayout.Button("Use Ibrahim"))
            {
                ApplyPreset(initModel, initModel.ApplyIbrahimPreset);
            }
        }
    }

    void ApplyPreset(InitModel initModel, System.Action applyAction)
    {
        Undo.RecordObject(initModel, "Apply Piper Preset");
        if (initModel.tts != null)
        {
            Undo.RecordObject(initModel.tts, "Apply Piper Preset");
        }

        applyAction?.Invoke();

        EditorUtility.SetDirty(initModel);
        if (initModel.tts != null)
        {
            EditorUtility.SetDirty(initModel.tts);
        }
    }
}
