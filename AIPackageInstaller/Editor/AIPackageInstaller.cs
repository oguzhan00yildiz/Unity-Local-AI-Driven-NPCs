using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
public class AIPackageInstaller
{
    // Packages installed in order via Client.Add()
    // onnxruntime MUST come before piper (Piper depends on it)
    private static readonly string[] RequiredPackages = new string[]
    {
        "https://github.com/asus4/onnxruntime-unity.git?path=com.github.asus4.onnxruntime",
        "https://github.com/asus4/onnxruntime-unity.git?path=com.github.asus4.onnxruntime.unity",
        "https://github.com/undreamai/LLMUnity.git",
        "https://github.com/lookbe/piper-no-espeak-unity.git",
        "https://github.com/Macoron/whisper.unity.git?path=Packages/com.whisper.unity"
    };

    private static Queue<string> packagesToInstall = new Queue<string>();
    private static AddRequest currentRequest;
    private static ListRequest listRequest;

    static AIPackageInstaller()
    {
        Debug.Log("<b>[AI Package Installer]</b> Initializing...");
        EditorApplication.delayCall += CheckAndInstallPackages;
    }

    [MenuItem("Tools/AI Packages/Force Install Dependencies")]
    public static void ForceInstall()
    {
        Debug.Log("<b>[AI Package Installer]</b> Force install triggered.");
        packagesToInstall.Clear();
        foreach (var pkg in RequiredPackages)
            packagesToInstall.Enqueue(pkg);
        InstallNextPackage();
    }

    private static void CheckAndInstallPackages()
    {
        Debug.Log("<b>[AI Package Installer]</b> Checking installed packages...");
        listRequest = Client.List(true, false);
        EditorApplication.update += CheckInstalledProgress;
    }

    private static void CheckInstalledProgress()
    {
        if (!listRequest.IsCompleted) return;
        EditorApplication.update -= CheckInstalledProgress;

        if (listRequest.Status != StatusCode.Success)
        {
            Debug.LogError($"<b>[AI Package Installer]</b> Failed to list packages: {listRequest.Error.message}");
            return;
        }

        var installed = listRequest.Result.Select(p => p.packageId.ToLower()).ToList();
        packagesToInstall.Clear();

        foreach (var pkg in RequiredPackages)
        {
            bool isInstalled =
                (pkg.Contains("onnxruntime-unity") && pkg.Contains("onnxruntime.unity") && installed.Any(p => p.Contains("com.github.asus4.onnxruntime.unity"))) ||
                (pkg.Contains("onnxruntime-unity") && !pkg.Contains("onnxruntime.unity") && installed.Any(p => p.Contains("com.github.asus4.onnxruntime") && !p.Contains(".unity"))) ||
                (pkg.Contains("LLMUnity") && installed.Any(p => p.Contains("ai.undream.llm"))) ||
                (pkg.Contains("piper") && installed.Any(p => p.Contains("ai.lookbe.piper"))) ||
                (pkg.Contains("whisper") && installed.Any(p => p.Contains("com.whisper.unity")));

            if (!isInstalled)
                packagesToInstall.Enqueue(pkg);
        }

        if (packagesToInstall.Count > 0)
        {
            Debug.Log($"<b>[AI Package Installer]</b> Installing {packagesToInstall.Count} missing packages...");
            InstallNextPackage();
        }
        else
        {
            Debug.Log("<b>[AI Package Installer]</b> All packages are already installed! ");
        }
    }

    private static void InstallNextPackage()
    {
        if (packagesToInstall.Count == 0)
        {
            Debug.Log("<b>[AI Package Installer]</b> All AI packages installed successfully! ");
            return;
        }

        string pkg = packagesToInstall.Dequeue();
        Debug.Log($"<b>[AI Package Installer]</b> Installing: {pkg}...");
        currentRequest = Client.Add(pkg);
        EditorApplication.update += InstallProgress;
    }

    private static void InstallProgress()
    {
        if (!currentRequest.IsCompleted) return;
        EditorApplication.update -= InstallProgress;

        if (currentRequest.Status == StatusCode.Success)
            Debug.Log($"<b>[AI Package Installer]</b>  Installed: {currentRequest.Result.packageId}");
        else
            Debug.LogError($"<b>[AI Package Installer]</b>  Failed: {currentRequest.Error.message}");

        InstallNextPackage();
    }
}
