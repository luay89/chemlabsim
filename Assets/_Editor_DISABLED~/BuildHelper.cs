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
            "Assets/_ProjectV3/Scenes/LabV3.unity"
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

    public static void BuildWebGL()
    {
        string[] scenes = new string[]
        {
            "Assets/Boot.unity",
            "Assets/_Project/Scenes/Menu.unity",
            "Assets/_ProjectV3/Scenes/LabV3.unity"
        };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("WebGL build succeeded: " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("WebGL build failed");
            EditorApplication.Exit(1);
        }
    }

    public static void BuildLinuxV3()
    {
        string[] scenes = new string[]
        {
            "Assets/Boot.unity",
            "Assets/_Project/Scenes/Menu.unity",
            "Assets/_ProjectV3/Scenes/LabV3.unity"
        };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Linux-V3/ChemLabSim.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Linux V3 build succeeded: " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("Linux V3 build failed");
            EditorApplication.Exit(1);
        }
    }
}
