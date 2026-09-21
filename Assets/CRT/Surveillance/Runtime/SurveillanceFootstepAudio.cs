using System;
using MediaPipeTest.CRT.AudioPreview;
using UnityEngine;

namespace MediaPipeTest.CRT.Surveillance
{
    // A dedicated source on the CRT: the hospital has no audible AudioSource.
    // Short processed clips are owned by this component; imported clips are never modified.
    [RequireComponent(typeof(AudioSource))]
    public sealed class SurveillanceFootstepAudio : MonoBehaviour
    {
        public SurveillanceFeed feed;
        public HospitalWanderer walker;
        public CrtAudioPreset preset;
        [Tooltip("Alternating left/right recorded steps: L1, R1, L2, R2, L3, R3. PCM / Decompress On Load.")]
        public AudioClip[] footsteps = Array.Empty<AudioClip>();
        [Min(.1f)] public float metresPerStep = .5775f;
        [Min(0)] public float microphoneNear = 2;
        [Min(1)] public float microphoneFar = 22;
        [Range(0, 1)] public float speakerVolume = 1;
        public AudioSource Speaker { get; private set; }
        public int StepsPlayed { get; private set; }
        public int SettingsRebuilds { get; private set; }
        public int ListeningChannel { get; private set; }
        public float MicrophoneGain { get; private set; }
        public int CachedClipCount => processed?.Length ?? 0;
        public CrtAudioSettings AppliedSettings => applied?.Copy();
        public const string RuntimeClipPrefix = "CCTV footstep (runtime) ";
        AudioClip[] processed;
        CrtAudioSettings applied;
        CrtAudioPreset appliedPreset;
        Vector3 lastPosition;
        float distance, nextStepDistance, nextSettingsCheck;
        int stepIndex;

        void OnEnable()
        {
            Speaker = GetComponent<AudioSource>();
            Speaker.playOnAwake = false; Speaker.loop = false;
            lastPosition = walker ? walker.transform.position : Vector3.zero;
            distance = 0; nextStepDistance = .15f; stepIndex = 0;
            StepsPlayed = 0; SettingsRebuilds = 0;
            RefreshPreset();
        }

        // Game code may call this after assigning a different saved preset.
        public void RefreshPreset()
        {
            ClearClips();
            applied = preset ? preset.ReadControls().Effective() : null;
            appliedPreset = preset;
            nextSettingsCheck = Time.unscaledTime + .5f;
            if (applied == null || footsteps == null || footsteps.Length == 0) return;
            processed = new AudioClip[footsteps.Length];
            try
            {
                for (int i = 0; i < footsteps.Length; i++)
                {
                    var clip = footsteps[i];
                    if (!clip || clip.loadType != AudioClipLoadType.DecompressOnLoad)
                        throw new InvalidOperationException("CCTV footsteps require readable Decompress On Load clips.");
                    var pcm = new float[clip.samples * clip.channels];
                    if (!clip.GetData(pcm, 0)) throw new InvalidOperationException("Cannot read " + clip.name);
                    var output = CrtAudioDsp.Render(pcm, clip.channels, clip.frequency, applied);
                    // Fade the added hiss too, so each transmission ends without a click.
                    for (int frame = 0; frame < clip.samples; frame++)
                    {
                        float envelope = Mathf.Min(1, frame / (.002f * clip.frequency))
                            * Mathf.Min(1, (clip.samples - 1 - frame) / (.018f * clip.frequency));
                        for (int c = 0; c < clip.channels; c++) output[frame * clip.channels + c] *= envelope;
                    }
                    processed[i] = AudioClip.Create(RuntimeClipPrefix + clip.name, clip.samples, clip.channels, clip.frequency, false);
                    processed[i].hideFlags = HideFlags.HideAndDontSave;
                    processed[i].SetData(output, 0);
                }
                SettingsRebuilds++;
            }
            catch (Exception ex) { ClearClips(); Debug.LogException(ex, this); }
        }

        void Update()
        {
            if (Time.unscaledTime >= nextSettingsCheck)
            {
                nextSettingsCheck = Time.unscaledTime + .5f;
                var current = preset ? preset.ReadControls().Effective() : null;
                if (preset != appliedPreset || !SameSettings(applied, current)) RefreshPreset();
            }
            if (!walker || !feed || !Speaker) return;
            ListeningChannel = feed.CurrentChannel;
            var channels = feed.channels;
            bool hasCamera = channels != null && ListeningChannel >= 0 && ListeningChannel < channels.Length && channels[ListeningChannel];
            MicrophoneGain = hasCamera ? GainAtDistance(Vector3.Distance(channels[ListeningChannel].transform.position, walker.transform.position)) : 0;
            Speaker.volume = speakerVolume * MicrophoneGain;
            Vector3 position = walker.transform.position;
            float moved = Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).magnitude;
            lastPosition = position;
            var agent = walker.Agent;
            if (Time.timeScale == 0 || walker.Waiting || !agent || !agent.enabled || !agent.isOnNavMesh || agent.velocity.sqrMagnitude < .01f)
            { distance = 0; nextStepDistance = .15f; return; }
            // Warps must not produce a burst of footsteps.
            if (moved > Mathf.Max(1, agent.speed * Time.deltaTime * 3)) { distance = 0; return; }
            distance += moved;
            if (distance < nextStepDistance || processed == null || processed.Length == 0) return;
            distance %= nextStepDistance; nextStepDistance = Mathf.Max(.1f, metresPerStep);
            Speaker.PlayOneShot(processed[stepIndex++ % processed.Length]);
            StepsPlayed++;
        }

        public float GainAtDistance(float metres) => 1 - Mathf.InverseLerp(microphoneNear, Mathf.Max(microphoneNear + .1f, microphoneFar), metres);
        static bool SameSettings(CrtAudioSettings a, CrtAudioSettings b) => ReferenceEquals(a, b) || a != null && b != null
            && a.strength == b.strength && a.lowCutHz == b.lowCutHz && a.highCutHz == b.highCutHz
            && a.distortion == b.distortion && a.noise == b.noise && a.outputDb == b.outputDb && a.peakProtection == b.peakProtection;
        void ClearClips()
        {
            if (Speaker) Speaker.Stop();
            if (processed != null) foreach (var clip in processed) if (clip) Destroy(clip);
            processed = null;
        }
        void OnDisable() { ClearClips(); applied = null; appliedPreset = null; }
    }
}
