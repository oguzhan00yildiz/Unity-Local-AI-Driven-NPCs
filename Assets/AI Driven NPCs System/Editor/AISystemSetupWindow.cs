using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AISystem.Editor
{
    /// <summary>
    /// Unified setup window for the AI Driven NPCs package.
    /// 
    /// Flow:
    ///   1. On first import → show a consent dialog asking the user to confirm setup.
    ///   2. If accepted → open this window and run Phase 1 (package installation).
    ///   3. When all packages are installed → switch to Phase 2 (model file downloads)
    ///      in the SAME window without closing it.
    /// </summary>
    public class AISystemSetupWindow : EditorWindow
    {
    // ─────────────────────────────────────────────────────────────────────────
    // Public types
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
        public string DestRelPath;
        public int    SizeMB;

        public bool   IsDownloaded;
        public bool   IsDownloading;
        public float  Progress;
        public long   DownloadedBytes;
        public string StatusDetail;
        public string Error;

        public string FullPath => Path.Combine(
            Application.streamingAssetsPath,
            DestRelPath.Replace('/', Path.DirectorySeparatorChar));

        public void Refresh()
        {
            if (!File.Exists(FullPath))
            {
                IsDownloaded = false;
                return;
            }

            var fileInfo = new FileInfo(FullPath);
            if (fileInfo.Length == 0 || (SizeMB > 1 && fileInfo.Length < (long)(SizeMB * 0.5f * 1024 * 1024)))
            {
                IsDownloaded = false;
            }
            else
            {
                IsDownloaded = true;
            }
        }
    }

    /// <summary>
    /// Checks if any LLM model (.gguf file or LLMUnity configured model) is already present in the project.
    /// </summary>
    public static bool HasAnyLLMModelInstalled()
    {
        try
        {
            // 1. Check StreamingAssets for any .gguf file (> 10MB)
            if (Directory.Exists(Application.streamingAssetsPath))
            {
                var streamingGgufs = Directory.GetFiles(Application.streamingAssetsPath, "*.gguf", SearchOption.AllDirectories);
                foreach (var path in streamingGgufs)
                {
                    try
                    {
                        if (File.Exists(path) && new FileInfo(path).Length > 10 * 1024 * 1024)
                            return true;
                    }
                    catch { }
                }
            }

            // 2. Check entire Assets folder for any .gguf file (> 10MB)
            if (Directory.Exists(Application.dataPath))
            {
                var assetGgufs = Directory.GetFiles(Application.dataPath, "*.gguf", SearchOption.AllDirectories);
                foreach (var path in assetGgufs)
                {
                    try
                    {
                        if (File.Exists(path) && new FileInfo(path).Length > 10 * 1024 * 1024)
                            return true;
                    }
                    catch { }
                }
            }

            // 3. Check LLMUnity LLMManager registered models via reflection — verify the file actually exists!
            var llmManagerType = System.Type.GetType("LLMUnity.LLMManager, undream.llmunity.Runtime");
            if (llmManagerType != null)
            {
                var modelEntriesField = llmManagerType.GetField("modelEntries", BindingFlags.Public | BindingFlags.Static);
                if (modelEntriesField != null)
                {
                    var entries = modelEntriesField.GetValue(null) as System.Collections.IList;
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            if (entry == null) continue;
                            var pathProp = entry.GetType().GetProperty("path") ?? entry.GetType().GetProperty("AssetPath");
                            string p = pathProp?.GetValue(entry) as string;
                            if (!string.IsNullOrEmpty(p) && File.Exists(p))
                            {
                                try
                                {
                                    if (new FileInfo(p).Length > 10 * 1024 * 1024)
                                        return true;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }

            // 4. Check LLM components in open scenes / prefabs — verify assigned file actually exists!
            var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
            if (llmType != null)
            {
                var llmInScene = UnityEngine.Object.FindAnyObjectByType(llmType);
                if (llmInScene != null)
                {
                    var modelProp = llmType.GetProperty("model", BindingFlags.Public | BindingFlags.Instance);
                    if (modelProp != null)
                    {
                        string assignedModel = modelProp.GetValue(llmInScene) as string;
                        if (!string.IsNullOrEmpty(assignedModel))
                        {
                            string inStreaming = Path.Combine(Application.streamingAssetsPath, assignedModel);
                            if (File.Exists(inStreaming) && new FileInfo(inStreaming).Length > 10 * 1024 * 1024)
                                return true;

                            string inAssets = Path.Combine(Application.dataPath, assignedModel);
                            if (File.Exists(inAssets) && new FileInfo(inAssets).Length > 10 * 1024 * 1024)
                                return true;

                            if (File.Exists(assignedModel) && new FileInfo(assignedModel).Length > 10 * 1024 * 1024)
                                return true;
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AI System Setup] Error checking for installed LLM models: {ex.Message}");
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Model catalogue
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly List<ModelEntry> Models = new List<ModelEntry>
    {
        new ModelEntry
        {
            Group = "Whisper (Speech Recognition)",
            DisplayName = "ggml-tiny.bin",
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            DestRelPath = "Whisper/ggml-tiny.bin",
            SizeMB = 74
        },
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
        new ModelEntry
        {
            Group = "LLM (Language Model)",
            DisplayName = "Qwen3.5-0.8B-Q4_K_M.gguf",
            Url = "https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF/resolve/main/Qwen3.5-0.8B-Q4_K_M.gguf",
            DestRelPath = "Qwen3.5-0.8B-Q4_K_M.gguf",
            SizeMB = 500
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Window state
    // ─────────────────────────────────────────────────────────────────────────
    private enum WindowPhase { PackageInstall, ModelDownload }

    private class WindowHolder
    {
        public AISystemSetupWindow Instance;
        public CancellationTokenSource DownloadCts;
        public System.Diagnostics.Process CurrentCurlProcess;
        public WebClient CurrentWebClient;
        public string CachedSampleScenePath;
        public double LastScenePathCheckTime;
    }
    private static readonly WindowHolder Holder = new WindowHolder();

    // Phase 1
    public static List<InstallStep> Steps { get; } = new List<InstallStep>();

    // Phase 2
    private WindowPhase _phase = WindowPhase.PackageInstall;
    private Vector2     _modelScroll;
    private int         _activeDownloads;

    // ─────────────────────────────────────────────────────────────────────────
    // Static API — called by AIPackageInstaller
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/AI Packages/AI System Setup", false, 0)]
    public static void ShowWindow()
    {
        WindowPhase initialPhase = AreAllPackagesInstalled() ? WindowPhase.ModelDownload : WindowPhase.PackageInstall;
        EnsureWindow(initialPhase);
    }

    [MenuItem("Tools/AI Packages/Download Model Files", false, 1)]
    public static void ShowModelDownloaderWindow() => EnsureWindow(WindowPhase.ModelDownload);

    /// <summary>
    /// Shows a permission consent dialog for automatic setup on package import.
    /// Returns true if the user agreed to proceed.
    /// </summary>
    public static bool ShowConsentDialog()
    {
        return AISystemDialogWindow.ShowDialog(
            title: "AI Driven NPCs",
            heading: "Download AI models",
            message: "The speech, voice and language models (about 770 MB) download to Assets/StreamingAssets.",
            primaryBtn: "Download",
            secondaryBtn: "Later",
            accentColor: EditorGUIUtility.isProSkin ? new Color(0.35f, 0.95f, 0.5f) : new Color(0.1f, 0.65f, 0.2f),
            width: 420,
            height: 170);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Package-install phase helpers
    // ─────────────────────────────────────────────────────────────────────────

    public static bool AreAllPackagesInstalled()
    {
        if (Steps.Count == 0)
        {
            RefreshPackageSteps();
        }
        return Steps.Count > 0 && Steps.All(s => s.Status == StepStatus.Completed);
    }

    public static bool AreAllModelsDownloaded()
    {
        foreach (var m in Models)
        {
            m.Refresh();
            if (!m.IsDownloaded) return false;
        }
        return true;
    }

    public static void RefreshPackageSteps(bool force = false)
    {
        if (!force && Steps.Exists(s => s.Status == StepStatus.InProgress))
            return;

        // All components ship inside ThirdParty/. A missing type means its assembly failed to compile.
        Steps.Clear();
        AddBundledStep("LLMUnity", "Language model", "LLMUnity.LLM, undream.llmunity.Runtime");
        AddBundledStep("Piper TTS", "Text to speech", "PiperTTS.PiperTTS, PiperTTS");
        AddBundledStep("Whisper", "Speech to text", "Whisper.WhisperManager, com.whisper.unity");
        AddBundledStep("ONNX Runtime", "Inference for Piper", "Microsoft.ML.OnnxRuntime.InferenceSession, com.github.asus4.onnxruntime");

        Holder.Instance?.Repaint();
    }

    private static void AddBundledStep(string name, string description, string typeName)
    {
        bool loaded = System.Type.GetType(typeName) != null;
        Steps.Add(new InstallStep
        {
            Name = name,
            Description = description,
            Status = loaded ? StepStatus.Completed : StepStatus.Failed
        });
    }

    private void SetPhase(WindowPhase newPhase)
    {
        _phase = newPhase;
        if (_phase == WindowPhase.ModelDownload)
            RefreshModelStatus();
        else
            RefreshPackageSteps();
        Repaint();
        GUIUtility.ExitGUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Model-download phase helpers
    // ─────────────────────────────────────────────────────────────────────────

    private const string SetupCompleteNotifiedKey = "AISystemSetupWindow.SetupCompleteNotified";

    /// <summary>
    /// Checks if sample demo scene (AIOScene or AIOTest) exists in the project.
    /// </summary>
    public static string FindSampleScenePath()
    {
        if (Holder.CachedSampleScenePath != null && EditorApplication.timeSinceStartup - Holder.LastScenePathCheckTime < 2.0)
        {
            return Holder.CachedSampleScenePath;
        }

        Holder.LastScenePathCheckTime = EditorApplication.timeSinceStartup;
        string[] guids = AssetDatabase.FindAssets("AIOScene t:Scene");
        if (guids != null && guids.Length > 0)
        {
            Holder.CachedSampleScenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return Holder.CachedSampleScenePath;
        }

        guids = AssetDatabase.FindAssets("AIOTest t:Scene");
        if (guids != null && guids.Length > 0)
        {
            Holder.CachedSampleScenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return Holder.CachedSampleScenePath;
        }
        Holder.CachedSampleScenePath = null;
        return null;
    }

    /// <summary>
    /// Checks if the package is installed as a UPM package (e.g. via git URL in manifest.json)
    /// rather than imported as Asset Store content into Assets/.
    /// </summary>
    public static bool IsInstalledViaUPM()
    {
        return UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.yildizoguzhan.ai-driven-npcs") != null;
    }

    /// <summary>
    /// Opens the sample demo scene immediately.
    /// </summary>
    public static void OpenSampleScene()
    {
        string scenePath = FindSampleScenePath();
        if (!string.IsNullOrEmpty(scenePath))
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            }
        }
        else
        {
            if (IsInstalledViaUPM())
            {
                Debug.LogWarning("[AI System Setup] AIOScene demo scene not found. Opening Package Manager to import samples…");
                OpenPackageManager();
            }
            else
            {
                Debug.LogWarning("[AI System Setup] AIOScene demo scene not found in Assets.");
            }
        }
    }

    /// <summary>
    /// Opens Unity Package Manager directly focused on this package.
    /// </summary>
    public static void OpenPackageManager()
    {
        try
        {
            UnityEditor.PackageManager.UI.Window.Open("com.yildizoguzhan.ai-driven-npcs");
        }
        catch
        {
            EditorApplication.ExecuteMenuItem("Window/Package Manager");
        }
    }

    /// <summary>
    /// Prompts the developer when setup completes for the first time.
    /// </summary>
    public static void CheckAndNotifyCompletion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        if (AreAllPackagesInstalled() && AreAllModelsDownloaded())
        {
            AIPackageInstaller.EnsureDefaultSamplePresets();
            AIPackageInstaller.MarkSetupCompleted();

            bool alreadyNotified = SessionState.GetBool(SetupCompleteNotifiedKey, false);
            if (!alreadyNotified)
            {
                SessionState.SetBool(SetupCompleteNotifiedKey, true);

                string scenePath = FindSampleScenePath();
                bool hasSampleScene = !string.IsNullOrEmpty(scenePath);
                bool isUpm = IsInstalledViaUPM();

                string message;
                string primaryBtn;

                if (hasSampleScene)
                {
                    message = "Open the demo scene to try it.";
                    primaryBtn = "Open Demo Scene";
                }
                else if (isUpm)
                {
                    message = "Import the samples from the Package Manager to get the demo scene.";
                    primaryBtn = "Open Package Manager";
                }
                else
                {
                    message = "All models are downloaded.";
                    primaryBtn = "Close";
                }

                bool showCancel = hasSampleScene || isUpm;
                bool choosePrimary = AISystemDialogWindow.ShowDialog(
                    title: "AI Driven NPCs",
                    heading: "Setup complete",
                    message: message,
                    primaryBtn: primaryBtn,
                    secondaryBtn: showCancel ? "Close" : null,
                    accentColor: EditorGUIUtility.isProSkin ? new Color(0.35f, 0.95f, 0.5f) : new Color(0.1f, 0.65f, 0.2f),
                    width: 420,
                    height: 170);

                if (choosePrimary)
                {
                    if (hasSampleScene)
                        OpenSampleScene();
                    else if (isUpm)
                        OpenPackageManager();
                }
            }
        }
    }

    /// <summary>
    /// Transitions the existing window to the Model Download phase and starts
    /// downloading missing files. Called by AIPackageInstaller when packages are done.
    /// </summary>
    public static void AutoStartDownloads()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        foreach (var m in Models) m.Refresh();

        bool anyMissing = Models.Exists(m => !m.IsDownloaded);

        // Switch phase inside the already-open window (no new window!)
        var existingWin = EnsureWindow(WindowPhase.ModelDownload);
        if (existingWin != null)
        {
            existingWin.RefreshModelStatus();
        }

        if (!anyMissing)
        {
            Debug.Log("<b>[AI System Setup]</b> All model files already present. ✅");
            EditorApplication.delayCall += () => CheckAndNotifyCompletion();
            return;
        }

        if (existingWin != null)
        {
            Debug.Log("<b>[AI System Setup]</b> Starting download of missing model files…");
            existingWin.DownloadAllModels(isAutomatic: true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the one setup window, creating it if needed. Never creates a second one.
    /// </summary>
    private static AISystemSetupWindow EnsureWindow(WindowPhase phase)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return Holder.Instance;

        if (Holder.Instance == null)
        {
            // CreateInstance + ShowUtility keeps the window floating and prevents
            // it from being merged with other dockable windows (no duplicate docking).
            Holder.Instance = CreateInstance<AISystemSetupWindow>();
            Holder.Instance.titleContent = new GUIContent("AI System Setup");
            Holder.Instance.minSize      = new Vector2(560, 500);
            Holder.Instance.ShowUtility();
        }

        Holder.Instance._phase = phase;
        RefreshPackageSteps();
        if (phase == WindowPhase.ModelDownload)
            Holder.Instance.RefreshModelStatus();

        Holder.Instance.Focus();

        EditorApplication.delayCall += () =>
        {
            CheckAndNotifyCompletion();
        };

        return Holder.Instance;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        Holder.Instance = this;
        EditorApplication.update += ForceRepaint;
        Application.runInBackground = true;
        RefreshPackageSteps();
        RefreshModelStatus();
    }

    private void OnDisable()
    {
        EditorApplication.update -= ForceRepaint;
        if (Holder.Instance == this)
            Holder.Instance = null;
    }

    private void ForceRepaint() => Repaint();

    private void RefreshModelStatus()
    {
        foreach (var m in Models) m.Refresh();
        Repaint();
    }

    public void ForceRestartDownloads()
    {
        Debug.Log("<b>[AI System Setup]</b> ⚡ Force Restart Downloads requested.");

        // 1. Cancel running CancellationTokenSource
        try
        {
            Holder.DownloadCts?.Cancel();
            Holder.DownloadCts?.Dispose();
        }
        catch { }
        Holder.DownloadCts = null;

        // 2. Kill running curl process if any
        try
        {
            if (Holder.CurrentCurlProcess != null && !Holder.CurrentCurlProcess.HasExited)
            {
                Holder.CurrentCurlProcess.Kill();
            }
        }
        catch { }
        Holder.CurrentCurlProcess = null;

        // 3. Cancel WebClient if any
        try
        {
            Holder.CurrentWebClient?.CancelAsync();
            Holder.CurrentWebClient?.Dispose();
        }
        catch { }
        Holder.CurrentWebClient = null;

        // 4. Delete incomplete .download temp files to ensure clean state
        try
        {
            string tempDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "AIDownloads");
            if (Directory.Exists(tempDir))
            {
                var tempFiles = Directory.GetFiles(tempDir, "*.download");
                foreach (var f in tempFiles)
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
        catch { }

        // 5. Reset model states & counters
        _isDownloadingAll = false;
        _activeDownloads = 0;
        foreach (var m in Models)
        {
            m.IsDownloading = false;
            m.Progress = 0f;
            m.DownloadedBytes = 0;
            m.StatusDetail = null;
            m.Error = null;
            m.Refresh();
        }

        Repaint();

        // 6. Ensure background execution and start fresh download batch
        Application.runInBackground = true;
        EditorApplication.delayCall += () =>
        {
            Debug.Log("<b>[AI System Setup]</b> ⚡ Starting fresh download of missing models…");
            DownloadAllModels(isAutomatic: false, forceRedownload: false);
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GUI Layout Snapshot (Prevents ArgumentException / Repaint control count mismatch)
    // ─────────────────────────────────────────────────────────────────────────

    private class GuiLayoutState
    {
        public WindowPhase Phase;
        public bool AllPackagesInstalled;
        public bool AllModelsDownloaded;
        public bool HasSampleScene;
        public string SampleScenePath;
        public bool IsUpm;

        // Header / overall
        public int PkgsDoneCount;
        public int ModelsDoneCount;
        public int TotalItems;
        public int TotalDone;
        public float TotalProgress;

        // Package Phase
        public struct StepSnapshot
        {
            public string Name;
            public string Description;
            public StepStatus Status;
            public string ErrorMessage;
        }
        public readonly List<StepSnapshot> StepSnapshots = new List<StepSnapshot>();
        public int PkgCompletedCount;
        public bool AllPackagesCompleted;

        // Model Phase
        public int ActiveDownloads;
        public bool IsDownloadingAll;
        public int MissingCount;
        public int DownloadedCount;
        public int TotalCount;
        public float ModelProgress;
        public bool AllModelsDone;

        public ModelEntry ActiveDownloadingModel;

        public struct ModelSnapshot
        {
            public string Group;
            public string DisplayName;
            public int SizeMB;
            public string DestRelPath;
            public bool IsDownloading;
            public bool IsDownloaded;
            public string Error;
            public bool ExistsOnDisk;
            public float Progress;
            public long DownloadedBytes;
            public string StatusDetail;
        }
        public readonly Dictionary<string, ModelSnapshot> ModelSnapshots = new Dictionary<string, ModelSnapshot>();

        public ModelSnapshot GetModelSnapshot(ModelEntry entry)
        {
            if (entry != null && ModelSnapshots.TryGetValue(entry.DestRelPath, out var s))
                return s;
            return default;
        }
    }

    private GuiLayoutState _guiState;

    private GuiLayoutState CaptureGuiLayoutState()
    {
        var state = new GuiLayoutState
        {
            Phase = _phase,
            ActiveDownloads = _activeDownloads,
            IsDownloadingAll = _isDownloadingAll
        };

        // 1. Packages
        if (Steps.Count == 0)
        {
            RefreshPackageSteps();
        }
        int pkgDone = 0;
        foreach (var s in Steps)
        {
            if (s.Status == StepStatus.Completed) pkgDone++;
            state.StepSnapshots.Add(new GuiLayoutState.StepSnapshot
            {
                Name = s.Name,
                Description = s.Description,
                Status = s.Status,
                ErrorMessage = s.ErrorMessage
            });
        }
        state.PkgCompletedCount = pkgDone;
        state.AllPackagesCompleted = Steps.Count > 0 && pkgDone == Steps.Count;
        state.AllPackagesInstalled = state.AllPackagesCompleted;

        // 2. Models
        int missing = 0;
        int downloaded = 0;
        ModelEntry activeModel = null;

        foreach (var m in Models)
        {
            m.Refresh();
            bool exists = File.Exists(m.FullPath);
            if (m.IsDownloading)
            {
                if (activeModel == null) activeModel = m;
            }
            else if (m.IsDownloaded)
            {
                downloaded++;
            }
            else
            {
                missing++;
            }

            state.ModelSnapshots[m.DestRelPath] = new GuiLayoutState.ModelSnapshot
            {
                Group = m.Group,
                DisplayName = m.DisplayName,
                SizeMB = m.SizeMB,
                DestRelPath = m.DestRelPath,
                IsDownloading = m.IsDownloading,
                IsDownloaded = m.IsDownloaded,
                Error = m.Error,
                ExistsOnDisk = exists,
                Progress = m.Progress,
                DownloadedBytes = m.DownloadedBytes,
                StatusDetail = m.StatusDetail
            };
        }

        state.ActiveDownloadingModel = activeModel;
        state.DownloadedCount = downloaded;
        state.MissingCount = missing;
        state.TotalCount = Models.Count;
        state.AllModelsDone = (downloaded == Models.Count && Models.Count > 0);
        state.AllModelsDownloaded = state.AllModelsDone;
        state.ModelProgress = state.TotalCount > 0 ? (float)downloaded / state.TotalCount : 0f;

        // Overall progress in header
        state.PkgsDoneCount = pkgDone;
        state.ModelsDoneCount = downloaded;
        state.TotalItems = (Steps.Count > 0 ? Steps.Count : 4) + Models.Count;
        state.TotalDone = pkgDone + downloaded;
        state.TotalProgress = state.TotalItems > 0 ? (float)state.TotalDone / state.TotalItems : 0f;

        // Banner info (only search scene if completed)
        if (state.AllPackagesInstalled && state.AllModelsDownloaded)
        {
            state.SampleScenePath = FindSampleScenePath();
            state.HasSampleScene = !string.IsNullOrEmpty(state.SampleScenePath);
            state.IsUpm = IsInstalledViaUPM();
        }

        return state;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnGUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout || _guiState == null)
        {
            _guiState = CaptureGuiLayoutState();
        }

        DrawHeader(_guiState);
        DrawSetupCompleteBanner(_guiState);

        if (_guiState.Phase == WindowPhase.PackageInstall)
            DrawPackagePhase(_guiState);
        else
            DrawModelPhase(_guiState);
    }

    private void DrawSetupCompleteBanner(GuiLayoutState state)
    {
        if (!state.AllPackagesInstalled || !state.AllModelsDownloaded)
            return;

        string scenePath = state.SampleScenePath;
        bool hasSampleScene = state.HasSampleScene;
        bool isUpm = state.IsUpm;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.Space(3);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.95f, 0.5f) : new Color(0.1f, 0.6f, 0.2f) }
            };
            EditorGUILayout.LabelField("Setup complete", headerStyle);
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (hasSampleScene)
                {
                    GUIStyle playBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                    if (GUILayout.Button("Open Demo Scene", playBtnStyle, GUILayout.Height(26)))
                    {
                        OpenSampleScene();
                        GUIUtility.ExitGUI();
                    }
                }
                else if (isUpm)
                {
                    if (GUILayout.Button("Open Package Manager", GUILayout.Height(26)))
                    {
                        OpenPackageManager();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.Space(6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Header
    // ─────────────────────────────────────────────────────────────────────────

    private GUIStyle _activeLeftTabStyle;
    private GUIStyle _activeRightTabStyle;

    private GUIStyle GetActiveTabStyle(bool isLeft)
    {
        Color highlight = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.95f, 0.5f) : new Color(0.05f, 0.65f, 0.2f);
        GUIStyle baseStyle = isLeft ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonRight;
        ref GUIStyle cached = ref (isLeft ? ref _activeLeftTabStyle : ref _activeRightTabStyle);
        if (cached == null)
        {
            cached = new GUIStyle(baseStyle)
            {
                fontStyle = FontStyle.Bold
            };
        }
        cached.normal.textColor = highlight;
        cached.active.textColor = highlight;
        cached.focused.textColor = highlight;
        cached.hover.textColor = highlight;
        return cached;
    }

    private void DrawHeader(GuiLayoutState state)
    {
        EditorGUILayout.Space(10);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("AI Driven NPCs Setup", titleStyle);
        EditorGUILayout.Space(8);

        // Phase navigation bar with clickable breadcrumb tabs
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // Step 1 tab
            bool pkgsDone = state.AllPackagesInstalled;
            string step1Title = pkgsDone ? "1. Components  ✓" : "1. Components";
            GUIStyle step1Style = state.Phase == WindowPhase.PackageInstall 
                ? GetActiveTabStyle(true) 
                : EditorStyles.miniButtonLeft;

            if (GUILayout.Button(step1Title, step1Style, GUILayout.Width(170), GUILayout.Height(24)))
            {
                SetPhase(WindowPhase.PackageInstall);
            }

            // Step 2 tab
            bool modelsDone = state.AllModelsDownloaded;
            string step2Title = modelsDone ? "2. Models  ✓" : "2. Models";
            GUIStyle step2Style = state.Phase == WindowPhase.ModelDownload 
                ? GetActiveTabStyle(false) 
                : EditorStyles.miniButtonRight;

            if (GUILayout.Button(step2Title, step2Style, GUILayout.Width(170), GUILayout.Height(24)))
            {
                SetPhase(WindowPhase.ModelDownload);
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(6);

        // Overall System Setup progress bar
        Rect setupBarRect = EditorGUILayout.GetControlRect(false, 20);
        string setupBarText = $"{state.TotalDone} / {state.TotalItems}";
        EditorGUI.ProgressBar(setupBarRect, state.TotalProgress, setupBarText);

        EditorGUILayout.Space(6);
        DrawLine();
        EditorGUILayout.Space(6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1 — Package Install
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawPackagePhase(GuiLayoutState state)
    {
        if (!state.AllPackagesCompleted)
        {
            EditorGUILayout.HelpBox("A component failed to compile. Check the Console.", MessageType.Error);
            EditorGUILayout.Space(8);
        }

        foreach (var step in state.StepSnapshots)
            DrawPackageRow(step);

        GUILayout.FlexibleSpace();
        DrawLine();
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Height(26), GUILayout.Width(80)))
            {
                RefreshPackageSteps(force: true);
                GUIUtility.ExitGUI();
            }


            GUILayout.FlexibleSpace();

            GUIStyle nextBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Next", nextBtnStyle, GUILayout.Height(26), GUILayout.Width(90)))
            {
                SetPhase(WindowPhase.ModelDownload);
            }
        }
        EditorGUILayout.Space(6);
    }

    private void DrawPackageRow(GuiLayoutState.StepSnapshot step)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
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

            if (step.Status == StepStatus.InProgress)
            {
                Rect r = GUILayoutUtility.GetRect(130, 20);
                float anim = (float)((EditorApplication.timeSinceStartup * 0.35) % 1.0);
                EditorGUI.ProgressBar(r, anim, "Installing…");
            }
            else if (step.Status == StepStatus.Completed)
            {
                EditorGUILayout.LabelField("Ready", EditorStyles.miniLabel, GUILayout.Width(60));
            }
            else if (step.Status == StepStatus.Failed)
            {
                EditorGUILayout.LabelField("Not loaded", EditorStyles.miniLabel, GUILayout.Width(70));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2 — Model Downloads
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawModelPhase(GuiLayoutState state)
    {
        EditorGUILayout.LabelField("Files download to Assets/StreamingAssets. Change the language model on the LLM object in the AISystem prefab.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4);

        int missingCount = state.MissingCount;
        int downloadedCount = state.DownloadedCount;
        int totalCount = state.TotalCount;

        // Overall Models progress bar
        float modelProgress = state.ModelProgress;
        Rect mRect = EditorGUILayout.GetControlRect(false, 22);
        string modelProgressText = $"Models  {downloadedCount} / {totalCount}";
        EditorGUI.ProgressBar(mRect, modelProgress, modelProgressText);
        EditorGUILayout.Space(6);

        if (state.ActiveDownloads > 0)
        {
            var activeModel = state.ActiveDownloadingModel;
            int queueRemaining = state.MissingCount;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string activeName = activeModel != null ? activeModel.DisplayName : "Model files";
                    GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    EditorGUILayout.LabelField($"Downloading {activeName}", titleStyle);
                }

                if (activeModel != null)
                {

                    float pct = activeModel.Progress;
                    string progressText;
                    if (activeModel.DownloadedBytes > 0)
                    {
                        float curMB = activeModel.DownloadedBytes / (1024f * 1024f);
                        progressText = $"{curMB:0.1} / {activeModel.SizeMB} MB  ({(int)(pct * 100)}%)";
                    }
                    else if (!string.IsNullOrEmpty(activeModel.StatusDetail))
                    {
                        progressText = activeModel.StatusDetail;
                    }
                    else
                    {
                        progressText = "Connecting…";
                    }

                    Rect pRect = EditorGUILayout.GetControlRect(false, 20);
                    EditorGUI.ProgressBar(pRect, pct, progressText);
                }

                EditorGUILayout.Space(2);
            }
            EditorGUILayout.Space(4);
        }
        else if (missingCount > 0)
        {
            GUIStyle dlAllBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
            if (GUILayout.Button($"Download All ({missingCount})", dlAllBtnStyle, GUILayout.Height(30)))
            {
                DownloadAllModels(isAutomatic: false, forceRedownload: false);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.Space(4);
        }

        _modelScroll = EditorGUILayout.BeginScrollView(_modelScroll);

        string curGroup = null;
        foreach (var m in Models)
        {
            if (m.Group != curGroup)
            {
                curGroup = m.Group;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(curGroup, EditorStyles.boldLabel);
            }
            DrawModelRow(m, state);
        }

        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        DrawLine();
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Back", GUILayout.Height(26), GUILayout.Width(70)))
                SetPhase(WindowPhase.PackageInstall);

            if (state.ActiveDownloads > 0 && GUILayout.Button("Restart Downloads", GUILayout.Height(26), GUILayout.Width(130)))
            {
                ForceRestartDownloads();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Refresh", GUILayout.Height(26), GUILayout.Width(75)))
            {
                RefreshModelStatus();
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();

            if (state.AllModelsDone)
            {
                if (GUILayout.Button("Re-download All", GUILayout.Height(26), GUILayout.Width(120)))
                {
                    bool confirm = AISystemDialogWindow.ShowDialog(
                        title: "AI Driven NPCs",
                        heading: "Re-download all models?",
                        message: "Existing model files are replaced.",
                        primaryBtn: "Re-download",
                        secondaryBtn: "Cancel",
                        accentColor: EditorGUIUtility.isProSkin ? new Color(0.4f, 0.7f, 1f) : new Color(0.15f, 0.45f, 0.85f),
                        width: 400,
                        height: 160);
                    if (confirm)
                    {
                        DownloadAllModels(isAutomatic: false, forceRedownload: true);
                    }
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Close", GUILayout.Height(26), GUILayout.Width(80)))
                {
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUILayout.Space(6);
    }

    private void DrawModelRow(ModelEntry model, GuiLayoutState state)
    {
        var snap = state.GetModelSnapshot(model);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUIStyle nameStyle = snap.IsDownloading ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.LabelField($"{model.DisplayName}  ({model.SizeMB} MB)", nameStyle, GUILayout.MinWidth(180));

            if (snap.IsDownloading)
            {
                Rect r = GUILayoutUtility.GetRect(160, 18);
                float curProgress = model.Progress > 0 ? model.Progress : snap.Progress;
                long curBytes = model.DownloadedBytes > 0 ? model.DownloadedBytes : snap.DownloadedBytes;
                string barText;
                if (curBytes > 0)
                {
                    float curMB = curBytes / (1024f * 1024f);
                    barText = $"{curMB:0.1} / {model.SizeMB} MB ({(int)(curProgress * 100)}%)";
                }
                else if (!string.IsNullOrEmpty(model.StatusDetail))
                {
                    barText = model.StatusDetail;
                }
                else
                {
                    barText = "Starting…";
                }
                EditorGUI.ProgressBar(r, curProgress, barText);

            }
            else if (!string.IsNullOrEmpty(snap.Error))
            {
                GUIStyle err = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.9f, 0.2f, 0.2f) } };
                EditorGUILayout.LabelField(snap.Error, err, GUILayout.Width(100));
                if (GUILayout.Button("Retry", GUILayout.Width(65)))
                {
                    if (Holder.DownloadCts == null || Holder.DownloadCts.IsCancellationRequested)
                        Holder.DownloadCts = new CancellationTokenSource();
                    _ = DownloadModel(model, Holder.DownloadCts.Token);
                    GUIUtility.ExitGUI();
                }
            }
            else if (snap.IsDownloaded)
            {
                EditorGUILayout.LabelField("Ready", EditorStyles.miniLabel, GUILayout.Width(80));
                if (snap.ExistsOnDisk)
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(55)))
                    {
                        if (File.Exists(model.FullPath)) File.Delete(model.FullPath);
                        model.Refresh();
                        AssetDatabase.Refresh();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Download", GUILayout.Width(80)))
                {
                    if (Holder.DownloadCts == null || Holder.DownloadCts.IsCancellationRequested)
                        Holder.DownloadCts = new CancellationTokenSource();
                    _ = DownloadModel(model, Holder.DownloadCts.Token);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private bool _isDownloadingAll;

    public async void DownloadAllModels(bool isAutomatic = false, bool forceRedownload = false)
    {
        if (_isDownloadingAll) return;
        _isDownloadingAll = true;

        Holder.DownloadCts?.Cancel();
        Holder.DownloadCts?.Dispose();
        Holder.DownloadCts = new CancellationTokenSource();
        var ct = Holder.DownloadCts.Token;

        try
        {
            foreach (var m in Models)
            {
                if (ct.IsCancellationRequested) break;

                m.Refresh();
                if ((!m.IsDownloaded || forceRedownload) && !m.IsDownloading)
                {
                    if (forceRedownload && File.Exists(m.FullPath))
                    {
                        try { File.Delete(m.FullPath); } catch { }
                        m.Refresh();
                    }
                    await DownloadModel(m, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("<b>[AI System Setup]</b> Model downloads cancelled.");
        }
        finally
        {
            _isDownloadingAll = false;
        }
    }

    private async Task DownloadModel(ModelEntry model, CancellationToken ct = default)
    {
        if (model.IsDownloading) return;
        model.Refresh();
        if (model.IsDownloaded) return;

        model.IsDownloading = true;
        model.Error         = null;
        model.Progress      = 0f;
        model.DownloadedBytes = 0;
        model.StatusDetail  = "Starting…";
        _activeDownloads++;
        Repaint();

        Application.runInBackground = true;

        string tempDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "AIDownloads");
        if (!Directory.Exists(tempDir))
            Directory.CreateDirectory(tempDir);

        string tempPath = Path.Combine(tempDir, Path.GetFileName(model.FullPath) + ".download");
        long minExpectedBytes = model.SizeMB > 1 ? (long)(model.SizeMB * 0.85f * 1024 * 1024) : 100;
        bool alreadyDownloaded = false;

        if (File.Exists(tempPath))
        {
            var existingFi = new FileInfo(tempPath);
            if (existingFi.Length >= minExpectedBytes)
            {
                alreadyDownloaded = true;
                model.Progress = 1f;
                model.DownloadedBytes = existingFi.Length;
                model.StatusDetail = "Verifying…";
                Debug.Log($"<b>[AI System Setup]</b> Using completed temp download for {model.DisplayName} ({existingFi.Length / (1024 * 1024)} MB).");
            }
            else
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            if (!alreadyDownloaded)
            {
                bool downloadedWithCurl = false;
                model.StatusDetail = "Connecting…";

                // 1. Try native high-speed curl first (bypasses Mono single-thread TLS)
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "curl.exe",
                        Arguments = $"-L --fail --connect-timeout 20 --retry 3 --retry-delay 2 -s -S -o \"{tempPath}\" \"{model.Url}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    using (var process = new System.Diagnostics.Process { StartInfo = psi })
                    {
                        Holder.CurrentCurlProcess = process;
                        process.Start();
                        var stderrTask = process.StandardError.ReadToEndAsync();
                        long targetBytes = (long)model.SizeMB * 1024 * 1024;

                        while (!process.HasExited)
                        {
                            if (ct.IsCancellationRequested)
                            {
                                try { process.Kill(); } catch { }
                                ct.ThrowIfCancellationRequested();
                            }

                            await Task.Delay(150).ConfigureAwait(false);
                            process.Refresh();

                            if (File.Exists(tempPath) && targetBytes > 0)
                            {
                                try
                                {
                                    long curBytes = new FileInfo(tempPath).Length;
                                    model.DownloadedBytes = curBytes;
                                    model.Progress = Mathf.Clamp01((float)curBytes / targetBytes);
                                    float curMB = curBytes / (1024f * 1024f);
                                    model.StatusDetail = $"{curMB:0.1} / {model.SizeMB} MB ({(int)(model.Progress * 100)}%)";
                                }
                                catch { }
                            }
                        }

                        await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
                        string curlError = "";
                        try { curlError = await stderrTask.ConfigureAwait(false); } catch { }
                        Holder.CurrentCurlProcess = null;

                        if (process.ExitCode == 0 && File.Exists(tempPath))
                        {
                            downloadedWithCurl = true;
                        }
                        else if (!ct.IsCancellationRequested)
                        {
                            Debug.LogWarning($"<b>[AI System Setup]</b> curl download failed for {model.DisplayName} (code {process.ExitCode}): {curlError}. Falling back to WebClient…");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogWarning($"<b>[AI System Setup]</b> curl process threw for {model.DisplayName}: {ex.Message}. Falling back to WebClient…");
                    }
                    downloadedWithCurl = false;
                }
                finally
                {
                    Holder.CurrentCurlProcess = null;
                }

                ct.ThrowIfCancellationRequested();

                // 2. Fallback to WebClient if curl wasn't available
                if (!downloadedWithCurl)
                {
                    using (var client = new WebClient())
                    {
                        Holder.CurrentWebClient = client;
                        using (ct.Register(() => { try { client.CancelAsync(); } catch { } }))
                        {
                            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                            client.DownloadProgressChanged += (_, e) =>
                            {
                                model.DownloadedBytes = e.BytesReceived;
                                model.Progress = e.ProgressPercentage / 100f;
                                float curMB = e.BytesReceived / (1024f * 1024f);
                                float totMB = e.TotalBytesToReceive > 0 ? e.TotalBytesToReceive / (1024f * 1024f) : model.SizeMB;
                                model.StatusDetail = $"{curMB:0.1} / {totMB:0.1} MB ({e.ProgressPercentage}%)";
                            };
                            await client.DownloadFileTaskAsync(new System.Uri(model.Url), tempPath).ConfigureAwait(false);
                        }
                        Holder.CurrentWebClient = null;
                    }
                }
            }

            ct.ThrowIfCancellationRequested();

            // 3. Verify downloaded file size
            var fi = new FileInfo(tempPath);
            if (model.SizeMB > 1 && fi.Length < (long)(model.SizeMB * 0.5f * 1024 * 1024))
            {
                throw new System.Exception($"Downloaded file size ({fi.Length / (1024 * 1024)}MB) was smaller than expected ({model.SizeMB}MB).");
            }

            // 4. Move finished file atomically to target path
            string dir = Path.GetDirectoryName(model.FullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(tempPath, model.FullPath, true);
            try { File.Delete(tempPath); } catch { }

            model.IsDownloaded = true;
            model.Progress = 1f;
            model.DownloadedBytes = (long)model.SizeMB * 1024 * 1024;
            model.StatusDetail = "Done";

            if (Path.GetExtension(model.FullPath).ToLower() == ".gguf")
            {
                RegisterWithLLMManager(model.FullPath, model.DisplayName);
            }

            Debug.Log($"<b>[AI System Setup]</b> ✅ Downloaded: {model.DestRelPath}");
        }
        catch (OperationCanceledException)
        {
            model.Error = null;
            model.StatusDetail = "Cancelled";
            Debug.Log($"<b>[AI System Setup]</b> Download cancelled for {model.DisplayName}");
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch { }
            throw;
        }
        catch (System.Exception ex)
        {
            if (ct.IsCancellationRequested)
            {
                model.Error = null;
                model.StatusDetail = "Cancelled";
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw new OperationCanceledException();
            }

            model.Error = "Failed";
            model.StatusDetail = "Failed";
            Debug.LogError($"<b>[AI System Setup]</b> ❌ {model.DisplayName}: {ex.Message}");

            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (File.Exists(model.FullPath) && !model.IsDownloaded)
                {
                    File.Delete(model.FullPath);
                }
            }
            catch { }
        }
        finally
        {
            model.IsDownloading = false;
            _activeDownloads = Mathf.Max(0, _activeDownloads - 1);
            Holder.CurrentCurlProcess = null;
            Holder.CurrentWebClient = null;

            if (_activeDownloads == 0 && !_isDownloadingAll && !ct.IsCancellationRequested)
            {
                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.Refresh();
                    AutoConfigureLLM();
                    Repaint();
                    Debug.Log("<b>[AI System Setup]</b> All downloads complete. ✅");
                    AIPackageInstaller.MarkSetupCompleted();
                    CheckAndNotifyCompletion();
                };
            }
        }
    }

    /// <summary>
    /// After all models are downloaded, find ALL prefab assets and open scenes
    /// that contain an LLM component and call SetModel() on each one.
    /// Uses reflection to avoid hard dependency on LLMUnity types (which may
    /// still be installing when this script first compiles).
    /// </summary>
    public static void AutoConfigureLLM()
    {
        // Only the package's default model is assigned. Other .gguf files in the project are left alone.
        ModelEntry llmModel = Models.Find(m =>
            Path.GetExtension(m.DestRelPath).ToLower() == ".gguf" && m.IsDownloaded);
        if (llmModel == null || !File.Exists(llmModel.FullPath)) return;

        string registeredName = RegisterWithLLMManager(llmModel.FullPath, llmModel.DisplayName);
        if (string.IsNullOrEmpty(registeredName)) return;

        try
        {
            // Only this package's prefabs and scenes (Asset Store or UPM sample folder) are changed
            const string PackageFolder = "/AI Driven NPCs System/";
            var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
            int prefabsUpdated = 0;

            if (llmType != null)
            {
                var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                foreach (string guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains(PackageFolder)) continue;
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    // Check for LLM component via reflection
                    var comp = prefab.GetComponent(llmType);
                    if (comp != null)
                    {
                        CallSetModelOnComponent(comp, llmType, registeredName);
                        EditorUtility.SetDirty(prefab);
                        prefabsUpdated++;
                        Debug.Log($"<b>[AI System Setup]</b> ✅ Set model '{registeredName}' on LLM in: {path}");
                    }
                }
            }

            // Update any LLM components in currently open scenes
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.path.Contains(PackageFolder)) continue;

                foreach (var rootGo in scene.GetRootGameObjects())
                {
                    SetLLMModelOnChildren(rootGo, registeredName);
                }
            }

            AssetDatabase.SaveAssets();

            if (prefabsUpdated > 0)
            {
                Debug.Log($"<b>[AI System Setup]</b> ✅ Auto-configured {prefabsUpdated} LLM prefab(s) with model '{registeredName}'");
            }
            else
            {
                Debug.Log($"<b>[AI System Setup]</b> ✅ LLM model '{registeredName}' registered. " +
                    "Prefabs will use it automatically.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"<b>[AI System Setup]</b> LLM auto-configuration skipped " +
                $"(LLMUnity may still be installing): {ex.Message}");
        }
    }

    /// <summary>
    /// Calls SetModel() on an LLM component via reflection.
    /// </summary>
    private static void CallSetModelOnComponent(object component, System.Type llmType, string modelName)
    {
        try
        {
            var modelProp = llmType.GetProperty("model",
                BindingFlags.Public | BindingFlags.Instance);
            if (modelProp == null) return;

            string currentModel = modelProp.GetValue(component) as string ?? "";
            if (currentModel == modelName) return;

            var setModelMethod = llmType.GetMethod("SetModel",
                BindingFlags.Public | BindingFlags.Instance);
            if (setModelMethod != null)
            {
                setModelMethod.Invoke(component, new object[] { modelName });
            }
        }
        catch
        {
            // Silently skip — will be handled by the caller's catch
            throw;
        }
    }

    /// <summary>
    /// Registers a model file with LLMUnity's LLMManager via reflection.
    /// Returns the registered filename, or falls back to the file's own name.
    /// </summary>
    private static string RegisterWithLLMManager(string fullPath, string label)
    {
        string modelName = Path.GetFileName(fullPath);
        try
        {
            // Resolve: LLMUnity.LLMManager.LoadModel(path, false, label)
            var llmManagerType = System.Type.GetType("LLMUnity.LLMManager, undream.llmunity.Runtime");
            if (llmManagerType == null)
            {
                Debug.LogWarning("[AI System Setup] LLMManager type not found — LLMUnity may not be loaded yet.");
                return modelName;
            }

            var loadMethod = llmManagerType.GetMethod("LoadModel",
                BindingFlags.Public | BindingFlags.Static);
            if (loadMethod != null)
            {
                var result = loadMethod.Invoke(null, new object[] { fullPath, false, label });
                if (result != null)
                {
                    Debug.Log($"<b>[AI System Setup]</b> ✅ Registered LLM model with LLMManager: {result}");
                    return result.ToString();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"<b>[AI System Setup]</b> LLMManager registration failed: {ex.Message}");
        }
        return modelName;
    }

    /// <summary>
    /// Recursively searches for LLM components on a GameObject and its children,
    /// calling SetModel() via reflection to properly initialize each one.
    /// </summary>
    private static void SetLLMModelOnChildren(GameObject go, string modelName)
    {
        var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
        if (llmType == null) return;

        var components = go.GetComponentsInChildren(llmType, true);
        foreach (var comp in components)
        {
            CallSetModelOnComponent(comp, llmType, modelName);
            Debug.Log($"<b>[AI System Setup]</b> ✅ LLM '{comp.name}' in scene set to: {modelName}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────────────

    private static void DrawLine()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f, 0.5f));
    }
}

/// <summary>
/// Friendly modal dialog window providing a clean green checkmark (or custom icon)
/// instead of the harsh yellow warning exclamation mark from standard OS dialogs.
/// </summary>
public class AISystemDialogWindow : EditorWindow
{
    private class DialogState
    {
        public bool Result;
    }
    private static readonly DialogState State = new DialogState();

    private string _heading;
    private string _message;
    private string _primaryBtn;
    private string _secondaryBtn;
    private string _icon;
    private Color _accentColor;

    private Vector2 _scroll;

    public static bool ShowDialog(
        string title,
        string heading,
        string message,
        string primaryBtn = "OK",
        string secondaryBtn = null,
        string icon = null,
        Color? accentColor = null,
        float width = 520f,
        float height = 285f)
    {
        try
        {
            var win = CreateInstance<AISystemDialogWindow>();
            win.titleContent = new GUIContent(title);
            win._heading = heading;
            win._message = message;
            win._primaryBtn = primaryBtn;
            win._secondaryBtn = secondaryBtn;
            win._icon = icon;
            win._accentColor = accentColor ?? (EditorGUIUtility.isProSkin 
                ? new Color(0.35f, 0.95f, 0.5f) 
                : new Color(0.1f, 0.65f, 0.2f));

            Vector2 size = new Vector2(width, height);
            win.minSize = size;
            win.maxSize = size;

            try
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                win.position = new Rect(
                    main.x + (main.width - size.x) * 0.5f,
                    main.y + (main.height - size.y) * 0.5f,
                    size.x, size.y);
            }
            catch { }

            State.Result = false;
            win.ShowModalUtility();
            return State.Result;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AI System Setup] Custom dialog fallback: {ex.Message}");
            return EditorUtility.DisplayDialog(title, message, primaryBtn, secondaryBtn ?? "");
        }
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                State.Result = true;
                Close();
                Event.current.Use();
                GUIUtility.ExitGUI();
            }
            else if (Event.current.keyCode == KeyCode.Escape)
            {
                State.Result = false;
                Close();
                Event.current.Use();
                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.Space(16);

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.Space(16);

                if (!string.IsNullOrEmpty(_icon))
                {
                    GUIStyle iconStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 36,
                        alignment = TextAnchor.MiddleCenter
                    };
                    EditorGUILayout.LabelField(_icon, iconStyle, GUILayout.Width(46), GUILayout.Height(46));
                    EditorGUILayout.Space(12);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUIStyle headingStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        normal = { textColor = _accentColor }
                    };
                    EditorGUILayout.LabelField(_heading, headingStyle);
                    EditorGUILayout.Space(6);

                    GUIStyle msgStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                    {
                        fontSize = 12
                    };
                    EditorGUILayout.LabelField(_message, msgStyle);
                }

                EditorGUILayout.Space(16);
            }
        }

        GUILayout.FlexibleSpace();

        // Divider
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f, 0.35f));
        EditorGUILayout.Space(10);

        // Buttons
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.Space(16);

            if (!string.IsNullOrEmpty(_secondaryBtn))
            {
                if (GUILayout.Button(_secondaryBtn, GUILayout.Height(28), GUILayout.MinWidth(95)))
                {
                    State.Result = false;
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.FlexibleSpace();

            GUIStyle primaryStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
            if (GUILayout.Button(_primaryBtn, primaryStyle, GUILayout.Height(28), GUILayout.MinWidth(120)))
            {
                State.Result = true;
                Close();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(16);
        }
        EditorGUILayout.Space(12);
    }
}

/// <summary>
/// Automatically ensures LLM components in imported sample prefabs and scenes
/// are configured with the downloaded GGUF model immediately upon import.
/// </summary>
public class AISystemSamplePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool hasRelevantAsset = false;
        foreach (string asset in importedAssets)
        {
            if (asset.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase) ||
                asset.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase) ||
                asset.EndsWith(".gguf", System.StringComparison.OrdinalIgnoreCase))
            {
                hasRelevantAsset = true;
                break;
            }
        }

        if (hasRelevantAsset)
        {
            EditorApplication.delayCall += () =>
            {
                AISystemSetupWindow.AutoConfigureLLM();
            };
        }
    }
}
}
