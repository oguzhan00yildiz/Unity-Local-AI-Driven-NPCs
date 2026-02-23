using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
public class AIPackageInstaller
{
    // List of Git URLs to install
    private static readonly string[] RequiredPackages = new string[]
    {
        "https://github.com/undreamai/LLMUnity.git",
        "https://github.com/lookbe/piper-no-espeak-unity.git",
        "https://github.com/Macoron/whisper.unity.git?path=Whisper",
        "https://github.com/asus4/onnxruntime-unity.git"
    };

    private static Queue<string> packagesToInstall = new Queue<string>();
    private static AddRequest currentRequest;
    private static ListRequest listRequest;

    static AIPackageInstaller()
    {
        Debug.Log("<b>[AI Package Installer]</b> Initialized. Scheduling installation check...");
        // Run the check after a short delay to ensure Unity is fully loaded
        EditorApplication.delayCall += CheckAndInstallPackages;
    }

    [MenuItem("Tools/AI Packages/Force Install Dependencies")]
    public static void ForceInstall()
    {
        Debug.Log("<b>[AI Package Installer]</b> Force install triggered by user");
        packagesToInstall.Clear();
        foreach (var pkg in RequiredPackages)
        {
            packagesToInstall.Enqueue(pkg);
        }
        InstallNextPackage();
    }

    private static void CheckAndInstallPackages()
    {
        // First, list installed packages to avoid reinstalling
        listRequest = Client.List(true, false);
        EditorApplication.update += CheckInstalledProgress;
    }

    private static void CheckInstalledProgress()
    {
        if (listRequest.IsCompleted)
        {
            EditorApplication.update -= CheckInstalledProgress;

            if (listRequest.Status == StatusCode.Success)
            {
                var installedPackages = listRequest.Result.Select(p => p.packageId).ToList();
                packagesToInstall.Clear();

                foreach (var pkgUrl in RequiredPackages)
                {
                    bool isInstalled = false;
                    if (pkgUrl.Contains("LLMUnity") && installedPackages.Any(p => p.Contains("ai.undream.llm"))) isInstalled = true;
                    if (pkgUrl.Contains("piper") && installedPackages.Any(p => p.Contains("ai.lookbe.piper"))) isInstalled = true;
                    if (pkgUrl.Contains("whisper") && installedPackages.Any(p => p.Contains("com.whisper.unity"))) isInstalled = true;

                    if (!isInstalled)
                    {
                        packagesToInstall.Enqueue(pkgUrl);
                    }
                }

                if (packagesToInstall.Count > 0)
                {
                    Debug.Log($"<b>[AI Package Installer]</b> Found {packagesToInstall.Count} missing AI packages. Installing...");
                    InstallNextPackage();
                }
            }
            else
            {
                Debug.LogError($"<b>[AI Package Installer]</b> Failed to list packages: {listRequest.Error.message}");
            }
        }
    }

    private static void InstallNextPackage()
    {
        if (packagesToInstall.Count == 0)
        {
            Debug.Log("<b>[AI Package Installer]</b> All AI packages are successfully installed!");
            return;
        }

        string packageUrl = packagesToInstall.Dequeue();
        Debug.Log($"<b>[AI Package Installer]</b> Installing package: {packageUrl}...");
        
        currentRequest = Client.Add(packageUrl);
        EditorApplication.update += InstallProgress;
    }

    private static void InstallProgress()
    {
        if (currentRequest.IsCompleted)
        {
            EditorApplication.update -= InstallProgress;

            if (currentRequest.Status == StatusCode.Success)
            {
                Debug.Log($"<b>[AI Package Installer]</b> Successfully installed: {currentRequest.Result.packageId}");
            }
            else if (currentRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"<b>[AI Package Installer]</b> Failed to install package: {currentRequest.Error.message}");
            }

            // Install the next one regardless of success/failure of the current one
            InstallNextPackage();
        }
    }
}
