// ChemLabSim v3 — Vessel Template Builder
// Editor-only utility that scaffolds the standardized vessel hierarchy
// expected by the four visual controllers:
//
//   [Vessel_Template]                   ← Rigidbody / Interactable live here
//     ├── Vessel_Mesh                   ← glass MeshRenderer
//     ├── Liquid_Volume                 ← LiquidColorController + Renderer
//     ├── VFX_Hub
//     │    ├── BubbleSystem             ← LiquidVFXController + ParticleSystem
//     │    ├── GasExitPoint             ← GasEvolutionController + ParticleSystem
//     │    ├── HeatVolume               ← VesselHeatDistortionController + Renderer
//     │    └── Audio_Hub                ← VesselAudioController
//     │         ├── BubbleAudio         ← AudioSource (looping, !playOnAwake)
//     │         └── HissAudio           ← AudioSource (looping, !playOnAwake)
//     └── InfoCanvas                    ← VesselInfoDisplay (world-space HUD)
//          ├── ReactionNameText         ← TextMeshProUGUI
//          ├── TemperatureText          ← TextMeshProUGUI
//          └── StatusText               ← TextMeshProUGUI
//
// Two entry points (under "ChemLabSim/Vessels"):
//   • "Create Vessel Template (Empty)"  — scaffolds a fresh root in the scene.
//   • "Standardize Selected Vessel"     — adds any missing nodes/components to
//                                         the currently selected GameObject so
//                                         existing prefabs can be upgraded.

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ChemLabSimV3.Views;

namespace ChemLabSimV3.Editor
{
    public static class VesselTemplateBuilder
    {
        // Canonical child names — kept in sync with the controllers'
        // FindChildByName / GetComponentInChildren defaults.
        public const string RootName        = "[Vessel_Template]";
        public const string MeshName        = "Vessel_Mesh";
        public const string LiquidName      = "Liquid_Volume";
        public const string VfxHubName      = "VFX_Hub";
        public const string BubbleName      = "BubbleSystem";
        public const string GasExitName     = "GasExitPoint";
        public const string HeatVolumeName  = "HeatVolume";
        public const string AudioHubName    = "Audio_Hub";
        public const string BubbleAudioName = "BubbleAudio";
        public const string HissAudioName   = "HissAudio";

        // HUD subtree (mirrors VesselInfoDisplay constants).
        public const string InfoCanvasName  = "InfoCanvas";
        public const string InfoNameField   = "ReactionNameText";
        public const string InfoTempField   = "TemperatureText";
        public const string InfoStatusField = "StatusText";

        // ── Menu entries ──────────────────────────────────────
        [MenuItem("ChemLabSim/Vessels/Create Vessel Template (Empty)", false, 10)]
        private static void CreateNewTemplate()
        {
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Vessel Template");
            EnsureRootComponents(root);
            BuildHierarchy(root);
            Selection.activeGameObject = root;
            Debug.Log($"[VesselTemplateBuilder] Created '{RootName}'. " +
                      "Replace 'Vessel_Mesh' with your beaker/flask/tube model and save as a prefab.", root);
        }

