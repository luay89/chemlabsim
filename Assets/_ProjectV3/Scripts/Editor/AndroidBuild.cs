// Android player build for the V3 lab (LabV3 is the only scene; it has its own AppRoot).
// Menu:  ChemLabSim V3 > Build Android APK
// Batch: Unity -batchmode -quit -projectPath . -executeMethod ChemLabSimV3.Editor.AndroidBuild.BuildApk -logFile -
// Output: Builds/Android/ChemLabSim.apk (debug-signed; Play Store upload needs a release keystore + AAB).
#if UNITY_EDITOR
using System.IO;
using ChemLabSimV3.Views.Bench3D;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemLabSimV3.Editor
{
    public static class AndroidBuild
    {
        private const string ScenePath       = "Assets/_ProjectV3/Scenes/LabV3.unity";
        private const string OutputPath      = "Builds/Android/ChemLabSim.apk";
        private const string ApplicationId   = "com.chemlabsim.app";
        private const string ShaderMatFolder = "Assets/_ProjectV3/Resources/RuntimeShaders";

        [MenuItem("ChemLabSim V3/Build Android APK")]
        public static void BuildApk()
        {
            ConfigurePlayer();
            EnsureRuntimeShaderMaterials();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuild] BUILD SUCCEEDED — {OutputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            }
            else
            {
                Debug.LogError($"[AndroidBuild] BUILD FAILED: {report.summary.result} — {report.summary.totalErrors} error(s)");
                if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Android player settings for a landscape phone/tablet lab app.</summary>
        private static void ConfigurePlayer()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.productName = "ChemLabSim";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.renderOutsideSafeArea = true;   // LabScreenLayout keeps the UI inside the safe area
            ApplyBranding();
        }

        private const string IconPath = "Assets/_ProjectV3/Branding/AppIcon.png";   // drawn by tools/branding/make_icon.py

        /// <summary>App icon (all platforms) and a splash screen in the app's colours.</summary>
        internal static void ApplyBranding()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            else
                Debug.LogWarning($"[AndroidBuild] {IconPath} not found; keeping the default icon.");

            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.06f, 0.12f);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
        }

        /// <summary>
        /// The 3D bench creates its materials in code (Shader.Find), which a player build strips.
        /// One material per shader + keyword set under Resources keeps those shaders and variants.
        /// </summary>
        internal static void EnsureRuntimeShaderMaterials()
        {
            Directory.CreateDirectory(ShaderMatFolder);
            var soft = Texture2D.whiteTexture;

            Save("BenchLit",              Bench3DMeshes.Lit(Color.white, 0.5f));
            Save("BenchGlass",            Bench3DMeshes.Glass(Color.white, 3000, CullMode.Back));
            Save("BenchParticleAlpha",    Bench3DMeshes.Particle(soft, false, 3005));
            Save("BenchParticleAdditive", Bench3DMeshes.Particle(soft, true, 3005));

            var liquid = Shader.Find("ChemLabSim/Liquid");
            if (liquid != null) Save("BenchLiquid", new Material(liquid));
            else Debug.LogWarning("[AndroidBuild] ChemLabSim/Liquid shader not found.");

            AssetDatabase.SaveAssets();
        }

        private static void Save(string name, Material material)
        {
            material.SetTexture("_BaseMap", null);   // keep the assets free of runtime textures
            material.SetTexture("_MainTex", null);
            // Create once only: URP re-validates the asset on import, so rewriting it on
            // every build would keep changing the committed file.
            string path = $"{ShaderMatFolder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                AssetDatabase.CreateAsset(material, path);
        }
    }
}
#endif
