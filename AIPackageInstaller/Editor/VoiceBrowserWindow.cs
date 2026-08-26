using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System.Net.Http;
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
        
        public string GetOnnxPath()
        {
            string subfolder = GetSubfolder();
            string pathSub = Path.Combine(Application.streamingAssetsPath, "PiperTTS", subfolder, $"{Name}.onnx");
            if (File.Exists(pathSub)) return pathSub;

            string pathDirect = Path.Combine(Application.streamingAssetsPath, "PiperTTS", Name, $"{Name}.onnx");
            if (File.Exists(pathDirect)) return pathDirect;

            return pathSub;
        }

        public string GetJsonPath()
        {
            string subfolder = GetSubfolder();
            string pathSub = Path.Combine(Application.streamingAssetsPath, "PiperTTS", subfolder, $"{Name}.onnx.json");
            if (File.Exists(pathSub)) return pathSub;

            string pathDirect = Path.Combine(Application.streamingAssetsPath, "PiperTTS", Name, $"{Name}.onnx.json");
            if (File.Exists(pathDirect)) return pathDirect;

            return pathSub;
        }

        private string GetSubfolder()
        {
            if (Name.Contains("amy")) return "Amy";
            if (Name.Contains("ibrahim")) return "ibrahim";
            if (Name.Contains("ljspeech")) return "ljspeech";
            if (Name.Contains("jenny")) return "jenny";
            if (Name.Contains("ryan")) return "ryan";
            return Name;
        }

        public bool IsDownloaded
        {
            get
            {
                var onnx = GetOnnxPath();
                var json = GetJsonPath();
                if (!File.Exists(onnx) || !File.Exists(json)) return false;
                var fi = new FileInfo(onnx);
                return fi.Length > 1024 * 1024; // must be at least 1MB
            }
        }
    }

    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler)
        {
            Timeout = System.TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    private List<VoiceEntry> voices = new List<VoiceEntry>
    {
        new VoiceEntry { Name = "en_US-ljspeech-high", DisplayName = "LJSpeech (English Female, High)", UrlOnnx = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ljspeech/high/en_US-ljspeech-high.onnx", UrlJson = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ljspeech/high/en_US-ljspeech-high.onnx.json" },
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

        string onnxPath = voice.GetOnnxPath();
        string jsonPath = voice.GetJsonPath();
        string tempOnnx = onnxPath + ".tmp";
        string tempJson = jsonPath + ".tmp";

        try
        {
            string dir = Path.GetDirectoryName(onnxPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Download ONNX model (90% of progress bar)
            using (var response = await _httpClient.GetAsync(voice.UrlOnnx, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempOnnx, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            voice.Progress = ((float)totalRead / totalBytes.Value) * 0.9f;
                        }
                    }
                }
            }

            // Download JSON config (remaining 10%)
            using (var response = await _httpClient.GetAsync(voice.UrlJson, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempJson, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await contentStream.CopyToAsync(fileStream);
                }
            }

            if (File.Exists(onnxPath)) File.Delete(onnxPath);
            if (File.Exists(jsonPath)) File.Delete(jsonPath);

            File.Move(tempOnnx, onnxPath);
            File.Move(tempJson, jsonPath);

            voice.Progress = 1f;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VoiceBrowser] Failed to download {voice.Name}: {ex.Message}");
            if (File.Exists(tempOnnx)) File.Delete(tempOnnx);
            if (File.Exists(tempJson)) File.Delete(tempJson);
        }
        finally
        {
            voice.IsDownloading = false;
            activeDownloads--;
            AssetDatabase.Refresh();
        }
    }
}
