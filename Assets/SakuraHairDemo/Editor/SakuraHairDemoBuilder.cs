using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MediaPipeTest.SakuraHair.Editor
{
    public static class SakuraHairDemoBuilder
    {
        public const string ScenePath = "Assets/06.Scenes/SakuraHairDemo.unity";
        public const string Root = "Assets/SakuraHairDemo";
        const string SourceScene = "Assets/06.Scenes/Henshin.unity";
        const string GraphPath = Root + "/Shaders/SakuraHair.shadergraph";
        public static string Evidence => Path.GetFullPath("Captures/SakuraHairDemo/InteriorFall");

        [MenuItem("Tools/Sakura Hair/Create Demo Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("The demo already exists. Open it to tune the material; this command does not overwrite scenes.");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Evidence);
            BakePetalMask();
            AssetDatabase.Refresh();
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (!shader || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Sakura Shader Graph import failed: " + string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));

            Scene previous = SceneManager.GetActiveScene();
            Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(source);
            if (previous.IsValid() && string.IsNullOrEmpty(previous.path) && !previous.isDirty && previous.rootCount == 0)
                EditorSceneManager.CloseScene(previous, true);
            var original = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HenshinInvoker>(true)).Single().gameObject;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject model = Object.Instantiate(original);
            model.name = "Sakura Hair Character";
            SceneManager.MoveGameObjectToScene(model, scene);
            if (PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            Renderer hair = model.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Hair1");
            Material originalHair = hair.sharedMaterial;
            Material material = new Material(shader) { name = "Sakura Hair" };
            material.SetTexture("_BaseMap", originalHair.mainTexture);
            material.SetColor("_BaseTint", originalHair.HasProperty("_Color") ? originalHair.GetColor("_Color") : Color.white);
            material.SetTexture("_PetalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/SakuraPetal_BaseColor.png"));
            material.SetTexture("_PetalMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/SakuraPetal_Mask.png"));
            material.SetVector("_VolumeCenterWS",hair.bounds.center);
            material.SetVector("_VolumeExtentsWS",hair.bounds.extents);
            AssetDatabase.CreateAsset(material, Root + "/Materials/SakuraHair.mat");
            hair.sharedMaterial = material;
            hair.gameObject.AddComponent<SakuraHairVolumeBounds>();

            // Keep the copied character's original visibility/outfit and all non-hair materials.
            Bounds b = hair.bounds;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = b.center + new Vector3(.24f, -.025f, -1.00f);
            camera.transform.LookAt(b.center + Vector3.down * .06f);
            camera.fieldOfView = 34;
            camera.nearClipPlane = .02f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.065f, .082f, .11f);
            camera.allowHDR = true; camera.allowMSAA = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = true;
            camera.gameObject.AddComponent<AudioListener>();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.65f, .7f, .8f);
            RenderSettings.ambientEquatorColor = new Color(.38f, .4f, .45f);
            RenderSettings.ambientGroundColor = new Color(.23f, .2f, .24f);
            Light key = MakeLight("Soft Key", new Vector3(30,-30,0), new Color(1,.91f,.86f), 1.5f);
            key.shadows = LightShadows.Soft;
            MakeLight("Cool Fill", new Vector3(20,135,0), new Color(.68f,.79f,1), .65f);
            RenderSettings.sun = key;
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(source, true);
            AssetDatabase.SaveAssets();
            var info = new
            {
                unity = Application.unityVersion,
                scene = ScenePath,
                hair = hair.name,
                sourceHairMaterial = AssetDatabase.GetAssetPath(originalHair),
                sourceHairShader = originalHair.shader.name,
                sourceHairTexture = AssetDatabase.GetAssetPath(originalHair.mainTexture),
                bounds = new { center = b.center.ToString("F4"), size = b.size.ToString("F4") },
                activeRenderers = model.GetComponentsInChildren<Renderer>().Select(r => new
                    { name = r.name, materials = r.sharedMaterials.Select(m => new { material = m.name, shader = m.shader.name, path = AssetDatabase.GetAssetPath(m) }).ToArray() }).ToArray(),
                shaderMessages = ShaderUtil.GetShaderMessages(shader).Select(m => new { m.message, severity = m.severity.ToString() }).ToArray()
            };
            File.WriteAllText(Path.Combine(Evidence, "scene-validation.json"), JsonConvert.SerializeObject(info, Formatting.Indented));
            Debug.Log("SAKURA_SCENE_READY " + ScenePath);
        }

        static Light MakeLight(string name, Vector3 rotation, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(rotation); return light;
        }

        static void BakePetalMask()
        {
            const string modelPath = Root + "/Editor/Source/SakuraPetal_Static.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.isReadable = true; importer.importAnimation = false;
            importer.SaveAndReimport();
            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().Single();
            Vector2[] uv = mesh.uv; int[] triangles = mesh.triangles;
            // Bake actual mesh UV coverage at 2x resolution, then box-filter the edge.
            const int size = 512, high = size * 2;
            var coverage = new byte[high * high];
            for (int t=0; t<triangles.Length; t+=3)
            {
                Vector2 a=uv[triangles[t]]*high, b=uv[triangles[t+1]]*high, c=uv[triangles[t+2]]*high;
                float area=Cross(b-a,c-a);
                if (Mathf.Abs(area)<.00001f) continue;
                int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)),0,high-1);
                int maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)),0,high-1);
                int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)),0,high-1);
                int maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)),0,high-1);
                for (int y=minY;y<=maxY;y++) for(int x=minX;x<=maxX;x++)
                {
                    Vector2 p=new Vector2(x+.5f,y+.5f);
                    float u=Cross(b-p,c-p)/area,v=Cross(c-p,a-p)/area,w=1-u-v;
                    if(u>=-.000001f&&v>=-.000001f&&w>=-.000001f) coverage[y*high+x]=1;
                }
            }
            var colors=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int i=2*y*high+2*x;
                byte v=(byte)((coverage[i]+coverage[i+1]+coverage[i+high]+coverage[i+high+1])*255/4);
                colors[y*size+x]=new Color32(v,v,v,255);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            texture.SetPixels32(colors);texture.Apply();
            string path=Root+"/Textures/SakuraPetal_Mask.png";
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(path,false);
            ConfigureTexture(Root+"/Textures/SakuraPetal_BaseColor.png",true);
            importer.isReadable=false;importer.SaveAndReimport();
        }

        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static void ConfigureTexture(string path,bool srgb)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=srgb;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        public static void CreateBatch()
        {
            try { CreateScene(); EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
