using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

public class ModelDownloader : EditorWindow
{
    private class ModelEntry
    {
        public string Group;
        public string DisplayName;
        public string Url;
        public string DestRelPath; // relative to StreamingAssets
        public int SizeMB;

        // Runtime state
        public bool IsDownloaded;
        public bool IsDownloading;
        public float Progress;
        public string Error;

        public string FullPath => Path.Combine(Application.streamingAssetsPath, DestRelPath.Replace('/', Path.DirectorySeparatorChar));
        public void Refresh() => IsDownloaded = File.Exists(FullPath);
    }

    private static readonly List<ModelEntry> Models = new List<ModelEntry>
    {
        // ── Whisper ──────────────────────────────────────────────────────
        new ModelEntry
        {
            Group = "Whisper (Speech Recognition)",
            DisplayName = "ggml-tiny.bin",
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            DestRelPath = "Whisper/ggml-tiny.bin",
            SizeMB = 74
        },

        // ── Piper Phonemizer (required by Piper TTS) ─────────────────────
        new ModelEntry
        {
            Group = "Piper TTS – Phonemizer (required)",
            DisplayName = "model.onnx",
            Url = "https://huggingface.co/lookbe/open-phonemizer-onnx/resolve/main/model.onnx",
            DestRelPath = "PiperTTS/model.onnx",
            SizeMB = 59
        },
        new ModelEntry
        {
            Group = "Piper TTS – Phonemizer (required)",
            DisplayName = "phoneme_dict.json",
            Url = "https://huggingface.co/lookbe/open-phonemizer-onnx/resolve/main/phoneme_dict.json",
            DestRelPath = "PiperTTS/phoneme_dict.json",
            SizeMB = 10
        },
        new ModelEntry
        {
            Group = "Piper TTS – Phonemizer (required)",
            DisplayName = "tokenizer.json",
            Url = "https://huggingface.co/lookbe/open-phonemizer-onnx/resolve/main/tokenizer.json",
            DestRelPath = "PiperTTS/tokenizer.json",
            SizeMB = 1
        },

        // ── Piper Voice – Amy ─────────────────────────────────────────────
        new ModelEntry
        {
            Group = "Piper Voice – Amy (English Female)",
            DisplayName = "en_US-amy-low.onnx",
            Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/low/en_US-amy-low.onnx",
            DestRelPath = "PiperTTS/Amy/en_US-amy-low.onnx",
            SizeMB = 60
        },
        new ModelEntry
        {
            Group = "Piper Voice – Amy (English Female)",
            DisplayName = "en_US-amy-low.onnx.json",
            Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/low/en_US-amy-low.onnx.json",
            DestRelPath = "PiperTTS/Amy/en_US-amy-low.onnx.json",
            SizeMB = 1
        },

        // ── Piper Voice – Ibrahim ─────────────────────────────────────────
        new ModelEntry
        {
            Group = "Piper Voice – Ibrahim (English Male)",
            DisplayName = "en_US-reza_ibrahim-medium.onnx",
            Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/reza_ibrahim/medium/en_US-reza_ibrahim-medium.onnx",
            DestRelPath = "PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx",
            SizeMB = 61
        },
        new ModelEntry
        {
            Group = "Piper Voice – Ibrahim (English Male)",
            DisplayName = "en_US-reza_ibrahim-medium.onnx.json",
            Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/reza_ibrahim/medium/en_US-reza_ibrahim-medium.onnx.json",
            DestRelPath = "PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx.json",
            SizeMB = 1
        },
    };

    private Vector2 _scroll;
    private int _activeDownloads;
    private bool _isDownloadingAll;

    [MenuItem("Tools/AI Packages/Download Model Files")]
    public static void ShowWindow()
    {
        var window = GetWindow<ModelDownloader>("AI Model Downloader");
        window.minSize = new Vector2(480, 500);
        window.RefreshStatus();
    }

    private void OnEnable() => RefreshStatus();

    private void RefreshStatus()
    {
        foreach (var m in Models)
            m.Refresh();
        Repaint();
    }

