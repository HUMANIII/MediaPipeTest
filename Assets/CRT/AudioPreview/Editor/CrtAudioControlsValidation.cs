using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MediaPipeTest.CRT.AudioPreview
{
    public static class CrtAudioControlsValidation
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string unity;
            public List<string> checks = new(), failures = new();
        }
        static bool running;
        [MenuItem("Tools/CRT/Validate Audio Controls")]
        public static async void Run() => await RunAsync();

        public static async Task<bool> RunAsync()
        {
            if (running) return false;
            running = true;
            var report = new Report { unity = Application.unityVersion };
            void Check(bool passed, string name)
            { report.checks.Add((passed ? "PASS " : "FAIL ") + name); if (!passed) report.failures.Add(name); }
            string root = "Logs/CRTAudioPreview";
            Directory.CreateDirectory(root);
            string folder = "Assets/CRT/AudioPreview/Editor/ControlsValidation-" + Guid.NewGuid().ToString("N");
            string history = EditorPrefs.GetString(CrtAudioHistory.PreferenceKey, "");
            bool hadHistory = EditorPrefs.HasKey(CrtAudioHistory.PreferenceKey);
            CrtAudioPreviewWindow window = null;
            var windows = new List<CrtAudioPreviewWindow>();
            int previewCount = Resources.FindObjectsOfTypeAll<AudioClip>().Count(a => a.name == "CRT Audio Tool Preview (temporary)");
            try
            {
                CrtAudioPreviewWindow.EnsureDefaultPreset();
                byte[] legacyFile = File.ReadAllBytes(CrtAudioPreviewWindow.DefaultPresetPath);
                foreach (var existing in Resources.FindObjectsOfTypeAll<CrtAudioPreviewWindow>()) existing.Engine?.Stop();
                var source = AssetDatabase.LoadAssetAtPath<AudioClip>(CrtAudioPreviewWindow.ExamplePath);
                var clip = await CrtAudioClipLoader.Read(source, default);
                var anchors = new[]
                {
                    new CrtAudioSettings { strength = .5f, lowCutHz = 150, highCutHz = 6500, distortion = .1f, noise = .025f },
                    new CrtAudioSettings(),
                    new CrtAudioSettings { lowCutHz = 450, highCutHz = 2200, distortion = .65f, noise = .25f }
                };
                for (int i = 0; i < anchors.Length; i++)
                {
                    var actual = CrtAudioDsp.Render(clip.samples, 1, clip.rate, CrtAudioControls.Tone((i - 1) * 100));
                    var expected = CrtAudioDsp.Render(clip.samples, 1, clip.rate, anchors[i]);
                    Check(actual.Zip(expected, (a, b) => Math.Abs(a - b)).Max() < .000001, "tone " + ((i - 1) * 100) + " matches existing anchor PCM");
                }
                var middle = CrtAudioControls.Tone(-50);
                Check(Math.Abs(middle.lowCutHz - Math.Sqrt(150 * 250)) < .001 && Math.Abs(middle.highCutHz - Math.Sqrt(6500 * 3500)) < .01
                    && Math.Abs(middle.strength - .75f) < .000001 && Math.Abs(middle.noise - .054165f) < .000001,
                    "midpoint uses logarithmic frequencies and linear effect amounts");
                bool monotonic = true;
                var previous = CrtAudioControls.Tone(-100);
                for (int i = -99; i <= 100; i++)
                {
                    var next = CrtAudioControls.Tone(i);
                    monotonic &= next.lowCutHz >= previous.lowCutHz && next.highCutHz <= previous.highCutHz && next.distortion >= previous.distortion
                        && next.noise >= previous.noise && next.strength >= previous.strength;
                    previous = next;
                }
                Check(monotonic, "all simple slider steps change controls monotonically");
                var controls = new CrtAudioControls { tone = -37 };
                controls.settings.outputDb = -9; controls.settings.peakProtection = false;
                var before = controls.Effective(); controls.SetDetailed(true);
                Check(Same(before, controls.Effective()), "entering detailed mode preserves every current audio setting");
                controls.settings.highCutHz = 1700;
                var changed = CrtAudioDsp.Render(clip.samples, 1, clip.rate, controls.Effective());
                var unchanged = CrtAudioDsp.Render(clip.samples, 1, clip.rate, before);
                Check(changed.Zip(unchanged, (a, b) => Math.Abs(a - b)).Max() > .01, "detailed edit changes actual audio output");
                controls.SetDetailed(false);
                Check(controls.tone == -37 && Same(before, controls.Effective()), "returning to simple mode restores last simple tone");
                controls.SetDetailed(true);
                Check(Same(before, controls.Effective()), "re-entering detailed mode starts from current simple tone");
                controls.ApplyTone(100);
                Check(controls.settings.outputDb == -9 && !controls.settings.peakProtection && controls.tone == -37,
                    "detailed tone buttons preserve volume, peak protection, and simple slider");
                foreach (float db in new[] { -40f, 12f })
                {
                    controls.settings.outputDb = db; controls.SetDetailed(false); controls.SetDetailed(true);
                    Check(controls.Effective().outputDb == db, "mode changes retain volume endpoint " + db + " dB");
                }

                EditorPrefs.DeleteKey(CrtAudioHistory.PreferenceKey);
                window = NewWindow(windows); await Wait(.1);
                Check(!window.Source && window.Engine == null && !window.Controls.detailed && window.Controls.tone == 0,
                    "new window without history is empty in simple mode at zero");
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                var preset = ScriptableObject.CreateInstance<CrtAudioPreset>();
                string presetPath = folder + "/Settings.asset";
                AssetDatabase.CreateAsset(preset, presetPath);
                window.Controls.tone = 42; window.Controls.settings.outputDb = -7; window.Controls.settings.peakProtection = false;
                window.Bypass = true; window.SavePreset(preset);
                var saved = window.Controls.Effective();
                Resources.UnloadAsset(preset); preset = AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(presetPath);
                Check(Same(saved, preset.ReadControls().Effective()) && !preset.ReadControls().detailed && preset.ReadControls().tone == 42,
                    "save without a clip preserves simple mode and parameters despite original preview bypass");
                string contents = File.ReadAllText(presetPath);
                Check(contents.Contains("formatVersion: 1") && !contents.Contains("source:") && !contents.Contains("bypass:") && !contents.Contains("AudioClip"),
                    "settings asset stores no source clip or preview bypass");
                window.LoadPreset(preset); window.Controls.SetDetailed(true); window.Controls.settings.noise = .317f;
                Check(File.ReadAllText(presetPath) == contents && preset.settings.noise != .317f, "editing controls does not mutate saved asset");
                window.SavePreset(preset); saved = window.Controls.Effective();
                Resources.UnloadAsset(preset); preset = AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(presetPath);
                window.LoadPreset(preset);
                Check(window.Controls.detailed && window.Controls.tone == 42 && Same(saved, window.Controls.Effective()),
                    "detailed settings asset round-trip restores mode, tone, volume and protection");
                var legacy = AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(CrtAudioPreviewWindow.DefaultPresetPath);
                window.LoadPreset(legacy);
                Check(window.Controls.detailed && Same(legacy.settings, window.Controls.Effective()), "legacy asset loads as detailed without changing tone");
                Check(legacyFile.SequenceEqual(File.ReadAllBytes(CrtAudioPreviewWindow.DefaultPresetPath)), "loading legacy asset does not rewrite it");

                window.SelectClip(source); await WaitForClip(window);
                Check(!CrtAudioHistory.LastPlayed() && !window.Engine.Playing, "selecting a clip neither records nor starts playback");
                window.Bypass = false;
                window.TogglePlay(); await Wait(.2);
                var liveEngine = window.Engine;
                int position = liveEngine.Position;
                window.Controls.SetDetailed(false); window.Controls.tone = -50; window.UpdatePreview();
                await Wait(.2);
                Check(window.Engine == liveEngine && liveEngine.Playing && liveEngine.Position > position,
                    "simple tone changes keep the same live engine and advancing playback position");
                position = liveEngine.Position;
                window.Controls.SetDetailed(true); window.UpdatePreview(); await Wait(.2);
                Check(window.Engine == liveEngine && liveEngine.Playing && liveEngine.Position > position,
                    "entering detailed mode preserves running playback");
                window.Engine.Stop();
                Check(CrtAudioHistory.LastPlayed() == source, "successful window playback records clip GUID");
                window.Controls.SetDetailed(true); window.Controls.tone = -23; window.Controls.settings.noise = .21f;
                string sessionSnapshot = EditorJsonUtility.ToJson(window);
                var clone = NewWindow(windows); EditorJsonUtility.FromJsonOverwrite(sessionSnapshot, clone); clone.InitializeSession();
                Check(clone.Source == source && clone.Controls.detailed && clone.Controls.tone == -23 && clone.Controls.settings.noise == .21f,
                    "serialized window keeps working controls in the same Editor session");
                var reopened = NewWindow(windows); await WaitForClip(reopened);
                Check(reopened.Source == source && !reopened.Engine.Playing && !reopened.Controls.detailed && reopened.Controls.tone == 0,
                    "reopened window restores last played clip without autoplay and resets simple controls");
                string fixture = folder + "/Other.wav", moved = folder + "/Moved.wav";
                CrtAudioDsp.WriteWav(fixture, Enumerable.Range(0, 44100).Select(i => .12f * (float)Math.Sin(2 * Math.PI * 400 * i / 44100)).ToArray(), 1, 44100);
                AssetDatabase.ImportAsset(fixture, ImportAssetOptions.ForceSynchronousImport);
                var second = AssetDatabase.LoadAssetAtPath<AudioClip>(fixture);
                window.LoadPreset(preset); saved = window.Controls.Effective();
                window.SelectClip(second); await WaitForClip(window);
                Check(CrtAudioHistory.LastPlayed() == source && Same(saved, window.Controls.Effective()),
                    "different clip uses same settings without replacing last played history on selection");
                window.TogglePlay(); await Wait(.2); window.Engine.Stop();
                string guid = AssetDatabase.AssetPathToGUID(fixture);
                Check(EditorPrefs.GetString(CrtAudioHistory.PreferenceKey) == guid, "new playback replaces persisted history with selected clip GUID");
                window.SelectClip(null);
                Check(AssetDatabase.MoveAsset(fixture, moved) == "" && AssetDatabase.GetAssetPath(CrtAudioHistory.LastPlayed()) == moved,
                    "last played GUID resolves after clip move");
                AssetDatabase.DeleteAsset(moved);
                var deletedWindow = NewWindow(windows); await Wait(.1);
                Check(!deletedWindow.Source && deletedWindow.Engine == null, "deleted last played clip restores empty window");
                Check(CrtAudioHistory.KeyForProject("E:/ProjectA/Assets") != CrtAudioHistory.KeyForProject("E:/ProjectB/Assets"),
                    "history keys isolate different Unity projects");
            }
            catch (Exception ex) { report.failures.Add(ex.ToString()); Debug.LogException(ex); }
            finally
            {
                foreach (var item in windows) if (item) UnityEngine.Object.DestroyImmediate(item);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                if (hadHistory) EditorPrefs.SetString(CrtAudioHistory.PreferenceKey, history); else EditorPrefs.DeleteKey(CrtAudioHistory.PreferenceKey);
                Check(Resources.FindObjectsOfTypeAll<AudioClip>().Count(a => a.name == "CRT Audio Tool Preview (temporary)") == previewCount,
                    "all validation windows release temporary preview clips");
                report.passed = report.failures.Count == 0;
                File.WriteAllText(root + "/controls-validation.json", JsonUtility.ToJson(report, true));
                running = false; Debug.Log("CRT_AUDIO_CONTROLS_" + (report.passed ? "PASS" : "FAIL"));
            }
            return report.passed;
        }

        static CrtAudioPreviewWindow NewWindow(List<CrtAudioPreviewWindow> windows)
        {
            var window = ScriptableObject.CreateInstance<CrtAudioPreviewWindow>();
            windows.Add(window); window.InitializeSession(); return window;
        }
        static bool Same(CrtAudioSettings a, CrtAudioSettings b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        static async Task WaitForClip(CrtAudioPreviewWindow window)
        {
            double limit = EditorApplication.timeSinceStartup + 10;
            while (window.Engine == null && EditorApplication.timeSinceStartup < limit) await Wait(.05);
            if (window.Engine == null) throw new Exception("Validation clip did not load.");
        }
        internal static Task Wait(double seconds)
        {
            var done = new TaskCompletionSource<bool>(); double end = EditorApplication.timeSinceStartup + seconds;
            void Update() { if (EditorApplication.timeSinceStartup < end) return; EditorApplication.update -= Update; done.SetResult(true); }
            EditorApplication.update += Update; return done.Task;
        }
    }
}
