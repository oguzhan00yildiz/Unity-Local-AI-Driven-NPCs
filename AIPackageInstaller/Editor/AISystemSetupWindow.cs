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
            Group = "LLM — Qwen3.5 0.8B (Language Model)",
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
            "This package needs to install several Unity packages and download AI model files (~765 MB):\n\n" +
            "  • LLMUnity (language model runtime)\n" +
            "  • Piper TTS (text-to-speech)\n" +
            "  • Whisper Unity (speech recognition)\n" +
            "  • ONNX Runtime 0.4.4 (via NPM registry)\n\n" +
            "  • Qwen3.5 0.8B LLM model (~500 MB)\n" +
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

    // ─────────────────────────────────────────────────────────────────────────
    // Model-download phase helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions the existing window to the Model Download phase and starts
    /// downloading missing files. Called by AIPackageInstaller when packages are done.
    /// If all models are already present, closes the window silently.
    /// </summary>
    public static void AutoStartDownloads()
    {
        foreach (var m in Models) m.Refresh();

        bool anyMissing = Models.Exists(m => !m.IsDownloaded);
        if (!anyMissing)
        {
            Debug.Log("<b>[AI System Setup]</b> All model files already present. ✅");
            _instance?.Close();
            return;
        }

        // Switch phase inside the already-open window (no new window!)
        var win = EnsureWindow(WindowPhase.ModelDownload);
        win.RefreshModelStatus();

        Debug.Log("<b>[AI System Setup]</b> Auto-starting download of missing model files…");
        win.DownloadAllModels();
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
            _instance.minSize      = new Vector2(500, 460);
            _instance.ShowUtility();
        }

        _instance._phase = phase;
        if (phase == WindowPhase.ModelDownload)
            _instance.RefreshModelStatus();

        _instance.Focus();
        return _instance;
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
    // OnGUI
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
    // Header
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
        EditorGUILayout.Space(3);

        // Phase breadcrumbs
        using (new EditorGUILayout.HorizontalScope())
        {
            GUIStyle normal = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUIStyle active = new GUIStyle(normal)
            {
                fontStyle = FontStyle.Bold,
                normal    = { textColor = new Color(0.3f, 0.8f, 0.4f) }
            };

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("① Package Install",
                _phase == WindowPhase.PackageInstall ? active : normal, GUILayout.Width(130));
            EditorGUILayout.LabelField("→", normal, GUILayout.Width(20));
            EditorGUILayout.LabelField("② Model Download",
                _phase == WindowPhase.ModelDownload ? active : normal, GUILayout.Width(130));
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(6);
        DrawLine();
        EditorGUILayout.Space(6);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1 — Package Install
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawPackagePhase()
    {
        EditorGUILayout.LabelField(
            "Installing required Unity packages. Unity may reload between steps — the window will restore automatically.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        // Overall progress bar
        int completed = 0;
        foreach (var s in Steps)
            if (s.Status == StepStatus.Completed) completed++;

        float progress = Steps.Count > 0 ? (float)completed / Steps.Count : 0f;
        Rect pRect = EditorGUILayout.GetControlRect(false, 22);
        EditorGUI.ProgressBar(pRect, progress, $"Packages  {completed} / {Steps.Count}");
        EditorGUILayout.Space(10);

        foreach (var step in Steps)
            DrawPackageRow(step);

        GUILayout.FlexibleSpace();
        DrawLine();
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Keep Unity open. This window will automatically advance to model downloads when done.",
            EditorStyles.centeredGreyMiniLabel);
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
        EditorGUILayout.HelpBox(
            "The LLM language model (Qwen3.5 0.8B) will be downloaded and\n" +
            "automatically configured on the LLM prefab. Just hit Play when done!",
            MessageType.Info);
        EditorGUILayout.Space(4);

        bool anyMissing = Models.Exists(m => !m.IsDownloaded && !m.IsDownloading);
        using (new EditorGUI.DisabledGroupScope(_activeDownloads > 0 || !anyMissing))
        {
            string label = _activeDownloads > 0
                ? $"Downloading… ({_activeDownloads} active)"
                : "⬇  Download All Missing Models";
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
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Status"))
                RefreshModelStatus();
            if (GUILayout.Button("← Package Log"))
                _phase = WindowPhase.PackageInstall;
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

            string temp = model.FullPath + ".tmp";

            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (_, e) =>
                    model.Progress = e.ProgressPercentage / 100f;

                await client.DownloadFileTaskAsync(new System.Uri(model.Url), temp);
            }

            if (File.Exists(model.FullPath)) File.Delete(model.FullPath);
            File.Move(temp, model.FullPath);

            model.IsDownloaded = true;

            // Register LLM (.gguf) models with LLMUnity's model manager (via reflection
            // to avoid hard dependency — LLMUnity package may still be installing)
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

            string temp = model.FullPath + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
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
            }
        }
    }

    /// <summary>
    /// After all models are downloaded, find and configure the LLM component
    /// in the LLM.prefab asset and in any open scenes to use the downloaded model.
    /// Uses reflection to avoid hard dependency on LLMUnity types (which may
    /// still be installing when this script first compiles).
    /// </summary>
    private static void AutoConfigureLLM()
    {
        ModelEntry llmModel = Models.Find(m =>
            Path.GetExtension(m.DestRelPath).ToLower() == ".gguf" && m.IsDownloaded);

        if (llmModel == null) return;

        string modelFilename = Path.GetFileName(llmModel.DestRelPath);

        try
        {
            // Ensure the model is registered with LLMUnity's model manager
            string registeredName = RegisterWithLLMManager(llmModel.FullPath, llmModel.DisplayName);

            // ── 1. Update LLM.prefab asset ──────────────────────────────────
            string prefabAssetPath = "Assets/Prefabs/LLM.prefab";
            GameObject llmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (llmPrefab != null)
            {
                SetLLMModelOnGameObject(llmPrefab, registeredName, "LLM.prefab");
            }

            // ── 2. Update any LLM components in currently open scenes ───────
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
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"<b>[AI System Setup]</b> LLM auto-configuration skipped " +
                $"(LLMUnity may still be installing): {ex.Message}");
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
    /// Calls LLM.SetModel(modelName) via reflection on a specific GameObject.
    /// This properly reads GGUF metadata, sets context size, and initializes the model
    /// — unlike directly setting _model via SerializedProperty.
    /// </summary>
    private static void SetLLMModelOnGameObject(GameObject go, string modelName, string contextName)
    {
        var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
        if (llmType == null) return;

        var llmComponent = go.GetComponent(llmType);
        if (llmComponent == null) return;

        // Read current model via the "model" property (getter)
        var modelProp = llmType.GetProperty("model",
            BindingFlags.Public | BindingFlags.Instance);
        if (modelProp == null) return;

        string currentModel = modelProp.GetValue(llmComponent) as string ?? "";
        if (currentModel == modelName) return;

        // Call SetModel() which handles GGUF metadata reading, context size, etc.
        var setModelMethod = llmType.GetMethod("SetModel",
            BindingFlags.Public | BindingFlags.Instance);
        if (setModelMethod != null)
        {
            setModelMethod.Invoke(llmComponent, new object[] { modelName });
            EditorUtility.SetDirty(llmComponent);
            Debug.Log($"<b>[AI System Setup]</b> ✅ {contextName} model set to: {modelName}");
        }
        else
        {
            // Fallback: set via SerializedProperty (less ideal but works if SetModel unavailable)
            SerializedObject so = new SerializedObject(llmComponent);
            SerializedProperty sp = so.FindProperty("_model");
            if (sp != null)
            {
                sp.stringValue = modelName;
                so.ApplyModifiedProperties();
                Debug.Log($"<b>[AI System Setup]</b> ✅ {contextName} model set to: {modelName} (fallback)");
            }
        }
    }

    /// <summary>
    /// Recursively searches for LLM components on a GameObject and its children,
    /// calling SetModel() via reflection to properly initialize each one.
    /// </summary>
    private static void SetLLMModelOnChildren(GameObject go, string modelName)
    {
        var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
        if (llmType == null) return;

        var setModelMethod = llmType.GetMethod("SetModel",
            BindingFlags.Public | BindingFlags.Instance);
        var modelProp = llmType.GetProperty("model",
            BindingFlags.Public | BindingFlags.Instance);

        var components = go.GetComponentsInChildren(llmType, true);
        foreach (var comp in components)
        {
            string currentModel = modelProp?.GetValue(comp) as string ?? "";
            if (currentModel == modelName) continue;

            if (setModelMethod != null)
            {
                setModelMethod.Invoke(comp, new object[] { modelName });
                EditorUtility.SetDirty(comp);
            }
            else
            {
                SerializedObject so = new SerializedObject(comp);
                SerializedProperty sp = so.FindProperty("_model");
                if (sp != null && sp.stringValue != modelName)
                {
                    sp.stringValue = modelName;
                    so.ApplyModifiedProperties();
                }
            }

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
