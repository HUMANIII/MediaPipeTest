// Editor-only capture runner. It is added to the running scene in memory and is
// never serialized into the demo scene or included in a player build.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace MediaPipeTest.SakuraHair
{
    public sealed class SakuraHairRuntimeCapture : MonoBehaviour
    {
        Camera cameraToCapture;
        Renderer hair;
        Material originalMaterial, runtimeMaterial;
        RenderTexture target;
        Texture2D readback;
        string output;
        Vector3 initialPosition, initialTarget;
        Quaternion initialRotation;
        int frame;
        float strength;
        readonly List<string> errors = new List<string>();
        readonly List<object> checkpoints = new List<object>();
        bool finished;
        Transform model;
        Vector3 modelPosition;
        Quaternion modelRotation;

        void Start()
        {
            try
            {
                output = Path.GetFullPath("Captures/SakuraHairDemo/InteriorFall");
                Directory.CreateDirectory(output + "/Frames");
                Application.logMessageReceived += OnLog;
                cameraToCapture = Camera.main;
                hair = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Single(r => r.name == "Hair1");
                model=GameObject.Find("Sakura Hair Character").transform;
                modelPosition=model.position;modelRotation=model.rotation;
                originalMaterial = hair.sharedMaterial;
                runtimeMaterial = new Material(originalMaterial);
                hair.sharedMaterial = runtimeMaterial;
                strength = runtimeMaterial.GetFloat("_EffectStrength");
                initialPosition = cameraToCapture.transform.position;
                initialRotation = cameraToCapture.transform.rotation;
                initialTarget = hair.bounds.center + Vector3.down * .06f;
                target = new RenderTexture(1280, 1280, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                target.Create();
                readback = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
                Time.captureFramerate = 30;
                Application.targetFrameRate = 30;
                Debug.Log("SAKURA_CAPTURE_STARTED");
            }
            catch(Exception e) { Fail(e); }
        }

        void LateUpdate()
        {
            if (finished || !runtimeMaterial) return;
            try
            {
                frame++;
                if (frame < 8) return;
                if (frame == 8)
                {
                    CaptureComparison("front");
                    ViewFrom(new Vector3(-1.15f, -.025f, -.1f)); CaptureComparison("side");
                    ViewFrom(new Vector3(.1f, -.025f, 1.08f)); CaptureComparison("back");
                    cameraToCapture.transform.SetPositionAndRotation(initialPosition, initialRotation);
                    // Render the same camera with two different material states at the same time.
                    runtimeMaterial.SetFloat("_Density",0); Capture("density-zero.png");
                    runtimeMaterial.SetFloat("_Density",originalMaterial.GetFloat("_Density"));
                    runtimeMaterial.SetFloat("_EffectStrength",0); runtimeMaterial.SetFloat("_Glow",1);
                    Capture("strength-zero-glow-one.png");
                    runtimeMaterial.CopyPropertiesFromMaterial(originalMaterial);
                    CaptureInteriorProbe();
                }
                int movieFrame=frame-8;
                if (movieFrame < 180)
                {
                    // Show falling first, then orbit to reveal independent depth.
                    if(movieFrame>=90)
                    {
                        float angle=(movieFrame-90)/89f*28f;
                        cameraToCapture.transform.position=initialTarget+Quaternion.AngleAxis(angle,Vector3.up)*(initialPosition-initialTarget);
                        cameraToCapture.transform.LookAt(initialTarget);
                    }
                    Capture($"Frames/{movieFrame:D4}.png");
                    if(movieFrame==0||movieFrame==60||movieFrame==179)
                        checkpoints.Add(new { frame=movieFrame, time=Time.time, realtime=Time.realtimeSinceStartup, playMode=Application.isPlaying });
                    if(movieFrame==60) Capture("motion-after-2s.png");
                }
                else if(movieFrame==180)
                {
                    cameraToCapture.transform.SetPositionAndRotation(initialPosition,initialRotation);
                    // Runtime-only roll around the hair center: check that world
                    // gravity is preserved when the character's local Y tilts.
                    model.RotateAround(hair.bounds.center,Vector3.forward,25f);
                    CaptureComparison("tilted");
                    Capture("tilted-t0.png");
                }
                else if(movieFrame==186)Capture("tilted-t0.2s.png");
                else if(movieFrame==195)
                {
                    Capture("tilted-t0.5s.png");
                    model.SetPositionAndRotation(modelPosition,modelRotation);
                    Finish();
                }
            }
            catch(Exception e) { Fail(e); }
        }

        void ViewFrom(Vector3 offset)
        {
            cameraToCapture.transform.position=hair.bounds.center+offset;
            cameraToCapture.transform.LookAt(initialTarget);
        }

        void CaptureInteriorProbe()
        {
            // A shader-only probe: the same viewing window at two different
            // surface depths must reveal the same virtual petals.
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name="Temporary Interior Projection Probe";quad.layer=30;
            var probeMaterial=new Material(runtimeMaterial);
            probeMaterial.SetColor("_BaseTint",Color.black);
            Vector3 volumeCenter=hair.bounds.center;
            Vector3 volumeExtent=hair.bounds.extents;
            int previousMask=cameraToCapture.cullingMask;
            try
            {
                cameraToCapture.cullingMask=1<<30;
                quad.GetComponent<Renderer>().sharedMaterial=probeMaterial;
                foreach(float distance in new[]{.55f,.8f})
                {
                    quad.transform.SetPositionAndRotation(cameraToCapture.transform.position+cameraToCapture.transform.forward*distance,cameraToCapture.transform.rotation);
                    float height=2*distance*Mathf.Tan(cameraToCapture.fieldOfView*.5f*Mathf.Deg2Rad);
                    quad.transform.localScale=new Vector3(height,height,1);
                    probeMaterial.SetVector("_VolumeCenterWS",volumeCenter);
                    probeMaterial.SetVector("_VolumeExtentsWS",volumeExtent);
                    Capture(distance<.6f?"probe-surface-near.png":"probe-surface-far.png");
                }
            }
            finally
            {
                cameraToCapture.cullingMask=previousMask;
                quad.SetActive(false);Object.Destroy(quad);Object.Destroy(probeMaterial);
            }
        }

        void CaptureComparison(string view)
        {
            runtimeMaterial.SetFloat("_EffectStrength",0); Capture(view+"-off.png");
            runtimeMaterial.SetFloat("_EffectStrength",strength); Capture(view+"-on.png");
        }

        void Capture(string name)
        {
            if(hair.TryGetComponent<SakuraHairVolumeBounds>(out var binder))binder.Refresh();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(cameraToCapture,request))
                throw new InvalidOperationException("URP camera render requests are not supported.");
            RenderPipeline.SubmitRenderRequest(cameraToCapture, request);
            RenderTexture previous=RenderTexture.active;
            try
            {
                RenderTexture.active=target;
                readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0);readback.Apply();
                File.WriteAllBytes(Path.Combine(output,name),readback.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;}
        }

        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);
        }

        void Finish()
        {
            finished=true;
            var shaderMessages=ShaderUtil.GetShaderMessages(originalMaterial.shader).Select(m=>new {message=m.message,severity=m.severity.ToString()}).ToArray();
            bool success=errors.Count==0&&!ShaderUtil.ShaderHasError(originalMaterial.shader);
            File.WriteAllText(Path.Combine(output,"runtime-validation.json"),JsonConvert.SerializeObject(new
            {
                success,unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceName,
                isPlaying=Application.isPlaying,frames=180,fps=30,duration=6,
                effectStrength=strength,checkpoints,errors,shaderMessages,
                flowWorld=runtimeMaterial.GetVector("_FlowDirection").ToString(),
                speedMetersPerSecond=runtimeMaterial.GetFloat("_Speed"),
                worldTiling=runtimeMaterial.GetFloat("_WorldTiling"),
                mode="Independent virtual 3D billboards inside the hair silhouette",
                interiorDepth=runtimeMaterial.GetFloat("_InteriorDepth"),
                depthFade=runtimeMaterial.GetFloat("_DepthFade"),
                volumeCenterWS=runtimeMaterial.GetVector("_VolumeCenterWS").ToString("F5"),
                volumeExtentsWS=runtimeMaterial.GetVector("_VolumeExtentsWS").ToString("F5"),
                tiltedCharacterDegrees=25,
                scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                material=AssetDatabase.GetAssetPath(originalMaterial),
                particleSystems=FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length,
                invokers=FindObjectsByType<HenshinInvoker>(FindObjectsSortMode.None).Length
            },Formatting.Indented));
            Restore();
            Debug.Log(success?"SAKURA_CAPTURE_PASSED":"SAKURA_CAPTURE_FAILED");
            EditorApplication.Exit(success?0:1);
        }

        void Fail(Exception e)
        {
            finished=true; Debug.LogException(e);Restore();EditorApplication.Exit(1);
        }
        void Restore()
        {
            Application.logMessageReceived-=OnLog;Time.captureFramerate=0;
            if(hair&&originalMaterial)hair.sharedMaterial=originalMaterial;
            if(model)model.SetPositionAndRotation(modelPosition,modelRotation);
            if(runtimeMaterial)Object.Destroy(runtimeMaterial);
            if(target){target.Release();Object.Destroy(target);}
            if(readback)Object.Destroy(readback);
        }
    }
}
#endif
