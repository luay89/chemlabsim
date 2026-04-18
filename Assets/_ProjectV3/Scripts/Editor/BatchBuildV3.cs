// Batch-mode helper: generate LabV3 scene + build Linux player.
// Usage: Unity -batchmode -executeMethod ChemLabSimV3.Editor.BatchBuildV3.Run -quit -logFile -
// Delete after use.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChemLabSimV3.Editor
{
    public static class BatchBuildV3
    {
        public static void Run()
        {
            // 1) Generate LabV3 scene if missing
            string scenePath = "Assets/_ProjectV3/Scenes/LabV3.unity";
            string fullPath = System.IO.Path.Combine(Application.dataPath, "../", scenePath);
            if (!System.IO.File.Exists(fullPath))
            {
                Debug.Log("[BatchBuild] Generating LabV3 scene...");
                LabV3SceneSetup.CreateLabV3Scene();
            }
            else
            {
                Debug.Log("[BatchBuild] LabV3.unity already exists.");
            }

            // 2) Build with LabV3 as the primary scene (standalone v3 test build)
            var scenes = new System.Collections.Generic.List<string>();
            scenes.Add(scenePath);

            // Include Boot + Menu + Lab Scene if they exist (for full flow testing)
            string bootScene = "Assets/Boot.unity";
            string labScene = "Assets/Lab Scene.unity";
            string menuScene = "Assets/_Project/Scenes/Menu.unity";
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../", bootScene)))
                scenes.Add(bootScene);
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../", menuScene)))
                scenes.Add(menuScene);
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../", labScene)))
                scenes.Add(labScene);

            Debug.Log($"[BatchBuild] Building with {scenes.Count} scene(s): {string.Join(", ", scenes)}");

            // 3) Build Linux player
            string buildDir = "Builds/Linux-V3";
            System.IO.Directory.CreateDirectory(buildDir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = buildDir + "/ChemLabSim.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BatchBuild] BUILD SUCCEEDED — {report.summary.totalSize / (1024*1024)} MB");
            }
            else
            {
                Debug.LogError($"[BatchBuild] BUILD FAILED: {report.summary.result} — {report.summary.totalErrors} error(s)");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
