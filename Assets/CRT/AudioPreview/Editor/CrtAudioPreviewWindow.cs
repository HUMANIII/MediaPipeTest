using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace MediaPipeTest.CRT.AudioPreview
{
    public sealed class CrtAudioPreviewWindow : EditorWindow
    {
        public const string ExamplePath = "Assets/CRT/AudioPreview/Examples/A_Recorded_Original.wav";
        public const string DefaultPresetPath = "Assets/CRT/AudioPreview/Presets/DefaultCRT.asset";
        [SerializeField] AudioClip source;
        [SerializeField] CrtAudioPreset preset;
        [SerializeField] CrtAudioControls controls = new();
        [SerializeField] string editorSession;
        [SerializeField] bool loop = true, bypass;
        Vector2 scroll;
        CrtDecodedAudio decoded;
        CrtAudioPreviewEngine engine;
        CancellationTokenSource loading;
        float[] waveform;
        string error, notice;
        bool reading;
        double nextRepaint;
        public CrtAudioPreviewEngine Engine => engine;
        public CrtDecodedAudio Decoded => decoded;
        internal AudioClip Source => source;
        internal CrtAudioControls Controls => controls;
        internal bool Bypass { get => bypass; set { bypass = value; UpdatePreview(); } }

        [MenuItem("Tools/CRT/Audio Preview")]
        public static void Open()
        {
            EnsureDefaultPreset();
            var window = GetWindow<CrtAudioPreviewWindow>("CRT Audio Lab");
            window.minSize = new Vector2(490, 620); window.Show();
        }
        public static void EnsureDefaultPreset()
        {
            var importer = AssetImporter.GetAtPath(ExamplePath) as AudioImporter;
            if (importer)
            {
                var sample = importer.defaultSampleSettings;
                if (sample.compressionFormat != AudioCompressionFormat.PCM || sample.loadType != AudioClipLoadType.DecompressOnLoad)
                { sample.compressionFormat = AudioCompressionFormat.PCM; sample.loadType = AudioClipLoadType.DecompressOnLoad; importer.defaultSampleSettings = sample; importer.SaveAndReimport(); }
            }
            if (AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(DefaultPresetPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPresetPath));
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(CreateInstance<CrtAudioPreset>(), DefaultPresetPath);
        }
        void OnEnable()
        {
            minSize = new Vector2(490, 620); controls ??= new();
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAudio;
            EditorApplication.delayCall += LoadOnOpen;
        }
        void LoadOnOpen()
        {
            if (!this) return;
            InitializeSession();
            if (source) SelectClip(source);
        }
        internal void InitializeSession()
        {
            if (editorSession == CrtAudioHistory.Session) return;
            editorSession = CrtAudioHistory.Session;
            controls = new(); preset = null; bypass = false; loop = true;
            source = CrtAudioHistory.LastPlayed();
        }
        void OnDisable()
        {
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeAudio; EditorApplication.delayCall -= LoadOnOpen;
            DisposeAudio();
        }
        void DisposeAudio()
        {
            loading?.Cancel(); loading?.Dispose(); loading = null;
            engine?.Dispose(); engine = null; decoded = null; waveform = null;
        }
        void PlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingEditMode) engine?.Stop(); }
        void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextRepaint) return;
            nextRepaint = EditorApplication.timeSinceStartup + .05;
            if (engine?.Playing == true || reading) Repaint();
        }
        CrtAudioSettings CurrentSettings()
            => controls.Preview(bypass, decoded?.rate ?? 44100);
        internal void UpdatePreview() { engine?.SetSettings(CurrentSettings()); Repaint(); }

        public async void SelectClip(AudioClip clip)
        {
            loading?.Cancel(); loading?.Dispose(); loading = new();
            var token = loading.Token;
            engine?.Dispose(); engine = null; decoded = null; waveform = null;
            source = clip; error = notice = null; reading = clip;
            try
            {
                if (!clip) return;
                var data = await CrtAudioClipLoader.Read(clip, token);
                token.ThrowIfCancellationRequested(); if (!this) return;
                decoded = data; engine = new CrtAudioPreviewEngine(data, CurrentSettings());
                waveform = new float[360];
                for (int i = 0; i < data.Frames; i++)
                {
                    int bin = Math.Min(waveform.Length - 1, (int)((long)i * waveform.Length / data.Frames));
                    for (int c = 0; c < data.channels; c++) waveform[bin] = Math.Max(waveform[bin], Math.Abs(data.samples[i * data.channels + c]));
                }
                notice = "재생 중에도 설정을 바꿀 수 있어요.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) error = ex.Message; }
            finally { if (this && !token.IsCancellationRequested) { reading = false; Repaint(); } }
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);
            GUILayout.Label("CRT AUDIO LAB", new GUIStyle(EditorStyles.boldLabel) { fontSize = 23, fixedHeight = 30 });
            EditorGUILayout.LabelField("발소리부터 다른 오디오까지, 원본과 CRT 음색을 비교해 보세요.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                var next = (AudioClip)EditorGUILayout.ObjectField("오디오 클립", source, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck()) SelectClip(next);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("A 발소리 불러오기")) SelectClip(AssetDatabase.LoadAssetAtPath<AudioClip>(ExamplePath));
                    if (GUILayout.Button("선택한 클립", GUILayout.Width(100)) && Selection.activeObject is AudioClip selected) SelectClip(selected);
                }
                EditorGUILayout.LabelField(decoded == null ? "프로젝트의 AudioClip을 위 칸으로 끌어 넣으세요." :
                    $"{decoded.Duration:F2}초  ·  {decoded.rate:N0} Hz  ·  {decoded.channels}채널", EditorStyles.miniLabel);
            }
            DrawWaveform();
            using (new EditorGUI.DisabledScope(engine == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string playLabel = engine?.Paused == true ? "▶ 계속" : engine?.Playing == true ? "Ⅱ 일시정지" : "▶ 재생";
                    if (GUILayout.Button(playLabel, GUILayout.Height(30))) TogglePlay();
                    if (GUILayout.Button("■ 정지", GUILayout.Height(30), GUILayout.Width(90))) engine?.Stop();
                    EditorGUI.BeginChangeCheck(); loop = GUILayout.Toggle(loop, "반복", "Button", GUILayout.Height(30), GUILayout.Width(65));
                    if (EditorGUI.EndChangeCheck()) engine?.SetLoop(loop);
                }
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.Slider("재생 위치", engine == null || decoded == null ? 0 : engine.Position / (float)decoded.rate, 0, decoded?.Duration ?? 1);
                if (EditorGUI.EndChangeCheck() && decoded != null) engine?.Seek((int)(time * decoded.rate));
            }
            GUILayout.Space(8);
            EditorGUI.BeginChangeCheck();
            int mode = GUILayout.Toolbar(bypass ? 0 : 1, new[] { "원본 듣기", "CRT 효과 듣기" }, GUILayout.Height(30));
            bypass = mode == 0;
            using (new EditorGUI.DisabledScope(controls.detailed))
                controls.tone = EditorGUILayout.Slider(new GUIContent("음색 강도", "−100: 약하게 / 0: 기본 CRT / +100: 강하게"), controls.tone, -100, 100);
            EditorGUILayout.LabelField(controls.detailed ? "상세 설정으로 조절 중이에요." : "−100 약하게     ·     0 기본 CRT     ·     +100 강하게", EditorStyles.miniLabel);
            controls.settings.outputDb = EditorGUILayout.Slider("출력 볼륨 (dB)", controls.settings.outputDb, -40, 12);
            controls.settings.peakProtection = EditorGUILayout.Toggle("피크 보호", controls.settings.peakProtection);
            bool nextDetailed = EditorGUILayout.ToggleLeft("자세히 조절하기", controls.detailed);
            controls.SetDetailed(nextDetailed);
            bool changed = EditorGUI.EndChangeCheck();
            GUILayout.Space(8);
            if (controls.detailed)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("스피커 음색", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var settings = controls.settings;
                settings.strength = EditorGUILayout.Slider("효과 혼합 (%)", settings.strength * 100, 0, 100) / 100;
                settings.lowCutHz = EditorGUILayout.Slider("저음 제거 (Hz)", settings.lowCutHz, 20, 2000);
                settings.highCutHz = EditorGUILayout.Slider("고음 제한 (Hz)", settings.highCutHz, Math.Max(800, settings.lowCutHz + 100), 20000);
                settings.distortion = EditorGUILayout.Slider("찌그러짐 (%)", settings.distortion * 100, 0, 100) / 100;
                settings.noise = EditorGUILayout.Slider("잡음 (%)", settings.noise * 100, 0, 100) / 100;
                changed |= EditorGUI.EndChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("약하게")) { controls.ApplyTone(-100); changed = true; }
                    if (GUILayout.Button("기본 CRT")) { controls.ApplyTone(0); changed = true; }
                    if (GUILayout.Button("강하게")) { controls.ApplyTone(100); changed = true; }
                }
            }
            if (changed) { controls.settings = controls.settings.Validated(); controls.tone = CrtAudioControls.ClampTone(controls.tone); UpdatePreview(); }
            float peak = engine?.Playing == true ? engine.Peak : 0;
            EditorGUILayout.LabelField("출력 피크", peak > .00001f ? $"{20 * Mathf.Log10(peak):F1} dBFS" : "—");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                var nextPreset = (CrtAudioPreset)EditorGUILayout.ObjectField("설정 에셋", preset, typeof(CrtAudioPreset), false);
                if (EditorGUI.EndChangeCheck()) LoadPreset(nextPreset);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!preset)) if (GUILayout.Button("설정 에셋에 저장")) SavePreset(preset);
                    if (GUILayout.Button("새 설정 에셋 저장…")) SaveNewPreset();
                }
                EditorGUILayout.LabelField("효과 설정을 저장해 다른 클립에도 사용할 수 있어요.", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("저장 버튼을 누르기 전에는 에셋이 변경되지 않아요.", EditorStyles.miniLabel);
            }
            if (reading) EditorGUILayout.HelpBox("클립을 읽는 중…", MessageType.Info);
            else if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            else if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorGUILayout.HelpBox("Play 모드를 종료한 뒤 미리듣기를 사용할 수 있어요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space && GUIUtility.keyboardControl == 0)
            { TogglePlay(); Event.current.Use(); }
        }
        public void TogglePlay()
        {
            if (engine == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (engine.Paused) engine.Resume();
            else if (engine.Playing) engine.Pause();
            else
            {
                engine.Play(loop);
                if (engine.Playing) CrtAudioHistory.RememberPlayed(source);
            }
        }
        void DrawWaveform()
        {
            Rect rect = GUILayoutUtility.GetRect(10, 75, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.10f, .13f, .14f));
            if (waveform == null) return;
            Handles.BeginGUI(); Handles.color = new Color(.48f, .85f, .73f);
            for (int i = 0; i < waveform.Length; i++)
            {
                float x = rect.x + (float)i / waveform.Length * rect.width, h = Mathf.Min(1, waveform[i]) * rect.height * .46f;
                Handles.DrawLine(new Vector3(x, rect.center.y - h), new Vector3(x, rect.center.y + h));
            }
            Handles.color = Color.white;
            float cursor = rect.x + Mathf.Clamp01((engine?.Position ?? 0) / (float)decoded.Frames) * rect.width;
            Handles.DrawLine(new Vector3(cursor, rect.y), new Vector3(cursor, rect.yMax)); Handles.EndGUI();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            { engine?.Seek((int)((Event.current.mousePosition.x - rect.x) / rect.width * decoded.Frames)); Event.current.Use(); }
        }
        internal void LoadPreset(CrtAudioPreset target)
        {
            preset = target;
            if (target) controls = target.ReadControls();
            UpdatePreview();
        }
        internal void SavePreset(CrtAudioPreset target)
        {
            if (!target) return;
            Undo.RecordObject(target, "Save CRT audio preset"); target.Store(controls);
            EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
            error = null; notice = "설정 에셋을 저장했어요.";
        }
        void SaveNewPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("CRT 오디오 설정 에셋 저장", "MyCrtAudio", "asset", "설정 에셋을 저장할 위치를 선택하세요.", "Assets/CRT/AudioPreview/Presets");
            if (string.IsNullOrEmpty(path)) return;
            var target = AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(path);
            if (!target && AssetDatabase.LoadMainAssetAtPath(path))
            { error = "CRT 오디오 설정 에셋과 다른 파일은 덮어쓸 수 없어요."; return; }
            if (!target) { target = CreateInstance<CrtAudioPreset>(); AssetDatabase.CreateAsset(target, path); }
            preset = target; SavePreset(target);
        }
    }
}