    private void OnGUI()
    {
        // Header
        EditorGUILayout.Space(8);
        GUIStyle header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("AI Model Downloader", header);
        EditorGUILayout.LabelField("Downloads model files to StreamingAssets.", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // LLM note
        EditorGUILayout.HelpBox(
            "LLM models are managed by LLMUnity directly.\n" +
            "Open the LLM component in your scene → click 'Download Model' in the Inspector.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        // Download All button
        bool anyMissing = Models.Exists(m => !m.IsDownloaded && !m.IsDownloading);
        using (new EditorGUI.DisabledGroupScope(_activeDownloads > 0 || !anyMissing))
        {
            if (GUILayout.Button(_activeDownloads > 0 ? $"Downloading... ({_activeDownloads} active)" : "⬇  Download All Missing Models", GUILayout.Height(32)))
                DownloadAll();
        }
        EditorGUILayout.Space(6);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        string currentGroup = null;
        foreach (var model in Models)
        {
            if (model.Group != currentGroup)
            {
                currentGroup = model.Group;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
            }

            DrawModelRow(model);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Refresh Status"))
            RefreshStatus();

        if (_activeDownloads > 0)
        {
            EditorApplication.update += Repaint;
        }
        else
        {
            EditorApplication.update -= Repaint;
        }
    }

    private void DrawModelRow(ModelEntry model)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            // Status icon
            string icon = model.IsDownloading ? "⬇" : (model.IsDownloaded ? "✅" : "○");
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));

            // Name + size
            EditorGUILayout.LabelField($"{model.DisplayName}  ({model.SizeMB} MB)", GUILayout.MinWidth(200));

            if (model.IsDownloading)
            {
                // Progress bar
                Rect r = GUILayoutUtility.GetRect(120, 18);
                EditorGUI.ProgressBar(r, model.Progress, $"{(int)(model.Progress * 100)}%");
            }
            else if (!string.IsNullOrEmpty(model.Error))
            {
                GUIStyle err = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } };
                EditorGUILayout.LabelField(model.Error, err, GUILayout.Width(120));
                if (GUILayout.Button("Retry", GUILayout.Width(55)))
                    _ = DownloadModel(model);
            }
            else if (model.IsDownloaded)
            {
                EditorGUILayout.LabelField("Ready", EditorStyles.miniLabel, GUILayout.Width(80));
                if (GUILayout.Button("Delete", GUILayout.Width(55)))
                {
                    File.Delete(model.FullPath);
                    model.Refresh();
                    AssetDatabase.Refresh();
                }
            }
            else
            {
                if (GUILayout.Button("Download", GUILayout.Width(80)))
                    _ = DownloadModel(model);
            }
        }
    }

    private void DownloadAll()
    {
        foreach (var model in Models)
        {
            if (!model.IsDownloaded && !model.IsDownloading)
                _ = DownloadModel(model);
        }
    }

    private async Task DownloadModel(ModelEntry model)
    {
        model.IsDownloading = true;
        model.Error = null;
        model.Progress = 0f;
        _activeDownloads++;
        Repaint();

        try
        {
            string dir = Path.GetDirectoryName(model.FullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = model.FullPath + ".tmp";

            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (_, e) =>
                {
                    model.Progress = e.ProgressPercentage / 100f;
                };

                await client.DownloadFileTaskAsync(new System.Uri(model.Url), tempPath);
            }

            // Replace with final file
            if (File.Exists(model.FullPath))
                File.Delete(model.FullPath);
            File.Move(tempPath, model.FullPath);

            model.IsDownloaded = true;
            Debug.Log($"<b>[Model Downloader]</b> ✅ Downloaded: {model.DestRelPath}");
        }
        catch (System.Exception ex)
        {
            model.Error = "Failed";
            Debug.LogError($"<b>[Model Downloader]</b> ❌ {model.DisplayName}: {ex.Message}");

            // Clean up temp file on failure
            string tempPath = model.FullPath + ".tmp";
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        finally
        {
            model.IsDownloading = false;
            _activeDownloads--;

            if (_activeDownloads == 0)
            {
                AssetDatabase.Refresh();
                Repaint();
                Debug.Log("<b>[Model Downloader]</b> All downloads complete. ✅");
            }
        }
    }
}
