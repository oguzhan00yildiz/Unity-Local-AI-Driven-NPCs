using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class InitModel : MonoBehaviour
{
    public PiperTTS.PiperTTS tts;

    const string AmyModelPath = "Assets/StreamingAssets/PiperTTS/Amy/en_US-amy-low.onnx";
    const string AmyConfigPath = "Assets/StreamingAssets/PiperTTS/Amy/en_US-amy-low.onnx.json";
    const string IbrahimModelPath = "Assets/StreamingAssets/PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx";
    const string IbrahimConfigPath = "Assets/StreamingAssets/PiperTTS/ibrahim/en_US-reza_ibrahim-medium.onnx.json";
    const string PhonemizerModelPath = "Assets/StreamingAssets/PiperTTS/model.onnx";
    const string PhonemizerConfigPath = "Assets/StreamingAssets/PiperTTS/tokenizer.json";
    const string PhonemizerDictPath = "Assets/StreamingAssets/PiperTTS/phoneme_dict.json";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        tts.piperModelPath = ResolveFilePath(GetAbsolutePath(tts.piperModelPath), null, "*.onnx", "model.onnx");
        string piperConfigCandidate = GetDefaultPiperConfigName(tts.piperModelPath);
        tts.piperConfigPath = ResolveFilePath(GetAbsolutePath(tts.piperConfigPath), piperConfigCandidate, "*.onnx.json", null);

        tts.phonemizerModelPath = ResolveFilePath(GetAbsolutePath(tts.phonemizerModelPath), "model.onnx", "model.onnx", null);
        tts.phonemizerConfigPath = ResolveFilePath(GetAbsolutePath(tts.phonemizerConfigPath), "tokenizer.json", "tokenizer.json", null);
        tts.phonemizerDictPath = ResolveFilePath(GetAbsolutePath(tts.phonemizerDictPath), "phoneme_dict.json", "phoneme_dict.json", null);

        tts.InitModel();
    }

    string GetAbsolutePath(string filepath)
    {
        if (Path.IsPathRooted(filepath))
        {
            return filepath;
        }

        string normalized = filepath.Replace('\\', '/');
        const string assetsStreamingPrefix = "Assets/StreamingAssets/";
        const string streamingPrefix = "StreamingAssets/";

        if (normalized.StartsWith(assetsStreamingPrefix))
        {
            string relative = normalized.Substring(assetsStreamingPrefix.Length);
            return Path.Join(Application.streamingAssetsPath, relative);
        }

        if (normalized.StartsWith(streamingPrefix))
        {
            string relative = normalized.Substring(streamingPrefix.Length);
            return Path.Join(Application.streamingAssetsPath, relative);
        }

        return Path.Join(Application.streamingAssetsPath, filepath);
    }

    string ResolveFilePath(string pathOrDir, string preferredFileName, string searchPattern, string excludeFileName)
    {
        if (File.Exists(pathOrDir))
        {
            return pathOrDir;
        }

        if (Directory.Exists(pathOrDir))
        {
            if (!string.IsNullOrEmpty(preferredFileName))
            {
                string preferredPath = Path.Combine(pathOrDir, preferredFileName);
                if (File.Exists(preferredPath))
                {
                    return preferredPath;
                }
            }

            if (!string.IsNullOrEmpty(searchPattern))
            {
                string[] files = Directory.GetFiles(pathOrDir, searchPattern);
                for (int i = 0; i < files.Length; i++)
                {
                    string fileName = Path.GetFileName(files[i]);
                    if (!string.Equals(fileName, excludeFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return files[i];
                    }
                }
            }
        }

        return pathOrDir;
    }

    string GetDefaultPiperConfigName(string piperModelPath)
    {
        if (string.IsNullOrEmpty(piperModelPath))
        {
            return null;
        }

        string modelFileName = Path.GetFileName(piperModelPath);
        if (string.IsNullOrEmpty(modelFileName))
        {
            return null;
        }

        return modelFileName + ".json";
    }

    [ContextMenu("Preset/Use Amy")]
    public void ApplyAmyPreset()
    {
        if (tts == null)
        {
            return;
        }

        tts.piperModelPath = AmyModelPath;
        tts.piperConfigPath = AmyConfigPath;
        ApplyCommonPhonemizerPaths();
    }

    [ContextMenu("Preset/Use Ibrahim")]
    public void ApplyIbrahimPreset()
    {
        if (tts == null)
        {
            return;
        }

        tts.piperModelPath = IbrahimModelPath;
        tts.piperConfigPath = IbrahimConfigPath;
        ApplyCommonPhonemizerPaths();
    }

    void ApplyCommonPhonemizerPaths()
    {
        tts.phonemizerModelPath = PhonemizerModelPath;
        tts.phonemizerConfigPath = PhonemizerConfigPath;
        tts.phonemizerDictPath = PhonemizerDictPath;
    }
}
