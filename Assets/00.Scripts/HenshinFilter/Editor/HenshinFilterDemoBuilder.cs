using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MediaPipeTest.HenshinFilter.Editor
{
    public static class HenshinFilterDemoBuilder
    {
        public const string ScenePath = "Assets/06.Scenes/HenshinFilterDemo.unity";
        const string SourceScene = "Assets/06.Scenes/Henshin.unity";
        const string GraphPath = "Assets/03.Shaders/MagicalHenshinFilter.shadergraph";
        const string MaterialFolder = "Assets/04.Materials/HenshinFilter";

        [MenuItem("Tools/Henshin Filter/Create or Rebuild Demo")]
        public static void CreateScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before building the scene.");
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
                throw new InvalidOperationException("Close HenshinFilterDemo before rebuilding it.");
            Directory.CreateDirectory("Logs/HenshinFilter");
            Directory.CreateDirectory(MaterialFolder);
            DeriveShaderGraph();
            AssetDatabase.Refresh();
            int baseLayer = EnsureLayer("HenshinBase");
            int filteredLayer = EnsureLayer("HenshinFiltered");
            int panelLayer = EnsureLayer("HenshinPanel");
            Material effect = SaveMaterial("CharacterEffect", new Material(AssetDatabase.LoadAssetAtPath<Shader>(GraphPath)));
            Material sourceEffect = AssetDatabase.LoadAssetAtPath<Material>("Assets/04.Materials/Henshin.mat");
            foreach (string property in new[] { "_speed", "_S", "_V" }) effect.SetFloat(property, sourceEffect.GetFloat(property));
            effect.EnableKeyword("_USEALPHACLIP_ON");
            EditorUtility.SetDirty(effect);
            Material window = SaveMaterial("Window", new Material(Shader.Find("Henshin/Filter Window")));
            Material dark = Unlit("Frame", new Color(.025f, .055f, .085f));
            Material cyan = Unlit("Frame Light", new Color(.08f, .8f, 1f));

            Scene previous = SceneManager.GetActiveScene();
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(SourceScene, ScenePath))
                throw new IOException("Could not copy the Henshin scene.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Always rebuild from a fresh in-memory copy; source scene/prefab assets are never saved.
                if (!scene.GetRootGameObjects().Any(g => g.GetComponentInChildren<HenshinInvoker>(true)))
                {
                    foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                    Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
                    foreach (GameObject root in source.GetRootGameObjects())
                    {
                        GameObject copy = Object.Instantiate(root);
                        copy.name = root.name;
                        SceneManager.MoveGameObjectToScene(copy, scene);
                    }
                    EditorSceneManager.CloseScene(source, true);
                    SceneManager.SetActiveScene(scene);
                }
                HenshinInvoker invoker = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HenshinInvoker>(true)).Single();
                GameObject model = invoker.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(model))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var invokerData = new SerializedObject(invoker);
                SerializedProperty previousOutfit = invokerData.FindProperty("PrevHenshinObjects");
                var bikini = new HashSet<Renderer>();
                for (int i = 0; i < previousOutfit.arraySize; i++)
                {
                    var target = (GameObject)previousOutfit.GetArrayElementAtIndex(i).objectReferenceValue;
                    foreach (Renderer r in target.GetComponentsInChildren<Renderer>(true)) bikini.Add(r);
                }
                var factory = invokerData.FindProperty("sequenceEffectFactory").objectReferenceValue;
                var effects = new SerializedObject(factory).FindProperty("effects");
                var clothes = new List<Renderer>();
                for (int i = 0; i < effects.arraySize; i++)
                {
                    var entry = effects.GetArrayElementAtIndex(i);
                    if (!entry.managedReferenceFullTypename.EndsWith("RepeatEffect")) continue;
                    var targets = entry.FindPropertyRelative("targets");
                    for (int t = 0; t < targets.arraySize; t++)
                    {
                        var target = (Transform)targets.GetArrayElementAtIndex(t).objectReferenceValue;
                        clothes.AddRange(target.GetComponentsInChildren<Renderer>(true));
                    }
                }
                if (bikini.Count == 0 || clothes.Count < 3) throw new InvalidOperationException("Source outfit references are incomplete.");
                Renderer[] baseRenderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                Renderer[] casual = baseRenderers.Where(r => !bikini.Contains(r)).Concat(clothes).Distinct().ToArray();
                // Remove only components on the copied model, including HenshinInvoker.Awake listeners.
                foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (Renderer r in baseRenderers) r.gameObject.layer = baseLayer;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root != model && !root.GetComponent<Light>() && root.name != "Cube") Object.DestroyImmediate(root);

                Bounds bounds = baseRenderers[0].bounds;
                foreach (Renderer r in baseRenderers) bounds.Encapsulate(r.bounds);
                float height = bounds.size.y;
                if (height < .1f) throw new InvalidOperationException("Invalid avatar bounds.");
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = bounds.center - Vector3.forward * (height * 1.65f);
                camera.transform.rotation = Quaternion.identity;
                camera.fieldOfView = 42;
                camera.nearClipPlane = .03f;
                camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                camera.cullingMask = ~(1 << filteredLayer);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.gameObject.AddComponent<AudioListener>();
                var auxiliary = new GameObject("Casual View Camera").AddComponent<Camera>();
                auxiliary.enabled = false;
                auxiliary.GetUniversalAdditionalCameraData().renderPostProcessing = false;

                var panel = new GameObject("Drag Filter Panel");
                panel.transform.position = new Vector3(bounds.center.x + height * .275f, bounds.center.y + height * .03f, bounds.min.z - height * .28f);
                float width = height * .55f, panelHeight = height * .28f;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Filter Window";
                quad.transform.SetParent(panel.transform, false);
                quad.transform.localScale = new Vector3(width, panelHeight, 1);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var panelRenderer = quad.GetComponent<Renderer>();
                panelRenderer.sharedMaterial = window;
                panelRenderer.shadowCastingMode = ShadowCastingMode.Off;
                panelRenderer.receiveShadows = false;
                float thickness = height * .022f;
                FrameBar(panel.transform, "Top", new Vector3(0, panelHeight / 2 + thickness / 2, 0), new Vector3(width + thickness * 2, thickness, thickness), dark);
                FrameBar(panel.transform, "Bottom", new Vector3(0, -panelHeight / 2 - thickness / 2, 0), new Vector3(width + thickness * 2, thickness, thickness), dark);
                FrameBar(panel.transform, "Left", new Vector3(-width / 2 - thickness / 2, 0, 0), new Vector3(thickness, panelHeight, thickness), dark);
                FrameBar(panel.transform, "Right", new Vector3(width / 2 + thickness / 2, 0, 0), new Vector3(thickness, panelHeight, thickness), dark);
                FrameBar(panel.transform, "Top Light", new Vector3(0, panelHeight / 2, -thickness), new Vector3(width, thickness * .18f, thickness * .15f), cyan);
                FrameBar(panel.transform, "Bottom Light", new Vector3(0, -panelHeight / 2, -thickness), new Vector3(width, thickness * .18f, thickness * .15f), cyan);
                FrameBar(panel.transform, "Left Light", new Vector3(-width / 2, 0, -thickness), new Vector3(thickness * .18f, panelHeight, thickness * .15f), cyan);
                FrameBar(panel.transform, "Right Light", new Vector3(width / 2, 0, -thickness), new Vector3(thickness * .18f, panelHeight, thickness * .15f), cyan);
                foreach (Transform t in panel.GetComponentsInChildren<Transform>()) t.gameObject.layer = panelLayer;
                var collider = panel.AddComponent<BoxCollider>();
                collider.size = new Vector3(width + thickness * 2, panelHeight + thickness * 2, thickness * 2);
                var drag = panel.AddComponent<FilterPanelDrag>();
                drag.viewCamera = camera;
                drag.panelCollider = collider;
                var controller = new GameObject("Henshin Filter Controller").AddComponent<HenshinFilterController>();
                controller.viewCamera = camera;
                controller.filterCamera = auxiliary;
                controller.panelRenderer = panelRenderer;
                controller.baseRenderers = baseRenderers;
                controller.casualRenderers = casual;
                controller.effectMaterial = effect;
                controller.baseLayer = baseLayer;
                controller.filteredLayer = filteredLayer;
                controller.panelLayer = panelLayer;
                controller.gameObject.AddComponent<HenshinFilterValidation>();
                controller.gameObject.AddComponent<FilterDemoControls>().controller = controller;
                EditorSceneManager.SaveScene(scene, ScenePath);
                File.WriteAllText("Logs/HenshinFilter/source-renderers.txt", "Base:\n" + string.Join("\n", baseRenderers.Select(r => r.name))
                    + "\nBikini:\n" + string.Join("\n", bikini.Select(r => r.name)) + "\nCasual:\n" + string.Join("\n", casual.Select(r => r.name))
                    + "\nBounds: " + bounds);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            ValidateSavedScene();
            Debug.Log("HENSHIN_FILTER_SETUP_PASS " + ScenePath);
        }

        static void DeriveShaderGraph()
        {
            var objects = new List<JObject>();
            using (var reader = new JsonTextReader(new StringReader(File.ReadAllText("Assets/03.Shaders/MagicalHenshin.shadergraph"))) { SupportMultipleContent = true })
                while (reader.Read()) objects.Add(JObject.Load(reader));
            JObject graph = objects[0];
            graph["m_Path"] = "Henshin";
            JObject target = objects.Single(o => ((string)o["m_Type"]).EndsWith(".UniversalTarget"));
            target["m_SurfaceType"] = 0;
            target["m_AlphaClip"] = true;
            target["m_ZWriteControl"] = 1;
            JObject alpha = objects.Single(o => (string)o["m_Name"] == "SurfaceDescription.Alpha");
            JObject threshold = (JObject)alpha.DeepClone();
            string blockId = "8b4be1ce058249e69ac9c30088701001", slotId = "8b4be1ce058249e69ac9c30088701002";
            threshold["m_ObjectId"] = blockId;
            threshold["m_Name"] = threshold["m_SerializedDescriptor"] = "SurfaceDescription.AlphaClipThreshold";
            threshold["m_Slots"][0]["m_Id"] = slotId;
            JObject alphaSlot = objects.Single(o => (string)o["m_ObjectId"] == (string)alpha["m_Slots"][0]["m_Id"]);
            JObject thresholdSlot = (JObject)alphaSlot.DeepClone();
            thresholdSlot["m_ObjectId"] = slotId;
            thresholdSlot["m_DisplayName"] = "Alpha Clip Threshold";
            thresholdSlot["m_ShaderOutputName"] = "AlphaClipThreshold";
            thresholdSlot["m_Value"] = thresholdSlot["m_DefaultValue"] = .5f;
            ((JArray)graph["m_Nodes"]).Add(new JObject { ["m_Id"] = blockId });
            ((JArray)graph["m_FragmentContext"]["m_Blocks"]).Add(new JObject { ["m_Id"] = blockId });
            objects.Add(threshold); objects.Add(thresholdSlot);
            File.WriteAllText(GraphPath, string.Join("\n\n", objects.Select(o => o.ToString(Formatting.Indented))) + "\n");
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
        }

        static int EnsureLayer(string name)
        {
            int current = LayerMask.NameToLayer(name);
            if (current >= 0) return current;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { layers.GetArrayElementAtIndex(i).stringValue = name; tags.ApplyModifiedProperties(); return i; }
            throw new InvalidOperationException("No free user layers.");
        }

        static Material SaveMaterial(string name, Material material)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { EditorUtility.CopySerialized(material, existing); Object.DestroyImmediate(material); return existing; }
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        static Material Unlit(string name, Color color)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            return SaveMaterial(name, m);
        }
        static void FrameBar(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        [MenuItem("Tools/Henshin Filter/Validate Saved Demo")]
        public static void ValidateSavedScene()
        {
            Scene loaded = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !loaded.isLoaded;
            if (opened) loaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var c = loaded.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HenshinFilterController>(true)).Single();
                string error = c.ConfigurationError();
                if (error != null) throw new InvalidOperationException(error);
                if (loaded.GetRootGameObjects().Any(g => g.GetComponentInChildren<HenshinInvoker>(true)))
                    throw new InvalidOperationException("The copied scene still contains a transformation invoker.");
                foreach (string path in new[] { GraphPath, "Assets/03.Shaders/HenshinFilterWindow.shader" })
                {
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader error: " + path);
                }
                Debug.Log("HENSHIN_FILTER_SAVED_VALIDATION_PASS");
            }
            finally { if (opened) EditorSceneManager.CloseScene(loaded, true); }
        }

        public static void CreateAndBuild()
        {
            Directory.CreateDirectory("Logs/HenshinFilter");
            CreateScene();
            BuildPlayer();
        }

        [MenuItem("Tools/Henshin Filter/Build Demo")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds/HenshinFilter");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/HenshinFilter/HenshinFilterDemo.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Filter build failed: " + report.summary.result);
            Debug.Log("HENSHIN_FILTER_BUILD_PASS");
        }
    }
}
