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
        if (voice.IsDownloading || voice.IsDownloaded) return;
        voice.IsDownloading = true;
        voice.Progress = 0f;
        activeDownloads++;

        string onnxPath = voice.GetOnnxPath();
        string jsonPath = voice.GetJsonPath();

        try
        {
            string dir = Path.GetDirectoryName(onnxPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool downloadedWithCurl = false;

            // 1. Try native high-speed curl first
            try
            {
                var psiOnnx = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "curl.exe",
                    Arguments = $"-L --fail --retry 3 -s -S -o \"{onnxPath}\" \"{voice.UrlOnnx}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var process = System.Diagnostics.Process.Start(psiOnnx))
                {
                    if (process != null)
                    {
                        long targetBytes = 60L * 1024 * 1024; // ~60MB estimate
                        while (!process.HasExited)
                        {
                            await Task.Delay(150);
                            if (File.Exists(onnxPath) && targetBytes > 0)
                            {
                                try
                                {
                                    long curBytes = new FileInfo(onnxPath).Length;
                                    voice.Progress = Mathf.Clamp01((float)curBytes / targetBytes) * 0.9f;
                                    Repaint();
                                }
                                catch { }
                            }
                        }

                        await Task.Run(() => process.WaitForExit());

                        if (process.ExitCode == 0 && File.Exists(onnxPath))
                        {
                            // Download JSON with curl
                            var psiJson = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "curl.exe",
                                Arguments = $"-L --fail --retry 3 -s -S -o \"{jsonPath}\" \"{voice.UrlJson}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            using (var pJson = System.Diagnostics.Process.Start(psiJson))
                            {
                                if (pJson != null) await Task.Run(() => pJson.WaitForExit());
                            }

                            if (File.Exists(jsonPath)) downloadedWithCurl = true;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                downloadedWithCurl = false;
            }

            // 2. Fallback to WebClient if curl wasn't used
            if (!downloadedWithCurl)
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        voice.Progress = (e.ProgressPercentage / 100f) * 0.9f;
                        Repaint();
                    };
                    await client.DownloadFileTaskAsync(new System.Uri(voice.UrlOnnx), onnxPath);
                    await client.DownloadFileTaskAsync(new System.Uri(voice.UrlJson), jsonPath);
                }
            }

            voice.Progress = 1f;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VoiceBrowser] Failed to download {voice.Name}: {ex.Message}");
            try
            {
                if (File.Exists(onnxPath) && !voice.IsDownloaded) File.Delete(onnxPath);
                if (File.Exists(jsonPath) && !voice.IsDownloaded) File.Delete(jsonPath);
            }
            catch { }
        }
        finally
        {
            voice.IsDownloading = false;
            activeDownloads--;
            AssetDatabase.Refresh();
        }
    }
}
