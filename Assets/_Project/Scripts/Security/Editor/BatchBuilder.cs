#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BatchBuilder
{
    [MenuItem("Tools/Build/Linux x86_64")]
    public static void BuildLinux()
    {
        // Re-encrypt reactions first to ensure key consistency
        ReactionPacker.Encrypt();

        string[] scenes = new string[]
        {
            "Assets/Boot.unity",
            "Assets/_Project/Scenes/Menu.unity",
            "Assets/Lab Scene.unity"
        };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Linux/ChemLabSim.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BatchBuilder] Linux build succeeded: {summary.totalSize} bytes, time: {summary.totalTime}");
        }
        else
        {
            Debug.LogError($"[BatchBuilder] Linux build failed: {summary.result}");
            EditorApplication.Exit(1);
        }
    }
}
#endif
