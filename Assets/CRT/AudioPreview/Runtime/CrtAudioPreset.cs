using System;
using UnityEngine;

namespace MediaPipeTest.CRT.AudioPreview
{
    [Serializable]
    public sealed class CrtAudioSettings
    {
        [Range(0, 1)] public float strength = 1;
        public float lowCutHz = 250, highCutHz = 3500;
        [Range(0, 1)] public float distortion = .2f, noise = .08333f;
        [Range(-40, 12)] public float outputDb;
        public bool peakProtection = true;

        public CrtAudioSettings Copy() => (CrtAudioSettings)MemberwiseClone();
        public CrtAudioSettings Validated(int sampleRate = 44100)
        {
            var s = Copy();
            s.strength = FiniteClamp(s.strength, 0, 1, 1);
            s.lowCutHz = FiniteClamp(s.lowCutHz, 20, Math.Min(2000, sampleRate * .2f), 250);
            s.highCutHz = FiniteClamp(s.highCutHz, s.lowCutHz + 100, Math.Min(20000, sampleRate * .45f), 3500);
            s.distortion = FiniteClamp(s.distortion, 0, 1, .2f); s.noise = FiniteClamp(s.noise, 0, 1, .08333f);
            s.outputDb = FiniteClamp(s.outputDb, -40, 12, 0); return s;
        }
        static float FiniteClamp(float v, float min, float max, float fallback) =>
            float.IsNaN(v) || float.IsInfinity(v) ? fallback : Math.Max(min, Math.Min(max, v));
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceAssembly: "MediaPipeTest.CRT.AudioPreview.Editor")]
    [CreateAssetMenu(menuName = "CRT/Audio Preview Preset", fileName = "CrtAudioPreset")]
    public sealed class CrtAudioPreset : ScriptableObject
    {
        public CrtAudioSettings settings = new();
        // Zero deliberately represents assets saved before mode information existed.
        [SerializeField, HideInInspector] int formatVersion;
        [SerializeField, HideInInspector] bool detailed;
        [SerializeField, HideInInspector] float tone;

        public CrtAudioControls ReadControls() => new()
        {
            settings = (settings ?? new CrtAudioSettings()).Validated(),
            detailed = formatVersion == 0 || detailed,
            tone = formatVersion == 0 ? 0 : CrtAudioControls.ClampTone(tone)
        };

        public void Store(CrtAudioControls controls)
        {
            settings = controls.Effective();
            detailed = controls.detailed; tone = CrtAudioControls.ClampTone(controls.tone); formatVersion = 1;
        }
    }
}
