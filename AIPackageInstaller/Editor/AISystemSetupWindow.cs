using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

/// <summary>
/// Unified single-window setup flow for the AI Driven NPCs package.
/// Phase 1 — Package Installation: shows step-by-step progress for npm registry + git packages.
/// Phase 2 — Model Downloads:       shows per-file download bars once all packages are ready.
/// </summary>
public class AISystemSetupWindow : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // Shared types
    // ─────────────────────────────────────────────────────────────────────────
    public enum StepStatus { Pending, InProgress, Completed, Failed }

    public class InstallStep
    {
        public string Name;
        public string Description;
        public StepStatus Status = StepStatus.Pending;
        public string ErrorMessage;
    }

    private class ModelEntry
    {
        public string Group;
        public string DisplayName;
        public string Url;
        public string DestRelPath;   // relative to StreamingAssets
        public int    SizeMB;

        // Runtime
        public bool   IsDownloaded;
        public bool   IsDownloading;
        public float  Progress;
        public string Error;

        public string FullPath => Path.Combine(
            Application.streamingAssetsPath,
            DestRelPath.Replace('/', Path.DirectorySeparatorChar));

        public void Refresh() => IsDownloaded = File.Exists(FullPath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Model catalogue
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly List<ModelEntry> Models = new List<ModelEntry>
    {
        // ── Whisper ──────────────────────────────────────────────────────────
        new ModelEntry
        {
            Group = "Whisper (Speech Recognition)",
            DisplayName = "ggml-tiny.bin",
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            DestRelPath = "Whisper/ggml-tiny.bin",
            SizeMB = 74
        },

        // ── Piper Phonemizer (required) ───────────────────────────────────────
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

        // ── Piper Voice – Amy ─────────────────────────────────────────────────
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

        // ── Piper Voice – Ibrahim ─────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────────
    // Window state
    // ─────────────────────────────────────────────────────────────────────────
    private enum WindowPhase { PackageInstall, ModelDownload }

    private static AISystemSetupWindow _instance;

    // Phase 1
    public static List<InstallStep> Steps { get; } = new List<InstallStep>();

    // Phase 2
    private WindowPhase _phase = WindowPhase.PackageInstall;
    private Vector2     _scroll;
    private int         _activeDownloads;

    // ─────────────────────────────────────────────────────────────────────────
    // Static API — called by AIPackageInstaller
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/AI Packages/AI System Setup")]
    public static void ShowWindow()
    {
        Open(WindowPhase.PackageInstall);
    }

    [MenuItem("Tools/AI Packages/Download Model Files")]
    public static void ShowModelDownloaderWindow()
    {
        Open(WindowPhase.ModelDownload);
    }

    private static AISystemSetupWindow Open(WindowPhase phase)
    {
        if (_instance == null)
        {
            _instance = GetWindow<AISystemSetupWindow>(false, "AI System Setup", true);
            _instance.minSize = new Vector2(500, 440);
        }
        _instance._phase = phase;
        if (phase == WindowPhase.ModelDownload)
            _instance.RefreshModelStatus();
        _instance.Focus();
        return _instance;
    }

    // ── Package-install phase helpers ─────────────────────────────────────────

    public static void InitPackageSteps(List<InstallStep> steps)
    {
        Steps.Clear();
        Steps.AddRange(steps);
        _instance?.Repaint();
    }

    public static void UpdatePackageStep(string name, StepStatus status, string error = null)
    {
        var step = Steps.Find(s => s.Name == name);
        if (step != null)
        {
            step.Status      = status;
            step.ErrorMessage = error;
        }
        else
        {
            Steps.Add(new InstallStep { Name = name, Status = status, ErrorMessage = error });
        }
        _instance?.Repaint();
    }

    /// <summary>Switches window to the Model Download phase (called by AIPackageInstaller).</summary>
    public static void TransitionToModelDownload()
    {
        if (_instance == null)
            _instance = Open(WindowPhase.ModelDownload);
        else
        {
            _instance._phase = WindowPhase.ModelDownload;
            _instance.RefreshModelStatus();
            _instance.Focus();
            _instance.Repaint();
        }
    }

    // ── Model-download phase helpers ──────────────────────────────────────────

    /// <summary>
    /// Called by AIPackageInstaller after all packages are installed.
    /// Transitions the window to the model-download phase and starts downloading.
    /// Does nothing if all models are already present.
    /// </summary>
    public static void AutoStartDownloads()
    {
        foreach (var m in Models) m.Refresh();

        bool anyMissing = Models.Exists(m => !m.IsDownloaded);
        if (!anyMissing)
        {
            Debug.Log("<b>[AI System Setup]</b> All model files already present. ✅");
            // If window was open for packages only, no need to keep it around
            _instance?.Close();
            return;
        }

        TransitionToModelDownload();

        Debug.Log("<b>[AI System Setup]</b> Auto-starting download of missing model files…");
        _instance?.DownloadAllModels();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        _instance = this;
        EditorApplication.update += ForceRepaint;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ForceRepaint;
        if (_instance == this)
            _instance = null;
    }

    private void ForceRepaint() => Repaint();

    private void RefreshModelStatus()
    {
        foreach (var m in Models) m.Refresh();
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnGUI — dispatches to the active phase
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        DrawHeader();

        if (_phase == WindowPhase.PackageInstall)
            DrawPackagePhase();
        else
            DrawModelPhase();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shared header
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.Space(10);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("AI Driven NPCs — System Setup", titleStyle);
        EditorGUILayout.Space(2);

        // Phase breadcrumbs
        using (new EditorGUILayout.HorizontalScope())
        {
            GUIStyle crumb = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUIStyle crumbActive = new GUIStyle(crumb) { fontStyle = FontStyle.Bold };

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("① Package Install",
                _phase == WindowPhase.PackageInstall ? crumbActive : crumb,
                GUILayout.Width(130));
            EditorGUILayout.LabelField("→", crumb, GUILayout.Width(20));
            EditorGUILayout.LabelField("② Model Download",
                _phase == WindowPhase.ModelDownload ? crumbActive : crumb,
                GUILayout.Width(130));
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(8);
        DrawHorizontalLine();
        EditorGUILayout.Space(6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1 — Package Installation
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawPackagePhase()
    {
        EditorGUILayout.LabelField(
            "Installing required Unity packages. Please wait — Unity may reload the domain between steps.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        int completed = 0;
        foreach (var s in Steps)
            if (s.Status == StepStatus.Completed) completed++;

        float progress = Steps.Count > 0 ? (float)completed / Steps.Count : 0f;
        Rect progressRect = EditorGUILayout.GetControlRect(false, 22);
        EditorGUI.ProgressBar(progressRect, progress, $"Packages  {completed} / {Steps.Count}");
        EditorGUILayout.Space(10);

        foreach (var step in Steps)
            DrawPackageStepRow(step);

        EditorGUILayout.Space(8);
    }

    private void DrawPackageStepRow(InstallStep step)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            string icon = step.Status switch
            {
                StepStatus.InProgress => "⏳",
                StepStatus.Completed  => "✅",
                StepStatus.Failed     => "❌",
                _                     => "⚪"
            };

            // Animate dots for in-progress
            if (step.Status == StepStatus.InProgress)
            {
                int dots = (int)(EditorApplication.timeSinceStartup * 3) % 4;
                icon += new string('.', dots);
            }

            EditorGUILayout.LabelField(icon, GUILayout.Width(42));

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(step.Name, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(step.ErrorMessage))
                {
                    GUIStyle err = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = new Color(0.9f, 0.2f, 0.2f) } };
                    EditorGUILayout.LabelField(step.ErrorMessage, err);
                }
                else if (!string.IsNullOrEmpty(step.Description))
                {
                    EditorGUILayout.LabelField(step.Description, EditorStyles.miniLabel);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2 — Model Downloads
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawModelPhase()
    {
        EditorGUILayout.HelpBox(
            "LLM models are managed by LLMUnity directly.\n" +
            "Open the LLM component in your scene → click 'Download Model' in the Inspector.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        bool anyMissing = Models.Exists(m => !m.IsDownloaded && !m.IsDownloading);
        using (new EditorGUI.DisabledGroupScope(_activeDownloads > 0 || !anyMissing))
        {
            string btnLabel = _activeDownloads > 0
                ? $"Downloading… ({_activeDownloads} active)"
                : "⬇  Download All Missing Models";
            if (GUILayout.Button(btnLabel, GUILayout.Height(32)))
                DownloadAllModels();
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

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Status"))
                RefreshModelStatus();

            if (GUILayout.Button("← Back to Package Log"))
                _phase = WindowPhase.PackageInstall;
        }

        // Keep UI updating while downloads are active
        if (_activeDownloads > 0)
            EditorApplication.update += Repaint;
        else
            EditorApplication.update -= Repaint;
    }

    private void DrawModelRow(ModelEntry model)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            string icon = model.IsDownloading ? "⬇" : (model.IsDownloaded ? "✅" : "○");
            EditorGUILayout.LabelField(icon, GUILayout.Width(22));

            EditorGUILayout.LabelField($"{model.DisplayName}  ({model.SizeMB} MB)", GUILayout.MinWidth(200));

            if (model.IsDownloading)
            {
                Rect r = GUILayoutUtility.GetRect(120, 18);
                EditorGUI.ProgressBar(r, model.Progress, $"{(int)(model.Progress * 100)}%");
            }
            else if (!string.IsNullOrEmpty(model.Error))
            {
                GUIStyle err = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.9f, 0.2f, 0.2f) } };
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

    private void DownloadAllModels()
    {
        foreach (var m in Models)
            if (!m.IsDownloaded && !m.IsDownloading)
                _ = DownloadModel(m);
    }

    private async Task DownloadModel(ModelEntry model)
    {
        model.IsDownloading = true;
        model.Error         = null;
        model.Progress      = 0f;
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

            if (File.Exists(model.FullPath))
                File.Delete(model.FullPath);
            File.Move(tempPath, model.FullPath);

            model.IsDownloaded = true;
            Debug.Log($"<b>[AI System Setup]</b> ✅ Downloaded: {model.DestRelPath}");
        }
        catch (System.Exception ex)
        {
            model.Error = "Failed";
            Debug.LogError($"<b>[AI System Setup]</b> ❌ {model.DisplayName}: {ex.Message}");

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
                Debug.Log("<b>[AI System Setup]</b> All downloads complete. ✅");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void DrawHorizontalLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
    }
}
