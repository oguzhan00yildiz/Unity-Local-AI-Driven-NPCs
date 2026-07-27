using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Pre-loads Whisper native DLLs by temporarily setting the DLL search
    /// directory to the plugin folder. This ensures ggml.dll and libwhisper.dll
    /// can resolve their own dependencies by name at load time.
    /// </summary>
    public static class NativePluginLoader
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Init()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer)
                return;

            string pluginDir = Path.Combine(Application.dataPath,
                "com.whisper.unity", "Plugins", "Windows");

            if (!Directory.Exists(pluginDir))
            {
                Debug.LogWarning($"[NativePluginLoader] Whisper plugin dir not found: {pluginDir}");
                return;
            }

            // Point Windows DLL search to plugin dir so dependencies resolve by name
            SetDllDirectory(pluginDir);

            string[] deps = { "ggml-base.dll", "ggml-cpu.dll", "ggml-vulkan.dll", "ggml.dll", "libwhisper.dll" };
            foreach (string dll in deps)
            {
                string fullPath = Path.Combine(pluginDir, dll);
                IntPtr handle = LoadLibraryW(fullPath);
                if (handle == IntPtr.Zero)
                    Debug.LogWarning($"[NativePluginLoader] FAILED ({Marshal.GetLastWin32Error()}): {dll}");
                else
                    Debug.Log($"[NativePluginLoader] Loaded: {dll}");
            }

            // Restore default search path
            SetDllDirectory(null);
        }
    }
}
