// Linux desktop build of LabV3 only, used to preview the lab and to run the screenshot tour.
// Batch: Unity -batchmode -quit -projectPath . -buildTarget Linux64 -executeMethod ChemLabSimV3.Editor.LinuxPreviewBuild.Build -logFile -
// Run:   Builds/LinuxPreview/ChemLabSim.x86_64 -screen-width 2400 -screen-height 1080 -chemlab-tour <folder>
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChemLabSimV3.Editor
{
    public static class LinuxPreviewBuild
    {
        private const string OutputPath = "Builds/LinuxPreview/ChemLabSim.x86_64";

        public static void Build()
        {
            AndroidBuild.EnsureRuntimeShaderMaterials();
            AndroidBuild.ApplyBranding();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_ProjectV3/Scenes/LabV3.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[LinuxPreviewBuild] BUILD SUCCEEDED — {OutputPath}");
            }
            else
            {
                Debug.LogError($"[LinuxPreviewBuild] BUILD FAILED: {report.summary.result} — {report.summary.totalErrors} error(s)");
                if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
