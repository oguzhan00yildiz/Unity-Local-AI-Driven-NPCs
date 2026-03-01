using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Finds TextMeshProUGUI components that are missing their CanvasRenderer
/// and either fixes them or removes them.
/// Run via: Tools → Fix Broken TMP Components
/// </summary>
public static class FixBrokenTMP
{
    [MenuItem("Tools/Fix Broken TMP Components")]
    static void FindAndFix()
    {
        int fixed_count = 0;
        int removed = 0;

        var allTMP = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var tmp in allTMP)
        {
            var cr = tmp.GetComponent<CanvasRenderer>();
            if (cr != null) continue;

            Debug.LogWarning(
                $"[FixBrokenTMP] Missing CanvasRenderer on: {GetPath(tmp.gameObject)}",
                tmp.gameObject
            );

            // If this is a stale/unnamed TMP text with no useful content, remove it.
            bool isEmpty = string.IsNullOrWhiteSpace(tmp.text);
            bool isOrphanedName = tmp.gameObject.name.StartsWith("Text (TMP)") ||
                                  tmp.gameObject.name.StartsWith("Text--");

            if (isEmpty && isOrphanedName)
            {
                Debug.Log($"[FixBrokenTMP] Removing orphaned TMP object: {GetPath(tmp.gameObject)}");
                Undo.DestroyObjectImmediate(tmp.gameObject);
                removed++;
            }
            else
            {
                // Add the missing CanvasRenderer so TMP stops throwing.
                Undo.AddComponent<CanvasRenderer>(tmp.gameObject);
                Debug.Log($"[FixBrokenTMP] Added CanvasRenderer to: {GetPath(tmp.gameObject)}");
                fixed_count++;
            }
        }

        if (fixed_count == 0 && removed == 0)
            Debug.Log("[FixBrokenTMP] No broken TextMeshProUGUI components found. Scene is clean.");
        else
            Debug.Log($"[FixBrokenTMP] Done — Fixed: {fixed_count}, Removed: {removed}");
    }

    [MenuItem("Tools/Report All TMP Components")]
    static void ReportAll()
    {
        var allTMP = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (allTMP.Length == 0)
        {
            Debug.Log("[FixBrokenTMP] No TextMeshProUGUI components in scene.");
            return;
        }

        foreach (var tmp in allTMP)
        {
            var cr = tmp.GetComponent<CanvasRenderer>();
            string status = cr != null ? "OK" : "⚠ MISSING CanvasRenderer";
            Debug.Log($"[TMP] {status} | {GetPath(tmp.gameObject)} | text: \"{tmp.text}\"", tmp.gameObject);
        }
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
