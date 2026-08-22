using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;

public class VoiceBrowserWindow : EditorWindow
{
    private class VoiceEntry
    {
        public string Name;
        public string DisplayName;
        public string UrlOnnx;
        public string UrlJson;
        
        public bool IsDownloading;
        public float Progress;
        
        public string GetOnnxPath() => Path.Combine(Application.streamingAssetsPath, "PiperTTS", Name, $"{Name}.onnx");
        public string GetJsonPath() => Path.Combine(Application.streamingAssetsPath, "PiperTTS", Name, $"{Name}.onnx.json");
        public bool IsDownloaded => File.Exists(GetOnnxPath()) && File.Exists(GetJsonPath());
    }

    private List<VoiceEntry> voices = new List<VoiceEntry>
    {
        new VoiceEntry { Name = "en_US-amy-low", DisplayName = "Amy (English Female, Low)", UrlOnnx = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/low/en_US-amy-low.onnx", UrlJson = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/low/en_US-amy-low.onnx.json" },
        new VoiceEntry { Name = "en_US-reza_ibrahim-medium", DisplayName = "Ibrahim (English Male, Medium)", UrlOnnx = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/reza_ibrahim/medium/en_US-reza_ibrahim-medium.onnx", UrlJson = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/reza_ibrahim/medium/en_US-reza_ibrahim-medium.onnx.json" },
        new VoiceEntry { Name = "en_US-jenny-dioco-medium", DisplayName = "Jenny (English Female, Medium)", UrlOnnx = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/jenny_dioco/medium/en_US-jenny_dioco-medium.onnx", UrlJson = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/jenny_dioco/medium/en_US-jenny_dioco-medium.onnx.json" },
        new VoiceEntry { Name = "en_US-ryan-high", DisplayName = "Ryan (English Male, High)", UrlOnnx = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx", UrlJson = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx.json" }
    };

    private Vector2 scrollPos;
    private int activeDownloads = 0;

    [MenuItem("Tools/AI Packages/Voice Browser")]
    public static void ShowWindow()
    {
        GetWindow<VoiceBrowserWindow>("Voice Browser");
    }

    private void OnGUI()
    {
        GUILayout.Label("PiperTTS Voice Browser", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Download voices for your NPCs. The downloaded voices will be saved in StreamingAssets/PiperTTS.", MessageType.Info);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        foreach (var voice in voices)
        {
            DrawVoiceRow(voice);
        }
        
        EditorGUILayout.EndScrollView();

        if (activeDownloads > 0)
        {
            Repaint();
        }
    }

    private void DrawVoiceRow(VoiceEntry voice)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(voice.DisplayName, GUILayout.MinWidth(200));

            if (voice.IsDownloading)
            {
                Rect r = GUILayoutUtility.GetRect(120, 18);
                EditorGUI.ProgressBar(r, voice.Progress, $"{(int)(voice.Progress * 100)}%");
            }
            else if (voice.IsDownloaded)
            {
                EditorGUILayout.LabelField("✅ Ready", EditorStyles.miniLabel, GUILayout.Width(80));
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    File.Delete(voice.GetOnnxPath());
                    File.Delete(voice.GetJsonPath());
                    AssetDatabase.Refresh();
                }
            }
            else
            {
                if (GUILayout.Button("Download", GUILayout.Width(80)))
                {
                    _ = DownloadVoice(voice);
                }
            }
        }
    }

    private async Task DownloadVoice(VoiceEntry voice)
    {
        voice.IsDownloading = true;
        voice.Progress = 0f;
        activeDownloads++;

        try
        {
            string dir = Path.GetDirectoryName(voice.GetOnnxPath());
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (s, e) => { voice.Progress = (e.ProgressPercentage / 100f) * 0.9f; };
                await client.DownloadFileTaskAsync(new System.Uri(voice.UrlOnnx), voice.GetOnnxPath());
                
                // download json
                await client.DownloadFileTaskAsync(new System.Uri(voice.UrlJson), voice.GetJsonPath());
                voice.Progress = 1f;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VoiceBrowser] Failed to download {voice.Name}: {ex.Message}");
        }
        finally
        {
            voice.IsDownloading = false;
            activeDownloads--;
            AssetDatabase.Refresh();
        }
    }
}
