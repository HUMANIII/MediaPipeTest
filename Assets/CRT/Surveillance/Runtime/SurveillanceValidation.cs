using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using MediaPipeTest.CRT.AudioPreview;

namespace MediaPipeTest.CRT.Surveillance
{
    // Opt-in standalone acceptance run. Normal Play mode never runs this component's checks.
    public sealed class SurveillanceValidation : MonoBehaviour
    {
        public SurveillancePlayer player;
        public SurveillanceFeed feed;
        public HospitalWanderer wanderer;
        public SurveillanceHud hud;
        [Serializable] public sealed class Report
        {
            public bool passed = true;
            public List<string> checks = new(), failures = new(), errors = new();
            public float observedSeconds, walkedMetres;
            public int arrivals, destinations, recoveries, walkingSamples, waitingSamples, wallOverlaps, reloads;
            public int animatedWalkingSamples, walkClipSamples, idleClipSamples;
            public float peakAudioRms;
            public float peakFootstepRms, footstepPresetGainRatio;
            public int footstepsPlayed, idleStepViolations, idleAudibleFrames;
            public List<string> transitionSamples = new();
        }
        static Report report;
        static int phase;
        static string output;
        bool active, observe;
        float started, nextSample;
        Vector3 lastPosition;
        Transform leg;
        Quaternion previousLegRotation;
        readonly float[] audioSamples = new float[256];
        SurveillanceFootstepAudio footsteps;
        int previousSteps;
        bool wasWaiting;
        float waitingSince;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeValidation()
        {
            if (!Environment.GetCommandLineArgs().Contains("-surveillanceValidate")) return;
            report = new Report(); phase = 0;
            Application.logMessageReceived += Error;
        }
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("-surveillanceValidate")) { enabled = false; return; }
            active = true; Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            footsteps = FindFirstObjectByType<SurveillanceFootstepAudio>();
            int index = Array.IndexOf(args, "-surveillanceOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "SurveillanceValidation");
            Directory.CreateDirectory(output); report ??= new Report();
            player.AcceptInput = false;
            leg = wanderer.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (leg) previousLegRotation = leg.localRotation;
            StartCoroutine(Guard(Run()));
        }
        static void Error(string condition, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) report.errors.Add(condition + "\n" + stack); }
        void OnApplicationQuit() { if (active) Application.logMessageReceived -= Error; }
        IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception ex) { report.failures.Add(ex.ToString()); Finish(); yield break; }
                if (!next) yield break;
                yield return routine.Current;
            }
        }
        void Check(bool success, string name)
        { report.checks.Add((success ? "PASS " : "FAIL ") + name); if (!success) report.failures.Add(name); }
        void Update()
        {
            if (active && feed.staticAudio.isPlaying)
            {
                feed.staticAudio.GetOutputData(audioSamples, 0);
                float squared = 0; foreach (float sample in audioSamples) squared += sample * sample;
                report.peakAudioRms = Mathf.Max(report.peakAudioRms, Mathf.Sqrt(squared / audioSamples.Length));
            }
            if (!observe || Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + .25f;
            report.walkedMetres += Vector3.Distance(lastPosition, wanderer.transform.position); lastPosition = wanderer.transform.position;
            if (wanderer.Waiting) report.waitingSamples++; else if (wanderer.Agent.velocity.magnitude > .2f) report.walkingSamples++;
            foreach (var clip in wanderer.animator.GetCurrentAnimatorClipInfo(0))
            {
                if (clip.weight > .7f && clip.clip.name.Contains("Walk")) report.walkClipSamples++;
                if (clip.weight > .7f && clip.clip.name.Contains("Idle")) report.idleClipSamples++;
            }
            if (leg)
            {
                if (wanderer.Agent.velocity.magnitude > .2f && Quaternion.Angle(previousLegRotation, leg.localRotation) > .5f) report.animatedWalkingSamples++;
                previousLegRotation = leg.localRotation;
            }
            Vector3 p = wanderer.transform.position;
            if (Physics.CheckCapsule(p + Vector3.up * .40f, p + Vector3.up * 1.45f, .12f, feed.feedCamera.cullingMask, QueryTriggerInteraction.Ignore)) report.wallOverlaps++;
            report.observedSeconds = Time.unscaledTime - started;
        }
        void LateUpdate()
        {
            if (!active || !observe || !footsteps) return;
            footsteps.Speaker.GetOutputData(audioSamples, 0);
            float squared = 0; foreach (float sample in audioSamples) squared += sample * sample;
            float rms = Mathf.Sqrt(squared / audioSamples.Length);
            report.peakFootstepRms = Mathf.Max(report.peakFootstepRms, rms);
            if (wanderer.Waiting)
            {
                if (!wasWaiting) waitingSince = Time.unscaledTime;
                if (wasWaiting && footsteps.StepsPlayed != previousSteps) report.idleStepViolations++;
                if (Time.unscaledTime - waitingSince > .65f && (footsteps.Speaker.isPlaying || rms > .0001f)) report.idleAudibleFrames++;
            }
            wasWaiting = wanderer.Waiting; previousSteps = footsteps.StepsPlayed;
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2);
            Check(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(r => r.name == "Surveillance Feed (runtime)") == 1, "one owned RenderTexture, phase " + phase);
            Check(Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name == "CRTSurface (CRT instance)") == 1, "one screen material, phase " + phase);
            Check(FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.clip == feed.staticAudio.clip) == 1, "one static AudioSource, phase " + phase);
            Check(footsteps && footsteps.CachedClipCount == 6 && RuntimeSteps().Length == 6, "six owned footstep clips, phase " + phase);
            Check(footsteps && footsteps.preset && footsteps.AppliedSettings != null && footsteps.Speaker != feed.staticAudio,
                "saved runtime preset and separate CRT footstep source, phase " + phase);
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "only player AudioListener, phase " + phase);
            if (phase > 0)
            {
                Check(!feed.staticAudio.isPlaying && !feed.IsTransitioning && feed.screen.transitionStrength == 0, "clean state after scene reentry " + phase);
                report.reloads++;
                if (phase < 2) { phase++; SceneManager.LoadScene(SceneManager.GetActiveScene().name); yield break; }
                Finish(); yield break;
            }
            started = Time.unscaledTime; lastPosition = wanderer.transform.position; observe = true;
            Check(wanderer.Agent.isOnNavMesh && wanderer.animator.isHuman, "saved NavMesh loaded and humanoid animation active");
            yield return Capture("room-entry");
            // Drive the real CharacterController into the back wall and desk, without bypassing collisions.
            PlacePlayer(new Vector3(11.6f, .05f, -.6f), Quaternion.identity);
            for (int i = 0; i < 150; i++) player.Move(Vector2.down, 1f / 60);
            Check(player.transform.position.z > -2.16f, "back wall blocks CharacterController");
            PlacePlayer(new Vector3(9.4f, .05f, -.4f), Quaternion.identity);
            for (int i = 0; i < 150; i++) player.Move(Vector2.up, 1f / 60);
            Check(player.transform.position.z < 1.0f, "desk blocks CharacterController");
            PlacePlayer(new Vector3(9.91f, .05f, -.75f), Quaternion.Euler(0, 8, 0));
            // Queue real Input System events and let SurveillancePlayer.Update consume the bindings.
            var keyboard = InputSystem.AddDevice<Keyboard>("Surveillance acceptance keyboard");
            player.AcceptInput = true;
            yield return Press(keyboard, Key.E);
            yield return new WaitForSecondsRealtime(.3f);
            Check(player.Seated && !player.MovingToSeat, "keyboard E enters fixed seated view");
            int keyboardChannel = feed.CurrentChannel;
            yield return Press(keyboard, Key.RightArrow); yield return new WaitForSecondsRealtime(.25f);
            Check(feed.CurrentChannel == (keyboardChannel + 1) % 4, "keyboard right arrow selects next channel");
            yield return Press(keyboard, Key.LeftArrow); yield return new WaitForSecondsRealtime(.25f);
            Check(feed.CurrentChannel == keyboardChannel, "keyboard left arrow selects previous channel");
            yield return Press(keyboard, Key.C); Check(!feed.CrtEnabled, "keyboard C enables original mode");
            yield return Press(keyboard, Key.C); Check(feed.CrtEnabled, "keyboard C restores CRT mode");
            yield return Press(keyboard, Key.Escape); Check(!player.Seated, "keyboard Escape stands up");
            yield return Press(keyboard, Key.E); yield return new WaitForSecondsRealtime(.3f);
            yield return Press(keyboard, Key.E); Check(!player.Seated, "keyboard E also stands up");
            var inputStart = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Check(Vector3.Distance(inputStart, player.transform.position) > .2f, "keyboard movement drives CharacterController");
            player.AcceptInput = false; InputSystem.RemoveDevice(keyboard);
            PlacePlayer(new Vector3(9.91f, .05f, -.75f), Quaternion.Euler(0, 8, 0));
            for (int i = 0; i < 3; i++)
            {
                var pos = player.transform.position; var rot = player.transform.rotation;
                var viewPos = player.view.transform.localPosition; var viewRot = player.view.transform.localRotation;
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                player.Sit(); Check(player.Seated && !player.Controller.enabled && Cursor.visible, "seat releases cursor and disables movement " + i);
                yield return new WaitForSecondsRealtime(.3f);
                Check(Vector3.Distance(player.view.transform.position, player.seatedView.position) < .001f && !player.MovingToSeat, "fixed view reached " + i);
                player.Move(Vector2.one, 1); Check(Vector3.Distance(pos, player.transform.position) < .001f, "seated movement blocked " + i);
                if (i == 1) feed.NextChannel();
                player.Stand();
                Check(Vector3.Distance(pos, player.transform.position) < .001f && Quaternion.Angle(rot, player.transform.rotation) < .01f
                    && Vector3.Distance(viewPos, player.view.transform.localPosition) < .001f && Quaternion.Angle(viewRot, player.view.transform.localRotation) < .01f
                    && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, "standing restores pose and cursor " + i);
                yield return new WaitForSecondsRealtime(.25f);
                if (i == 1) Check(!feed.IsTransitioning && feed.CurrentChannel == 1, "channel transition finishes after standing");
            }
            player.Sit(); yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("seated");
            int before = feed.CurrentChannel, starts = feed.AudioStarts, completed = feed.CompletedTransitions;
            float requested = Time.unscaledTime, observedCut = -1, observedEnd = -1, cutStrength = 0;
            feed.NextChannel();
            while (Time.unscaledTime - requested < .32f)
            {
                // Same public entry points used by buttons and keyboard. Only the first request may win.
                if (feed.IsTransitioning) { feed.NextChannel(); feed.PreviousChannel(); }
                float elapsed = Time.unscaledTime - requested;
                report.transitionSamples.Add($"t={elapsed:F4} channel={feed.CurrentChannel} noise={feed.screen.transitionStrength:F4}");
                if (feed.CurrentChannel != before && observedCut < 0) { observedCut = elapsed; cutStrength = feed.screen.transitionStrength; }
                if (!feed.IsTransitioning && observedEnd < 0) observedEnd = elapsed;
                yield return null;
            }
            Check(observedCut >= .035f && observedCut <= .09f && cutStrength >= .7f, "cut occurs at noise peak near 0.05 seconds");
            Check(observedEnd >= .18f && observedEnd <= .25f && feed.screen.transitionStrength == 0, "noise finishes near 0.20 seconds");
            Check(feed.AudioStarts == starts + 1 && feed.CompletedTransitions == completed + 1 && feed.CurrentChannel == (before + 1) % 4, "spam produces one channel change and one sound");
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForEndOfFrame();
                int c = feed.CurrentChannel;
                Check(footsteps.ListeningChannel == c && Mathf.Abs(footsteps.MicrophoneGain - footsteps.GainAtDistance(
                    Vector3.Distance(feed.channels[c].transform.position, wanderer.transform.position))) < .01f,
                    "footstep microphone follows displayed channel " + (c + 1));
                Check(Vector3.Distance(feed.feedCamera.transform.position, feed.channels[c].transform.position) < .01f
                    && Quaternion.Angle(feed.feedCamera.transform.rotation, feed.channels[c].transform.rotation) < .1f, "fixed camera pose CAM " + (c + 1));
                SaveTexture(feed.Texture, "cam-" + (c + 1));
                feed.NextChannel(); yield return new WaitForSecondsRealtime(.25f);
            }
            Check(feed.CurrentChannel == (before + 1) % 4, "four channels wrap forward");
            feed.PreviousChannel(); yield return new WaitForSecondsRealtime(.25f);
            Check(feed.CurrentChannel == (before + 4) % 4, "previous channel wraps backward");
            // The exact same CCTV texture is held for the visual comparison captures.
            feed.feedCamera.enabled = false; feed.brain.enabled = false; hud.Visible = false;
            feed.SetCrtEnabled(false); feed.screen.transitionStrength = 0; feed.screen.Refresh(); yield return Capture("same-frame-original");
            feed.screen.transitionStrength = 1; feed.screen.Refresh(); yield return Capture("same-frame-original-with-transition");
            feed.SetCrtEnabled(true); feed.screen.transitionStrength = 0; feed.screen.Refresh(); yield return Capture("same-frame-crt");
            feed.screen.transitionStrength = 1; feed.screen.Refresh(); yield return Capture("same-frame-glitch");
            // Retain the bezel's depth occlusion while rendering a white display against black geometry.
            var screenRenderer = feed.screen.GetComponent<Renderer>();
            var savedMaterials = new Dictionary<Renderer, Material[]>();
            // Use opaque depth-writing geometry so the bezel still occludes the display mesh.
            var black = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Surveillance validation mask" };
            black.SetColor("_BaseColor", Color.black); black.SetFloat("_Metallic", 1); black.SetFloat("_Smoothness", 0);
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == screenRenderer || (player.view.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                savedMaterials[renderer] = renderer.sharedMaterials;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => black).ToArray();
            }
            float aspect = feed.screen.displayAspect;
            feed.SetCrtEnabled(false); feed.screen.transitionStrength = 0; feed.screen.source = Texture2D.whiteTexture; feed.screen.displayAspect = 1;
            feed.screen.Refresh(); yield return Capture("screen-mask");
            foreach (var pair in savedMaterials) pair.Key.sharedMaterials = pair.Value;
            Destroy(black);
            feed.screen.source = feed.Texture; feed.screen.displayAspect = aspect;
            feed.SetCrtEnabled(true); feed.feedCamera.enabled = true; feed.brain.enabled = true; hud.Visible = true;
            feed.SetCrtEnabled(false); before = feed.CurrentChannel; starts = feed.AudioStarts;
            feed.NextChannel(); yield return new WaitForSecondsRealtime(.25f);
            Check(feed.CurrentChannel == (before + 1) % 4 && feed.AudioStarts == starts + 1 && !feed.screen.Instance.IsKeywordEnabled("_CRT_EFFECT_ON"), "original mode retains channel timing and audio");
            feed.SetCrtEnabled(true);
            while (Time.unscaledTime - started < 65) yield return null;
            observe = false; report.observedSeconds = Time.unscaledTime - started;
            report.arrivals = wanderer.Arrivals; report.destinations = wanderer.Destinations; report.recoveries = wanderer.StuckRecoveries;
            Check(report.walkedMetres > 15 && report.destinations >= 3 && report.arrivals >= 2, "65 seconds random patrol across multiple destinations");
            Check(report.walkingSamples > 20 && report.waitingSamples > 4, "walking and 1-3 second waiting states observed");
            Check(report.walkClipSamples > 20 && report.idleClipSamples > 4 && report.animatedWalkingSamples > 10, "retargeted Walk and Idle clips drive actual humanoid bones");
            Check(report.peakAudioRms > .0001f, "static AudioSource produces nonzero audio samples");
            report.footstepsPlayed = footsteps.StepsPlayed;
            Check(report.footstepsPlayed > 25 && report.peakFootstepRms > .0001f, "walking produces recorded footsteps through actual AudioSource output");
            Check(report.idleStepViolations == 0 && report.idleAudibleFrames == 0, "waiting stops new footsteps and becomes silent after the last tail");
            Check(report.wallOverlaps == 0 && report.recoveries == 0, "no wall penetration or persistent stalls observed");
            yield return Capture("patrol-complete");
            yield return ValidateFootstepPreset();
            File.WriteAllText(Path.Combine(output, "runtime-results.json"), JsonUtility.ToJson(report, true));
            phase = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        static AudioClip[] RuntimeSteps() => Resources.FindObjectsOfTypeAll<AudioClip>()
            .Where(c => c.name.StartsWith(SurveillanceFootstepAudio.RuntimeClipPrefix)).OrderBy(c => c.name).ToArray();
        static float[] Pcm(AudioClip clip) { var data = new float[clip.samples * clip.channels]; clip.GetData(data, 0); return data; }
        static double Rms(float[] data) => Math.Sqrt(data.Sum(x => (double)x * x) / data.Length);
        IEnumerator ValidateFootstepPreset()
        {
            var originalPreset = footsteps.preset;
            var beforeClip = RuntimeSteps()[0]; var before = Pcm(beforeClip);
            var sourceClip = footsteps.footsteps.First(c => beforeClip.name.EndsWith(c.name));
            var raw = Pcm(sourceClip);
            CrtAudioDsp.WriteWav(Path.Combine(output, "footstep-approved-original.wav"), raw, sourceClip.channels, sourceClip.frequency);
            CrtAudioDsp.WriteWav(Path.Combine(output, "footstep-runtime-crt.wav"), before, beforeClip.channels, beforeClip.frequency);
            Check(raw.Zip(before, (a, b) => Math.Abs(a - b)).Average() > .001, "saved CRT preset changes the actual cached footstep PCM");
            var controls = originalPreset.ReadControls(); controls.SetDetailed(true); controls.settings.outputDb = -6;
            var temporary = ScriptableObject.Instantiate(originalPreset); temporary.Store(controls);
            int rebuilds = footsteps.SettingsRebuilds;
            footsteps.preset = temporary;
            yield return new WaitForSecondsRealtime(.65f);
            var quieter = Pcm(RuntimeSteps()[0]);
            report.footstepPresetGainRatio = (float)(Rms(quieter) / Rms(before));
            Check(footsteps.SettingsRebuilds == rebuilds + 1 && Mathf.Abs(footsteps.AppliedSettings.outputDb + 6) < .001f
                && Mathf.Abs(report.footstepPresetGainRatio - Mathf.Pow(10, (-6 - originalPreset.ReadControls().Effective().outputDb) / 20)) < .002f,
                "changing saved preset is detected and output gain changes rendered PCM");
            feed.SetCrtEnabled(false); yield return null;
            Check(footsteps.preset == temporary && footsteps.AppliedSettings.strength == controls.Effective().strength,
                "C video comparison keeps the independent audio preset");
            feed.SetCrtEnabled(true);
            footsteps.preset = originalPreset; footsteps.RefreshPreset(); Destroy(temporary);
            footsteps.enabled = false; yield return null; yield return null;
            Check(RuntimeSteps().Length == 0 && !footsteps.Speaker.isPlaying, "disabling CRT footsteps stops playback and destroys all owned clips");
            footsteps.enabled = true; yield return null;
            Check(RuntimeSteps().Length == 6, "reenabling CRT footsteps creates exactly six fresh clips");
        }
        void PlacePlayer(Vector3 position, Quaternion rotation)
        { player.Controller.enabled = false; player.transform.SetPositionAndRotation(position, rotation); player.Controller.enabled = true; Physics.SyncTransforms(); }
        static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); Destroy(image);
        }
        static void SaveTexture(RenderTexture texture, string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(output, name + ".png"), copy.EncodeToPNG()); Destroy(copy);
        }
        void Finish()
        {
            report.passed = report.failures.Count == 0 && report.errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "runtime-results.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SURVEILLANCE_RUNTIME_" + (report.passed ? "PASS" : "FAIL") + " " + output);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
