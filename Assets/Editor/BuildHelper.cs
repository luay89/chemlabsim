using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildHelper
{
    public static void BuildAndroid()
    {
        string[] scenes = new string[]
        {
            "Assets/Boot.unity",
            "Assets/_Project/Scenes/Menu.unity",
            "Assets/Lab Scene.unity"
        };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Android/ChemLabSim.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Android build succeeded: " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("Android build failed");
            EditorApplication.Exit(1);
        }
    }
}
