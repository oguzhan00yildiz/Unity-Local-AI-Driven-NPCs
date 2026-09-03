using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

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
        public bool   IsOptionalLlm;

        public bool   IsDownloaded;
        public bool   IsDownloading;
        public float  Progress;
        public string Error;

        public string FullPath => Path.Combine(
            Application.streamingAssetsPath,
            DestRelPath.Replace('/', Path.DirectorySeparatorChar));

        public void Refresh()
        {
            if (IsOptionalLlm)
            {
                if (File.Exists(FullPath))
                {
                    var fi = new FileInfo(FullPath);
                    IsDownloaded = fi.Length >= (long)(SizeMB * 0.5f * 1024 * 1024);
                }
                else
                {
                    // If another valid LLM model is already available in the project/LLMUnity, consider LLM requirement satisfied
                    IsDownloaded = HasAnyLLMModelInstalled();
                }
                return;
            }

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
                        if (new FileInfo(path).Length > 10 * 1024 * 1024)
                            return true;
                    }
                    catch { }
                }
            }

            // 2. Check entire Assets folder for any .gguf file
            if (Directory.Exists(Application.dataPath))
            {
                var assetGgufs = Directory.GetFiles(Application.dataPath, "*.gguf", SearchOption.AllDirectories);
                foreach (var path in assetGgufs)
                {
                    try
                    {
                        if (new FileInfo(path).Length > 10 * 1024 * 1024)
                            return true;
                    }
                    catch { }
                }
            }

            // 3. Check LLMUnity LLMManager registered models via reflection
            var llmManagerType = System.Type.GetType("LLMUnity.LLMManager, undream.llmunity.Runtime");
            if (llmManagerType != null)
            {
                var modelEntriesField = llmManagerType.GetField("modelEntries", BindingFlags.Public | BindingFlags.Static);
                if (modelEntriesField != null)
                {
                    var entries = modelEntriesField.GetValue(null) as System.Collections.IList;
                    if (entries != null && entries.Count > 0)
                    {
                        return true;
                    }
                }
            }

            // 4. Check LLM components in open scenes / prefabs
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
                            return true;
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
            Group = "LLM — Qwen3.5 0.8B (Language Model - Optional)",
            DisplayName = "Qwen3.5-0.8B-Q4_K_M.gguf",
            Url = "https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF/resolve/main/Qwen3.5-0.8B-Q4_K_M.gguf",
            DestRelPath = "Qwen3.5-0.8B-Q4_K_M.gguf",
            SizeMB = 500,
            IsOptionalLlm = true
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Window state
    // ─────────────────────────────────────────────────────────────────────────
    private enum WindowPhase { PackageInstall, ModelDownload }

    // Only ever one instance — never use GetWindow more than once.
    private static AISystemSetupWindow _instance;

    // Phase 1
    public static List<InstallStep> Steps { get; } = new List<InstallStep>();

    // Phase 2
    private WindowPhase _phase = WindowPhase.PackageInstall;
    private Vector2     _modelScroll;
    private int         _activeDownloads;

    // ─────────────────────────────────────────────────────────────────────────
    // Static API — called by AIPackageInstaller
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/AI Packages/AI System Setup")]
    public static void ShowWindow() => EnsureWindow(WindowPhase.PackageInstall);

    [MenuItem("Tools/AI Packages/Download Model Files")]
    public static void ShowModelDownloaderWindow() => EnsureWindow(WindowPhase.ModelDownload);

    /// <summary>
    /// Shows a yes/no consent dialog on first package import.
    /// Returns true if the user agreed to proceed.
    /// </summary>
    public static bool ShowConsentDialog()
    {
        return EditorUtility.DisplayDialog(
            "AI Driven NPCs — Setup Required",
            "This package needs to install several Unity packages and download required voice model files (~265 MB):\n\n" +
            "  • LLMUnity (language model runtime)\n" +
            "  • Piper TTS (text-to-speech)\n" +
            "  • Whisper Unity (speech recognition)\n" +
            "  • ONNX Runtime 0.4.4 (via NPM registry)\n\n" +
            "Note: The default LLM model (Qwen3.5 0.8B, ~500 MB) is optional and you will be prompted before downloading.\n\n" +
            "A setup window will open to track progress.\n" +
            "You can also run this later via Tools → AI Packages → AI System Setup.",
            "Yes, Set Up Now",
            "Skip for Now");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Package-install phase helpers
    // ─────────────────────────────────────────────────────────────────────────

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
            step.Status       = status;
            step.ErrorMessage = error;
        }
        else
        {
            Steps.Add(new InstallStep { Name = name, Status = status, ErrorMessage = error });
        }
        _instance?.Repaint();
    }

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

        bool onnxInstalled = CheckScopedRegistryAndOnnx();
        bool llmInstalled = CheckPackageInManifest("ai.undream.llm") ||
                            System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime") != null;
        bool piperInstalled = CheckPackageInManifest("ai.lookbe.piper") ||
                              System.Type.GetType("Piper.PiperManager, ai.lookbe.piper") != null;
        bool whisperInstalled = CheckPackageInManifest("com.whisper.unity") ||
                                System.Type.GetType("Whisper.WhisperManager, com.whisper.unity") != null;

        Steps.Clear();

        Steps.Add(new InstallStep
        {
            Name = "Scoped Registry & ONNX",
            Description = onnxInstalled ? "NPM scoped registry and ONNX Runtime 0.4.4 installed" : "Configuring registry.npmjs.org and ONNX 0.4.4",
            Status = onnxInstalled ? StepStatus.Completed : StepStatus.Pending
        });

        Steps.Add(new InstallStep
        {
            Name = "LLMUnity Package",
            Description = llmInstalled ? "LLM inference engine installed and active" : "Installing LLMUnity package from Git",
            Status = llmInstalled ? StepStatus.Completed : StepStatus.Pending
        });

        Steps.Add(new InstallStep
        {
            Name = "Piper TTS Package",
            Description = piperInstalled ? "Local Piper text-to-speech runtime installed" : "Installing Piper TTS package from Git",
            Status = piperInstalled ? StepStatus.Completed : StepStatus.Pending
        });

        Steps.Add(new InstallStep
        {
            Name = "Whisper Unity Package",
            Description = whisperInstalled ? "Local Whisper speech recognition runtime installed" : "Installing Whisper Unity package from Git",
            Status = whisperInstalled ? StepStatus.Completed : StepStatus.Pending
        });

        _instance?.Repaint();
    }

    private static bool CheckScopedRegistryAndOnnx()
    {
        try
        {
            string manifestPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                string text = File.ReadAllText(manifestPath);
                bool hasRegistry = text.Contains("registry.npmjs.org") && text.Contains("com.github.asus4");
                bool hasOnnx = text.Contains("com.github.asus4.onnxruntime");
                if (hasRegistry && hasOnnx) return true;
            }
        }
        catch { }

        return CheckPackageInManifest("com.github.asus4.onnxruntime");
    }

    private static bool CheckPackageInManifest(string packageId)
    {
        try
        {
            string manifestPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                string text = File.ReadAllText(manifestPath);
                if (text.Contains($"\"{packageId}\""))
                    return true;

                if (packageId == "ai.undream.llm" && (text.Contains("LLMUnity.git") || text.Contains("undreamai/LLMUnity")))
                    return true;
                if (packageId == "ai.lookbe.piper" && (text.Contains("piper-no-espeak-unity.git") || text.Contains("lookbe/piper")))
                    return true;
                if (packageId == "com.whisper.unity" && (text.Contains("whisper.unity.git") || text.Contains("Macoron/whisper.unity")))
                    return true;
            }
        }
        catch { }

        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string packagesDir = Path.Combine(projectRoot, "Packages");
            if (Directory.Exists(Path.Combine(packagesDir, packageId)))
                return true;

            string cacheDir = Path.Combine(projectRoot, "Library", "PackageCache");
            if (Directory.Exists(cacheDir))
            {
                var dirs = Directory.GetDirectories(cacheDir, packageId + "@*");
                if (dirs.Length > 0) return true;
            }
        }
        catch { }

        return false;
    }

    private void SetPhase(WindowPhase newPhase)
    {
        _phase = newPhase;
        if (_phase == WindowPhase.ModelDownload)
            RefreshModelStatus();
        else
            RefreshPackageSteps();
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Model-download phase helpers
    // ─────────────────────────────────────────────────────────────────────────

    private const string SetupCompleteNotifiedKey = "AISystemSetupWindow.SetupCompleteNotified";

    /// <summary>
    /// Checks if sample scene AIOTest exists in the project.
    /// </summary>
    public static string FindSampleScenePath()
    {
        string[] guids = AssetDatabase.FindAssets("AIOTest t:Scene");
        if (guids != null && guids.Length > 0)
        {
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        return null;
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
            Debug.LogWarning("[AI System Setup] AIOTest scene not found. Opening Package Manager to import samples…");
            OpenPackageManager();
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
        if (AreAllPackagesInstalled() && AreAllModelsDownloaded())
        {
            bool alreadyNotified = SessionState.GetBool(SetupCompleteNotifiedKey, false);
            if (!alreadyNotified)
            {
                SessionState.SetBool(SetupCompleteNotifiedKey, true);

                string scenePath = FindSampleScenePath();
                bool hasSampleScene = !string.IsNullOrEmpty(scenePath);

                string message = hasSampleScene
                    ? "All required packages and models are downloaded, and the sample scene is imported!\n\n" +
                      "Would you like to open the demo scene (AIOTest) to test it right away?"
                    : "All required packages and models are downloaded and ready!\n\n" +
                      "You can now go to Package Manager to import the samples and open the demo scene to test it right away.";

                string primaryBtn = hasSampleScene ? "Open Demo Scene" : "Open Package Manager";

                bool choosePrimary = EditorUtility.DisplayDialog(
                    "AI Driven NPCs — Setup Complete! 🎉",
                    message,
                    primaryBtn,
                    "Got It");

                if (choosePrimary)
                {
                    if (hasSampleScene)
                        OpenSampleScene();
                    else
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
        foreach (var m in Models) m.Refresh();

        bool anyMissing = Models.Exists(m => !m.IsDownloaded);
        if (!anyMissing)
        {
            Debug.Log("<b>[AI System Setup]</b> All model files already present. ✅");
            var win = EnsureWindow(WindowPhase.ModelDownload);
            win.RefreshModelStatus();
            EditorApplication.delayCall += () => CheckAndNotifyCompletion();
            return;
        }

        // Switch phase inside the already-open window (no new window!)
        var existingWin = EnsureWindow(WindowPhase.ModelDownload);
        existingWin.RefreshModelStatus();

        Debug.Log("<b>[AI System Setup]</b> Starting download of missing model files…");
        existingWin.DownloadAllModels(isAutomatic: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the one setup window, creating it if needed. Never creates a second one.
    /// </summary>
    private static AISystemSetupWindow EnsureWindow(WindowPhase phase)
    {
        if (_instance == null)
        {
            // CreateInstance + ShowUtility keeps the window floating and prevents
            // it from being merged with other dockable windows (no duplicate docking).
            _instance = CreateInstance<AISystemSetupWindow>();
            _instance.titleContent = new GUIContent("AI System Setup");
            _instance.minSize      = new Vector2(560, 500);
            _instance.ShowUtility();
        }

        _instance._phase = phase;
        RefreshPackageSteps();
        if (phase == WindowPhase.ModelDownload)
            _instance.RefreshModelStatus();

        _instance.Focus();

        EditorApplication.delayCall += () =>
        {
            CheckAndNotifyCompletion();
        };

        return _instance;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        _instance = this;
        EditorApplication.update += ForceRepaint;
        RefreshPackageSteps();
        RefreshModelStatus();
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
    // OnGUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        DrawHeader();
        DrawSetupCompleteBanner();

        if (_phase == WindowPhase.PackageInstall)
            DrawPackagePhase();
        else
            DrawModelPhase();
    }

    private void DrawSetupCompleteBanner()
    {
        if (!AreAllPackagesInstalled() || !AreAllModelsDownloaded())
            return;

        string scenePath = FindSampleScenePath();
        bool hasSampleScene = !string.IsNullOrEmpty(scenePath);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.Space(3);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.95f, 0.5f) : new Color(0.1f, 0.6f, 0.2f) }
            };
            EditorGUILayout.LabelField("🎉 Everything is Downloaded & Ready!", headerStyle);
            EditorGUILayout.Space(2);

            string infoText = hasSampleScene
                ? "All required packages and models are downloaded, and samples are imported! You can open the demo scene (AIOTest) to test right away."
                : "All required packages and models are downloaded! Go to Package Manager to import the samples and open the demo scene to test.";

            EditorGUILayout.LabelField(infoText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("📦 Open Package Manager", GUILayout.Height(26)))
                {
                    OpenPackageManager();
                }

                if (hasSampleScene)
                {
                    GUIStyle playBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                    if (GUILayout.Button("▶ Open Demo Scene (AIOTest)", playBtnStyle, GUILayout.Height(26)))
                    {
                        OpenSampleScene();
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

    private static GUIStyle _activeLeftTabStyle;
    private static GUIStyle _activeRightTabStyle;

    private static GUIStyle GetActiveTabStyle(bool isLeft)
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

    private void DrawHeader()
    {
        EditorGUILayout.Space(10);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("AI Driven NPCs — System Setup", titleStyle);
        EditorGUILayout.Space(8);

        // Phase navigation bar with clickable breadcrumb tabs
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // Step 1 tab
            bool pkgsDone = AreAllPackagesInstalled();
            string step1Title = pkgsDone ? "① Package Install  ✓" : "① Package Install";
            GUIStyle step1Style = _phase == WindowPhase.PackageInstall 
                ? GetActiveTabStyle(true) 
                : EditorStyles.miniButtonLeft;

            if (GUILayout.Button(step1Title, step1Style, GUILayout.Width(170), GUILayout.Height(24)))
            {
                SetPhase(WindowPhase.PackageInstall);
            }

            // Step 2 tab
            bool modelsDone = AreAllModelsDownloaded();
            string step2Title = modelsDone ? "② Model Download  ✓" : "② Model Download";
            GUIStyle step2Style = _phase == WindowPhase.ModelDownload 
                ? GetActiveTabStyle(false) 
                : EditorStyles.miniButtonRight;

            if (GUILayout.Button(step2Title, step2Style, GUILayout.Width(170), GUILayout.Height(24)))
            {
                SetPhase(WindowPhase.ModelDownload);
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(8);
        DrawLine();
        EditorGUILayout.Space(8);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1 — Package Install
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawPackagePhase()
    {
        if (Steps.Count == 0)
        {
            RefreshPackageSteps();
        }

        int completed = 0;
        foreach (var s in Steps)
            if (s.Status == StepStatus.Completed) completed++;

        bool allCompleted = Steps.Count > 0 && completed == Steps.Count;

        if (allCompleted)
        {
            EditorGUILayout.HelpBox(
                "All required Unity packages are installed and up to date! ✅\nClick 'Next: Model Downloads ▶' to view or download voice and language models.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField(
                "Installing required Unity packages. Unity may reload between steps — the window will restore automatically.",
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.Space(8);

        // Overall progress bar
        float progress = Steps.Count > 0 ? (float)completed / Steps.Count : 0f;
        Rect pRect = EditorGUILayout.GetControlRect(false, 22);
        string progressText = allCompleted
            ? $"Packages  {completed} / {Steps.Count}  (All Complete ✅)"
            : $"Packages  {completed} / {Steps.Count}";
        EditorGUI.ProgressBar(pRect, progress, progressText);
        EditorGUILayout.Space(10);

        foreach (var step in Steps)
            DrawPackageRow(step);

        GUILayout.FlexibleSpace();
        DrawLine();
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("↻ Re-check Status", GUILayout.Height(26), GUILayout.Width(130)))
            {
                RefreshPackageSteps(force: true);
            }

            if (GUILayout.Button("Install / Repair Packages", GUILayout.Height(26), GUILayout.Width(170)))
            {
                AIPackageInstaller.ForceInstall();
            }

            GUILayout.FlexibleSpace();

            GUIStyle nextBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Next: Model Downloads ▶", nextBtnStyle, GUILayout.Height(26), GUILayout.Width(190)))
            {
                SetPhase(WindowPhase.ModelDownload);
            }
        }
        EditorGUILayout.Space(6);
    }

    private void DrawPackageRow(InstallStep step)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            // Icon
            string icon;
            switch (step.Status)
            {
                case StepStatus.InProgress:
                    int dots = (int)(EditorApplication.timeSinceStartup * 3) % 4;
                    icon = "⏳" + new string('.', dots);
                    break;
                case StepStatus.Completed: icon = "✅"; break;
                case StepStatus.Failed:    icon = "❌"; break;
                default:                   icon = "⚪"; break;
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
        bool allModelsDone = AreAllModelsDownloaded();

        if (allModelsDone)
        {
            EditorGUILayout.HelpBox(
                "All required model files are downloaded and ready! ✅\nYour local AI NPC system is fully configured and ready to use.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Whisper (Speech Recognition) and Piper (Text-to-Speech) models are downloaded for voice.\n" +
                "The LLM model (Qwen3.5 0.8B, ~500 MB) is optional — you can download it here or assign your own custom .gguf model in LLMUnity.",
                MessageType.Info);
        }
        EditorGUILayout.Space(4);

        bool anyMissing = Models.Exists(m => !m.IsDownloaded && !m.IsDownloading);
        using (new EditorGUI.DisabledGroupScope(_activeDownloads > 0 || !anyMissing))
        {
            string label = _activeDownloads > 0
                ? $"Downloading… ({_activeDownloads} active)"
                : (allModelsDone ? "✅  All Models Downloaded" : "⬇  Download All Missing Models");
            if (GUILayout.Button(label, GUILayout.Height(32)))
                DownloadAllModels();
        }
        EditorGUILayout.Space(6);

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
            DrawModelRow(m);
        }

        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        DrawLine();
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("◀ Back: Package Install", GUILayout.Height(26), GUILayout.Width(170)))
                SetPhase(WindowPhase.PackageInstall);

            if (GUILayout.Button("↻ Refresh Status", GUILayout.Height(26), GUILayout.Width(120)))
                RefreshModelStatus();

            GUILayout.FlexibleSpace();

            if (allModelsDone)
            {
                if (GUILayout.Button("Done / Close", GUILayout.Height(26), GUILayout.Width(110)))
                    Close();
            }
        }
        EditorGUILayout.Space(6);

        // Keep repainting while downloading
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
                string statusLabel = (model.IsOptionalLlm && !File.Exists(model.FullPath)) ? "LLM Ready" : "Ready";
                EditorGUILayout.LabelField(statusLabel, EditorStyles.miniLabel, GUILayout.Width(80));
                if (File.Exists(model.FullPath))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(55)))
                    {
                        File.Delete(model.FullPath);
                        model.Refresh();
                        AssetDatabase.Refresh();
                    }
                }
                else if (model.IsOptionalLlm)
                {
                    if (GUILayout.Button("Download", GUILayout.Width(80)))
                        _ = DownloadModel(model);
                }
            }
            else
            {
                if (GUILayout.Button("Download", GUILayout.Width(80)))
                    _ = DownloadModel(model);
            }
        }
    }

    private const string LlmPromptSessionKey = "AISystemSetupWindow.LlmPromptAnswered";
    private static bool _isDownloadingAll;

    private async void DownloadAllModels(bool isAutomatic = false)
    {
        if (_isDownloadingAll) return;
        _isDownloadingAll = true;

        try
        {
            bool hasLlmInstalled = HasAnyLLMModelInstalled();
            bool alreadyPrompted = SessionState.GetBool(LlmPromptSessionKey, false);
            bool downloadOptionalLlm = false;

            var missingLlm = Models.Find(m => m.IsOptionalLlm && !m.IsDownloaded && !m.IsDownloading);

            // Only prompt if NO LLM is installed at all, and (if automatic) hasn't been prompted already this session
            if (missingLlm != null && !hasLlmInstalled)
            {
                if (!isAutomatic || !alreadyPrompted)
                {
                    SessionState.SetBool(LlmPromptSessionKey, true);
                    downloadOptionalLlm = EditorUtility.DisplayDialog(
                        "Download LLM Language Model?",
                        $"Would you like to download the default LLM model ({missingLlm.DisplayName}, ~{missingLlm.SizeMB} MB)?\n\n" +
                        $"• Download: Downloads {missingLlm.DisplayName} (~{missingLlm.SizeMB} MB) to StreamingAssets and configures it automatically for LLMUnity.\n\n" +
                        "• Skip: Do not download. You can download it later from this window or use your own custom GGUF model in LLMUnity.",
                        $"Download ({missingLlm.SizeMB} MB)",
                        "Skip LLM Download");
                }
            }

            foreach (var m in Models)
            {
                m.Refresh();
                if (!m.IsDownloaded && !m.IsDownloading)
                {
                    if (m.IsOptionalLlm && !downloadOptionalLlm)
                    {
                        continue;
                    }
                    await DownloadModel(m);
                }
            }
        }
        finally
        {
            _isDownloadingAll = false;
        }
    }

    private async Task DownloadModel(ModelEntry model)
    {
        if (model.IsDownloading) return;
        model.Refresh();
        if (model.IsDownloaded) return;

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

            bool downloadedWithCurl = false;

            // 1. Try native high-speed curl first (bypasses Mono single-thread TLS)
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "curl.exe",
                    Arguments = $"-L --fail --retry 3 -s -S -o \"{model.FullPath}\" \"{model.Url}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    if (process != null)
                    {
                        long targetBytes = (long)model.SizeMB * 1024 * 1024;
                        while (!process.HasExited)
                        {
                            await Task.Delay(150);
                            if (File.Exists(model.FullPath) && targetBytes > 0)
                            {
                                try
                                {
                                    long curBytes = new FileInfo(model.FullPath).Length;
                                    model.Progress = Mathf.Clamp01((float)curBytes / targetBytes);
                                    Repaint();
                                }
                                catch { }
                            }
                        }

                        await Task.Run(() => process.WaitForExit());

                        if (process.ExitCode == 0 && File.Exists(model.FullPath))
                        {
                            downloadedWithCurl = true;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                downloadedWithCurl = false;
            }

            // 2. Fallback to WebClient if curl wasn't available
            if (!downloadedWithCurl)
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.DownloadProgressChanged += (_, e) =>
                    {
                        model.Progress = e.ProgressPercentage / 100f;
                        Repaint();
                    };
                    await client.DownloadFileTaskAsync(new System.Uri(model.Url), model.FullPath);
                }
            }

            // 3. Verify downloaded file size
            var fi = new FileInfo(model.FullPath);
            if (model.SizeMB > 1 && fi.Length < (long)(model.SizeMB * 0.5f * 1024 * 1024))
            {
                throw new System.Exception($"Downloaded file size ({fi.Length / (1024 * 1024)}MB) was smaller than expected ({model.SizeMB}MB).");
            }

            model.IsDownloaded = true;
            model.Progress = 1f;

            if (Path.GetExtension(model.FullPath).ToLower() == ".gguf")
            {
                RegisterWithLLMManager(model.FullPath, model.DisplayName);
            }

            Debug.Log($"<b>[AI System Setup]</b> ✅ Downloaded: {model.DestRelPath}");
        }
        catch (System.Exception ex)
        {
            model.Error = "Failed";
            Debug.LogError($"<b>[AI System Setup]</b> ❌ {model.DisplayName}: {ex.Message}");

            try
            {
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
            _activeDownloads--;

            if (_activeDownloads == 0)
            {
                AssetDatabase.Refresh();
                AutoConfigureLLM();
                Repaint();
                Debug.Log("<b>[AI System Setup]</b> All downloads complete. ✅");
                EditorApplication.delayCall += () => CheckAndNotifyCompletion();
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
        string registeredName = null;

        ModelEntry llmModel = Models.Find(m =>
            Path.GetExtension(m.DestRelPath).ToLower() == ".gguf" && m.IsDownloaded);

        if (llmModel != null && File.Exists(llmModel.FullPath))
        {
            registeredName = RegisterWithLLMManager(llmModel.FullPath, llmModel.DisplayName);
        }
        else
        {
            // Fallback: search StreamingAssets or Assets for any available .gguf file
            string streamingDir = Application.streamingAssetsPath;
            if (Directory.Exists(streamingDir))
            {
                var streamingGgufs = Directory.GetFiles(streamingDir, "*.gguf", SearchOption.AllDirectories);
                if (streamingGgufs.Length > 0)
                {
                    registeredName = RegisterWithLLMManager(streamingGgufs[0], Path.GetFileName(streamingGgufs[0]));
                }
            }

            if (string.IsNullOrEmpty(registeredName) && Directory.Exists(Application.dataPath))
            {
                var assetGgufs = Directory.GetFiles(Application.dataPath, "*.gguf", SearchOption.AllDirectories);
                if (assetGgufs.Length > 0)
                {
                    registeredName = RegisterWithLLMManager(assetGgufs[0], Path.GetFileName(assetGgufs[0]));
                }
            }
        }

        if (string.IsNullOrEmpty(registeredName)) return;

        try
        {
            // Search ALL prefab assets in the project for LLM components
            var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
            int prefabsUpdated = 0;

            if (llmType != null)
            {
                var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                foreach (string guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
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
                if (!scene.isLoaded) continue;

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
