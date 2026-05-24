#if UNITY_EDITOR
using System;
using System.Linq;
using ChemLabSimV3.Engine.SimulationV1;
using ChemLabSimV3.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChemLabSimV3.Editor
{
    public static class SimulationV1SetupTool
    {
        [MenuItem("Tools/ChemLabSim/V1/Open Lab Scene")]
        public static void OpenLabScene()
        {
            string preferred = "Assets/Lab Scene.unity";
            if (!System.IO.File.Exists(preferred))
            {
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene MainScene LabScene");
                if (sceneGuids.Length > 0)
                    preferred = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
            }

            if (!System.IO.File.Exists(preferred))
            {
                Debug.LogError("[SimulationV1SetupTool] Could not find a scene to open.");
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(preferred, OpenSceneMode.Single);
            Debug.Log($"[SimulationV1SetupTool] Opened scene: {preferred}");
        }

        [MenuItem("Tools/ChemLabSim/V1/Setup Simulation Runner")]
        public static void SetupSimulationRunner()
        {
            EnsureSceneLoaded();

            GameObject runner = GameObject.Find("SimulationRunner");
            if (runner == null)
                runner = new GameObject("SimulationRunner");

            var engine = runner.GetComponent<ReactionSimulationEngine>();
            if (engine == null)
                engine = runner.AddComponent<ReactionSimulationEngine>();

            var overlay = runner.GetComponent<SimulationDebugOverlay>();
            if (overlay == null)
                overlay = runner.AddComponent<SimulationDebugOverlay>();

            var so = new SerializedObject(engine);

            // Inputs
            SetObject(so, "stirringSlider", UnityEngine.Object.FindAnyObjectByType<StirringSliderView>());
            SetObject(so, "grindingSlider", UnityEngine.Object.FindAnyObjectByType<GrindingSliderView>());

            Slider[] allSliders = UnityEngine.Object.FindObjectsOfType<Slider>(true);
            SetObject(so, "stirringSliderRaw", FindByNameContains(allSliders, "stir") ?? allSliders.FirstOrDefault());
            SetObject(so, "grindingSliderRaw", FindByNameContains(allSliders, "grind") ?? allSliders.Skip(1).FirstOrDefault());

            // Visuals
            var images = UnityEngine.Object.FindObjectsOfType<Image>(true);
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            var particles = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(true);

            SetObject(so, "liquidImage", FindByNameContains(images, "liquid"));
            SetObject(so, "liquidRenderer", FindByNameContains(renderers, "liquid") ?? renderers.FirstOrDefault());
            SetObject(so, "gasBubbleParticles", FindByNameContains(particles, "bubble") ?? particles.FirstOrDefault());
            SetObject(so, "foamParticles", FindByNameContains(particles, "foam"));

            var foamImage = FindByNameContains(images, "foam");
            SetObject(so, "foamObject", foamImage != null ? foamImage.gameObject : null);

            // Reaction preset: AcidCarbonate, k=2.0, concentration=1.0
            SetInt(so, "defaultReactionType", 0);
            SetFloat(so, "reactionConstantK", 2.0f);
            SetFloat(so, "initialConcentration", 1.0f);
            SetBool(so, "autoStart", true);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(engine);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Selection.activeObject = runner;
            Debug.Log("[SimulationV1SetupTool] SimulationRunner configured. Press Play to test reaction behavior.");
        }

        private static void EnsureSceneLoaded()
        {
            if (!SceneManager.GetActiveScene().isLoaded)
                OpenLabScene();
        }

        private static T FindByNameContains<T>(T[] items, string token) where T : UnityEngine.Object
        {
            if (items == null || items.Length == 0)
                return null;

            return items.FirstOrDefault(i => i != null && i.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void SetObject(SerializedObject so, string fieldName, UnityEngine.Object value)
        {
            var p = so.FindProperty(fieldName);
            if (p != null)
                p.objectReferenceValue = value;
        }

        private static void SetFloat(SerializedObject so, string fieldName, float value)
        {
            var p = so.FindProperty(fieldName);
            if (p != null)
                p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string fieldName, int value)
        {
            var p = so.FindProperty(fieldName);
            if (p != null)
                p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string fieldName, bool value)
        {
            var p = so.FindProperty(fieldName);
            if (p != null)
                p.boolValue = value;
        }
    }
}
#endif
