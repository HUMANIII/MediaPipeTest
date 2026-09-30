using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MediaPipeTest.HenshinFilter
{
    /// <summary>Opt-in standalone rendering checks; inert during normal demo use.</summary>
    public sealed class HenshinFilterValidation : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public List<string> checks = new List<string>();
            public List<string> failures = new List<string>();
            public List<string> errors = new List<string>();
            public int width, height;
        }
        Report report;
        string outputDirectory;
        HenshinFilterController controller;
        FilterPanelDrag drag;
        bool active;
        bool record;

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-henshinFilterValidate")) { enabled = false; return; }
            report = new Report(); active = true;
            record = args.Contains("-henshinFilterRecord");
            int index = Array.IndexOf(args, "-henshinFilterOutput");
            outputDirectory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "HenshinFilterValidation");
            Directory.CreateDirectory(outputDirectory);
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            controller = GetComponent<HenshinFilterController>();
            drag = FindFirstObjectByType<FilterPanelDrag>();
            drag.acceptInput = false;
            StartCoroutine(Guard(Run()));
        }

        IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception e) { report.failures.Add(e.ToString()); Finish(); yield break; }
                if (!next) yield break;
                yield return routine.Current;
            }
        }

        void Check(bool condition, string label)
        {
            report.checks.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) report.failures.Add(label);
        }

        IEnumerator Run()
        {
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            report.width = Screen.width; report.height = Screen.height;
            Check(controller.IsReady, "Controller initialized");
            Check(controller.FilteredRenderers.Count == controller.casualRenderers.Length, "All casual meshes have proxies");
            Check(!FindFirstObjectByType<HenshinInvoker>(), "No transformation sequence or pose server dependency");
            Check((controller.viewCamera.cullingMask & (1 << controller.filteredLayer)) == 0, "Main camera excludes casual proxies");
            Check((controller.filterCamera.cullingMask & ((1 << controller.baseLayer) | (1 << controller.panelLayer))) == 0,
                "Filter camera excludes bikini and recursive panel rendering");
            Check(controller.Output && controller.Output.width == Screen.width && controller.Output.height == Screen.height,
                "RenderTexture matches display resolution");
            Material[][] originals = controller.casualRenderers.Select(r => r.sharedMaterials).ToArray();
            for (int i = 0; i < controller.FilteredRenderers.Count; i++)
            {
                Renderer proxy = controller.FilteredRenderers[i];
                Check(proxy.sharedMaterials.All(m => m.shader == controller.effectMaterial.shader), "Whole-character shader: " + proxy.name);
                if (proxy is SkinnedMeshRenderer skin)
                {
                    var source = (SkinnedMeshRenderer)controller.casualRenderers[i];
                    Check(skin.sharedMesh == source.sharedMesh && skin.bones.SequenceEqual(source.bones) && skin.rootBone == source.rootBone,
                        "Shared skeleton: " + proxy.name);
                    if (source.sharedMesh.blendShapeCount > 0)
                    {
                        float before = source.GetBlendShapeWeight(0);
                        source.SetBlendShapeWeight(0, 37);
                        yield return null; yield return new WaitForEndOfFrame();
                        Check(Mathf.Abs(skin.GetBlendShapeWeight(0) - 37) < .01f, "BlendShape synchronization: " + proxy.name);
                        source.SetBlendShapeWeight(0, before);
                    }
                }
            }
            drag.gameObject.SetActive(false);
            yield return null; yield return new WaitForEndOfFrame();
            Texture2D baseline = Capture("01-bikini-baseline.png");
            drag.gameObject.SetActive(true);
            yield return null; yield return new WaitForEndOfFrame();
            Texture2D torso = Capture("02-filter-torso.png");
            Bounds panelBounds = drag.panelCollider.bounds;
            Vector3 min = controller.viewCamera.WorldToScreenPoint(panelBounds.min);
            Vector3 max = controller.viewCamera.WorldToScreenPoint(panelBounds.max);
            int left = Mathf.FloorToInt(Mathf.Min(min.x, max.x)) - 15, right = Mathf.CeilToInt(Mathf.Max(min.x, max.x)) + 15;
            int bottom = Mathf.FloorToInt(Mathf.Min(min.y, max.y)) - 15, top = Mathf.CeilToInt(Mathf.Max(min.y, max.y)) + 15;
            int changedOutside = 0, changedInside = 0;
            for (int y = 20; y < torso.height - 20; y += 6)
                for (int x = 20; x < torso.width - 20; x += 6)
                {
                    Color a = baseline.GetPixel(x, y), b = torso.GetPixel(x, y);
                    bool changed = Mathf.Abs(a.r-b.r) + Mathf.Abs(a.g-b.g) + Mathf.Abs(a.b-b.b) > .04f;
                    if (changed && (x < left || x > right || y < bottom || y > top)) changedOutside++;
                    else if (changed) changedInside++;
                }
            Check(changedOutside == 0, "No changed pixels outside panel (samples=" + changedOutside + ")");
            Check(changedInside > 30, "Outfit/effect visibly changes inside panel (samples=" + changedInside + ")");
            Destroy(baseline); Destroy(torso);
            controller.effectEnabled = false;
            yield return null; yield return new WaitForEndOfFrame();
            Destroy(Capture("03-casual-without-effect.png"));
            Check(controller.FilteredRenderers.Select((r, i) => r.sharedMaterials.SequenceEqual(originals[i])).All(v => v), "Effect bypass restores original casual materials");
            controller.effectEnabled = true;
            float height = controller.baseRenderers.Select(r => r.bounds.max.y).Max() - controller.baseRenderers.Select(r => r.bounds.min.y).Min();
            Vector3 initial = drag.InitialPosition;
            Vector3 startPoint = controller.viewCamera.WorldToScreenPoint(initial);
            Physics.SyncTransforms();
            Check(drag.BeginDrag(startPoint), "Pointer ray starts drag on panel");
            Vector3 head = initial + Vector3.up * height * .35f;
            drag.DragTo(controller.viewCamera.WorldToScreenPoint(head));
            Check(Vector3.Distance(drag.transform.position, head) < .001f, "Drag tracks pointer in a fixed-depth plane");
            drag.EndDrag();
            yield return null; yield return new WaitForEndOfFrame();
            Destroy(Capture("04-filter-head.png"));
            drag.ResetPanel(); Physics.SyncTransforms();
            drag.BeginDrag(controller.viewCamera.WorldToScreenPoint(initial));
            drag.DragTo(controller.viewCamera.WorldToScreenPoint(initial - Vector3.up * height * .35f));
            drag.EndDrag();
            yield return null; yield return new WaitForEndOfFrame();
            Destroy(Capture("05-filter-legs.png"));
            drag.ResetPanel();
            Check(drag.transform.position == initial && !drag.IsDragging, "Reset restores initial panel position");
            controller.resolutionScale = .5f;
            yield return null; yield return new WaitForEndOfFrame();
            Check(controller.Output.width == Mathf.RoundToInt(Screen.width * .5f), "RenderTexture resizes at half resolution");
            controller.resolutionScale = 1;
            // Exercise a resized camera target without opening or resizing a desktop window.
            var resized = new RenderTexture(960, 640, 24);
            resized.Create();
            controller.viewCamera.targetTexture = resized;
            controller.RefreshView();
            Check(controller.Output.width == 960 && controller.Output.height == 640, "Resized camera target updates RenderTexture dimensions");
            controller.viewCamera.targetTexture = null;
            resized.Release(); Destroy(resized);
            controller.RefreshView();
            controller.enabled = false;
            yield return null;
            Check(!controller.Output && !controller.filterCamera.enabled && controller.FilteredRenderers.Count == 0, "Disable releases rendering resources");
            controller.enabled = true;
            yield return null; yield return new WaitForEndOfFrame();
            Check(controller.IsReady && controller.FilteredRenderers.Count == controller.casualRenderers.Length, "Re-enable recreates a single set of proxies");
            Check(controller.casualRenderers.Select((r, i) => r.sharedMaterials.SequenceEqual(originals[i])).All(v => v), "Source shared materials are unchanged");
            if (record)
            {
                Directory.CreateDirectory(Path.Combine(outputDirectory, "frames"));
                Time.captureFramerate = 30;
                Physics.SyncTransforms();
                drag.BeginDrag(controller.viewCamera.WorldToScreenPoint(initial));
                for (int frame = 0; frame < 240; frame++)
                {
                    float phase = frame / 239f;
                    Vector3 position = initial + Vector3.up * (Mathf.Sin(phase * Mathf.PI * 2) * height * .36f)
                        + Vector3.right * (Mathf.Sin(phase * Mathf.PI * 4) * height * .12f);
                    drag.DragTo(controller.viewCamera.WorldToScreenPoint(position));
                    yield return null; yield return new WaitForEndOfFrame();
                    Destroy(Capture("frames/frame-" + frame.ToString("D4") + ".png"));
                }
                drag.EndDrag(); drag.ResetPanel(); Time.captureFramerate = 0;
            }
            Finish();
        }

        Texture2D Capture(string name)
        {
            // A hidden Windows player may never present its backbuffer. Explicit camera render
            // requests capture the same scene/shaders independently of window visibility.
            Camera view = controller.viewCamera;
            RenderTexture previousTarget = view.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var capture = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            try
            {
                view.targetTexture = capture;
                controller.RefreshView();
                RenderPipeline.SubmitRenderRequest(controller.filterCamera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = controller.Output });
                RenderPipeline.SubmitRenderRequest(view,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = capture });
                RenderTexture.active = capture;
                var texture = new Texture2D(capture.width, capture.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(outputDirectory, name), texture.EncodeToPNG());
                return texture;
            }
            finally
            {
                view.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(capture);
                controller.RefreshView();
            }
        }
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); }
        void Finish()
        {
            File.WriteAllText(Path.Combine(outputDirectory, "validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("HENSHIN_FILTER_RUNTIME_" + (report.failures.Count == 0 && report.errors.Count == 0 ? "PASS" : "FAIL"));
            Application.Quit(report.failures.Count == 0 && report.errors.Count == 0 ? 0 : 1);
        }
        void OnDestroy() { if (active) Application.logMessageReceived -= OnLog; }
    }
}
