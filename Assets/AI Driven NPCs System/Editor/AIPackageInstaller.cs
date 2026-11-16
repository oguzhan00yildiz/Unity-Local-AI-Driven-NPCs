using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace AISystem.Editor
{
    /// <summary>
    /// Runs when the package is imported or the Editor reloads.
    /// LLMUnity, Piper TTS, Whisper and ONNX Runtime ship inside ThirdParty/, so no packages are installed.
    /// With user consent it opens the setup window and downloads the AI model files.
    /// </summary>
    [InitializeOnLoad]
    public class AIPackageInstaller
    {
    // ─────────────────────────────────────────────────────────────────────────
    static AIPackageInstaller()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        EditorApplication.delayCall += Initialize;
    }

    /// <summary>
    /// Called when the AI System package is imported or dragged into the project.
    /// </summary>
    public static void OnPackageImported()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        // Reset stale Done flags because package was just imported/dragged into project
        string donePrefKey = $"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}";
        EditorPrefs.DeleteKey(donePrefKey);
        SessionState.SetBool("AIPackageInstaller.Done", false);

        CheckAndPromptSetup(isDirectImport: true);
    }

    public static void StartAutomaticSetup()
    {
        SessionState.SetBool("AIPackageInstaller.AutoSetupRunning", true);
        SessionState.SetBool("AIPackageInstaller.Done", false);
        string donePrefKey = $"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}";
        EditorPrefs.DeleteKey(donePrefKey);

        ContinueAutomaticSetup();
    }

    /// <summary>
    /// Shows the bundled component check if any is missing, otherwise starts model downloads.
    /// </summary>
    private static void ContinueAutomaticSetup()
    {
        if (!AISystemSetupWindow.AreAllPackagesInstalled())
        {
            Debug.LogError("<b>[AI Package Installer]</b> A bundled component in ThirdParty/ failed to load. Check the Console for compile errors, or re-import the package.");
            AISystemSetupWindow.ShowWindow();
            return;
        }

        if (!AISystemSetupWindow.AreAllModelsDownloaded())
        {
            AISystemSetupWindow.AutoStartDownloads();
            return;
        }

        MarkSetupCompleted();
        AISystemSetupWindow.CheckAndNotifyCompletion();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static void Initialize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        // Resuming automatic setup across domain reloads (e.g. a recompile during model downloads)
        if (SessionState.GetBool("AIPackageInstaller.AutoSetupRunning", false))
        {
            Debug.Log("<b>[AI Package Installer]</b> Resuming automatic setup after reload…");
            ContinueAutomaticSetup();
            return;
        }

        CheckAndPromptSetup(isDirectImport: false);
    }

    /// <summary>
    /// Checks system installation status and prompts the user for setup permission if needed.
    /// </summary>
    public static void CheckAndPromptSetup(bool isDirectImport)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

        if (SessionState.GetBool("AIPackageInstaller.AutoSetupRunning", false))
            return;

        // Package development repository: open setup only via the Tools menu
        if (IsPackageDevelopmentProject())
            return;

        bool allPkgs = AISystemSetupWindow.AreAllPackagesInstalled();
        bool allModels = AISystemSetupWindow.AreAllModelsDownloaded();

        if (allPkgs && allModels)
        {
            if (isDirectImport)
            {
                AISystemSetupWindow.ShowWindow();
            }
            return;
        }

        // Avoid repeated dialog popups on every script recompile during the same editor session
        if (!isDirectImport && SessionState.GetBool("AIPackageInstaller.PromptAnswered", false))
            return;

        SessionState.SetBool("AIPackageInstaller.PromptAnswered", true);

        // Ask user for permission to run automatic setup
        bool proceed = AISystemSetupWindow.ShowConsentDialog();
        if (proceed)
        {
            StartAutomaticSetup();
        }
        else
        {
            Debug.Log("<b>[AI Package Installer]</b> Setup postponed by user. You can start setup anytime via Tools → AI Packages → AI System Setup.");
        }
    }

    /// <summary>
    /// True in the package source repository, which has the AIPackageInstaller folder at its root.
    /// </summary>
    private static bool IsPackageDevelopmentProject()
    {
        return Directory.Exists(Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "AIPackageInstaller"));
    }

    public static void MarkSetupCompleted()
    {
        SessionState.SetBool("AIPackageInstaller.Done", true);
        SessionState.SetBool("AIPackageInstaller.AutoSetupRunning", false);
        string donePrefKey = $"AIPackageInstaller.Done.{Application.dataPath.GetHashCode()}";
        EditorPrefs.SetBool(donePrefKey, true);
    }

    /// <summary>
    /// Generates default personality presets at default installation without needing a manual menu button.
    /// </summary>
    public static void EnsureDefaultSamplePresets()
    {
        try
        {
            var type = Type.GetType("AISystem.SamplePresetGenerator, AIDrivenNPCs.Editor");
            if (type != null)
            {
                var method = type.GetMethod("EnsureDefaultPresets", new Type[] { typeof(bool) });
                if (method != null)
                {
                    method.Invoke(null, new object[] { false });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AI Package Installer] Note: Could not auto-generate sample presets: " + ex.Message);
        }
    }
}

/// <summary>
/// Detects when the AI System package or installer assets are imported/updated,
/// automatically prompting the user for permission to run setup.
/// </summary>
public class AIPackagePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool packageImported = false;
        foreach (string asset in importedAssets)
        {
            if (asset.Contains("AIPackageInstaller") ||
                asset.Contains("AISystemSetupWindow") ||
                asset.StartsWith("Assets/AI Driven NPCs System"))
            {
                packageImported = true;
                break;
            }
        }

        if (packageImported)
        {
            EditorApplication.delayCall += () => AIPackageInstaller.OnPackageImported();
        }
    }
}
}
