using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json;

namespace MediaPipeTest.SakuraHair.Editor
{
    [InitializeOnLoad]
    public static class SakuraHairCaptureTools
    {
        const string Pending = "SakuraHair.CapturePending";
        static SakuraHairCaptureTools()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void CreateAndCaptureBatch()
        {
            try
            {
                if (!File.Exists(SakuraHairDemoBuilder.ScenePath)) SakuraHairDemoBuilder.CreateScene();
                CaptureBatch();
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void CaptureBatch()
        {
            EditorSceneManager.OpenScene(SakuraHairDemoBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void TuneAndCaptureBatch()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene(SakuraHairDemoBuilder.ScenePath, OpenSceneMode.Single);
                var hair=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Single(r=>r.name=="Hair1");
                var material=hair.sharedMaterial;
                material.SetFloat("_EffectStrength",.78f);
                material.SetFloat("_PetalSize",.64f);
                material.SetFloat("_Density",.7f);
                material.SetFloat("_Glow",.08f);
                material.SetFloat("_Speed",.065f);
                material.SetFloat("_WorldTiling",26f);
                material.SetFloat("_Flutter",.75f);
                material.SetFloat("_InteriorDepth",1f);
                material.SetFloat("_DepthFade",.65f);
                material.SetVector("_VolumeCenterWS",hair.bounds.center);
                material.SetVector("_VolumeExtentsWS",hair.bounds.extents);
                if(!hair.GetComponent<SakuraHairVolumeBounds>())hair.gameObject.AddComponent<SakuraHairVolumeBounds>();
                material.SetVector("_FlowDirection",new Vector4(0,-1,0,0));
                EditorUtility.SetDirty(material);
                Camera.main.transform.position=hair.bounds.center+new Vector3(.24f,-.025f,-1f);
                Camera.main.transform.LookAt(hair.bounds.center+Vector3.down*.06f);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                CaptureBatch();
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void ValidateSavedSceneBatch()
        {
            try
            {
                const string graphPath=SakuraHairDemoBuilder.Root+"/Shaders/SakuraHair.shadergraph";
                AssetDatabase.ImportAsset(graphPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                var scene=EditorSceneManager.OpenScene(SakuraHairDemoBuilder.ScenePath,OpenSceneMode.Single);
                var model=scene.GetRootGameObjects().Single(g=>g.name=="Sakura Hair Character");
                var hair=model.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Hair1");
                var mat=hair.sharedMaterial;
                if(ShaderUtil.ShaderHasError(mat.shader))throw new InvalidOperationException("Shader compile error.");
                if(model.GetComponentsInChildren<MonoBehaviour>(true).Any(b=>!(b is SakuraHairVolumeBounds)))throw new InvalidOperationException("Unexpected character behaviour.");
                if(model.GetComponentsInChildren<SakuraHairVolumeBounds>(true).Length!=1)throw new InvalidOperationException("Missing hair bounds binding.");
                if(Mathf.Abs(mat.GetFloat("_EffectStrength")-.78f)>.001f)throw new InvalidOperationException("Material defaults were not saved.");
                var settings=new System.Collections.Generic.Dictionary<string,object>();
                foreach(string key in new[]{"_EffectStrength","_PetalSize","_Density","_Speed","_Flutter","_Glow","_WorldTiling","_InteriorDepth","_DepthFade"})settings[key]=mat.GetFloat(key);
                foreach(string key in new[]{"_VolumeCenterWS","_VolumeExtentsWS"})settings[key]=mat.GetVector(key).ToString("F5");
                Vector4 flow=mat.GetVector("_FlowDirection");settings["_FlowDirection"]=new[]{flow.x,flow.y,flow.z};
                if((flow-new Vector4(0,-1,0,0)).sqrMagnitude>.00001f)throw new InvalidOperationException("Flow must use world -Y.");
                foreach(string key in new[]{"_BaseMap","_PetalMap","_PetalMask"})
                {
                    if(!mat.GetTexture(key))throw new InvalidOperationException("Missing texture "+key);
                    settings[key]=AssetDatabase.GetAssetPath(mat.GetTexture(key));
                }
                string guid=AssetDatabase.AssetPathToGUID(SakuraHairDemoBuilder.Root+"/Shaders/SakuraSurface.hlsl");
                if(!File.ReadAllText(graphPath).Contains(guid))throw new InvalidOperationException("Custom Function must reference the include GUID.");
                Directory.CreateDirectory(SakuraHairDemoBuilder.Evidence);
                File.WriteAllText(Path.Combine(SakuraHairDemoBuilder.Evidence,"saved-scene-validation.json"),JsonConvert.SerializeObject(new
                {success=true,scene=scene.path,shader=mat.shader.name,shaderMessages=ShaderUtil.GetShaderMessages(mat.shader).Select(m=>m.message).ToArray(),settings,customFunctionGuid=guid},Formatting.Indented));
                Debug.Log("SAKURA_SAVED_SCENE_PASSED");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("Temporary Runtime Capture").AddComponent<SakuraHairRuntimeCapture>();
        }
    }
}
