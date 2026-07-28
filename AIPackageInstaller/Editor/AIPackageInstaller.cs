using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

    // ─────────────────────────────────────────────────────────────────────────
    static AIPackageInstaller()
    {
        EditorApplication.delayCall += Initialize;
    }

    [MenuItem("Tools/AI Packages/Force Install Dependencies")]
    public static void ForceInstall()
    {
        Debug.Log("<b>[AI Package Installer]</b> Force install triggered.");
        SessionState.SetBool("AIPackageInstaller.Done", false);
        SessionState.SetBool("AIPackageInstaller.ConsentGiven", true); // user explicitly triggered — skip dialog

        InitUI();
        AISystemSetupWindow.ShowWindow();

        if (PatchManifest())
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
            new AISystemSetupWindow.InstallStep { Name = "LLMUnity Package", Description = "Installing LLMUnity package from Git" },
            new AISystemSetupWindow.InstallStep { Name = "Piper TTS Package", Description = "Installing Piper TTS package from Git" },
            new AISystemSetupWindow.InstallStep { Name = "Whisper Unity Package", Description = "Installing Whisper Unity package from Git" }
        };
        AISystemSetupWindow.InitPackageSteps(steps);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PHASE 1 — ensure npm registry + ONNX packages in manifest.json
    // ─────────────────────────────────────────────────────────────────────────
    private static void Initialize()
    {
        // Already fully installed this session — just trigger model check.
        if (SessionState.GetBool("AIPackageInstaller.Done", false))
        {
            EditorApplication.delayCall += AISystemSetupWindow.AutoStartDownloads;
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

        bool manifestChanged = PatchManifest();
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
    /// Edits manifest.json to:
    ///   1. Add "com.github.asus4" scoped registry pointing to registry.npmjs.org (if absent).
    ///   2. Add/update ONNX packages to version 0.4.4 in dependencies.
    ///   3. Remove any old git-URL entries for onnxruntime.
    /// Returns true if the file was changed.
    /// </summary>
    private static bool PatchManifest()
    {
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
            const string registryBlock =
                "\"scopedRegistries\": [\n" +
                "    {\n" +
                "      \"name\": \"NPM\",\n" +
                "      \"url\": \"https://registry.npmjs.org\",\n" +
                "      \"scopes\": [\"com.github.asus4\"]\n" +
                "    }\n" +
                "  ],\n  ";
            text = Regex.Replace(text, @"""dependencies""\s*:", registryBlock + "\"dependencies\":");
            changed = true;
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
            EditorApplication.delayCall += AISystemSetupWindow.AutoStartDownloads;
        }
    }

    private static void InstallNext()
    {
        if (_gitQueue.Count == 0)
        {
            Debug.Log("<b>[AI Package Installer]</b> All AI packages installed successfully! ✅");
            SessionState.SetBool("AIPackageInstaller.Done", true);
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
}