        [MenuItem("ChemLabSim/Vessels/Standardize Selected Vessel", false, 11)]
        private static void StandardizeSelected()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Standardize Vessel",
                    "Select the root GameObject of the vessel first.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Standardize Vessel Template");
            EnsureRootComponents(root);
            BuildHierarchy(root);
            EditorUtility.SetDirty(root);
            Debug.Log($"[VesselTemplateBuilder] Standardized hierarchy under '{root.name}'.", root);
        }

        [MenuItem("ChemLabSim/Vessels/Standardize Selected Vessel", true)]
        private static bool StandardizeSelectedValidate() => Selection.activeGameObject != null;

        // ── Builders ──────────────────────────────────────────
        private static void EnsureRootComponents(GameObject root)
        {
            // Physics. Interaction components belong to the consuming project
            // (e.g. XR Interaction Toolkit) so we deliberately do not force one.
            if (root.GetComponent<Rigidbody>() == null)
                Undo.AddComponent<Rigidbody>(root);
        }

        private static void BuildHierarchy(GameObject root)
        {
            // 1) Vessel_Mesh — placeholder MeshRenderer for the glass
            var mesh = GetOrCreateChild(root.transform, MeshName);
            if (mesh.GetComponent<MeshFilter>() == null)
                Undo.AddComponent<MeshFilter>(mesh.gameObject);
            if (mesh.GetComponent<MeshRenderer>() == null)
                Undo.AddComponent<MeshRenderer>(mesh.gameObject);

            // 2) Liquid_Volume — LiquidColorController target
            var liquid = GetOrCreateChild(root.transform, LiquidName);
            if (liquid.GetComponent<MeshFilter>() == null)
                Undo.AddComponent<MeshFilter>(liquid.gameObject);
            if (liquid.GetComponent<MeshRenderer>() == null)
                Undo.AddComponent<MeshRenderer>(liquid.gameObject);
            EnsureComponent<LiquidColorController>(liquid.gameObject);

            // 3) VFX_Hub
            var vfxHub = GetOrCreateChild(root.transform, VfxHubName);

            // 3a) BubbleSystem — LiquidVFXController + ParticleSystem
            var bubbles = GetOrCreateChild(vfxHub, BubbleName);
            EnsureComponent<ParticleSystem>(bubbles.gameObject);
            EnsureComponent<LiquidVFXController>(bubbles.gameObject);

            // 3b) GasExitPoint — GasEvolutionController + ParticleSystem.
            //     Anchor lives at the rim by convention; offset on Y so a
            //     follow-up artist pass has a sensible starting point.
            var gasExit = GetOrCreateChild(vfxHub, GasExitName);
            if (gasExit.localPosition == Vector3.zero)
                gasExit.localPosition = new Vector3(0f, 0.15f, 0f);
            EnsureComponent<ParticleSystem>(gasExit.gameObject);
            EnsureComponent<GasEvolutionController>(gasExit.gameObject);

            // 3c) HeatVolume — VesselHeatDistortionController + sphere renderer
            var heat = GetOrCreateChild(vfxHub, HeatVolumeName);
            if (heat.GetComponent<MeshFilter>() == null)
            {
                var mf = Undo.AddComponent<MeshFilter>(heat.gameObject);
                // Borrow Unity's built-in sphere mesh so the volume has a
                // reasonable default shell. Artists can swap to a cylinder
                // or custom mesh later without code changes.
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            }
            if (heat.GetComponent<MeshRenderer>() == null)
            {
                var mr = Undo.AddComponent<MeshRenderer>(heat.gameObject);
                mr.shadowCastingMode      = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows         = false;
                mr.lightProbeUsage        = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage   = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.enabled                = false; // controller toggles when hot
            }
            EnsureComponent<VesselHeatDistortionController>(heat.gameObject);

            // 3d) Audio_Hub — VesselAudioController + two looping AudioSources
            BuildAudioHub(vfxHub);

            // 4) InfoCanvas — VesselInfoDisplay world-space HUD
            BuildInfoHud(root.transform);
        }

        // ── Audio scaffolding ─────────────────────────────────
        private static void BuildAudioHub(Transform vfxHub)
        {
            var audioHub = GetOrCreateChild(vfxHub, AudioHubName);

            // Two child carriers for the AudioSources. Splitting them out
            // keeps mixer routing per-clip and lets designers reposition
            // the bubble vs. hiss emission origin independently if desired.
            var bubbleAudio = GetOrCreateChild(audioHub, BubbleAudioName);
            var hissAudio   = GetOrCreateChild(audioHub, HissAudioName);

            var bubbleSrc = EnsureAudioSource(bubbleAudio.gameObject);
            var hissSrc   = EnsureAudioSource(hissAudio.gameObject);

            // VesselAudioController itself lives on the hub so a single
            // component owns both sources and the ChemistryProcessedEvent
            // subscription (no double-subscribe risk).
            var controller = audioHub.GetComponent<VesselAudioController>();
            if (controller == null)
                controller = Undo.AddComponent<VesselAudioController>(audioHub.gameObject);

            // Wire the [SerializeField] references via SerializedObject so the
            // assignment is recorded in the scene/prefab file and undoable.
            var so = new SerializedObject(controller);
            so.FindProperty("_bubblingSource").objectReferenceValue = bubbleSrc;
            so.FindProperty("_hissingSource").objectReferenceValue  = hissSrc;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioSource EnsureAudioSource(GameObject host)
        {
            var src = host.GetComponent<AudioSource>();
            if (src == null) src = Undo.AddComponent<AudioSource>(host);

            // Mirror VesselAudioController.ConfigureSource() defaults so the
            // template behaves identically before the controller's Awake runs.
            src.playOnAwake  = false;
            src.loop         = true;
            src.spatialBlend = 1f;                              // full 3D
            src.rolloffMode  = AudioRolloffMode.Logarithmic;
            src.minDistance  = 0.2f;
            src.maxDistance  = 5f;
            src.dopplerLevel = 0f;
            src.volume       = 0f;                              // controller ramps it up
            return src;
        }

        // ── HUD scaffolding ───────────────────────────────────
        private static void BuildInfoHud(Transform root)
        {
            var hud = GetOrCreateChild(root, InfoCanvasName);

            // Position above the vessel so it doesn't overlap the mesh.
            if (hud.localPosition == Vector3.zero)
                hud.localPosition = new Vector3(0f, 0.35f, 0f);
            // World-space canvases need a tiny scale so a 200-unit RectTransform
            // ends up roughly the size of the vessel rim.
            if (hud.localScale == Vector3.one)
                hud.localScale = Vector3.one * 0.005f;

            var canvas = hud.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = Undo.AddComponent<Canvas>(hud.gameObject);
                canvas.renderMode = RenderMode.WorldSpace;
            }
            EnsureComponent<CanvasScaler>(hud.gameObject);
            EnsureComponent<GraphicRaycaster>(hud.gameObject);
            EnsureComponent<CanvasGroup>(hud.gameObject);

            // Size the RectTransform sensibly (≈ 1m × 0.5m at scale 0.005).
            var rt = hud.GetComponent<RectTransform>();
            if (rt != null && rt.sizeDelta == Vector2.zero)
                rt.sizeDelta = new Vector2(200f, 100f);

            EnsureHudText(hud, InfoNameField,   new Vector2(0f,  35f), 18, FontStyles.Bold,   "—");
            EnsureHudText(hud, InfoTempField,   new Vector2(0f,   0f), 16, FontStyles.Normal, "—");
            EnsureHudText(hud, InfoStatusField, new Vector2(0f, -30f), 14, FontStyles.Italic, "Idle");

            EnsureComponent<VesselInfoDisplay>(hud.gameObject);
        }

        private static void EnsureHudText(Transform parent, string name, Vector2 anchoredPos,
                                          int fontSize, FontStyles style, string defaultText)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.GetComponent<TMP_Text>() != null) return;

            GameObject go;
            if (existing == null)
            {
                go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                Undo.SetTransformParent(go.transform, parent, "Parent " + name);
            }
            else
            {
                go = existing.gameObject;
            }

            var rt = go.GetComponent<RectTransform>();
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale    = Vector3.one;
            rt.anchorMin     = new Vector2(0.5f, 0.5f);
            rt.anchorMax     = new Vector2(0.5f, 0.5f);
            rt.pivot         = new Vector2(0.5f, 0.5f);
            rt.sizeDelta     = new Vector2(200f, 30f);
            rt.anchoredPosition = anchoredPos;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(go);
            tmp.text          = defaultText;
            tmp.fontSize      = fontSize;
            tmp.fontStyle     = style;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
        }

        // ── Tiny helpers ──────────────────────────────────────
        // Resolves an existing child by name (direct first, then deep search)
        // and only creates a new GameObject when none is found. Guarantees
        // standardization never duplicates a hub/canvas the artist already
        // placed under a wrapper transform.
        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            // Direct child lookup is the common case.
            var existing = parent.Find(name);
            if (existing != null) return existing;

            // Deep search — handles cases where VFX_Hub or InfoCanvas was
            // pre-placed under a sub-rig (e.g. "Beaker/Rigging/VFX_Hub").
            existing = FindDeepChildByName(parent, name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Undo.SetTransformParent(go.transform, parent, "Parent " + name);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            return go.transform;
        }

        private static Transform FindDeepChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var hit = FindDeepChildByName(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                Undo.AddComponent<T>(go);
        }
    }
}
