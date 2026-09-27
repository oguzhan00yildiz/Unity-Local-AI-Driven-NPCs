using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Pre-loads Whisper native DLLs by setting the DLL search directory
    /// and pre-resolving dependencies. In standalone builds or custom paths,
    /// this ensures ggml.dll and libwhisper.dll can resolve their dependencies.
    /// In Unity Editor, Unity's internal native plugin manager handles binding.
    /// </summary>
    public static class NativePluginLoader
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);

        private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Init()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer)
                return;

            string pluginDir = FindPluginDirectory();
            if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir))
                return;

            try
            {
                AddDllDirectory(pluginDir);
                SetDllDirectory(pluginDir);
            }
            catch
            {
                // Fallback for environments where dynamic directory registration is restricted
            }

            string[] deps = { "ggml-base.dll", "ggml-cpu.dll", "ggml-vulkan.dll", "ggml.dll", "libwhisper.dll" };
            foreach (string dll in deps)
            {
                if (GetModuleHandleW(dll) != IntPtr.Zero)
                    continue;

                string fullPath = Path.Combine(pluginDir, dll);
                if (!File.Exists(fullPath))
                    continue;

                IntPtr handle = LoadLibraryExW(fullPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
                if (handle != IntPtr.Zero)
                {
                    Debug.Log($"[NativePluginLoader] Loaded: {dll}");
                }
                else if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    // In Editor, Unity's native plugin importer automatically binds DLLs on demand,
                    // so warnings are only actionable for standalone builds.
                    Debug.LogWarning($"[NativePluginLoader] FAILED ({Marshal.GetLastWin32Error()}): {dll}");
                }
            }

            try
            {
                SetDllDirectory(null);
            }
            catch
            {
            }
        }

        private static string FindPluginDirectory()
        {
            // Standalone Player paths
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                string playerPlugins64 = Path.Combine(Application.dataPath, "Plugins", "x86_64");
                if (Directory.Exists(playerPlugins64)) return playerPlugins64;

                string playerPlugins = Path.Combine(Application.dataPath, "Plugins");
                if (Directory.Exists(playerPlugins)) return playerPlugins;
            }

            // Direct Assets folder
            string assetPlugins = Path.Combine(Application.dataPath, "com.whisper.unity", "Plugins", "Windows");
            if (Directory.Exists(assetPlugins)) return assetPlugins;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;

            // Packages folder
            string packagesDir = Path.Combine(projectRoot, "Packages", "com.whisper.unity", "Plugins", "Windows");
            if (Directory.Exists(packagesDir)) return packagesDir;

            // Library/PackageCache
            string packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
            if (Directory.Exists(packageCache))
            {
                var matches = Directory.GetDirectories(packageCache, "com.whisper.unity*");
                if (matches.Length > 0)
                {
                    string candidate = Path.Combine(matches[0], "Plugins", "Windows");
                    if (Directory.Exists(candidate)) return candidate;
                }
            }

            return null;
        }
    }
}
