#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DriftX.EditorTools
{
    /// <summary>
    /// Headless build entry point: configures Android + URP, generates the scene in code, builds the APK.
    /// Run by GameCI via buildMethod DriftX.EditorTools.CIBuild.Build
    /// Unity Build Automation uses DriftX.EditorTools.CIBuild.PreExport instead (it exports the player itself).
    /// </summary>
    public static class CIBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>
        /// Pre-export method for Unity Build Automation: sets up the project and generates the scene,
        /// but does NOT build. Build Automation exports the player after this runs.
        /// </summary>
        public static void PreExport()
        {
            ConfigurePlayer();
            ConfigureURP();
            CreateScene();
        }

        public static void Build()
        {
            try
            {
                ConfigurePlayer();
                ConfigureURP();
                CreateScene();

                string arg = GetArg("-customBuildPath");
                string path = string.IsNullOrEmpty(arg) ? "build/Android/DriftX.apk" : arg;
                if (!path.EndsWith(".apk")) path = Path.Combine(path, "DriftX.apk");
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                EditorUserBuildSettings.buildAppBundle = false;
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(opts);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.LogError("DriftX build failed: " + report.summary.result);
                    EditorApplication.Exit(1);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SurpriseKhoza";
            PlayerSettings.productName = "DriftX";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.surprisekhoza.driftx");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        static void ConfigureURP()
        {
            Directory.CreateDirectory("Assets/Settings");
            const string rendererPath = "Assets/Settings/DriftX_Renderer.asset";
            const string urpPath = "Assets/Settings/DriftX_URP.asset";

            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rendererPath);
            }
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, urpPath);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            AssetDatabase.SaveAssets();
        }

        static Material MakeMat(Shader shader, string name, Color c, float smooth, float metal)
        {
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            AssetDatabase.CreateAsset(m, "Assets/Materials/" + name + ".mat");
            return m;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Materials");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            var asphalt = MakeMat(lit, "Asphalt", new Color(0.12f, 0.12f, 0.13f), 0.35f, 0f);
            var paint = MakeMat(lit, "CarPaint", new Color(0.75f, 0.05f, 0.05f), 0.85f, 0.6f);
            var glass = MakeMat(lit, "Glass", new Color(0.05f, 0.08f, 0.1f), 0.95f, 0.2f);
            var tire = MakeMat(lit, "Tire", new Color(0.03f, 0.03f, 0.03f), 0.25f, 0f);
            var cone = MakeMat(lit, "Cone", new Color(1f, 0.45f, 0.05f), 0.4f, 0f);

            // Effect materials (created here so their shaders are included in the build)
            var skid = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Skid" };
            skid.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
            skid.SetFloat("_Cull", 0f); // visible from both sides
            AssetDatabase.CreateAsset(skid, "Assets/Materials/Skid.mat");
            var smoke = new Material(Shader.Find("Sprites/Default")) { name = "Smoke" };
            AssetDatabase.CreateAsset(smoke, "Assets/Materials/Smoke.mat");

            // Sun
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground (600 m x 600 m)
            Prim(PrimitiveType.Plane, "Ground", null, Vector3.zero, new Vector3(60f, 1f, 60f), asphalt, true);

            // Cone ring + center pair
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Prim(PrimitiveType.Cylinder, "Cone" + i, null, new Vector3(Mathf.Cos(a) * 35f, 0.5f, Mathf.Sin(a) * 35f), new Vector3(0.6f, 0.5f, 0.6f), cone, true);
            }
            Prim(PrimitiveType.Cylinder, "CenterConeA", null, new Vector3(-6f, 0.5f, 0f), new Vector3(0.6f, 0.5f, 0.6f), cone, true);
            Prim(PrimitiveType.Cylinder, "CenterConeB", null, new Vector3(6f, 0.5f, 0f), new Vector3(0.6f, 0.5f, 0.6f), cone, true);

            // Car
            var car = new GameObject("Car");
            car.layer = 2; // Ignore Raycast, so suspension rays skip the body
            car.transform.position = new Vector3(0f, 1.2f, -20f);
            car.AddComponent<Rigidbody>();
            var box = car.AddComponent<BoxCollider>();
            box.size = new Vector3(1.8f, 0.7f, 4.2f);
            var dc = car.AddComponent<DriftCar>();

            // Tire smoke + skid marks
            var fx = car.AddComponent<DriftEffects>();
            fx.car = dc;
            fx.skidMaterial = skid;
            fx.smokeMaterial = smoke;

            Prim(PrimitiveType.Cube, "Body", car.transform, Vector3.zero, new Vector3(1.8f, 0.6f, 4.2f), paint, false);
            Prim(PrimitiveType.Cube, "Cabin", car.transform, new Vector3(0f, 0.5f, -0.2f), new Vector3(1.5f, 0.5f, 2f), glass, false);

            var pivots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Wheel" + i);
                pivot.transform.SetParent(car.transform, false);
                pivot.transform.localPosition = dc.MountLocal(i);
                var vis = Prim(PrimitiveType.Cylinder, "Tire", pivot.transform, Vector3.zero, new Vector3(0.68f, 0.12f, 0.68f), tire, false);
                vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pivots[i] = pivot.transform;
            }
            dc.wheelVisuals = pivots;

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 4f, -28f);
            var chase = camGo.AddComponent<ChaseCamera>();
            chase.target = car.transform;
            chase.body = car.GetComponent<Rigidbody>();

            // Game systems
            var game = new GameObject("Game");
            game.AddComponent<TouchControls>();
            game.AddComponent<QualityTier>();
            var score = game.AddComponent<DriftScore>();
            score.car = dc;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
