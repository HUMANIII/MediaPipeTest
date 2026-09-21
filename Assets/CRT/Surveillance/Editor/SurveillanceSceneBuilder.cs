using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MediaPipeTest.CRT.Surveillance.Editor
{
    public static class SurveillanceSceneBuilder
    {
        public const string Root = "Assets/CRT/Surveillance";
        public const string ScenePath = Root + "/Scenes/SurveillanceDemo.unity";
        const string HospitalScene = "Assets/Dnk_Dev/HospitalHorrorPack/Map_Hosp1.unity";
        const string HorrorModels = "Assets/CRT/Resource/HorrorPackFBX/HorrorPackFBX/";
        const string HorrorTextures = "Assets/CRT/Resource/HorrorPackFBX/Textures/";
        static int hospitalLayer, roomLayer;

        [MenuItem("Tools/CRT/Surveillance/Create or Rebuild Demo")]
        public static void CreateScene()
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
                throw new InvalidOperationException("Close SurveillanceDemo before rebuilding. Rebuild replaces this generated scene.");
            foreach (string dir in new[] { "Scenes", "Materials", "Profiles", "Meshes", "Prefabs", "Navigation", "Audio", "Animation", "Settings" }) Directory.CreateDirectory(Root + "/" + dir);
            AssetDatabase.Refresh();
            hospitalLayer = EnsureLayer("HospitalWorld"); roomLayer = EnsureLayer("SurveillanceRoom");
            var previous = SceneManager.GetActiveScene();
            // Open a saved template additively: Unity rejects NewScene(Additive) while an
            // unrelated untitled scene is open. This also preserves that scene's unsaved state.
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(HospitalScene, ScenePath))
                throw new IOException("Could not copy the hospital scene template.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.15f, .18f, .20f); RenderSettings.fog = false;
                var hospital = CopyHospital(scene);
                var nav = Bake(hospital);
                var environment = new GameObject("Surveillance Environment").AddComponent<SurveillanceEnvironment>();
                environment.navigation = nav; environment.pipeline = Pipeline();
                var wanderer = CreateDummy(hospital, nav);
                var room = CreateRoom(out var surface, out var seatPoint, out var seatView, out var sound);
                var feed = new GameObject("CCTV Controller").AddComponent<SurveillanceFeed>();
                feed.screen = surface; feed.staticAudio = sound;
                var cameraGo = new GameObject("CCTV Output Camera");
                feed.feedCamera = cameraGo.AddComponent<Camera>();
                feed.feedCamera.clearFlags = CameraClearFlags.SolidColor; feed.feedCamera.backgroundColor = Color.black;
                feed.feedCamera.nearClipPlane = .05f; feed.feedCamera.farClipPlane = 70; feed.feedCamera.depth = -10;
                feed.feedCamera.cullingMask = 1 << hospitalLayer;
                feed.feedCamera.GetUniversalAdditionalCameraData().SetRenderer(0);
                feed.brain = cameraGo.AddComponent<CinemachineBrain>();
                feed.brain.ChannelMask = OutputChannels.Channel02;
                feed.brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
                feed.channels = new[]
                {
                    Channel("CAM 01 - West corridor", new(-21.95f, 2.45f, 1.45f), new(-12, .95f, .35f), 64),
                    Channel("CAM 02 - East corridor", new(-5.05f, 2.45f, -.48f), new(-14, .95f, .5f), 64),
                    Channel("CAM 03 - Ward A", new(-13.05f, 2.5f, 7.25f), new(-11.9f, .8f, 3.3f), 70),
                    Channel("CAM 04 - Ward B", new(-16.1f, 2.5f, 7.3f), new(-14.95f, .85f, 3.1f), 70)
                };
                foreach (var cam in feed.channels) cam.transform.SetParent(feed.transform, true);
                feed.channels[0].Priority = 20;
                SurveillanceAudioSetup.Configure(feed, wanderer);
                var player = CreatePlayer(feed, seatPoint, seatView);
                var hud = new GameObject("Controls and Channel Display").AddComponent<SurveillanceHud>();
                hud.player = player; hud.feed = feed;
                var validation = new GameObject("Runtime Validation (command line only)").AddComponent<SurveillanceValidation>();
                validation.player = player; validation.feed = feed; validation.wanderer = wanderer; validation.hud = hud;
                LayerRecursively(room, roomLayer);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            ValidateScene();
            Debug.Log("SURVEILLANCE_SETUP_PASS " + ScenePath);
        }
        static GameObject CopyHospital(Scene target)
        {
            var sourceScene = SceneManager.GetSceneByPath(HospitalScene);
            bool openedSource = !sourceScene.isLoaded;
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(HospitalScene, OpenSceneMode.Additive);
            var hospital = new GameObject("Hospital - Map_Hosp1 Copy");
            SceneManager.MoveGameObjectToScene(hospital, target);
            try
            {
                foreach (var root in sourceScene.GetRootGameObjects())
                {
                    var copy = Object.Instantiate(root); copy.name = root.name;
                    SceneManager.MoveGameObjectToScene(copy, target); copy.transform.SetParent(hospital.transform, true);
                }
            }
            finally { if (openedSource) EditorSceneManager.CloseScene(sourceScene, true); SceneManager.SetActiveScene(target); }
            foreach (var cam in hospital.GetComponentsInChildren<Camera>()) Object.DestroyImmediate(cam.gameObject);
            foreach (var listener in hospital.GetComponentsInChildren<AudioListener>()) Object.DestroyImmediate(listener);
            foreach (var light in hospital.GetComponentsInChildren<Light>())
            {
                light.lightmapBakeType = LightmapBakeType.Realtime; light.cullingMask = 1 << hospitalLayer;
                light.intensity = 2.3f; light.range = 9; light.shadows = LightShadows.Soft;
                light.color = new Color(.74f, .85f, .83f);
            }
            foreach (var t in hospital.GetComponentsInChildren<Transform>())
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                if (t.name.StartsWith("P_Door_01"))
                {
                    float x = t.position.x < -14 ? -15.76f : -12.76f;
                    t.SetPositionAndRotation(new Vector3(x, .004f, 2.59f), Quaternion.Euler(0, 90, 0));
                    t.name += " (fixed open)";
                }
                // The source's diagonal corridor bed blocks its full width; place this one in the empty ward.
                if (t.name.StartsWith("P_Bed_01") && t.position.z < 2)
                    t.SetPositionAndRotation(new Vector3(-11.65f, .01f, 5.9f), Quaternion.Euler(0, 90, 0));
            }
            foreach (var renderer in hospital.GetComponentsInChildren<Renderer>()) { renderer.lightmapIndex = -1; renderer.lightProbeUsage = LightProbeUsage.Off; }
            PointLight("Ward A fluorescent", new(-12.1f, 2.6f, 4.8f), 2.0f, 7, hospitalLayer).transform.SetParent(hospital.transform);
            PointLight("West fluorescent", new(-21, 2.5f, .5f), 1.4f, 7, hospitalLayer).transform.SetParent(hospital.transform);
            LayerRecursively(hospital, hospitalLayer);
            return hospital;
        }
        static NavMeshData Bake(GameObject hospital)
        {
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(hospital.transform, 1 << hospitalLayer, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .23f; settings.agentHeight = 1.8f; settings.agentClimb = .25f; settings.agentSlope = 45;
            settings.overrideVoxelSize = true; settings.voxelSize = .055f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new(-13.5f, 1.1f, 3.4f), new(20, 2.8f, 11)), Vector3.zero, Quaternion.identity);
            if (!data) throw new InvalidOperationException("Hospital NavMesh build failed.");
            Save(data, Root + "/Navigation/HospitalNavigation.asset");
            return AssetDatabase.LoadAssetAtPath<NavMeshData>(Root + "/Navigation/HospitalNavigation.asset");
        }
        static HospitalWanderer CreateDummy(GameObject hospital, NavMeshData nav)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_M White.prefab");
            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(model);
            dummy.name = "White Dummy - Random Patrol";
            var instance = NavMesh.AddNavMeshData(nav);
            try
            {
                if (!NavMesh.SamplePosition(new Vector3(-18, .05f, .45f), out var hit, 2, NavMesh.AllAreas)) throw new InvalidOperationException("No walkable hospital spawn.");
                dummy.transform.position = hit.position;
            }
            finally { instance.Remove(); }
            var animator = dummy.GetComponentInChildren<Animator>();
            if (!animator) animator = dummy.AddComponent<Animator>();
            animator.runtimeAnimatorController = AnimationController(); animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var white = Lit("Dummy White", new Color(.86f, .89f, .88f));
            foreach (var r in dummy.GetComponentsInChildren<Renderer>()) r.sharedMaterial = white;
            var agent = dummy.AddComponent<NavMeshAgent>();
            agent.enabled = false; // Enabled in Start, after the saved NavMesh is registered.
            agent.radius = .23f; agent.height = 1.8f; agent.speed = 1.05f; agent.angularSpeed = 200;
            agent.acceleration = 3; agent.stoppingDistance = .18f; agent.autoBraking = true;
            var wander = dummy.AddComponent<HospitalWanderer>(); wander.animator = animator;
            wander.roamingBounds = new Bounds(new(-13.5f, 0, 3.4f), new(17, 1, 8.4f));
            LayerRecursively(dummy, hospitalLayer);
            PrefabUtility.SaveAsPrefabAssetAndConnect(dummy, Root + "/Prefabs/WhitePatrolDummy.prefab", InteractionMode.AutomatedAction);
            dummy.transform.SetParent(hospital.transform, true);
            return wander;
        }
        static RuntimeAnimatorController AnimationController()
        {
            string path = Root + "/Animation/DummyLocomotion.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (existing) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var tree = new BlendTree { name = "Idle and Walk", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Samples/Cinemachine/3.1.7/Shared Assets/Cameron/Animations/Cameron@" + name + ".fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            tree.AddChild(Clip("Idle"), 0); tree.AddChild(Clip("Walk"), 1.05f);
            AssetDatabase.AddObjectToAsset(tree, controller);
            controller.layers[0].stateMachine.AddState("Locomotion").motion = tree;
            EditorUtility.SetDirty(controller); return controller;
        }
        static GameObject CreateRoom(out CrtSurface surface, out Transform seatPoint, out Transform seatView, out AudioSource sound)
        {
            var room = new GameObject("Surveillance Room - HorrorPackFBX");
            var wall = Lit("Room plaster", Color.white, HorrorTextures + "WallPaint.jpg");
            var floor = Lit("Room floor", new Color(.57f, .57f, .57f), HorrorTextures + "WoodGround.jpg");
            var wood = Lit("Worn wood", new Color(.6f, .56f, .5f), HorrorTextures + "weathered_brown_planks_diff_1k.jpg");
            var roof = Lit("Room ceiling", new Color(.35f, .38f, .37f), HorrorTextures + "Paint.jpg");
            Model("Floor_1", room.transform, new(10, -.11f, 0), new(5, .22f, 4.8f), floor);
            Model("Roof_1", room.transform, new(10, 3.11f, 0), new(5, .22f, 4.8f), roof);
            Model("Wall_1", room.transform, new(10, 1.5f, 2.4f), new(5, 3, .2f), wall);
            Model("WallForDoor_1", room.transform, new(10, 1.5f, -2.4f), new(5, 3, .2f), wall);
            Model("Door_1", room.transform, new(10, 1.08f, -2.41f), new(2.00f, 2.15f, .20f), wood);
            Model("Wall_1", room.transform, new(7.5f, 1.5f, 0), new(4.8f, 3, .2f), wall, 90);
            Model("Wall_1", room.transform, new(12.5f, 1.5f, 0), new(4.8f, 3, .2f), wall, 90);
            Model("Table_1", room.transform, new(10, .45f, 1.42f), new(2.05f, .9f, 1.2f), wood);
            Model("NightStand_1", room.transform, new(7.98f, .38f, 1.7f), new(.7f, .76f, .7f), wood);
            var steel = Lit("Stool steel", new Color(.11f, .13f, .13f));
            var stool = new GameObject("Simple Stool"); stool.transform.SetParent(room.transform);
            Primitive("Seat", PrimitiveType.Cylinder, stool.transform, new(9.91f, .51f, .18f), new(.44f, .045f, .44f), wood);
            foreach (float x in new[] { -.14f, .14f }) foreach (float z in new[] { -.14f, .14f })
                Primitive("Leg", PrimitiveType.Cube, stool.transform, new(9.91f + x, .25f, .18f + z), new(.045f, .5f, .045f), steel);
            PrefabUtility.SaveAsPrefabAssetAndConnect(stool, Root + "/Prefabs/SimpleStool.prefab", InteractionMode.AutomatedAction);
            var tv = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CRT/Resource/Complete CRT TV Set/Models/CRT TV.fbx"));
            tv.name = "CRT Monitor"; tv.transform.SetParent(room.transform);
            // Keep the FBX's axis/unit conversion on its root transform.
            tv.transform.localScale *= .18f;
            var front = tv.GetComponentsInChildren<Renderer>().First(r => r.name == "Front");
            tv.transform.position += new Vector3(10, 1.26f, 1.73f) - front.bounds.center;
            var casing = Lit("CRT casing", new Color(.6f, .64f, .61f), "Assets/CRT/Resource/Complete CRT TV Set/Texture/Color Palette.jpg");
            foreach (var r in tv.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = casing;
                if (r.name == "Legs") r.gameObject.SetActive(false);
            }
            var filter = tv.GetComponentsInChildren<MeshFilter>().First(f => f.name == "Screen");
            var mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "CRT Screen - normalized display UV";
            var points = mesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x), minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            mesh.uv = points.Select(p => new Vector2(Mathf.InverseLerp(minX, maxX, p.x), Mathf.InverseLerp(minY, maxY, p.y))).ToArray();
            Save(mesh, Root + "/Meshes/CRTScreenNormalized.asset");
            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Meshes/CRTScreenNormalized.asset");
            surface = filter.gameObject.AddComponent<CrtSurface>();
            surface.template = AssetDatabase.LoadAssetAtPath<Material>("Assets/CRT/Materials/CRTSurface.mat");
            surface.displayAspect = (maxX - minX) / (maxY - minY);
            var profile = ScriptableObject.CreateInstance<CrtProfile>();
            profile.settings = new CrtSettings { monochrome = 1, rgbStrength = .12f, scanlineCount = 240, scanlineStrength = .24f,
                pixelation = .3f, virtualResolution = new(512, 384), curvature = .035f, vignette = .24f, brightness = 1.25f };
            Save(profile, Root + "/Profiles/SurveillanceCRT.asset");
            surface.profile = AssetDatabase.LoadAssetAtPath<CrtProfile>(Root + "/Profiles/SurveillanceCRT.asset");
            BoxForBounds(tv, tv.GetComponentsInChildren<Renderer>().First(r => r.name == "Front").bounds);
            sound = tv.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.loop = false;
            sound.spatialBlend = 1; sound.volume = .12f; sound.minDistance = 1.5f; sound.maxDistance = 6; sound.dopplerLevel = 0;
            sound.clip = NoiseClip();
            LayerRecursively(tv, roomLayer);
            PrefabUtility.SaveAsPrefabAssetAndConnect(tv, Root + "/Prefabs/SurveillanceCRT.prefab", InteractionMode.AutomatedAction);
            // SaveAsPrefabAssetAndConnect can remap components, so return the connected references.
            surface = tv.GetComponentInChildren<CrtSurface>(); sound = tv.GetComponent<AudioSource>();
            seatPoint = new GameObject("Seat Interaction Point").transform; seatPoint.SetParent(room.transform); seatPoint.position = new(9.91f, .05f, -.45f);
            seatView = new GameObject("Fixed Seated View").transform; seatView.SetParent(room.transform); seatView.position = new(9.91f, 1.30f, .17f);
            seatView.LookAt(new Vector3(9.91f, 1.28f, 1.55f));
            PointLight("Ceiling light", new(10, 2.7f, -.7f), 2.5f, 5, roomLayer).transform.SetParent(room.transform);
            var glow = PointLight("Monitor glow", new(9.91f, 1.35f, 1.28f), .25f, 2, roomLayer);
            glow.color = new Color(.52f, .7f, .65f); glow.shadows = LightShadows.None; glow.transform.SetParent(room.transform);
            return room;
        }
        static SurveillancePlayer CreatePlayer(SurveillanceFeed feed, Transform point, Transform seat)
        {
            var root = new GameObject("Player - Surveillance Room Only"); root.transform.position = new(10.6f, .04f, -1.62f);
            root.transform.rotation = Quaternion.Euler(0, -13, 0);
            var cc = root.AddComponent<CharacterController>(); cc.height = 1.75f; cc.radius = .25f; cc.center = new(0, .90f, 0); cc.stepOffset = .22f; cc.skinWidth = .025f;
            var player = root.AddComponent<SurveillancePlayer>();
            var cam = new GameObject("Player Camera"); cam.transform.SetParent(root.transform); cam.transform.localPosition = new(0, 1.62f, 0);
            player.view = cam.AddComponent<Camera>(); player.view.tag = "MainCamera"; player.view.fieldOfView = 52;
            player.view.nearClipPlane = .04f; player.view.farClipPlane = 30; player.view.cullingMask = 1 << roomLayer;
            player.view.clearFlags = CameraClearFlags.SolidColor; player.view.backgroundColor = Color.black;
            player.view.GetUniversalAdditionalCameraData().SetRenderer(0); cam.AddComponent<AudioListener>();
            player.feed = feed; player.interactionPoint = point; player.seatedView = seat; return player;
        }
        static CinemachineCamera Channel(string name, Vector3 position, Vector3 target, float fov)
        {
            var go = new GameObject(name); go.transform.position = position; go.transform.LookAt(target);
            var cam = go.AddComponent<CinemachineCamera>(); cam.OutputChannel = OutputChannels.Channel02; cam.Priority = 0;
            cam.Lens.FieldOfView = fov; cam.Lens.NearClipPlane = .05f; cam.Lens.FarClipPlane = 70; return cam;
        }
        static GameObject Model(string name, Transform parent, Vector3 center, Vector3 size, Material material, float yaw = 0)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HorrorModels + name + ".fbx"));
            go.transform.SetParent(parent); var bounds = BoundsOf(go);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
            go.transform.position += center - BoundsOf(go).center;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
            if (name == "Table_1" || name == "NightStand_1") BoxForBounds(go, BoundsOf(go));
            else foreach (var mesh in go.GetComponentsInChildren<MeshFilter>()) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            return go;
        }
        static void BoxForBounds(GameObject go, Bounds bounds)
        {
            var local = new Bounds(go.transform.InverseTransformPoint(bounds.center), Vector3.zero);
            foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y }) foreach (float z in new[] { bounds.min.z, bounds.max.z })
                local.Encapsulate(go.transform.InverseTransformPoint(new Vector3(x, y, z)));
            var collider = go.AddComponent<BoxCollider>(); collider.center = local.center; collider.size = local.size;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); return bounds;
        }
        static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent);
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static Light PointLight(string name, Vector3 position, float intensity, float range, int layer)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.type = LightType.Point; light.transform.position = position;
            light.intensity = intensity; light.range = range; light.shadows = LightShadows.Soft; light.cullingMask = 1 << layer;
            light.color = new Color(.83f, .87f, .77f); return light;
        }
        static Material Lit(string name, Color tint, string texture = null)
        {
            string path = Root + "/Materials/" + name.Replace(' ', '_') + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", tint); mat.SetFloat("_Smoothness", .16f);
            if (texture != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            EditorUtility.SetDirty(mat); return mat;
        }
        static UniversalRenderPipelineAsset Pipeline()
        {
            string path = Root + "/Settings/SurveillancePipeline.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path); if (existing) return existing;
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); renderer.name = "Surveillance Renderer";
            AssetDatabase.CreateAsset(renderer, Root + "/Settings/SurveillanceRenderer.asset");
            var pipeline = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
            pipeline.name = "Surveillance Pipeline";
            var serialized = new SerializedObject(pipeline); var list = serialized.FindProperty("m_RendererDataList"); list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer; serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(pipeline, path); return pipeline;
        }
        static AudioClip NoiseClip()
        {
            string path = Root + "/Audio/ChannelStatic.wav";
            const int rate = 44100, count = 8820;
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + count * 2);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16); stream.Write((short)1); stream.Write((short)1);
                stream.Write(rate); stream.Write(rate * 2); stream.Write((short)2); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(count * 2);
                var random = new System.Random(2309); double previous = 0;
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / rate, sample = random.NextDouble() * 2 - 1;
                    double envelope = Math.Min(1, t / .008) * Math.Min(1, (.2 - t) / .025);
                    double highPass = sample - previous * .65; previous = sample;
                    stream.Write((short)(Math.Clamp(highPass * .43 * envelope, -1, 1) * 32767));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path); var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static void Save(Object asset, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing) { EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); }
            else AssetDatabase.CreateAsset(asset, path);
        }
        static int EnsureLayer(string name)
        {
            int current = LayerMask.NameToLayer(name); if (current >= 0) return current;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            { layers.GetArrayElementAtIndex(i).stringValue = name; manager.ApplyModifiedPropertiesWithoutUndo(); return i; }
            throw new InvalidOperationException("No free rendering layer for " + name);
        }
        static void LayerRecursively(GameObject root, int layer)
        { foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
        [MenuItem("Tools/CRT/Surveillance/Validate Saved Demo")]
        public static void ValidateScene()
        {
            MediaPipeTest.CRT.Editor.CrtProjectSetup.ValidateAssets();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            var previous = SceneManager.GetActiveScene();
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                T[] Components<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
                var feed = Components<SurveillanceFeed>().Single();
                var footsteps = Components<SurveillanceFootstepAudio>().Single();
                if (!footsteps.preset || footsteps.walker != Components<HospitalWanderer>().Single() || footsteps.feed != feed
                    || footsteps.footsteps.Length != 6 || footsteps.footsteps.Any(c => !c || c.loadType != AudioClipLoadType.DecompressOnLoad)
                    || footsteps.GetComponent<AudioSource>() == feed.staticAudio)
                    throw new Exception("CRT footstep audio references or import settings invalid.");
                if (feed.channels.Length != 4 || feed.channels.Any(c => !c)) throw new Exception("Four CCTV channels required.");
                if ((feed.feedCamera.cullingMask & Components<SurveillancePlayer>().Single().view.cullingMask) != 0) throw new Exception("Camera layer isolation failed.");
                if (!Components<SurveillanceEnvironment>().Single().navigation) throw new Exception("Missing saved navigation.");
                var navInstance = NavMesh.AddNavMeshData(Components<SurveillanceEnvironment>().Single().navigation);
                try
                {
                    var path = new NavMeshPath();
                    if (!NavMesh.SamplePosition(new Vector3(-18, 0, .5f), out var start, 1, NavMesh.AllAreas)) throw new Exception("Patrol spawn unreachable.");
                    foreach (var point in new[] { new Vector3(-6, 0, .5f), new Vector3(-12, 0, 3.8f), new Vector3(-15, 0, 3.8f) })
                        if (!NavMesh.SamplePosition(point, out var end, 1, NavMesh.AllAreas)
                            || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                            throw new Exception("Hospital corridor or ward disconnected at " + point);
                }
                finally { navInstance.Remove(); }
                if (!Components<HospitalWanderer>().Single().animator.avatar.isHuman) throw new Exception("Humanoid avatar required.");
                if (!feed.screen.template.shader || ShaderUtil.ShaderHasError(feed.screen.template.shader)) throw new Exception("CRT shader has errors.");
                var uv = feed.screen.GetComponent<MeshFilter>().sharedMesh.uv;
                var screenBounds = feed.screen.GetComponent<Renderer>().bounds;
                if (screenBounds.size.x < .5f || screenBounds.size.y < .4f || Mathf.Abs(feed.screen.displayAspect - 1.374f) > .02f)
                    throw new Exception("CRT imported scale/orientation invalid: " + screenBounds + " aspect=" + feed.screen.displayAspect);
                if (screenBounds.min.y < .95f || screenBounds.max.y > 1.7f) throw new Exception("CRT must stand on the desk: " + screenBounds);
                if (uv.Any(p => p.x < -.001f || p.y < -.001f || p.x > 1.001f || p.y > 1.001f)) throw new Exception("Screen UV range invalid.");
                if (Components<Transform>().Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)) throw new Exception("Missing scripts in scene.");
                Debug.Log("SURVEILLANCE_ASSETS_PASS channels=4 avatar=human uv=normalized layers=isolated nav=saved");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        [MenuItem("Tools/CRT/Surveillance/Bake Navigation in Open Demo")]
        public static void BakeOpenDemo()
        {
            var scene = SceneManager.GetActiveScene();
            var hospital = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Hospital - Map_Hosp1 Copy");
            if (!hospital) throw new InvalidOperationException("Make SurveillanceDemo the active scene before baking.");
            hospitalLayer = LayerMask.NameToLayer("HospitalWorld");
            var data = Bake(hospital);
            var environment = scene.GetRootGameObjects().Select(r => r.GetComponent<SurveillanceEnvironment>()).First(e => e);
            environment.navigation = data; EditorUtility.SetDirty(environment); EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); Debug.Log("SURVEILLANCE_NAVIGATION_PASS");
        }
        [MenuItem("Tools/CRT/Surveillance/Build Windows Demo")]
        public static void BuildWindows()
        {
            ValidateScene(); Directory.CreateDirectory("Build/Surveillance");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = "Build/Surveillance/SurveillanceDemo.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Surveillance build failed: " + report.summary.result);
            Debug.Log("SURVEILLANCE_BUILD_PASS");
        }
    }
}
