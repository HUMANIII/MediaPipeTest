using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace MediaPipeTest.CRT.Editor
{
    public static class CrtProjectSetup
    {
        public const string ScenePath = "Assets/CRT/Demo/CRTPlayground.unity";
        const string RendererPath = "Assets/CRT/Demo/CRT_Renderer.asset";

        [MenuItem("Tools/CRT/Rebuild Shader Graphs")]
        public static void GenerateGraphs()
        {
            try
            {
                CrtGraphBuilder.Generate();
                AssetDatabase.SaveAssets();
                Debug.Log("CRT_GRAPHS_OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Tools/CRT/Create or Rebuild Playground")]
        public static void PrepareAll()
        {
            try
            {
                GenerateGraphs();
                Directory.CreateDirectory("Assets/CRT/Materials");
                Directory.CreateDirectory("Assets/CRT/Demo");
                AssetDatabase.Refresh();
                ConfigureTexture("CRTTestPattern.png", false, false);
                ConfigureTexture("ScreenShape.png", true, false);
                ConfigureTexture("RightHalfMask.png", false, true);
                var surface = Material("CRTSurface");
                var sprite = Material("CRTSprite");
                var canvas = Material("CRTCanvas");
                var fullscreen = Material("CRTFullscreen");
                if (!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath))
                    AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset", RendererPath);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                renderer.name = "CRT_Renderer";
                var feature = AssetDatabase.LoadAllAssetsAtPath(RendererPath).OfType<FullScreenPassRendererFeature>().FirstOrDefault();
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = "CRT Fullscreen";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                }
                renderer.rendererFeatures.Clear(); renderer.rendererFeatures.Add(feature);
                renderer.renderingMode = RenderingMode.Forward;
                renderer.depthPrimingMode = DepthPrimingMode.Disabled;
                feature.passMaterial = fullscreen;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feature.fetchColorBuffer = true; feature.requirements = ScriptableRenderPassInput.None;
                feature.passIndex = 0; feature.SetActive(false);
                EditorUtility.SetDirty(feature); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
                var so = new SerializedObject(pipeline);
                var list = so.FindProperty("m_RendererDataList");
                int index = -1;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
                if (index < 0)
                {
                    index = list.arraySize; list.InsertArrayElementAtIndex(index);
                    list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                AssetDatabase.SaveAssets();
                // Create additively so a user's open scene is not replaced or saved.
                Scene previous = SceneManager.GetActiveScene();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("CRT Playground");
                var demo = root.AddComponent<CrtDemo>();
                demo.surfaceMaterial = surface; demo.spriteMaterial = sprite; demo.canvasMaterial = canvas; demo.fullscreenMaterial = fullscreen;
                demo.fullscreenFeature = feature; demo.pipeline = pipeline; demo.rendererIndex = index;
                demo.testImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CRT/Media/CRTTestPattern.png");
                demo.halfMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CRT/Media/RightHalfMask.png");
                demo.screenShape = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CRT/Media/ScreenShape.png");
                demo.testVideo = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/CRT/Media/CRTTestPattern.mp4");
                demo.presets = CreateProfiles();
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                ValidateAssets();
                Debug.Log("CRT_SETUP_OK");
            }
            catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }
        static void ConfigureTexture(string file, bool sprite, bool linear)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/CRT/Media/" + file);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = sprite;
            if (sprite)
            {
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            }
            importer.sRGBTexture = !linear;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        static Material Material(string name)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/CRT/Shaders/" + name + ".shadergraph");
            if (!shader) throw new Exception("Graph failed to import: " + name);
            string path = "Assets/CRT/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;
            new CrtSettings().Apply(mat);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CRT/Media/CRTTestPattern.png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }
        [MenuItem("Tools/CRT/Validate Assets")]
        public static void ValidateAssets()
        {
            int checkedShaders = 0;
            foreach (string path in Directory.GetFiles("Assets/CRT/Shaders", "*.shadergraph"))
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path.Replace('\\','/'));
                if (!shader) throw new Exception("Missing shader: " + path);
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
                if (errors.Length != 0) throw new Exception(path + ": " + string.Join("; ", errors.Select(e => e.message)));
                if (File.ReadAllText(path).Contains("CustomFunctionNode")) throw new Exception("Expected node-based graph: " + path);
                checkedShaders++;
            }
            if (checkedShaders != 4) throw new Exception("Expected four shader graphs");
            Assert(Mathf.Approximately(CrtSettings.FitScale(16f/9, 4f/3).y, 4f/3), "Fit letterboxes a wide source");
            Assert(Mathf.Approximately(CrtSettings.FitScale(4f/3, 16f/9).x, 4f/3), "Fit pillarboxes a narrow source");
            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/CRT/Shaders/CRTSurface.shadergraph"));
            try
            {
                var settings = new CrtSettings { virtualResolution = Vector2.zero, screenRect = Vector4.zero, rgbDensity = 0 };
                settings.Apply(material);
                Assert(!material.HasProperty("_Strength"), "Global blend property removed");
                Assert(material.GetVector("_VirtualResolution").x == 1 && material.GetFloat("_RGBDensity") == 1, "Zero resolution/density guard");
                Assert(material.GetVector("_ScreenRect").z > 0, "Zero UV extent guard");
                settings.Apply(material, false);
                Assert(!material.IsKeywordEnabled("_CRT_EFFECT_ON"), "Disabled surface bypasses CRT instructions");
                settings.Apply(material);
                Assert(material.IsKeywordEnabled("_CRT_EFFECT_ON"), "CRT variant re-enabled");
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            Debug.Log("CRT_ASSET_VALIDATION PASS shaders=" + checkedShaders + " settings=7");
        }
        static void Assert(bool condition, string message) { if (!condition) throw new Exception("CRT assertion: " + message); }

        [MenuItem("Tools/CRT/Create Missing Profiles")]
        public static void CreateMissingProfiles() => CreateProfiles();

        public static CrtProfile[] CreateProfiles()
        {
            const string directory = "Assets/CRT/Profiles";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets/CRT", "Profiles");
            string[] names = { "Classic", "Monochrome", "Green", "Amber" };
            Color[] colors = { Color.white, Color.white, new Color(.2f,1,.3f), new Color(1,.65f,.15f) };
            var result = new CrtProfile[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = directory + "/" + names[i] + ".asset";
                result[i] = AssetDatabase.LoadAssetAtPath<CrtProfile>(path);
                // Never reset an existing preset: it may contain the user's saved tuning.
                if (result[i]) continue;
                result[i] = ScriptableObject.CreateInstance<CrtProfile>();
                result[i].settings.monochrome = i == 0 ? 0 : 1;
                result[i].settings.monoTint = colors[i];
                AssetDatabase.CreateAsset(result[i], path);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        public static void UpgradeProfilesAndBuild()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("This upgrade entry point is for batch mode. Use the individual CRT menus in the editor.");
            GenerateGraphs();
            var profiles = CreateProfiles();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var demo = UnityEngine.Object.FindFirstObjectByType<CrtDemo>();
            demo.presets = profiles;
            EditorUtility.SetDirty(demo);
            EditorSceneManager.SaveScene(scene);
            CrtProfileValidation.ValidateData();
            BuildWindows();
        }

        [MenuItem("Tools/CRT/Build Windows Preview")]
        public static void BuildWindows()
        {
            try
            {
                ValidateAssets();
                Directory.CreateDirectory("Build/CRT");
                bool oldFrameStats = PlayerSettings.enableFrameTimingStats;
                try
                {
                    PlayerSettings.enableFrameTimingStats = true;
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes = new[] { ScenePath }, locationPathName = "Build/CRT/CRTPreview.exe", target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.Development
                    });
                    if (report.summary.result != BuildResult.Succeeded) throw new Exception("CRT build: " + report.summary.result);
                    ValidateAssets(); // Build success alone does not guarantee every shader pass compiled.
                    Debug.Log("CRT_BUILD_OK " + report.summary.totalSize);
                }
                finally { PlayerSettings.enableFrameTimingStats = oldFrameStats; }
            }
            catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }
        public static void PrepareAndBuild() { PrepareAll(); BuildWindows(); }
        public static void RebuildAndBuild() { GenerateGraphs(); BuildWindows(); }
    }
}
