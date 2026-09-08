using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AISystem.Editor
{
    /// <summary>
    /// Developer-only tools for packaging, exporting, and managing the AI Driven NPCs System.
    /// This file is intentionally placed under Assets/Editor/ so that it is NEVER bundled into
    /// the distributed package (neither UPM nor Asset Store .unitypackage), keeping consumer projects clean.
    /// </summary>
    public static class AIDeveloperTools
    {
        private const int DevMenuPriority = 100;

        /// <summary>
        /// Validates that we are running within the author/development repository
        /// by checking for author-specific files and directories at the project root.
        /// </summary>
        public static bool IsMainDevProject()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return false;

            return Directory.Exists(Path.Combine(projectRoot, "AIPackageInstaller")) &&
                   File.Exists(Path.Combine(projectRoot, "sync.ps1"));
        }

        // ── Validation for Dev Menu Items ───────────────────────────────────────────

        [MenuItem("Tools/AI Packages/Export Complete Asset Store Package (.unitypackage)", true)]
        [MenuItem("Tools/AI Packages/Prepare for Asset Store Upload", true)]
        [MenuItem("Tools/AI Packages/Restore Development Assets", true)]
        [MenuItem("Tools/AI Packages/Internal/Export Content Payload Only (.unitypackage)", true)]
        [MenuItem("Tools/AI Packages/Internal/Unpack Content Package", true)]
        private static bool ValidateDevMenuItems()
        {
            return IsMainDevProject();
        }

        // ── Developer Actions ───────────────────────────────────────────────────────

        [MenuItem("Tools/AI Packages/Export Complete Asset Store Package (.unitypackage)", false, DevMenuPriority)]
        public static void ExportCompleteAssetStorePackage()
        {
            string stagingDir = Path.Combine(Application.dataPath, "AI Driven NPCs System", ".staging~");
            if (Directory.Exists(stagingDir))
            {
                RestoreDevelopmentAssets(false);
            }

            string savePath = EditorUtility.SaveFilePanel(
                "Export Complete Asset Store Package",
                "",
                "AI-Driven-NPCs-System.unitypackage",
                "unitypackage");

            if (string.IsNullOrEmpty(savePath))
                return;

            // 1. Build inner content payload package
            ExportAssetStoreContentPackage(false);

            string contentPkgRelative = "Assets/AI Driven NPCs System/AI-Driven-NPCs-Content.unitypackage";
            string contentPkgFull = Path.Combine(Directory.GetParent(Application.dataPath).FullName, contentPkgRelative);

            if (!File.Exists(contentPkgFull))
            {
                EditorUtility.DisplayDialog("Export Failed", "Could not generate inner content package: " + contentPkgRelative, "OK");
                return;
            }

            // 2. Export complete outer package (Editor + Content Payload + Docs)
            string[] outerPaths = new[]
            {
                "Assets/AI Driven NPCs System/Editor",
                contentPkgRelative,
                "Assets/AI Driven NPCs System/README.md",
                "Assets/AI Driven NPCs System/SETUP_GUIDE_EN.md"
            };

            List<string> validPaths = new List<string>();
            foreach (string p in outerPaths)
            {
                string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, p);
                if (File.Exists(full) || Directory.Exists(full))
                {
                    validPaths.Add(p);
                }
            }

            AssetDatabase.ExportPackage(validPaths.ToArray(), savePath, ExportPackageOptions.Recurse);

            // 3. Clean up the temporary inner package from local Assets
            if (File.Exists(contentPkgFull))
            {
                AssetDatabase.DeleteAsset(contentPkgRelative);
            }

            AssetDatabase.Refresh();

            Debug.Log($"<b>[AI Developer Tools]</b> ✅ Complete Asset Store package exported to: {savePath}");
            EditorUtility.DisplayDialog("Export Complete",
                $"Complete Asset Store Package exported successfully to:\n{savePath}\n\n" +
                "This package contains:\n" +
                "• Editor/ (Setup window & automated dependency installer)\n" +
                "• AI-Driven-NPCs-Content.unitypackage (Self-extracting payload with Scenes, Prefabs, Scripts)\n" +
                "• Documentation\n\n" +
                "When imported into any project (such as AITest), it will import cleanly with 0 compile errors and immediately launch the AI System Setup window!",
                "OK");
        }

        [MenuItem("Tools/AI Packages/Prepare for Asset Store Upload", false, DevMenuPriority + 1)]
        public static void PrepareForAssetStoreUpload()
        {
            PrepareForAssetStoreUpload(true);
        }

        public static void PrepareForAssetStoreUpload(bool interactive)
        {
            ExportAssetStoreContentPackage(false);

            string stagingDir = Path.Combine(Application.dataPath, "AI Driven NPCs System", ".staging~");
            if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);

            string[] folders = new[] { "Prefabs", "Resources", "Scenes", "Scripts" };
            foreach (string f in folders)
            {
                string src = Path.Combine(Application.dataPath, "AI Driven NPCs System", f);
                string dst = Path.Combine(stagingDir, f);
                SafeMoveDirectory(src, dst);

                string metaSrc = src + ".meta";
                string metaDst = dst + ".meta";
                SafeMoveFile(metaSrc, metaDst);
            }

            AssetDatabase.Refresh();
            if (interactive)
            {
                EditorUtility.DisplayDialog("Ready for Asset Store Upload",
                    "Assets/AI Driven NPCs System is now ready for upload!\n\n" +
                    "It now contains ONLY:\n" +
                    "• Editor/ (Installer & Setup)\n" +
                    "• AI-Driven-NPCs-Content.unitypackage (Self-extracting payload)\n" +
                    "• Documentation (README & Setup Guide)\n\n" +
                    "You can now run the Publisher Tool on 'Assets/AI Driven NPCs System'.\n\n" +
                    "When you are done uploading, click Tools → AI Packages → Restore Development Assets.", "OK");
            }
        }

        [MenuItem("Tools/AI Packages/Restore Development Assets", false, DevMenuPriority + 2)]
        public static void RestoreDevelopmentAssets()
        {
            RestoreDevelopmentAssets(true);
        }

        public static void RestoreDevelopmentAssets(bool interactive)
        {
            string stagingDir = Path.Combine(Application.dataPath, "AI Driven NPCs System", ".staging~");
            if (!Directory.Exists(stagingDir))
            {
                if (interactive)
                    EditorUtility.DisplayDialog("Restore Development Assets", "No staging folder found. Assets are already in place.", "OK");
                return;
            }

            string[] folders = new[] { "Prefabs", "Resources", "Scenes", "Scripts" };
            foreach (string f in folders)
            {
                string src = Path.Combine(stagingDir, f);
                string dst = Path.Combine(Application.dataPath, "AI Driven NPCs System", f);
                SafeMoveDirectory(src, dst);

                string metaSrc = src + ".meta";
                string metaDst = dst + ".meta";
                SafeMoveFile(metaSrc, metaDst);
            }

            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, true); } catch { }
            }
            AssetDatabase.Refresh();
            Debug.Log("<b>[AI Developer Tools]</b> ✅ Development assets restored.");
            if (interactive)
                EditorUtility.DisplayDialog("Restore Complete", "Development assets restored successfully!", "OK");
        }

        [MenuItem("Tools/AI Packages/Internal/Export Content Payload Only (.unitypackage)", false, DevMenuPriority + 20)]
        public static void ExportAssetStoreContentPackage()
        {
            ExportAssetStoreContentPackage(true);
        }

        public static void ExportAssetStoreContentPackage(bool interactive)
        {
            string stagingDir = Path.Combine(Application.dataPath, "AI Driven NPCs System", ".staging~");
            if (Directory.Exists(stagingDir))
            {
                RestoreDevelopmentAssets(false);
            }

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
                if (interactive)
                    EditorUtility.DisplayDialog("Export Content Package", "No source folders found in Assets/AI Driven NPCs System/ to export.", "OK");
                return;
            }

            AssetDatabase.ExportPackage(validPaths.ToArray(), packagePath, ExportPackageOptions.Recurse);
            AssetDatabase.Refresh();
            Debug.Log($"<b>[AI Developer Tools]</b> ✅ Exported content package to: {packagePath}");
            if (interactive)
                EditorUtility.DisplayDialog("Export Complete", $"Content package successfully exported to:\n{packagePath}", "OK");
        }

        [MenuItem("Tools/AI Packages/Internal/Unpack Content Package", false, DevMenuPriority + 21)]
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
                    Debug.Log($"<b>[AI Developer Tools]</b> 📦 Unpacking content package: {relPath}...");
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

        private static void SafeMoveDirectory(string src, string dst)
        {
            if (!Directory.Exists(src)) return;
            if (!Directory.Exists(dst)) Directory.CreateDirectory(dst);

            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFile = Path.Combine(dst, rel);
                string targetDir = Path.GetDirectoryName(targetFile);
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                if (File.Exists(targetFile))
                {
                    File.SetAttributes(targetFile, FileAttributes.Normal);
                    File.Delete(targetFile);
                }

                int retries = 3;
                while (retries > 0)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Move(file, targetFile);
                        break;
                    }
                    catch (IOException)
                    {
                        retries--;
                        if (retries == 0) throw;
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }

            try { Directory.Delete(src, true); } catch { }
        }

        private static void SafeMoveFile(string src, string dst)
        {
            if (!File.Exists(src)) return;
            if (File.Exists(dst))
            {
                File.SetAttributes(dst, FileAttributes.Normal);
                File.Delete(dst);
            }
            File.SetAttributes(src, FileAttributes.Normal);
            File.Move(src, dst);
        }
    }
}

