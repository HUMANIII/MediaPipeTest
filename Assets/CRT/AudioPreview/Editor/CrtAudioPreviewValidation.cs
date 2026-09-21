using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MediaPipeTest.CRT.AudioPreview
{
    public static class CrtAudioPreviewValidation
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string unity;
            public List<string> checks = new(), failures = new();
            public int callbackCount, streamedSamples;
            public float observedPeak, observedRms;
        }
        static bool running;
        [MenuItem("Tools/CRT/Validate Audio Preview")]
        public static async void Run() => await RunAsync();
        internal static async Task RunAsync()
        {
            if (running) return;
            running = true; var report = new Report { unity = Application.unityVersion };
            string root = "Logs/CRTAudioPreview"; Directory.CreateDirectory(root);
            void Check(bool pass, string label)
            { report.checks.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) report.failures.Add(label); }
            CrtAudioPreviewEngine engine = null;
            string fixtureId = Guid.NewGuid().ToString("N");
            string temporaryClip = "Assets/CRT/AudioPreview/Editor/ValidationCompressed-" + fixtureId + ".wav";
            string temporaryPreset = "Assets/CRT/AudioPreview/Editor/ValidationPreset-" + fixtureId + ".asset";
            try
            {
                var openWindow = Resources.FindObjectsOfTypeAll<CrtAudioPreviewWindow>().FirstOrDefault();
                openWindow?.Engine?.Stop();
                CrtAudioPreviewWindow.EnsureDefaultPreset();
                var source = AssetDatabase.LoadAssetAtPath<AudioClip>(CrtAudioPreviewWindow.ExamplePath);
                var clip = await CrtAudioClipLoader.Read(source, CancellationToken.None);
                Check(clip.channels == 1 && clip.rate == 44100 && Math.Abs(clip.Duration - 8) < .001, "approved A example imports as eight-second mono PCM");
                var rawSettings = new CrtAudioSettings { strength = 0, peakProtection = false };
                var dry = CrtAudioDsp.Render(clip.samples, clip.channels, clip.rate, rawSettings);
                Check(dry.SequenceEqual(clip.samples), "zero strength preserves every source PCM sample");
                var quiet = rawSettings.Copy(); quiet.outputDb = -6;
                var quieter = CrtAudioDsp.Render(clip.samples, 1, clip.rate, quiet);
                Check(Math.Abs(Rms(quieter) / Rms(dry) - Math.Pow(10, -.3)) < .00001, "output level is independent and follows -6 dB gain");
                var tone = new CrtAudioSettings { noise = 0 };
                var full = CrtAudioDsp.Render(clip.samples, 1, clip.rate, tone);
                var halfSettings = tone.Copy(); halfSettings.strength = .5f;
                var half = CrtAudioDsp.Render(clip.samples, 1, clip.rate, halfSettings);
                Check(Difference(full, dry) > .005 && Math.Abs(Difference(half, dry) / Difference(full, dry) - .5) < .001,
                    "half strength is halfway between dry and processed PCM");
                var silence = new float[clip.rate];
                Check(CrtAudioDsp.Render(silence, 1, clip.rate, tone).All(x => x == 0), "noise zero produces exact silence from silence");
                var noisy = tone.Copy(); noisy.noise = .25f;
                Check(Rms(CrtAudioDsp.Render(silence, 1, clip.rate, noisy)) > .0001, "noise control adds signal independently");
                var stereo = new float[clip.rate * 2];
                for (int i = 0; i < clip.rate; i++) stereo[2 * i] = .2f * (float)Math.Sin(2 * Math.PI * 1000 * i / clip.rate);
                var stereoOut = CrtAudioDsp.Render(stereo, 2, clip.rate, tone);
                Check(stereoOut.Where((_, i) => i % 2 == 1).All(x => x == 0) && Rms(stereoOut) > .03,
                    "stereo channels remain separate");
                var extreme = tone.Copy(); extreme.outputDb = 12;
                var loud = Enumerable.Repeat(.9f, 44100).ToArray(); extreme.strength = 0;
                Check(CrtAudioDsp.Render(loud, 1, 44100, extreme).Max() < 1, "peak protection prevents overload");
                var preset = ScriptableObject.CreateInstance<CrtAudioPreset>(); preset.settings = halfSettings;
                AssetDatabase.CreateAsset(preset, temporaryPreset); AssetDatabase.SaveAssetIfDirty(preset);
                Resources.UnloadAsset(preset);
                var loaded = AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(temporaryPreset);
                Check(loaded.settings.strength == .5f && loaded.settings.noise == 0, "preset asset round-trip preserves settings");
                CrtAudioDsp.WriteWav(root + "/preview-original.wav", dry, 1, clip.rate);
                var defaults = new CrtAudioSettings();
                var processed = CrtAudioDsp.Render(clip.samples, 1, clip.rate, defaults);
                CrtAudioDsp.WriteWav(root + "/preview-crt.wav", processed, 1, clip.rate);
                CrtAudioDsp.WriteWav(root + "/preview-stereo.wav", stereoOut, 2, clip.rate);
                Check(new FileInfo(root + "/preview-crt.wav").Length == 44 + clip.samples.Length * 2,
                    "export writes a complete PCM WAV of unchanged duration");
                // This fixture changes only its own importer, not the user's source clip.
                File.Copy(CrtAudioPreviewWindow.ExamplePath, temporaryClip, true);
                AssetDatabase.ImportAsset(temporaryClip, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(temporaryClip);
                var options = importer.defaultSampleSettings; options.loadType = AudioClipLoadType.Streaming;
                options.compressionFormat = AudioCompressionFormat.Vorbis; importer.defaultSampleSettings = options; importer.SaveAndReimport();
                var streaming = AssetDatabase.LoadAssetAtPath<AudioClip>(temporaryClip);
                var fallback = await CrtAudioClipLoader.Read(streaming, CancellationToken.None);
                Check(Math.Abs(fallback.Duration - 8) < .001 && Rms(fallback.samples) > .01
                    && importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming,
                    "streaming source decodes from local file without changing its importer");
                engine = new CrtAudioPreviewEngine(clip, defaults); engine.Play(true);
                int initialSamples = engine.RenderedSamples;
                for (int i = 0; i < 16; i++)
                {
                    await Wait(.15); report.observedPeak = Math.Max(report.observedPeak, engine.Peak); report.observedRms = Math.Max(report.observedRms, engine.OutputRms);
                }
                Check(engine.Playing && engine.RenderedSamples > initialSamples && report.observedPeak > .01,
                    "Edit-mode streaming preview advances and renders nonzero audio after Play");
                float before = engine.Position / (float)clip.rate;
                engine.SetSettings(rawSettings); await Wait(.3);
                Check(engine.Playing && engine.Position / (float)clip.rate > before, "changing effects preserves running transport position");
                engine.Pause(); await Wait(.15); int pausedPosition = engine.Position;
                await Wait(.3);
                Check(engine.Paused && Math.Abs(engine.Position - pausedPosition) < 100, "pause holds the native preview position");
                engine.Resume(); await Wait(.3);
                Check(!engine.Paused && engine.Position > pausedPosition, "resume continues native playback");
                engine.Seek(clip.rate * 6); await Wait(.25);
                Check(Math.Abs(engine.Position / (float)clip.rate - 6.25f) < .5, "seek changes the native preview position");
                engine.SetLoop(true); engine.Seek(clip.Frames - clip.rate / 4); await Wait(.7);
                Check(engine.Playing && engine.Position < clip.rate * 2, "loop wraps without rebuilding the preview clip");
                report.callbackCount = engine.CallbackCount; report.streamedSamples = engine.RenderedSamples;
                engine.Stop(); Check(!engine.Playing && engine.Position == 0, "stop resets playback to the beginning");
                engine.Dispose(); engine = null;
                int existing = openWindow?.Engine == null ? 0 : 1;
                Check(Resources.FindObjectsOfTypeAll<AudioClip>().Count(a => a.name == "CRT Audio Tool Preview (temporary)") == existing,
                    "disposing preview releases its temporary AudioClip");
            }
            catch (Exception ex) { report.failures.Add(ex.ToString()); Debug.LogException(ex); }
            finally
            {
                engine?.Dispose(); AssetDatabase.DeleteAsset(temporaryClip); AssetDatabase.DeleteAsset(temporaryPreset);
                report.passed = report.failures.Count == 0;
                File.WriteAllText(root + "/validation.json", JsonUtility.ToJson(report, true));
                File.WriteAllText("Temp/CRT-audio-result.txt", "AUDIO_PREVIEW_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
                Debug.Log("CRT_AUDIO_PREVIEW_" + (report.passed ? "PASS" : "FAIL")); running = false;
            }
        }
        static double Rms(float[] x) => Math.Sqrt(x.Sum(v => (double)v * v) / x.Length);
        static double Difference(float[] a, float[] b) => Math.Sqrt(a.Select((v, i) => Math.Pow(v - b[i], 2)).Average());
        static Task Wait(double seconds)
        {
            var done = new TaskCompletionSource<bool>(); double end = EditorApplication.timeSinceStartup + seconds;
            void Update() { if (EditorApplication.timeSinceStartup < end) return; EditorApplication.update -= Update; done.SetResult(true); }
            EditorApplication.update += Update; return done.Task;
        }
    }
}
