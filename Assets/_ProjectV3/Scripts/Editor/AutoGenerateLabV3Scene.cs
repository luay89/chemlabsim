// AUTO-GENERATED — Delete this file after scene is created.
// Triggers LabV3 scene generation on next domain reload.
#if UNITY_EDITOR
using UnityEditor;

namespace ChemLabSimV3.Editor
{
    [InitializeOnLoad]
    public static class AutoGenerateLabV3Scene
    {
        static AutoGenerateLabV3Scene()
        {
            // Delay to ensure Editor is fully loaded
            EditorApplication.delayCall += () =>
            {
                string scenePath = "Assets/_ProjectV3/Scenes/LabV3.unity";
                if (System.IO.File.Exists(
                    System.IO.Path.Combine(
                        UnityEngine.Application.dataPath, "../", scenePath)))
                {
                    UnityEngine.Debug.Log("[AutoGen] LabV3.unity already exists — skipping.");
                    return;
                }

                UnityEngine.Debug.Log("[AutoGen] Generating LabV3 scene...");
                LabV3SceneSetup.CreateLabV3Scene();
                UnityEngine.Debug.Log("[AutoGen] LabV3 scene generated. Delete AutoGenerateLabV3Scene.cs now.");
            };
        }
    }
}
#endif
