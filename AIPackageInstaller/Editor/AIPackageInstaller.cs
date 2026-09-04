using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AISystem.Editor
{
    /// <summary>
    /// Runs automatically when the package is first imported.
    /// Phase 1: Edits manifest.json to add the npm scoped registry and ONNX 0.4.4 packages
    ///          (git-URL ONNX lacks LFS DLLs → native crash; npm has real binaries).
    ///          Calls Client.Resolve() which triggers a domain reload.
    /// Phase 2: After reload, installs remaining git-URL packages one by one.
    /// </summary>
    [InitializeOnLoad]
    public class AIPackageInstaller
    {
    // ── NPM packages (need scoped registry) ──────────────────────────────────
    // Key = package id, Value = version
    private static readonly Dictionary<string, string> NpmPackages = new Dictionary<string, string>
    {
        { "com.github.asus4.onnxruntime",       "0.4.4" },
        { "com.github.asus4.onnxruntime.unity",  "0.4.4" }
    };

    // ── Git-URL / Registry packages ───────────────────────────────────────────
    private static readonly string[] GitPackages = new[]
    {
        "https://github.com/undreamai/LLMUnity.git",
        "https://github.com/lookbe/piper-no-espeak-unity.git",
        "https://github.com/Macoron/whisper.unity.git?path=Packages/com.whisper.unity"
    };

    // Known package ids for the git packages above (for installed-check)
    private static readonly string[] GitPackageIds = new[]
    {
        "ai.undream.llm",
        "ai.lookbe.piper",
        "com.whisper.unity"
    };

    private static readonly string ManifestPath = Path.Combine(
        Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");

    private static Queue<string> _gitQueue  = new Queue<string>();
    private static Queue<string> _gitNames  = new Queue<string>();
    private static string        _currentPkgName;
    private static AddRequest    _addRequest;
    private static ListRequest   _listRequest;
    private static double        _closeProjectSettingsUntil;

    // ─────────────────────────────────────────────────────────────────────────
    static AIPackageInstaller()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        if (SessionState.GetBool("AIPackageInstaller.CloseProjectSettings", false))
        {
            SessionState.SetBool("AIPackageInstaller.CloseProjectSettings", false);
            StartProjectSettingsCloser(5f);
        }

        EditorApplication.delayCall += Initialize;
    }

    [MenuItem("Tools/AI Packages/Force Install Dependencies")]
    public static void ForceInstall()
    {
        Debug.Log("<b>[AI Package Installer]</b> Force install triggered.");
        SessionState.SetBool("AIPackageInstaller.Done", false);
        SessionState.SetBool("AIPackageInstaller.ConsentGiven", true); // user explicitly triggered — skip dialog
        string donePrefKey = $"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}";
        EditorPrefs.DeleteKey(donePrefKey);

        InitUI();
        AISystemSetupWindow.ShowWindow();

        bool manifestChanged = PatchManifest(out bool registryAdded);
        if (registryAdded)
        {
            SessionState.SetBool("AIPackageInstaller.CloseProjectSettings", true);
            StartProjectSettingsCloser(6f);
            ShowNpmRegistryExplanationDialog();
            CloseProjectSettingsWindow();
        }

        if (manifestChanged)
        {
            Debug.Log("<b>[AI Package Installer]</b> manifest.json updated → resolving packages (Unity will reload).");
            AISystemSetupWindow.UpdatePackageStep("Scoped Registry & ONNX", AISystemSetupWindow.StepStatus.Completed);
            Client.Resolve();
            return;
        }

        AISystemSetupWindow.UpdatePackageStep("Scoped Registry & ONNX", AISystemSetupWindow.StepStatus.Completed);
        EnqueueAndInstallGitPackages(checkFirst: false);
    }

    private static void InitUI()
    {
        List<AISystemSetupWindow.InstallStep> steps = new List<AISystemSetupWindow.InstallStep>
        {
            new AISystemSetupWindow.InstallStep { Name = "Scoped Registry & ONNX", Description = "Configuring registry.npmjs.org and ONNX 0.4.4" },
            new AISystemSetupWindow.InstallStep { Name = "LLMUnity Package", Description = "Installing LLMUnity package from Git (keep Unity focused during setup)" },
            new AISystemSetupWindow.InstallStep { Name = "Whisper Unity Package", Description = "Installing Whisper Unity package from Git" }
        };
        AISystemSetupWindow.InitPackageSteps(steps);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PHASE 1 — ensure npm registry + ONNX packages in manifest.json
    // ─────────────────────────────────────────────────────────────────────────
    private static void Initialize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        // Already fully installed this session — do not re-run or open windows.
        if (SessionState.GetBool("AIPackageInstaller.Done", false))
        {
            return;
        }

        string donePrefKey = $"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}";
        if (EditorPrefs.GetBool(donePrefKey, false))
        {
            return;
        }

        // User already answered the consent dialog this session — skip it.
        bool consentGiven = SessionState.GetBool("AIPackageInstaller.ConsentGiven", false);
        if (!consentGiven)
        {
            bool proceed = AISystemSetupWindow.ShowConsentDialog();
            if (!proceed)
            {
                Debug.Log("<b>[AI Package Installer]</b> Setup skipped by user. Run Tools → AI Packages → AI System Setup to install later.");
                return;
            }
            SessionState.SetBool("AIPackageInstaller.ConsentGiven", true);
        }

        Debug.Log("<b>[AI Package Installer]</b> Initializing…");
        InitUI();

        bool manifestChanged = PatchManifest(out bool registryAdded);
        if (registryAdded)
        {
            SessionState.SetBool("AIPackageInstaller.CloseProjectSettings", true);
            StartProjectSettingsCloser(6f);
            ShowNpmRegistryExplanationDialog();
            CloseProjectSettingsWindow();
        }

        if (manifestChanged)
        {
            AISystemSetupWindow.ShowWindow();
            AISystemSetupWindow.UpdatePackageStep("Scoped Registry & ONNX", AISystemSetupWindow.StepStatus.InProgress);
            Debug.Log("<b>[AI Package Installer]</b> manifest.json patched (npm ONNX registry added). " +
                      "Unity will reload — git packages will be installed afterwards automatically.");
            Client.Resolve();
            return;
        }

        AISystemSetupWindow.UpdatePackageStep("Scoped Registry & ONNX", AISystemSetupWindow.StepStatus.Completed);
        Debug.Log("<b>[AI Package Installer]</b> npm packages already present. Checking git packages…");
        EnqueueAndInstallGitPackages(checkFirst: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Displays a popup informing the user about the npm scoped registry configuration
    /// and why it is required for neural speech/voice inference.
    /// </summary>
    private static void ShowNpmRegistryExplanationDialog()
    {
        EditorUtility.DisplayDialog(
            "AI Driven NPCs — NPM Registry Added",
            "An NPM scoped registry (registry.npmjs.org) was added to your project's Packages/manifest.json.\n\n" +
            "• What was done:\n" +
            "Configured the scoped registry for 'com.github.asus4' and added ONNX Runtime 0.4.4 dependencies.\n\n" +
            "• Why we did that:\n" +
            "Piper TTS and Whisper Unity require ONNX Runtime to execute local voice synthesis and speech recognition models. These packages are distributed via NPM, so Unity needs this scoped registry to resolve and download them automatically.\n\n" +
            "The Project Settings window opened by Unity will be automatically closed.",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Project Settings window closer
    // Unity automatically opens the Project Settings window whenever a scoped registry
    // is added to manifest.json. We monitor and close it to keep focus on setup.
    // ─────────────────────────────────────────────────────────────────────────

    public static void StartProjectSettingsCloser(float duration = 6f)
    {
        _closeProjectSettingsUntil = EditorApplication.timeSinceStartup + duration;
        CloseProjectSettingsWindow();
        EditorApplication.update -= CloseProjectSettingsUpdate;
        EditorApplication.update += CloseProjectSettingsUpdate;
    }

    private static void CloseProjectSettingsUpdate()
    {
        CloseProjectSettingsWindow();
        if (EditorApplication.timeSinceStartup > _closeProjectSettingsUntil)
        {
            EditorApplication.update -= CloseProjectSettingsUpdate;
        }
    }

    public static void CloseProjectSettingsWindow()
    {
        try
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                if (w == null) continue;
                string typeName = w.GetType().Name;
                string title = w.titleContent != null ? w.titleContent.text : string.Empty;

                if (typeName == "ProjectSettingsWindow" ||
                    title == "Project Settings" ||
                    title.StartsWith("Project Settings"))
                {
                    w.Close();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("<b>[AI Package Installer]</b> Failed to close Project Settings window: " + ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Edits manifest.json to:
    ///   1. Add "com.github.asus4" scoped registry pointing to registry.npmjs.org (if absent).
    ///   2. Add/update ONNX packages to version 0.4.4 in dependencies.
    ///   3. Remove any old git-URL entries for onnxruntime.
    /// Returns true if the file was changed.
    /// </summary>
    private static bool PatchManifest()
    {
        return PatchManifest(out _);
    }

    private static bool PatchManifest(out bool registryAdded)
    {
        registryAdded = false;
        if (!File.Exists(ManifestPath))
        {
            Debug.LogError("<b>[AI Package Installer]</b> manifest.json not found at: " + ManifestPath);
            return false;
        }

        string text = File.ReadAllText(ManifestPath);
        bool changed = false;

        // ── 1. Remove old git-URL ONNX entries ───────────────────────────────
        string gitOnnxPattern =
            @",?\s*""[^""]*""\s*:\s*""https://github\.com/asus4/onnxruntime-unity[^""]*""";
        if (Regex.IsMatch(text, gitOnnxPattern))
        {
            text = Regex.Replace(text, gitOnnxPattern, "");
            changed = true;
        }

        // ── 2. Add scoped registry if missing ────────────────────────────────
        if (!text.Contains("registry.npmjs.org"))
        {
            const string npmRegistryEntry =
                "    {\n" +
                "      \"name\": \"NPM\",\n" +
                "      \"url\": \"https://registry.npmjs.org\",\n" +
                "      \"scopes\": [\n" +
                "        \"com.github.asus4\"\n" +
                "      ]\n" +
                "    }";

            if (text.Contains("\"scopedRegistries\""))
            {
                text = Regex.Replace(text, @"""scopedRegistries""\s*:\s*\[", "\"scopedRegistries\": [\n" + npmRegistryEntry + ",");
            }
            else
            {
                const string registryBlock =
                    "\"scopedRegistries\": [\n" +
                    npmRegistryEntry + "\n" +
                    "  ],\n  ";
                text = Regex.Replace(text, @"""dependencies""\s*:", registryBlock + "\"dependencies\":");
            }
            changed = true;
            registryAdded = true;
        }

        // ── 3. Add/update npm packages in dependencies ────────────────────────
        foreach (var kv in NpmPackages)
        {
            string desiredEntry = $"\"{kv.Key}\": \"{kv.Value}\"";

            // Package id already present (any version)?
            var existingMatch = Regex.Match(text, $@"""{Regex.Escape(kv.Key)}""\s*:\s*""([^""]+)""");
            if (existingMatch.Success)
            {
                if (existingMatch.Value != desiredEntry)
                {
                    text = text.Replace(existingMatch.Value, desiredEntry);
                    changed = true;
                }
            }
            else
            {
                // Insert at top of dependencies block
                text = Regex.Replace(text,
                    @"""dependencies""\s*:\s*\{",
                    "\"dependencies\": {\n    " + desiredEntry + ",");
                changed = true;
            }
        }

        if (changed)
            File.WriteAllText(ManifestPath, text);

        return changed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PHASE 2 — install git packages that are not yet installed
    // ─────────────────────────────────────────────────────────────────────────
    private static void EnqueueAndInstallGitPackages(bool checkFirst)
    {
        _gitQueue.Clear();
        _gitNames.Clear();

        string[] names = new[] { "LLMUnity Package", "Piper TTS Package", "Whisper Unity Package" };

        if (!checkFirst)
        {
            for (int i = 0; i < GitPackages.Length; i++)
            {
                _gitQueue.Enqueue(GitPackages[i]);
                _gitNames.Enqueue(names[i]);
            }
            AISystemSetupWindow.ShowWindow();
            InstallNext();
            return;
        }

        _listRequest = Client.List(true, false);
        EditorApplication.update += OnListComplete;
    }

    private static void OnListComplete()
    {
        if (!_listRequest.IsCompleted) return;
        EditorApplication.update -= OnListComplete;

        if (_listRequest.Status != StatusCode.Success)
        {
            Debug.LogError("<b>[AI Package Installer]</b> Failed to list packages: " + _listRequest.Error.message);
            return;
        }

        var installed = _listRequest.Result
            .Select(p => p.packageId.ToLower())
            .ToList();

        string[] names = new[] { "LLMUnity Package", "Piper TTS Package", "Whisper Unity Package" };

        _gitQueue.Clear();
        _gitNames.Clear();
        for (int i = 0; i < GitPackages.Length; i++)
        {
            bool found = installed.Any(p => p.StartsWith(GitPackageIds[i]));
            if (!found)
            {
                _gitQueue.Enqueue(GitPackages[i]);
                _gitNames.Enqueue(names[i]);
            }
            else
            {
                AISystemSetupWindow.UpdatePackageStep(names[i], AISystemSetupWindow.StepStatus.Completed);
            }
        }

        if (_gitQueue.Count > 0)
        {
            AISystemSetupWindow.ShowWindow();
            Debug.Log($"<b>[AI Package Installer]</b> Installing {_gitQueue.Count} missing package(s)…");
            InstallNext();
        }
        else
        {
            Debug.Log("<b>[AI Package Installer]</b> All packages already installed! ✅");
            SessionState.SetBool("AIPackageInstaller.Done", true);
            EditorPrefs.SetBool($"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}", true);
            ExtractContentPackageIfPresent();
            EditorApplication.delayCall += AISystemSetupWindow.AutoStartDownloads;
        }
    }

    private static void InstallNext()
    {
        if (_gitQueue.Count == 0)
        {
            Debug.Log("<b>[AI Package Installer]</b> All AI packages installed successfully! ✅");
            SessionState.SetBool("AIPackageInstaller.Done", true);
            EditorPrefs.SetBool($"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}", true);
            ExtractContentPackageIfPresent();
            EditorApplication.delayCall += AISystemSetupWindow.AutoStartDownloads;
            return;
        }

        string pkg = _gitQueue.Dequeue();
        _currentPkgName = _gitNames.Dequeue();

        AISystemSetupWindow.UpdatePackageStep(_currentPkgName, AISystemSetupWindow.StepStatus.InProgress);

        Debug.Log($"<b>[AI Package Installer]</b> Installing: {pkg}…");
        _addRequest = Client.Add(pkg);
        EditorApplication.update += OnAddComplete;
    }

    private static void OnAddComplete()
    {
        if (!_addRequest.IsCompleted) return;
        EditorApplication.update -= OnAddComplete;

        if (_addRequest.Status == StatusCode.Success)
        {
            Debug.Log($"<b>[AI Package Installer]</b> ✅ Installed: {_addRequest.Result.packageId}");
            AISystemSetupWindow.UpdatePackageStep(_currentPkgName, AISystemSetupWindow.StepStatus.Completed);
        }
        else
        {
            Debug.LogError($"<b>[AI Package Installer]</b> ❌ Failed: {_addRequest.Error.message}");
            AISystemSetupWindow.UpdatePackageStep(_currentPkgName, AISystemSetupWindow.StepStatus.Failed, _addRequest.Error.message);
        }

        InstallNext();
    }

    // ── Asset Store Content Packaging & Auto-Extraction ───────────────────────

    /// <summary>
    /// Automatically extracts the bundled content package if present in the project.
    /// Ensures scripts and prefabs are only imported AFTER packages are installed,
    /// preventing CS0246 missing namespace errors and broken nested prefabs on first import.
    /// </summary>
    public static void ExtractContentPackageIfPresent()
    {
        string managerScript = Path.Combine(Application.dataPath, "AI Driven NPCs System", "Scripts", "AISystem", "Core", "AISystemManager.cs");
        if (File.Exists(managerScript))
        {
            return;
        }

        string[] candidates = new[]
        {
            "Assets/AI Driven NPCs System/AI-Driven-NPCs-Content.unitypackage",
            "Assets/AI-Driven-NPCs-Content.unitypackage"
        };

        foreach (string relPath in candidates)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relPath);
            if (File.Exists(fullPath))
            {
                Debug.Log($"<b>[AI Package Installer]</b> 📦 Unpacking content package: {relPath}...");
                AssetDatabase.ImportPackage(relPath, false);
                break;
            }
        }
    }

    [MenuItem("Tools/AI Packages/Unpack Content Package")]
    public static void UnpackContentPackageManual()
    {
        string[] candidates = new[]
        {
            "Assets/AI Driven NPCs System/AI-Driven-NPCs-Content.unitypackage",
            "Assets/AI-Driven-NPCs-Content.unitypackage"
        };

        bool found = false;
        foreach (string relPath in candidates)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relPath);
            if (File.Exists(fullPath))
            {
                Debug.Log($"<b>[AI Package Installer]</b> 📦 Unpacking content package: {relPath}...");
                AssetDatabase.ImportPackage(relPath, false);
                found = true;
                break;
            }
        }

        if (!found)
        {
            EditorUtility.DisplayDialog("Unpack Content Package", "No 'AI-Driven-NPCs-Content.unitypackage' found to unpack.", "OK");
        }
    }

    [MenuItem("Tools/AI Packages/Export Asset Store Content Package")]
    public static void ExportAssetStoreContentPackage()
    {
        string packagePath = "Assets/AI Driven NPCs System/AI-Driven-NPCs-Content.unitypackage";
        string[] exportPaths = new[]
        {
            "Assets/AI Driven NPCs System/Prefabs",
            "Assets/AI Driven NPCs System/Resources",
            "Assets/AI Driven NPCs System/Scenes",
            "Assets/AI Driven NPCs System/Scripts"
        };

        List<string> validPaths = new List<string>();
        foreach (string p in exportPaths)
        {
            if (Directory.Exists(p)) validPaths.Add(p);
        }

        if (validPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Export Content Package", "No source folders found in Assets/AI Driven NPCs System/ to export.", "OK");
            return;
        }

        AssetDatabase.ExportPackage(validPaths.ToArray(), packagePath, ExportPackageOptions.Recurse);
        AssetDatabase.Refresh();
        Debug.Log($"<b>[AI Package Installer]</b> ✅ Exported content package to: {packagePath}");
        EditorUtility.DisplayDialog("Export Complete", $"Content package successfully exported to:\n{packagePath}", "OK");
    }

    [MenuItem("Tools/AI Packages/Prepare for Asset Store Upload")]
    public static void PrepareForAssetStoreUpload()
    {
        ExportAssetStoreContentPackage();

        string stagingDir = Path.Combine(Application.dataPath, "AI Driven NPCs System", ".staging~");
        if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);

        string[] folders = new[] { "Prefabs", "Resources", "Scenes", "Scripts" };
        foreach (string f in folders)
        {
            string src = Path.Combine(Application.dataPath, "AI Driven NPCs System", f);
            string dst = Path.Combine(stagingDir, f);
            if (Directory.Exists(src))
            {
                if (Directory.Exists(dst)) Directory.Delete(dst, true);
                Directory.Move(src, dst);
            }
            string metaSrc = src + ".meta";
            string metaDst = dst + ".meta";
            if (File.Exists(metaSrc))
            {
                if (File.Exists(metaDst)) File.Delete(metaDst);
                File.Move(metaSrc, metaDst);
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Ready for Asset Store Upload",
            "Assets/AI Driven NPCs System is now ready for upload!\n\n" +
            "It now contains ONLY:\n" +
            "• Editor/ (Installer & Setup)\n" +
            "• AI-Driven-NPCs-Content.unitypackage (Self-extracting payload)\n" +
            "• Documentation (README & Setup Guide)\n\n" +
            "You can now run the Publisher Tool on 'Assets/AI Driven NPCs System'.\n\n" +
            "When you are done uploading, click Tools → AI Packages → Restore Development Assets.", "OK");
    }

    [MenuItem("Tools/AI Packages/Restore Development Assets")]
    public static void RestoreDevelopmentAssets()
    {
        string stagingDir = Path.Combine(Application.dataPath, "AI Driven NPCs System", ".staging~");
        if (!Directory.Exists(stagingDir))
        {
            EditorUtility.DisplayDialog("Restore Development Assets", "No staging folder found. Assets are already in place.", "OK");
            return;
        }

        string[] folders = new[] { "Prefabs", "Resources", "Scenes", "Scripts" };
        foreach (string f in folders)
        {
            string src = Path.Combine(stagingDir, f);
            string dst = Path.Combine(Application.dataPath, "AI Driven NPCs System", f);
            if (Directory.Exists(src))
            {
                if (Directory.Exists(dst)) Directory.Delete(dst, true);
                Directory.Move(src, dst);
            }
            string metaSrc = src + ".meta";
            string metaDst = dst + ".meta";
            if (File.Exists(metaSrc))
            {
                if (File.Exists(metaDst)) File.Delete(metaDst);
                File.Move(metaSrc, metaDst);
            }
        }

        if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
        AssetDatabase.Refresh();
        Debug.Log("<b>[AI Package Installer]</b> ✅ Development assets restored.");
        EditorUtility.DisplayDialog("Restore Complete", "Development assets restored successfully!", "OK");
    }
}
}
