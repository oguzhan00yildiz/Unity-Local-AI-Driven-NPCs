using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Dropdown for [VoiceName] fields. Lists every voice in StreamingAssets/PiperTTS/&lt;speaker&gt;/
    /// that has both a .onnx and a .onnx.json file. A value that is not downloaded stays selectable.
    /// </summary>
    [CustomPropertyDrawer(typeof(VoiceNameAttribute))]
    public class VoiceNameDrawer : PropertyDrawer
    {
        private const string MissingSuffix = " (not downloaded)";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            List<string> voices = FindDownloadedVoices();
            string current = property.stringValue;
            int index = voices.IndexOf(current);

            var options = new List<string>(voices);
            if (index < 0 && !string.IsNullOrEmpty(current))
            {
                options.Insert(0, current + MissingSuffix);
                index = 0;
            }

            if (options.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "No voices downloaded. Open Tools > AI Packages > Voice Browser.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            int selected = EditorGUI.Popup(position, label.text, Mathf.Max(index, 0), options.ToArray());
            if (selected != index)
                property.stringValue = options[selected].Replace(MissingSuffix, "");
            EditorGUI.EndProperty();
        }

        private static List<string> FindDownloadedVoices()
        {
            var voices = new List<string>();
            string root = Path.Combine(Application.streamingAssetsPath, "PiperTTS");
            if (!Directory.Exists(root))
                return voices;

            // Voices are in speaker subfolders; the phonemizer model.onnx sits in the root and is skipped
            foreach (string dir in Directory.GetDirectories(root))
            {
                foreach (string onnx in Directory.GetFiles(dir, "*.onnx"))
                {
                    if (File.Exists(onnx + ".json"))
                        voices.Add(Path.GetFileNameWithoutExtension(onnx));
                }
            }

            voices.Sort();
            return voices;
        }
    }
}
