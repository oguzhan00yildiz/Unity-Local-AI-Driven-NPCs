using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

[InitializeOnLoad]
public class AIPackageInstaller
{
    // Git packages to install via Client.Add() after manifest is set up
    private static readonly string[] GitPackages = new string[]
    {
        "https://github.com/undreamai/LLMUnity.git",
        "https://github.com/lookbe/piper-no-espeak-unity.git",
        "https://github.com/Macoron/whisper.unity.git?path=Packages/com.whisper.unity"
    };

    // NPM scoped packages - written directly to manifest.json
    private static readonly Dictionary<string, string> NpmPackages = new Dictionary<string, string>
    {
        { "com.github.asus4.onnxruntime", "0.4.4" },
        { "com.github.asus4.onnxruntime.unity", "0.4.4" },
        { "com.github.asus4.onnxruntime-extensions", "0.4.4" }
    };

    private static Queue<string> packagesToInstall = new Queue<string>();
    private static AddRequest currentRequest;
    private static ListRequest listRequest;
    private static ResolveRequest resolveRequest;

    static AIPackageInstaller()
    {
        Debug.Log("<b>[AI Package Installer]</b> Initializing...");
        EditorApplication.delayCall += SetupManifestAndInstall;
    }

    private static void SetupManifestAndInstall()
    {
        bool manifestChanged = false;

        try
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");

            string manifestContent = File.ReadAllText(manifestPath);
            JObject manifest = JObject.Parse(manifestContent);

            // 1. Ensure NPM scoped registry exists
            if (manifest["scopedRegistries"] == null)
                manifest["scopedRegistries"] = new JArray();

            JArray registries = (JArray)manifest["scopedRegistries"];
            bool npmExists = registries.Any(r =>
                r["url"] != null && r["url"].ToString() == "https://registry.npmjs.com");

            if (!npmExists)
            {
                registries.Add(new JObject
                {
                    ["name"] = "NPM",
                    ["url"] = "https://registry.npmjs.com",
                    ["scopes"] = new JArray("com.github.asus4")
                });
                Debug.Log("<b>[AI Package Installer]</b> Added NPM scoped registry.");
                manifestChanged = true;
            }

            // 2. Add ONNX Runtime packages directly to dependencies
            if (manifest["dependencies"] == null)
                manifest["dependencies"] = new JObject();

            JObject deps = (JObject)manifest["dependencies"];
            foreach (var pkg in NpmPackages)
            {
                if (deps[pkg.Key] == null)
                {
                    deps[pkg.Key] = pkg.Value;
                    Debug.Log($"<b>[AI Package Installer]</b> Added {pkg.Key}@{pkg.Value} to manifest.");
                    manifestChanged = true;
                }
            }

            // 3. Save manifest if changed and reload
            if (manifestChanged)
            {
                File.WriteAllText(manifestPath, manifest.ToString(Newtonsoft.Json.Formatting.Indented));
                Debug.Log("<b>[AI Package Installer]</b> manifest.json saved. Waiting for Package Manager to resolve...");

                resolveRequest = Client.Resolve();
                EditorApplication.update += WaitForResolve;
            }
            else
            {
                Debug.Log("<b>[AI Package Installer]</b> Manifest already up to date. Checking git packages...");
                EditorApplication.delayCall += CheckAndInstallGitPackages;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<b>[AI Package Installer]</b> Error: {ex.Message}");
        }
    }

    private static void WaitForResolve()
    {
        if (resolveRequest.IsCompleted)
        {
            EditorApplication.update -= WaitForResolve;

            if (resolveRequest.Status == StatusCode.Success)
            {
                Debug.Log("<b>[AI Package Installer]</b> Package Manager resolved. Now installing git packages...");
            }
            else
            {
                Debug.LogWarning($"<b>[AI Package Installer]</b> Resolve warning: {resolveRequest.Error?.message}. Continuing anyway...");
            }

            EditorApplication.delayCall += CheckAndInstallGitPackages;
        }
    }

    [MenuItem("Tools/AI Packages/Force Install Dependencies")]
    public static void ForceInstall()
    {
        Debug.Log("<b>[AI Package Installer]</b> Force install triggered.");
        packagesToInstall.Clear();
        foreach (var pkg in GitPackages)
            packagesToInstall.Enqueue(pkg);
        InstallNextPackage();
    }

    private static void CheckAndInstallGitPackages()
    {
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

        foreach (var pkg in GitPackages)
        {
            bool isInstalled =
                (pkg.Contains("LLMUnity") && installed.Any(p => p.Contains("ai.undream.llm"))) ||
                (pkg.Contains("piper") && installed.Any(p => p.Contains("ai.lookbe.piper"))) ||
                (pkg.Contains("whisper") && installed.Any(p => p.Contains("com.whisper.unity")));

            if (!isInstalled)
                packagesToInstall.Enqueue(pkg);
        }

        if (packagesToInstall.Count > 0)
        {
            Debug.Log($"<b>[AI Package Installer]</b> Installing {packagesToInstall.Count} missing git packages...");
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
