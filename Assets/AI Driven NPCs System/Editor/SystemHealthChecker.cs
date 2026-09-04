using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

namespace AISystem.Editor
{
    public class SystemHealthChecker : EditorWindow
    {
    [MenuItem("Tools/AI Packages/System Health & GPU")]
    public static void ShowWindow()
    {
        GetWindow<SystemHealthChecker>("System Health");
    }

    private void OnGUI()
    {
        GUILayout.Label("System Health & GPU Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 1. Hardware Check
        GUILayout.Label("Hardware Information", EditorStyles.boldLabel);
        
        string gpuName = SystemInfo.graphicsDeviceName;
        int vram = SystemInfo.graphicsMemorySize;
        int sysRam = SystemInfo.systemMemorySize;
        int cpuCores = SystemInfo.processorCount;

        EditorGUILayout.LabelField("GPU:", $"{gpuName} ({vram} MB VRAM)");
        EditorGUILayout.LabelField("CPU:", $"{cpuCores} threads");
        EditorGUILayout.LabelField("RAM:", $"{sysRam} MB");

        EditorGUILayout.Space();
        if (vram < 4000)
        {
            EditorGUILayout.HelpBox("Your VRAM is below 4GB. You may experience slow performance with larger models. Recommend using small quantized models.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("Your hardware looks well-suited for running local models.", MessageType.Info);
        }

        EditorGUILayout.Space();
        DrawLine();
        EditorGUILayout.Space();

        // 2. DLL Check
        GUILayout.Label("Dependencies (Whisper/LLM)", EditorStyles.boldLabel);
        bool hasWhisper = File.Exists(Path.Combine(Application.dataPath, "com.whisper.unity/Plugins/Windows/libwhisper.dll"));
        
        if (hasWhisper)
        {
            EditorGUILayout.LabelField("Whisper DLLs:", "✅ Found");
        }
        else
        {
            EditorGUILayout.LabelField("Whisper DLLs:", "❌ Missing (Did package install correctly?)");
        }

        EditorGUILayout.Space();
        DrawLine();
        EditorGUILayout.Space();

        // 3. GPU Toggle
        GUILayout.Label("GPU Acceleration (LLM)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("By default, LLM calculations run on the CPU. Enabling GPU offloads layers to your graphics card for a massive speedup.", MessageType.None);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Enable GPU (All LLMs)", GUILayout.Height(30)))
        {
            SetGPULayers(99);
        }
        if (GUILayout.Button("Disable GPU (CPU Only)", GUILayout.Height(30)))
        {
            SetGPULayers(0);
        }
        GUILayout.EndHorizontal();
    }

    private void SetGPULayers(int layers)
    {
        var llmType = System.Type.GetType("LLMUnity.LLM, undream.llmunity.Runtime");
        if (llmType == null)
        {
            Debug.LogWarning("[System Health] LLMUnity package not found or not compiled.");
            return;
        }

        int updatedCount = 0;

        // Update in active scenes
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var rootGo in scene.GetRootGameObjects())
            {
                var components = rootGo.GetComponentsInChildren(llmType, true);
                foreach (var comp in components)
                {
                    UpdateLLMGpuLayers(comp, llmType, layers);
                    updatedCount++;
                }
            }
        }

        // Update in prefabs
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var comp = prefab.GetComponent(llmType);
            if (comp != null)
            {
                UpdateLLMGpuLayers(comp, llmType, layers);
                EditorUtility.SetDirty(prefab);
                updatedCount++;
            }
        }

        AssetDatabase.SaveAssets();

        if (updatedCount > 0)
        {
            Debug.Log($"[System Health] ✅ Updated {updatedCount} LLM components to use {layers} GPU layers.");
        }
        else
        {
            Debug.LogWarning("[System Health] No LLM components found to update.");
        }
    }

    private void UpdateLLMGpuLayers(object component, System.Type llmType, int layers)
    {
        try
        {
            var field = llmType.GetField("_numGPULayers", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, layers);
            }
            
            var prop = llmType.GetProperty("numGPULayers", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(component, layers);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[System Health] Failed to set GPU layers: {ex.Message}");
        }
    }

    private void DrawLine()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f, 0.5f));
    }
}
}
