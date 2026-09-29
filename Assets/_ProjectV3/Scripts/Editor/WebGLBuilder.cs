// Batch-mode helper: build WebGL player for ChemLabSim v3.
// Usage: Unity -quit -batchmode -nographics -buildTarget WebGL -executeMethod ChemLabSimV3.Editor.WebGLBuilder.Build
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChemLabSimV3.Editor
{
    public static class WebGLBuilder
    {
        public static void Build()
        {
            string dataPath = UnityEngine.Application.dataPath;
            string scenePath = "Assets/Boot.unity";
            string fullPath = System.IO.Path.Combine(dataPath, "../", scenePath);

            var scenes = new System.Collections.Generic.List<string>();

            if (System.IO.File.Exists(fullPath))
                scenes.Add(scenePath);

            string labV3Scene = "Assets/_ProjectV3/Scenes/LabV3.unity";
            if (System.IO.File.Exists(System.IO.Path.Combine(dataPath, "../", labV3Scene)))
                scenes.Add(labV3Scene);

            string labScene = "Assets/Lab Scene.unity";
            if (System.IO.File.Exists(System.IO.Path.Combine(dataPath, "../", labScene)))
                scenes.Add(labScene);

            if (scenes.Count == 0)
            {
                Debug.LogError("[WebGLBuilder] No scenes found! Add at least one scene to Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[WebGLBuilder] Building with {scenes.Count} scene(s): {string.Join(", ", scenes)}");

            string buildDir = "Builds/WebGL-V3";
            System.IO.Directory.CreateDirectory(buildDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = buildDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuilder] BUILD SUCCEEDED — {report.summary.totalSize / (1024*1024)} MB");
            }
            else
            {
                Debug.LogError($"[WebGLBuilder] BUILD FAILED: {report.summary.result} — {report.summary.totalErrors} error(s)");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
