#if UNITY_EDITOR
using ChemLabSimV3.Engine.SimulationV1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChemLabSimV3.Editor
{
    public static class LabPlayableSceneBuilder
    {
        private const string ScenePath = "Assets/LabPlayableScene.unity";

        [MenuItem("Tools/ChemLabSim/V1/Create Playable Lab Scene")]
        public static void CreatePlayableLabScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cameraGO = new GameObject("Main Camera", typeof(Camera));
            var cam = cameraGO.GetComponent<Camera>();
            cameraGO.tag = "MainCamera";
            cameraGO.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;

            // Canvas
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem for sliders
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Liquid container
            var liquidView = CreateImage(
                parent: canvasGO.transform,
                name: "LiquidView",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPos: Vector2.zero,
                size: new Vector2(700f, 420f),
                color: new Color(0.24f, 0.52f, 0.94f, 0.86f)
            );

            // Foam layer (disabled initially)
            var foamLayer = CreateImage(
                parent: liquidView.transform,
                name: "FoamLayer",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(0f, 0f),
                size: new Vector2(0f, 70f),
                color: new Color(1f, 1f, 1f, 0.32f)
            );
            foamLayer.gameObject.SetActive(false);

            // Bubbles particle system (world-space, visually aligned to liquid area)
            var bubblesGO = new GameObject("Bubbles", typeof(ParticleSystem));
            bubblesGO.transform.position = new Vector3(0f, -1.3f, 0f);
            var bubblesPS = bubblesGO.GetComponent<ParticleSystem>();
            ConfigureBubbles(bubblesPS);

            // Stirring slider (bottom left)
            var stirringSlider = CreateSlider(canvasGO.transform, "StirringSlider", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(220f, 24f), new Vector2(220f, 24f), new Vector2(180f, 80f));
            stirringSlider.minValue = 0f;
            stirringSlider.maxValue = 1f;
            stirringSlider.value = 0.25f;

            // Grinding slider (bottom right)
            var grindingSlider = CreateSlider(canvasGO.transform, "GrindingSlider", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(220f, 24f), new Vector2(220f, 24f), new Vector2(-180f, 80f));
            grindingSlider.minValue = 0f;
            grindingSlider.maxValue = 1f;
            grindingSlider.value = 0.20f;

            // Simulation runner
            var runner = new GameObject("SimulationRunner");
            var engine = runner.AddComponent<ReactionSimulationEngine>();
            var overlay = runner.AddComponent<SimulationDebugOverlay>();

            // Wire references via serialized properties
            var soEngine = new SerializedObject(engine);
            SetObj(soEngine, "liquidImage", liquidView);
            SetObj(soEngine, "gasBubbleParticles", bubblesPS);
            SetObj(soEngine, "foamObject", foamLayer.gameObject);
            SetObj(soEngine, "stirringSliderRaw", stirringSlider);
            SetObj(soEngine, "grindingSliderRaw", grindingSlider);
            SetBool(soEngine, "autoStart", true);
            SetInt(soEngine, "defaultReactionType", 0); // AcidCarbonate
            SetFloat(soEngine, "reactionConstantK", 2.0f);
            SetFloat(soEngine, "initialConcentration", 1.0f);
            soEngine.ApplyModifiedPropertiesWithoutUndo();

            var soOverlay = new SerializedObject(overlay);
            SetObj(soOverlay, "simulationEngine", engine);
            soOverlay.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(engine);
            EditorUtility.SetDirty(overlay);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeObject = runner;
            Debug.Log("[LabPlayableSceneBuilder] LabPlayableScene created and saved. Press Play to test.");
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        private static Slider CreateSlider(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 handleSize,
            Vector2 anchoredPos)
        {
            var sliderRoot = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderRoot.transform.SetParent(parent, false);

            var rootRt = sliderRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = anchorMin;
            rootRt.anchorMax = anchorMax;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = size;
            rootRt.anchoredPosition = anchoredPos;

            var bg = CreateImage(sliderRoot.transform, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.16f, 0.16f, 0.18f, 0.9f));

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderRoot.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0f);
            fillAreaRt.anchorMax = new Vector2(1f, 1f);
            fillAreaRt.offsetMin = new Vector2(5f, 5f);
            fillAreaRt.offsetMax = new Vector2(-20f, -5f);

            var fill = CreateImage(fillArea.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.25f, 0.65f, 1f, 1f));

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderRoot.transform, false);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0f);
            handleAreaRt.anchorMax = new Vector2(1f, 1f);
            handleAreaRt.offsetMin = new Vector2(10f, 0f);
            handleAreaRt.offsetMax = new Vector2(-10f, 0f);

            var handle = CreateImage(handleArea.transform, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, handleSize, new Color(0.95f, 0.95f, 0.95f, 1f));

            var slider = sliderRoot.GetComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            return slider;
        }

        private static void ConfigureBubbles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = 2.2f;
            main.startSpeed = 1.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.72f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;

            var emission = ps.emission;
            emission.rateOverTime = 6f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.2f, 0.1f, 0.1f);

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 50;
        }

        private static void SetObj(SerializedObject so, string propName, Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.objectReferenceValue = value;
        }

        private static void SetFloat(SerializedObject so, string propName, float value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string propName, int value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string propName, bool value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.boolValue = value;
        }
    }
}
#endif
