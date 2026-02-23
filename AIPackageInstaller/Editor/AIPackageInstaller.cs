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
    // List of Git URLs to install (in order of dependency)
    private static readonly string[] RequiredPackages = new string[]
    {
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
        
        // First, ensure manifest has required registries
        EditorApplication.delayCall += EnsureManifestSetup;
    }

    private static void EnsureManifestSetup()
    {
        try
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("<b>[AI Package Installer]</b> manifest.json not found!");
                return;
            }

            string manifestContent = File.ReadAllText(manifestPath);
            JObject manifest = JObject.Parse(manifestContent);

            // Ensure scopedRegistries array exists
            if (manifest["scopedRegistries"] == null)
            {
                manifest["scopedRegistries"] = new JArray();
            }

            JArray registries = (JArray)manifest["scopedRegistries"];
            
            // Check if NPM registry already exists
            bool npmRegistryExists = registries.Any(reg => 
                reg["name"] != null && reg["name"].ToString() == "NPM" &&
                reg["url"] != null && reg["url"].ToString() == "https://registry.npmjs.com");

            if (!npmRegistryExists)
            {
                Debug.Log("<b>[AI Package Installer]</b> Adding NPM scoped registry to manifest.json...");
                
                JObject npmRegistry = new JObject();
                npmRegistry["name"] = "NPM";
                npmRegistry["url"] = "https://registry.npmjs.com";
                npmRegistry["scopes"] = new JArray("com.github.asus4");
                
                registries.Add(npmRegistry);
            }

            // Ensure dependencies exists
            if (manifest["dependencies"] == null)
            {
                manifest["dependencies"] = new JObject();
            }

            JObject dependencies = (JObject)manifest["dependencies"];

            // Add ONNX Runtime dependencies
            string[] onnxDeps = new string[]
            {
                "com.github.asus4.onnxruntime",
                "com.github.asus4.onnxruntime.unity",
                "com.github.asus4.onnxruntime-extensions"
            };

            bool manifestModified = false;
            foreach (var dep in onnxDeps)
            {
                if (dependencies[dep] == null)
                {
                    Debug.Log($"<b>[AI Package Installer]</b> Adding {dep} to manifest.json...");
                    dependencies[dep] = "0.4.4";
                    manifestModified = true;
                }
            }

            // Write back if modified
            if (npmRegistryExists == false || manifestModified)
            {
                File.WriteAllText(manifestPath, manifest.ToString(Newtonsoft.Json.Formatting.Indented));
                Debug.Log("<b>[AI Package Installer]</b> Manifest.json updated successfully!");
                
                // Wait and reload packages after modifying manifest
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("<b>[AI Package Installer]</b> Reloading Package Manager...");
                    Client.Resolve();
                    
                    // Schedule package check after a delay to let packages resolve
                    EditorApplication.delayCall += () =>
                    {
                        System.Threading.Thread.Sleep(2000); // Wait 2 seconds
                        CheckAndInstallPackages();
                    };
                };
            }
            else
            {
                // Schedule package installation check if no manifest changes
                EditorApplication.delayCall += CheckAndInstallPackages;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<b>[AI Package Installer]</b> Error setting up manifest: {ex.Message}");
        }
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
        Debug.Log("<b>[AI Package Installer]</b> Checking installed packages...");
        
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
                else
                {
                    Debug.Log("<b>[AI Package Installer]</b> All AI packages are already installed!");
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

