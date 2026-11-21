using UnityEditor;

namespace AISystem.Editor
{
    /// <summary>
    /// When the CompanionDemo scene arrives in the project (UPM sample import) and its bigger models
    /// are missing, asks once whether to download them in the setup window.
    /// </summary>
    public class DemoModelsImportPrompt : AssetPostprocessor
    {
        private const string AskedKey = "AISystem.DemoModelsAsked";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.EndsWith("/CompanionDemo.unity")) continue;
                EditorApplication.delayCall += Ask;
                return;
            }
        }

        private static void Ask()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AskedKey, false)) return;
            if (!AISystemSetupWindow.DemoModelsMissing()) return;
            SessionState.SetBool(AskedKey, true);

            bool download = EditorUtility.DisplayDialog("Companion demo models",
                "The Companion demo uses bigger models: Qwen3.5 4B (2.7 GB), Whisper small.en (490 MB) and 6 extra voices (380 MB).\n\n" +
                "Download them now?",
                "Download", "Later");
            if (download)
                AISystemSetupWindow.AutoStartDownloads();
        }
    }
}
