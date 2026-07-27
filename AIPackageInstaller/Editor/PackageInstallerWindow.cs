using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PackageInstallerWindow : EditorWindow
{
    public enum StepStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed
    }

    public class InstallStep
    {
        public string Name;
        public string Description;
        public StepStatus Status = StepStatus.Pending;
        public string ErrorMessage;
    }

    private static PackageInstallerWindow _instance;
    public static List<InstallStep> Steps { get; } = new List<InstallStep>();

    public static void ShowWindow()
    {
        _instance = GetWindow<PackageInstallerWindow>(true, "AI Package Installation", true);
        _instance.minSize = new Vector2(450, 320);
        _instance.maxSize = new Vector2(550, 400);
        _instance.ShowUtility();
        _instance.Focus();
    }

    public static void CloseWindow()
    {
        if (_instance != null)
        {
            _instance.Close();
            _instance = null;
        }
    }

    public static void InitSteps(List<InstallStep> initialSteps)
    {
        Steps.Clear();
        Steps.AddRange(initialSteps);
        if (_instance != null)
        {
            _instance.Repaint();
        }
    }

    public static void UpdateStep(string name, StepStatus status, string error = null)
    {
        var step = Steps.Find(s => s.Name == name);
        if (step != null)
        {
            step.Status = status;
            step.ErrorMessage = error;
        }
        else
        {
            Steps.Add(new InstallStep { Name = name, Status = status, ErrorMessage = error });
        }

        if (_instance != null)
        {
            _instance.Repaint();
        }
    }

    private void OnEnable()
    {
        _instance = this;
        EditorApplication.update += ForceRepaint;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ForceRepaint;
        if (_instance == this)
            _instance = null;
    }

    private void ForceRepaint()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Installing AI Dependencies", headerStyle);
        EditorGUILayout.LabelField("Please wait while required packages and registries are configured...", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(12);

        int completed = 0;
        foreach (var s in Steps)
        {
            if (s.Status == StepStatus.Completed) completed++;
        }

        float progress = Steps.Count > 0 ? (float)completed / Steps.Count : 0f;
        Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
        EditorGUI.ProgressBar(progressRect, progress, $"Installing ({completed}/{Steps.Count})");
        EditorGUILayout.Space(15);

        foreach (var step in Steps)
        {
            DrawStepRow(step);
        }

        EditorGUILayout.Space(10);
    }

    private void DrawStepRow(InstallStep step)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            string icon;
            switch (step.Status)
            {
                case StepStatus.InProgress:
                    int dots = (int)(EditorApplication.timeSinceStartup * 4) % 4;
                    icon = "⏳" + new string('.', dots);
                    break;
                case StepStatus.Completed:
                    icon = "✅";
                    break;
                case StepStatus.Failed:
                    icon = "❌";
                    break;
                default:
                    icon = "⚪";
                    break;
            }

            EditorGUILayout.LabelField(icon, GUILayout.Width(35));

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(step.Name, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(step.ErrorMessage))
                {
                    GUIStyle errStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } };
                    EditorGUILayout.LabelField(step.ErrorMessage, errStyle);
                }
                else if (!string.IsNullOrEmpty(step.Description))
                {
                    EditorGUILayout.LabelField(step.Description, EditorStyles.miniLabel);
                }
            }
        }
    }
}
