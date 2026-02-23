using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

/// <summary>
/// One-click exporter for LLM and Whisper packages via Git URLs
/// </summary>
public class PackageExporter : EditorWindow
{
    private string whisperGitUrl = "https://github.com/";
    private bool showAdvanced = false;

    [MenuItem("Tools/AI Packages/Export AI Packages")]
    public static void ShowWindow()
    {
        GetWindow<PackageExporter>("Export AI Packages");
    }

    private void OnGUI()
    {
        GUILayout.Label("AI Packages - Git URL Exporter", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("This will generate git package URLs for Package Manager import:", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        GUILayout.Label("Packages to Export:", EditorStyles.boldLabel);
        GUILayout.Label("✓ LLMUnity (ai.undream.llm)", EditorStyles.label);
        GUILayout.Label("✓ Piper TTS (ai.lookbe.piper)", EditorStyles.label);
        GUILayout.Label("✓ Whisper Unity", EditorStyles.label);

        GUILayout.Space(20);

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Options");
        if (showAdvanced)
        {
            GUILayout.Label("Whisper Git URL:", EditorStyles.label);
            whisperGitUrl = EditorGUILayout.TextField(whisperGitUrl);
            GUILayout.Label("Leave empty to skip Whisper or enter the git repository URL", EditorStyles.miniLabel);
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Generate Import Script", GUILayout.Height(40)))
        {
            ExportPackages();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Show in Explorer", GUILayout.Height(30)))
        {
            ShowExportFolder();
        }
    }

    private void ExportPackages()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string exportPath = EditorUtility.SaveFolderPanel("Select Export Location", projectRoot, "AI_Packages_Export");

        if (string.IsNullOrEmpty(exportPath))
        {
            EditorUtility.DisplayDialog("Export Cancelled", "Export was cancelled.", "OK");
            return;
        }

        try
        {
            Directory.CreateDirectory(exportPath);

            // Read current manifest
            string manifestSource = Path.Combine(projectRoot, "Packages", "manifest.json");
            Dictionary<string, string> aiPackages = ExtractAIPackages(manifestSource);

            // Create import instructions
            CreateImportScript(exportPath, aiPackages);

            EditorUtility.DisplayDialog("Export Complete", 
                $"Import script generated successfully!\n\n{exportPath}\n\nUse ImportPackages.bat to import into another project.", "OK");
            ShowExportFolder();
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Export Error", $"Error during export:\n{ex.Message}", "OK");
            UnityEngine.Debug.LogError($"Export failed: {ex}");
        }
    }

    private Dictionary<string, string> ExtractAIPackages(string manifestPath)
    {
        Dictionary<string, string> aiPackages = new Dictionary<string, string>();

        if (!File.Exists(manifestPath))
        {
            UnityEngine.Debug.LogWarning("manifest.json not found");
            return aiPackages;
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            JObject manifest = JObject.Parse(json);
            JObject dependencies = (JObject)manifest["dependencies"];

            if (dependencies != null)
            {
                // Extract all git packages
                foreach (var dep in dependencies.Children())
                {
                    var property = dep as JProperty;
                    if (property != null && property.Value.Type == JTokenType.String)
                    {
                        string value = property.Value.ToString();
                        // Only include git URLs
                        if (value.Contains("github.com") && value.Contains(".git"))
                        {
                            aiPackages.Add(property.Name, value);
                            UnityEngine.Debug.Log($"Found: {property.Name} -> {value}");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Error reading manifest: {ex}");
        }

        return aiPackages;
    }

    private void CreateImportScript(string exportPath, Dictionary<string, string> aiPackages)
    {
        // Build manifest content
        string manifestContent = BuildManifestJson(aiPackages);

        // Create PowerShell import script
        string psScript = $@"# AI Packages Import Script (Git URLs)
# This script automatically adds AI packages to a Unity project via Package Manager

param(
    [Parameter(Mandatory=$true)]
    [string]$TargetProjectPath
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""========================================""
Write-Host ""AI Packages Importer (Git URLs)""
Write-Host ""========================================""

# Check if target project exists
if (-not (Test-Path $TargetProjectPath)) {{
    Write-Host ""Error: Target project path does not exist!"" -ForegroundColor Red
    exit 1
}}

$projectPackagesPath = Join-Path $TargetProjectPath ""Packages""
$targetManifest = Join-Path $projectPackagesPath ""manifest.json""

if (-not (Test-Path $projectPackagesPath)) {{
    Write-Host ""Error: Packages folder not found!"" -ForegroundColor Red
    exit 1
}}

Write-Host ""Target Project: $TargetProjectPath"" -ForegroundColor Green

# Backup existing manifest
if (Test-Path $targetManifest) {{
    $backupPath = ""$($targetManifest).backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')""
    Copy-Item $targetManifest $backupPath
    Write-Host ""Backup created: $backupPath""
}}

# Read existing manifest
$existingManifest = @{{}}
if (Test-Path $targetManifest) {{
    $json = Get-Content $targetManifest -Raw
    $existingManifest = $json | ConvertFrom-Json -AsHashtable
}}

# Prepare new packages to add
$newPackages = @{{
{string.Join(",\r\n", aiPackages.Select(kvp => $@"    ""{kvp.Key}"" = ""{kvp.Value}"""))}
}}

Write-Host ""Adding packages:""
foreach ($pkg in $newPackages.GetEnumerator()) {{
    Write-Host ""  - $($pkg.Name) : $($pkg.Value)""
}}

# Merge dependencies
if ($existingManifest.dependencies -eq $null) {{
    $existingManifest.dependencies = @{{}}
}}

foreach ($pkg in $newPackages.GetEnumerator()) {{
    $existingManifest.dependencies[$pkg.Name] = $pkg.Value
    Write-Host ""✓ Added $($pkg.Name)""
}}

# Write updated manifest
$manifestJson = $existingManifest | ConvertTo-Json -Depth 10
Set-Content -Path $targetManifest -Value $manifestJson -Encoding UTF8

Write-Host """"
Write-Host ""========================================""
Write-Host ""Import Complete!"" -ForegroundColor Green
Write-Host ""========================================""
Write-Host ""Open the target project in Unity to download packages.""
Write-Host ""The Package Manager will automatically resolve git URLs.""
"";

        string psPath = Path.Combine(exportPath, "ImportPackages.ps1");
        File.WriteAllText(psPath, psScript);
        UnityEngine.Debug.Log("✓ Created ImportPackages.ps1");

        // Create batch file
        string batScript = $@"@echo off
REM AI Packages Import Tool (Git URLs)

setlocal enabledelayedexpansion

set ""scriptDir=%~dp0""
set ""psScript=%scriptDir%ImportPackages.ps1""

echo.
echo ========================================
echo AI Packages Importer (Git URLs)
echo ========================================
echo.
echo Enter the full path to your target Unity project
echo Example: C:\Users\YourName\Projects\MyUnityProject
echo.

set /p targetPath=""Target Project Path: ""

if not exist ""!targetPath!"" (
    echo Error: Path does not exist!
    pause
    exit /b 1
)

if not exist ""!targetPath!\Packages"" (
    echo Error: Packages folder not found in target project!
    pause
    exit /b 1
)

echo.
echo Importing packages from git URLs...
echo.

powershell -ExecutionPolicy Bypass -File ""!psScript!"" -TargetProjectPath ""!targetPath!""

REM Keep window open to see results
pause
";

        string batPath = Path.Combine(exportPath, "ImportPackages.bat");
        File.WriteAllText(batPath, batScript);
        UnityEngine.Debug.Log("✓ Created ImportPackages.bat");

        // Create README
        string readme = $@"# AI Packages - Git URLs Importer

This folder contains scripts to import AI packages via Unity Package Manager git URLs.

## Included Packages

";

        foreach (var pkg in aiPackages)
        {
            readme += $"- **{pkg.Key}**: {pkg.Value}\n";
        }

        readme += $@"

## Quick Import (Recommended)

1. Run **ImportPackages.bat** (double-click)
2. Enter the path to your target project
3. Open the project in Unity
4. Wait for Package Manager to download and resolve packages
5. Done!

## Manual Import

1. Open your target project in Unity
2. Go to **Window → TextMesh Pro → Import TMP Essentials** (if needed)
3. Edit **Packages/manifest.json** and add these dependencies:

```json
""dependencies"": {{
{string.Join(",\n", aiPackages.Select(kvp => $@"    ""{kvp.Key}"": ""{kvp.Value}"""))}
}}
```

4. Save and Unity will automatically download packages

## What Happens

- The importer modifies your target project's `Packages/manifest.json`
- Unity Package Manager automatically clones git repositories
- No local files are copied - everything comes from git
- A backup of the original manifest is created

## Troubleshooting

- **Git not found**: Make sure Git is installed and added to PATH
- **Network errors**: Check your internet connection
- **Permission denied**: Run PowerShell as Administrator
- **Packages not found**: Check git repository URLs in the script

## Git Repository URLs

The following git repositories will be added:

{string.Join("\n", aiPackages.Select(kvp => $"- {kvp.Value}"))}

If any URL is incorrect or inaccessible, edit **ImportPackages.ps1** before running.

---
Generated by PackageExporter v2 (Git URL Mode)
";

        string readmePath = Path.Combine(exportPath, "README.md");
        File.WriteAllText(readmePath, readme);
        UnityEngine.Debug.Log("✓ Created README.md");
    }

    private string BuildManifestJson(Dictionary<string, string> aiPackages)
    {
        var manifest = new JObject();
        var dependencies = new JObject();

        foreach (var pkg in aiPackages)
        {
            dependencies[pkg.Key] = pkg.Value;
        }

        manifest["dependencies"] = dependencies;
        return manifest.ToString();
    }

    private void ShowExportFolder()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string lastExportKey = "AI_Package_Export_Path";

        string folderPath = EditorPrefs.GetString(lastExportKey, projectRoot);

        if (Directory.Exists(folderPath))
        {
            Process.Start("explorer.exe", folderPath.Replace("/", "\\"));
        }
        else
        {
            EditorUtility.DisplayDialog("Not Found", "Export folder not found. Please export packages first.", "OK");
        }
    }
}


