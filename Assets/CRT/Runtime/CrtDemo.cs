using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MediaPipeTest.CRT
{
    public sealed partial class CrtDemo : MonoBehaviour
    {
        public Material surfaceMaterial, spriteMaterial, canvasMaterial, fullscreenMaterial;
        public FullScreenPassRendererFeature fullscreenFeature;
        public UniversalRenderPipelineAsset pipeline;
        public int rendererIndex = 1;
        public Texture2D testImage, halfMask;
        public Sprite screenShape;
        public VideoClip testVideo;
        public CrtProfile[] presets = Array.Empty<CrtProfile>();
        public int selectedPreset;
        public CrtSettings settings = new CrtSettings();
        public CrtSettings fullscreenSettings = new CrtSettings();
        public int mode; // 0: compare, 1: fullscreen, 2: 3D, 3: sprite, 4: Canvas
        public bool original, useVideo = true, includeUI, partial;
        public CrtSurface[] Surfaces { get; private set; }
        public CrtVideoSource Video { get; private set; }
        public CrtFullscreen Fullscreen { get; private set; }
        public Camera ViewCamera { get; private set; }
        Canvas controls, display;
        Text status;
        Font font;
        bool automate;
        bool wasPartial;
        Vector4 completeScreenRect;
        RectTransform parameterContent;
        readonly List<Action> syncControls = new List<Action>();
        RenderPipelineAsset previousPipeline;
        readonly List<string> runtimeErrors = new List<string>();
        static Report pendingReport;
        static string pendingOutput;

        void Awake()
        {
            previousPipeline = QualitySettings.renderPipeline;
            // Runtime sliders edit a local copy, never a shared profile asset.
            settings = settings.Clone();
            fullscreenSettings = fullscreenSettings.Clone();
            ApplyPreset(selectedPreset);
            if (pipeline) QualitySettings.renderPipeline = pipeline;
            automate = Array.IndexOf(Environment.GetCommandLineArgs(), "-crtValidate") >= 0;
            if (automate) { Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Application.logMessageReceived += OnLog;
            var cameraObject = new GameObject("CRT Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform);
            cameraObject.transform.localPosition = new Vector3(0, 0, -16);
            ViewCamera = cameraObject.GetComponent<Camera>();
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(.024f, .035f, .052f);
            ViewCamera.fieldOfView = 40;
            ViewCamera.nearClipPlane = .1f;
            ViewCamera.farClipPlane = 100;
            var data = ViewCamera.GetUniversalAdditionalCameraData();
            data.SetRenderer(rendererIndex);
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            controls = MakeCanvas("Controls - always sharp", 20);
            display = MakeCanvas("Display UI - included optionally", 0);
            Fullscreen = cameraObject.AddComponent<CrtFullscreen>();
            // Configure while disabled so it captures the original feature/canvas state once.
            Fullscreen.enabled = false;
            Fullscreen.feature = fullscreenFeature; Fullscreen.template = fullscreenMaterial;
            Fullscreen.displayCanvases = new[] { display }; Fullscreen.settings = fullscreenSettings;
            Fullscreen.enabled = true;
            var videoObject = new GameObject("Video input"); videoObject.transform.SetParent(transform);
            videoObject.SetActive(false);
            Video = videoObject.AddComponent<CrtVideoSource>(); Video.clip = testVideo; Video.fallback = testImage;
            videoObject.SetActive(true);

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mesh.name = "3D monitor surface"; mesh.transform.SetParent(transform);
            mesh.transform.localPosition = new Vector3(-3.65f, 1.55f, 0);
            mesh.transform.localScale = new Vector3(6.4f, 3.6f, 1);
            mesh.transform.localRotation = Quaternion.Euler(0, -8, 0);
            Destroy(mesh.GetComponent<Collider>());
            mesh.SetActive(false);
            var a = mesh.AddComponent<CrtSurface>(); a.template = surfaceMaterial; a.source = testImage; a.settings = settings;
            mesh.SetActive(true);
            var sprite = new GameObject("2D monitor sprite", typeof(SpriteRenderer));
            sprite.transform.SetParent(transform); sprite.transform.localPosition = new Vector3(3.65f, 1.55f, 0);
            sprite.GetComponent<SpriteRenderer>().sprite = screenShape;
            sprite.transform.localScale = Vector3.one * (6.4f / screenShape.bounds.size.x);
            sprite.SetActive(false);
            var b = sprite.AddComponent<CrtSurface>(); b.template = spriteMaterial; b.source = testImage; b.settings = settings;
            sprite.SetActive(true);
            var ui = Rect("Canvas video", display.transform, new Vector2(.69f, .255f), new Vector2(640, 360));
            ui.gameObject.SetActive(false);
            var raw = ui.gameObject.AddComponent<RawImage>(); raw.texture = testImage; raw.raycastTarget = false;
            var c = ui.gameObject.AddComponent<CrtSurface>(); c.template = canvasMaterial; c.source = testImage; c.settings = settings;
            ui.gameObject.SetActive(true);
            Surfaces = new[] { a, b, c };
            Label(display.transform, "3D SURFACE / UV LOCKED", new Vector2(.325f, .86f), new Vector2(500, 35), 22);
            Label(display.transform, "2D SPRITE / ALPHA", new Vector2(.675f, .86f), new Vector2(500, 35), 22);
            Label(display.transform, "CANVAS / RAW IMAGE", new Vector2(.69f, .445f), new Vector2(500, 35), 22);
            Label(controls.transform, "CRT  /  PHOSPHOR LAB", new Vector2(.5f, .957f), new Vector2(750, 50), 34);
            BuildControls();
            if (!FindFirstObjectByType<EventSystem>())
            {
                var events = new GameObject("CRT EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform);
            }
        }
        void Start() { if (automate) StartCoroutine(ValidatePlayer()); }
        void Update()
        {
            if (Surfaces == null) return;
            Texture input = useVideo ? Video.Texture : testImage;
            if (partial != wasPartial)
            {
                if (partial) { completeScreenRect = settings.screenRect; settings.screenRect = new Vector4(.15f, .12f, .7f, .76f); }
                else settings.screenRect = completeScreenRect;
                wasPartial = partial;
            }
            foreach (var surface in Surfaces) surface.source = input;
            for (int i = 0; i < Surfaces.Length; i++) Surfaces[i].effectEnabled = !original && (mode == 0 || mode == i + 2);
            // Only one CRT stage is active in fullscreen mode, avoiding double processing.
            Fullscreen.effectEnabled = mode == 1 && !original;
            Fullscreen.includeUI = includeUI;
            CopySettings(settings, fullscreenSettings);
            // The demo's Partial UV toggle targets surfaces; asset regions/masks still apply to fullscreen.
            if (partial) fullscreenSettings.screenRect = completeScreenRect;
            if (status)
                status.text = (useVideo ? (Video.IsReady ? "VIDEO  /  " + Video.Player.frame + "  /  loops " + Video.LoopCount : "VIDEO  /  " + (Video.Error ?? "preparing")) : "STILL IMAGE")
                    + "     " + Screen.width + " x " + Screen.height + "     " + (original ? "ORIGINAL" : "CRT");
        }
        static void CopySettings(CrtSettings a, CrtSettings b)
        {
            a.CopyTo(b);
        }
        public void ApplyPreset(int index)
        {
            if (presets == null || index < 0 || index >= presets.Length || !presets[index]) return;
            selectedPreset = index;
            presets[index].settings.CopyTo(settings);
            wasPartial = false;
            foreach (var sync in syncControls) sync();
        }
        Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            return canvas;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = anchor; rt.sizeDelta = size; return rt;
        }
        Text Label(Transform parent, string text, Vector2 anchor, Vector2 size, int fontSize)
        {
            var rt = Rect(text, parent, anchor, size); var label = rt.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(.73f, .86f, .9f); label.raycastTarget = false; return label;
        }
        void Button(string text, float x, Action action)
        {
            var rt = Rect(text, controls.transform, new Vector2(x, .902f), new Vector2(215, 36));
            var image = rt.gameObject.AddComponent<Image>(); image.color = new Color(.09f, .17f, .22f);
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Label(rt, text, new Vector2(.5f, .5f), new Vector2(215, 36), 17);
        }
        void Slider(string name, int row, float min, float max, Func<float> value, Action<float> changed)
        {
            float y = -22 - row * 32;
            var text = Label(parameterContent, name, new Vector2(.24f, 1), new Vector2(265, 28), 17);
            text.rectTransform.anchoredPosition = new Vector2(0, y);
            text.alignment = TextAnchor.MiddleLeft;
            var rt = Rect(name, parameterContent, new Vector2(.73f, 1), new Vector2(250, 18));
            rt.anchoredPosition = new Vector2(0, y);
            var background = rt.gameObject.AddComponent<Image>(); background.color = new Color(.1f, .19f, .23f);
            var handle = Rect("Handle", rt, new Vector2(.5f, .5f), new Vector2(12, 25));
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.33f, .89f, .73f);
            var slider = rt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.handleRect = handle; slider.targetGraphic = handleImage; slider.minValue = min; slider.maxValue = max;
            Action sync = () => { slider.SetValueWithoutNotify(value()); text.text = name + "  " + value().ToString("0.##"); };
            syncControls.Add(sync); sync();
            slider.onValueChanged.AddListener(v => { changed(v); sync(); });
        }
        void BuildControls()
        {
            string[] names = { "COMPARE ALL", "FULL SCREEN", "3D SURFACE", "2D SPRITE", "CANVAS UI" };
            for (int i = 0; i < names.Length; i++) { int selected = i; Button(names[i], .22f + i * .14f, () => mode = selected); }
            BuildParameterPanel();
            int row = 0;
            Slider("RGB density", row++, 1, 1920, () => settings.rgbDensity, v => settings.rgbDensity = v);
            Slider("RGB strength", row++, 0, 1, () => settings.rgbStrength, v => settings.rgbStrength = v);
            Slider("Scanline count", row++, 1, 1080, () => settings.scanlineCount, v => settings.scanlineCount = v);
            Slider("Scanline width", row++, .02f, .95f, () => settings.scanlineWidth, v => settings.scanlineWidth = v);
            Slider("Scanline strength", row++, 0, 1, () => settings.scanlineStrength, v => settings.scanlineStrength = v);
            Slider("Pixelation", row++, 0, 1, () => settings.pixelation, v => settings.pixelation = v);
            Slider("Virtual width", row++, 80, 1920, () => settings.virtualResolution.x, v => settings.virtualResolution.x = Mathf.Round(v));
            Slider("Virtual height", row++, 45, 1080, () => settings.virtualResolution.y, v => settings.virtualResolution.y = Mathf.Round(v));
            Slider("Curvature", row++, 0, .5f, () => settings.curvature, v => settings.curvature = v);
            Slider("Vignette", row++, 0, 1, () => settings.vignette, v => settings.vignette = v);
            Slider("Brightness", row++, 0, 2, () => settings.brightness, v => settings.brightness = v);
            Slider("Monochrome", row++, 0, 1, () => settings.monochrome, v => settings.monochrome = v);
            Slider("Vignette radius", row++, 0, 1, () => settings.vignetteRadius, v => settings.vignetteRadius = v);
            Slider("Vignette softness", row++, .05f, 2, () => settings.vignetteSoftness, v => settings.vignetteSoftness = v);
            parameterContent.sizeDelta = new Vector2(630, row * 32 + 22);
            string[] labels = { "Original", "Video", "Include UI", "Partial UV", "Pause / play" };
            Action[] actions = { () => original = !original, () => useVideo = !useVideo, () => includeUI = !includeUI, () => partial = !partial, () => Video.TogglePlayback() };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var rt = Rect(labels[i], controls.transform, new Vector2(.23f + .14f*i, .04f), new Vector2(200, 33));
                var image = rt.gameObject.AddComponent<Image>(); image.color = new Color(.09f,.17f,.22f);
                var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => actions[index]());
                Label(rt, labels[i], new Vector2(.5f,.5f), new Vector2(200,33), 17);
            }
            status = Label(controls.transform, "Preparing", new Vector2(.5f,.077f), new Vector2(1700,28), 16);
        }
        void BuildParameterPanel()
        {
            string[] presetLabels = { "COLOR", "MONO", "GREEN", "AMBER", "REAPPLY" };
            for (int i = 0; i < presetLabels.Length; i++)
            {
                int index = i;
                var rt = Rect(presetLabels[i], controls.transform, new Vector2(.137f + i * .062f,.459f), new Vector2(108,30));
                var image = rt.gameObject.AddComponent<Image>(); image.color = new Color(.09f,.17f,.22f);
                var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                button.onClick.AddListener(() => ApplyPreset(index == 4 ? selectedPreset : index));
                button.interactable = index == 4 ? presets.Length > 0 : index < presets.Length;
                Label(rt,presetLabels[i],new Vector2(.5f,.5f),new Vector2(108,30),15);
            }
            var viewport = Rect("Parameter scroll", controls.transform, new Vector2(.255f,.28f), new Vector2(650,338));
            var background = viewport.gameObject.AddComponent<Image>(); background.color = new Color(.024f,.045f,.058f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            parameterContent = Rect("Parameters", viewport, new Vector2(.5f,1), Vector2.zero);
            parameterContent.pivot = new Vector2(.5f,1);
            scroll.content = parameterContent; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var bar = Rect("Scrollbar", viewport, new Vector2(1,.5f), new Vector2(9,326));
            bar.anchoredPosition = new Vector2(-8,0);
            var thumb = Rect("Thumb",bar,new Vector2(.5f,.5f),new Vector2(9,40));
            var thumbImage = thumb.gameObject.AddComponent<Image>(); thumbImage.color = new Color(.33f,.89f,.73f);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = thumb; scrollbar.targetGraphic = thumbImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = scrollbar;
            scroll.verticalNormalizedPosition = 1;
            Label(controls.transform,"SCROLL: MONO / VIGNETTE   -   TEMPORARY SETTINGS",new Vector2(.255f,.108f),new Vector2(670,22),14);
        }
        void OnLog(string condition, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors.Add(condition); }
        void OnDestroy()
        {
            if (pipeline && QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
            Application.logMessageReceived -= OnLog;
        }

        [Serializable] class Measurement { public string mode; public float meanMs, p95Ms, gpuMs; public int frames, gpuValidSamples, gpuInvalidSamples; }
        [Serializable] class Report
        {
            public string gpu, api, resolution, unity;
            public bool videoPrepared, videoAdvanced, videoLooped, videoReopened, pauseResume;
            public bool uiIncluded, uiExcluded;
            public bool sceneReloaded;
            public bool profilesUnchanged, profileBindingsWorking;
            public bool capturesHaveContent = true;
            public int renderTexturesBefore, renderTexturesAfter;
            public List<Measurement> timings = new List<Measurement>();
            public List<string> errors = new List<string>();
        }
        IEnumerator ValidatePlayer()
        {
            while (!SplashScreen.isFinished) yield return null;
            if (pendingReport == null)
            {
                // Recreate the swapchain once for unattended launches whose window starts hidden.
                Screen.SetResolution(1918,1078,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.2f);
                Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            }
            yield return new WaitForSecondsRealtime(.5f);
            if (pendingReport != null)
            {
                var completed = pendingReport; var destination = pendingOutput;
                pendingReport = null; pendingOutput = null;
                float deadline = Time.realtimeSinceStartup + 20;
                while (!Video.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(.5f);
                completed.sceneReloaded = Video.IsReady && Video.Player.frame > 0;
                completed.renderTexturesAfter = VideoTextures();
                completed.errors.AddRange(runtimeErrors);
                File.WriteAllText(Path.Combine(destination,"report.json"), JsonUtility.ToJson(completed,true));
                bool passed = completed.videoPrepared && completed.videoAdvanced && completed.videoLooped && completed.pauseResume && completed.videoReopened
                    && completed.uiIncluded && completed.uiExcluded && completed.sceneReloaded && completed.capturesHaveContent
                    && completed.renderTexturesBefore == completed.renderTexturesAfter && completed.profilesUnchanged && completed.profileBindingsWorking && completed.errors.Count == 0;
                Debug.Log("CRT_PLAYER_VALIDATION " + (passed ? "PASS" : "FAIL") + " " + destination);
                Application.Quit(passed ? 0 : 1);
                yield break;
            }
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-crtOutput");
            string output = i >= 0 && i + 1 < args.Length ? args[i+1] : Path.Combine(Application.persistentDataPath, "CRTValidation");
            Directory.CreateDirectory(output);
            var report = new Report { gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), resolution = Screen.width+"x"+Screen.height, unity = Application.unityVersion };
            float until = Time.realtimeSinceStartup + 30;
            while (!Video.IsReady && Video.Error == null && Time.realtimeSinceStartup < until) yield return null;
            report.videoPrepared = Video.IsReady;
            long startFrame = Video.Player.frame;
            yield return new WaitForSecondsRealtime(1);
            report.videoAdvanced = Video.Player.frame != startFrame;
            Video.TogglePlayback();
            yield return new WaitForSecondsRealtime(.2f);
            long paused = Video.Player.frame;
            yield return new WaitForSecondsRealtime(.25f);
            bool held = paused == Video.Player.frame;
            Video.TogglePlayback();
            yield return new WaitForSecondsRealtime(.3f);
            report.pauseResume = held && paused != Video.Player.frame;
            until = Time.realtimeSinceStartup + 6;
            while (Video.LoopCount == 0 && Time.realtimeSinceStartup < until) yield return null;
            report.videoLooped = Video.LoopCount > 0;
            // Baseline captures use fixed settings even when the user has tuned the saved presets.
            new CrtSettings().CopyTo(settings);
            foreach (var sync in syncControls) sync();
            // Fixed image snapshots make visual comparisons deterministic.
            useVideo = false;
            string[] shots = { "00-original", "01-compare", "02-fullscreen-ui-excluded", "03-fullscreen-ui-included", "04-pixelation", "05-partial", "06-mask" };
            for (int shot = 0; shot < shots.Length; shot++)
            {
                original = shot == 0; mode = shot == 2 || shot == 3 ? 1 : 0; includeUI = shot == 3;
                settings.pixelation = shot == 4 ? 1 : 0; settings.virtualResolution = new Vector2(160,90);
                partial = shot == 5; settings.effectMask = shot == 6 ? halfMask : null;
                yield return new WaitForSecondsRealtime(.2f);
                if (shot == 2) report.uiExcluded = display.renderMode == RenderMode.ScreenSpaceOverlay;
                if (shot == 3) report.uiIncluded = display.renderMode == RenderMode.ScreenSpaceCamera && display.worldCamera == ViewCamera;
                yield return new WaitForEndOfFrame();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                report.capturesHaveContent &= HasContent(texture);
                File.WriteAllBytes(Path.Combine(output, shots[shot] + ".png"), texture.EncodeToPNG()); Destroy(texture);
            }
            settings.effectMask = null; settings.pixelation = 0; partial = false; mode = 0;
            Screen.SetResolution(1024,768,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture(output, "07-aspect-4x3");
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.6f);
            var surfaceTransform = Surfaces[0].transform;
            Vector3 oldScale = surfaceTransform.localScale;
            Quaternion oldRotation = surfaceTransform.localRotation;
            surfaceTransform.localScale *= .55f; surfaceTransform.localRotation = Quaternion.Euler(0,48,0);
            yield return Capture(output, "08-distance-angle");
            surfaceTransform.localScale = oldScale; surfaceTransform.localRotation = oldRotation;
            var uiRect = (RectTransform)Surfaces[2].transform;
            Vector2 oldAnchor = uiRect.anchorMin;
            var clipping = Rect("Stencil and alpha test", display.transform, oldAnchor, new Vector2(520,270));
            var clippingImage = clipping.gameObject.AddComponent<Image>(); clippingImage.sprite = screenShape;
            clipping.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            clipping.gameObject.AddComponent<CanvasGroup>().alpha = .5f;
            uiRect.SetParent(clipping, false); uiRect.anchorMin = uiRect.anchorMax = new Vector2(.5f,.5f); uiRect.anchoredPosition = Vector2.zero;
            settings.pixelation = 1;
            yield return Capture(output, "09-ui-mask-alpha");
            uiRect.SetParent(display.transform, false); uiRect.anchorMin = uiRect.anchorMax = oldAnchor; uiRect.anchoredPosition = Vector2.zero;
            Destroy(clipping.gameObject);
            yield return ValidateProfiles(output, report);
            partial = false; settings.effectMask = null; settings.pixelation = 0; useVideo = true;
            settings.virtualResolution = new Vector2(640,360);
            for (int sample = 0; sample < 4; sample++)
            {
                original = sample == 0; mode = sample < 2 ? 0 : 1; includeUI = sample == 3;
                yield return new WaitForSecondsRealtime(1);
                var times = new List<float>(); double gpuTotal = 0; int gpuSamples = 0, gpuInvalid = 0;
                ulong lastTimestamp = 0;
                var timing = new FrameTiming[1];
                until = Time.realtimeSinceStartup + 3;
                while (Time.realtimeSinceStartup < until)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    times.Add(Time.unscaledDeltaTime * 1000);
                    if (FrameTimingManager.GetLatestTimings(1,timing) > 0 && timing[0].frameStartTimestamp != lastTimestamp)
                    {
                        lastTimestamp = timing[0].frameStartTimestamp;
                        double gpu = timing[0].gpuFrameTime;
                        // DX11 can report disjoint timestamp queries as enormous positive durations.
                        // Invalidate this measurement instead of presenting such data as GPU cost.
                        if (gpu > 0 && gpu < 1000 && !double.IsNaN(gpu) && !double.IsInfinity(gpu)) { gpuTotal += gpu; gpuSamples++; }
                        else if (gpu != 0) gpuInvalid++;
                    }
                }
                float total = 0; foreach (float t in times) total += t; times.Sort();
                report.timings.Add(new Measurement { mode = new[] { "baseline-video", "three-surfaces-video", "fullscreen-video", "fullscreen-with-ui-video" }[sample],
                    frames = times.Count, meanMs = total / times.Count, p95Ms = times[Mathf.Clamp((int)(times.Count*.95f),0,times.Count-1)],
                    gpuMs = gpuSamples > 0 && gpuInvalid == 0 ? (float)(gpuTotal/gpuSamples) : -1, gpuValidSamples = gpuSamples, gpuInvalidSamples = gpuInvalid });
            }
            report.renderTexturesBefore = VideoTextures();
            Video.enabled = false; yield return null; yield return null;
            Video.enabled = true;
            until = Time.realtimeSinceStartup + 15;
            while (!Video.IsReady && Time.realtimeSinceStartup < until) yield return null;
            report.videoReopened = Video.IsReady;
            yield return new WaitForSecondsRealtime(.5f);
            report.errors = new List<string>(runtimeErrors);
            pendingReport = report; pendingOutput = output;
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
        }
        static int VideoTextures()
        {
            int count = 0; foreach (var rt in Resources.FindObjectsOfTypeAll<RenderTexture>()) if (rt.name == "CRT video frames") count++;
            return count;
        }
        static bool HasContent(Texture2D texture)
        {
            var pixels = texture.GetPixels32(); int visible = 0, samples = 0;
            for (int i = 0; i < pixels.Length; i += 64)
            { var p = pixels[i]; if (p.r > 40 || p.g > 40 || p.b > 40) visible++; samples++; }
            return visible > samples * .025f;
        }
        static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
    }
}
