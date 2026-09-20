using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MediaPipeTest.CRT
{
    // Optional demonstration capture. Normal scene playback never adds this component.
    public sealed class CrtDemoRecording : MonoBehaviour
    {
        const int FrameRate = 30;
        const int SecondsPerChapter = 4;
        static readonly string[] Chapters = {
            "01 / ORIGINAL VIDEO", "02 / CRT - 3D + SPRITE + CANVAS",
            "03 / PIXELATION - 640 x 360 TO 160 x 90", "04 / PARTIAL UV REGION",
            "05 / 3D SURFACE ONLY", "06 / 2D SPRITE ONLY",
            "07 / FULL SCREEN - DISPLAY UI EXCLUDED", "08 / FULL SCREEN - DISPLAY UI INCLUDED",
            "09 / CANVAS UI ONLY"
        };
        CrtDemo demo;
        Text title;
        readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
        readonly List<string> errors = new List<string>();
        int previousCaptureRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crtRecord") < 0) return;
            var demo = FindFirstObjectByType<CrtDemo>();
            if (demo) demo.gameObject.AddComponent<CrtDemoRecording>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            previousCaptureRate = Time.captureFramerate;
            demo = GetComponent<CrtDemo>();
            foreach (var slider in demo.GetComponentsInChildren<Slider>()) sliders.Add(slider.name, slider);
            foreach (var text in demo.GetComponentsInChildren<Text>())
                if (text.text == "CRT  /  PHOSPHOR LAB") title = text;
            if (title) { title.rectTransform.sizeDelta = new Vector2(1600, 50); title.fontSize = 29; }
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-crtOutput");
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.Combine(Application.persistentDataPath, "CRTRecording", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            string frames = Path.Combine(output, "frames");
            if (Directory.Exists(frames)) throw new IOException("Use a fresh recording output directory: " + output);
            Directory.CreateDirectory(frames);

            while (!SplashScreen.isFinished) yield return null;
            // Recreate the swapchain for unattended players, as in the validation runner.
            Screen.SetResolution(1918, 1078, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.2f);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.5f);
            float deadline = Time.realtimeSinceStartup + 30;
            while (!demo.Video.IsReady && demo.Video.Error == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!demo.Video.IsReady) { Debug.LogError("CRT recording: video did not prepare"); Application.Quit(1); yield break; }
            // Keep the decoder on real time: synchronous capture stalls at loop boundaries
            // with this Windows VideoPlayer backend. Preserve actual frame timestamps below.
            demo.Video.Player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            Time.captureFramerate = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;
            long previousVideoFrame = -1;
            int movingFrames = 0;
            int count = 0;
            int totalSeconds = Chapters.Length * SecondsPerChapter;
            var timestamps = new List<double>();
            double started = Time.realtimeSinceStartupAsDouble;
            int previousChapter = -1;
            while (Time.realtimeSinceStartupAsDouble - started < totalSeconds)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - started;
                int chapter = Math.Min(Chapters.Length - 1, (int)(elapsed / SecondsPerChapter));
                float progress = (float)(elapsed % SecondsPerChapter / SecondsPerChapter);
                ApplyChapter(chapter, progress);
                // Let the normal demo Update and material LateUpdate consume these settings.
                yield return null;
                yield return new WaitForEndOfFrame();
                timestamps.Add(Time.realtimeSinceStartupAsDouble - started);
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(frames, count.ToString("D5") + ".jpg"), texture.EncodeToJPG(100));
                Destroy(texture);
                long videoFrame = demo.Video.Player.frame;
                if (videoFrame != previousVideoFrame) movingFrames++;
                previousVideoFrame = videoFrame;
                if (chapter != previousChapter) Debug.Log("CRT_RECORDING " + Chapters[chapter]);
                previousChapter = chapter;
                count++;
            }
            Time.captureFramerate = previousCaptureRate;
            var concat = new StringBuilder("ffconcat version 1.0\n");
            for (int frame = 0; frame < count; frame++)
            {
                double duration = frame + 1 < count ? timestamps[frame + 1] - timestamps[frame]
                    : Math.Max(.001, totalSeconds - timestamps[frame] + timestamps[0]);
                concat.Append("file 'frames/").Append(frame.ToString("D5")).Append(".jpg'\noption framerate 1000\n");
                concat.Append("duration ").Append(duration.ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
            }
            concat.Append("file 'frames/").Append((count - 1).ToString("D5")).Append(".jpg'\noption framerate 1000\n");
            File.WriteAllText(Path.Combine(output, "frames.ffconcat"), concat.ToString());
            File.WriteAllText(Path.Combine(output, "recording.json"), JsonUtility.ToJson(new RecordingReport {
                frames = count, fps = FrameRate, seconds = totalSeconds,
                width = Screen.width, height = Screen.height, videoFrameChanges = movingFrames,
                chapters = Chapters, errors = errors.ToArray()
            }, true));
            bool passed = errors.Count == 0 && count >= totalSeconds * 20 && movingFrames > count * .8f;
            Debug.Log("CRT_RECORDING " + (passed ? "PASS " : "FAIL ") + output);
            Application.Quit(passed ? 0 : 1);
        }

        void ApplyChapter(int chapter, float progress)
        {
            if (title) title.text = Chapters[chapter];
            demo.original = chapter == 0;
            demo.useVideo = true;
            demo.partial = chapter == 3;
            demo.includeUI = chapter == 7;
            demo.mode = chapter == 4 ? 2 : chapter == 5 ? 3 : chapter == 6 || chapter == 7 ? 1 : chapter == 8 ? 4 : 0;
            // Use the same slider callbacks as manual input, keeping displayed values accurate.
            Set("Pixelation", chapter == 2 ? Mathf.SmoothStep(0, 1, progress * 2) : 0);
            Set("Virtual width", chapter == 2 ? Mathf.Lerp(640, 160, Mathf.SmoothStep(0, 1, progress)) : 640);
            Set("Virtual height", chapter == 2 ? Mathf.Lerp(360, 90, Mathf.SmoothStep(0, 1, progress)) : 360);
        }
        void Set(string name, float value) => sliders[name].value = value;
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void OnDestroy()
        {
            Time.captureFramerate = previousCaptureRate;
            Application.logMessageReceived -= OnLog;
        }
        [Serializable] sealed class RecordingReport
        {
            public int frames, fps, seconds, width, height, videoFrameChanges;
            public string[] chapters, errors;
        }
    }
}
