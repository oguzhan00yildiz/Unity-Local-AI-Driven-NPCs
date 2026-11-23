using UnityEditor;

namespace AISystem.Editor
{
    /// <summary>
    /// When the DemoQuest scene arrives after setup is done (UPM sample import), asks once whether to
    /// download its bigger models. During first-time setup the setup flow asks instead.
    /// </summary>
    public class DemoModelsImportPrompt : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.EndsWith("/DemoQuest.unity")) continue;
                EditorApplication.delayCall += Ask;
                return;
            }
        }

        private static void Ask()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // First-time setup is still running or not started: it asks before downloading
            bool setupDone = EditorPrefs.GetBool($"AIPackageInstaller.Done.{UnityEngine.Application.dataPath.GetHashCode()}", false);
            if (!setupDone || AISystemSetupWindow.DemoChoiceMade) return;

            AISystemSetupWindow.AskDemoChoiceIfNeeded();
            if (AISystemSetupWindow.DemoModelsMissing())
                AISystemSetupWindow.ChooseDemoModels();
        }
    }
}
