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
    // ONNX Runtime must be installed FIRST as dependency for Piper
    private static readonly string[] RequiredPackages = new string[]
    {
        // Install ONNX Runtime first (npm scoped registry)
        "com.github.asus4.onnxruntime@0.4.4",
        "com.github.asus4.onnxruntime.unity@0.4.4",
        "com.github.asus4.onnxruntime-extensions@0.4.4",
        // Git packages that depend on ONNX Runtime
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
        EditorApplication.delayCall += EnsureNPMRegistry;
    }

    private static void EnsureNPMRegistry()
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
                File.WriteAllText(manifestPath, manifest.ToString(Newtonsoft.Json.Formatting.Indented));
                Debug.Log("<b>[AI Package Installer]</b> NPM registry added. Reloading Package Manager...");
                
                // Reload and wait before starting installations
                Client.Resolve();
                EditorApplication.delayCall += () =>
                {
                    System.Threading.Thread.Sleep(3000);
                    CheckAndInstallPackages();
                };
            }
            else
            {
                CheckAndInstallPackages();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<b>[AI Package Installer]</b> Error setting up registry: {ex.Message}");
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
                var installedPackages = listRequest.Result.Select(p => p.packageId.ToLower()).ToList();
                packagesToInstall.Clear();

                foreach (var pkg in RequiredPackages)
                {
                    bool isInstalled = false;
                    string searchTerm = pkg.ToLower();
                    
                    if (searchTerm.Contains("onnxruntime"))
                    {
                        // For ONNX packages, check by name prefix
                        if (searchTerm.Contains("com.github.asus4.onnxruntime"))
                        {
                            if (searchTerm.Contains("extensions"))
                                isInstalled = installedPackages.Any(p => p.Contains("onnxruntime-extensions"));
                            else if (searchTerm.Contains("onnxruntime.unity"))
                                isInstalled = installedPackages.Any(p => p.Contains("onnxruntime.unity"));
                            else if (searchTerm.Contains("com.github.asus4.onnxruntime"))
                                isInstalled = installedPackages.Any(p => p.Contains("com.github.asus4.onnxruntime") && !p.Contains("extensions") && !p.Contains("unity"));
                        }
                    }
                    else if (searchTerm.Contains("llmunity"))
                        isInstalled = installedPackages.Any(p => p.Contains("ai.undream.llm"));
                    else if (searchTerm.Contains("piper"))
                        isInstalled = installedPackages.Any(p => p.Contains("ai.lookbe.piper"));
                    else if (searchTerm.Contains("whisper"))
                        isInstalled = installedPackages.Any(p => p.Contains("com.whisper.unity"));

                    if (!isInstalled)
                        packagesToInstall.Enqueue(pkg);
                }

                if (packagesToInstall.Count > 0)
                {
                    Debug.Log($"<b>[AI Package Installer]</b> Found {packagesToInstall.Count} missing AI packages. Installing in order...");
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
            Debug.Log("<b>[AI Package Installer]</b> All AI packages installed successfully! ✅");
            return;
        }

        string packageUrl = packagesToInstall.Dequeue();
        Debug.Log($"<b>[AI Package Installer]</b> [{RequiredPackages.Length - packagesToInstall.Count}/{RequiredPackages.Length}] Installing: {packageUrl}...");
        
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
                Debug.Log($"<b>[AI Package Installer]</b> ✅ Successfully installed: {currentRequest.Result.packageId}");
            }
            else if (currentRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"<b>[AI Package Installer]</b> ❌ Failed to install: {currentRequest.Error.message}");
            }

            // Add delay before next installation to let Package Manager settle
            EditorApplication.delayCall += () =>
            {
                System.Threading.Thread.Sleep(1000);
                InstallNextPackage();
            };
        }
    }
}


