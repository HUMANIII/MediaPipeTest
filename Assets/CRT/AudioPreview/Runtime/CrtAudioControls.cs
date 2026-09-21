using System;

namespace MediaPipeTest.CRT.AudioPreview
{
    [Serializable]
    public sealed class CrtAudioControls
    {
        public bool detailed;
        public float tone;
        public CrtAudioSettings settings = new();

        public static float ClampTone(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Clamp(value, -100, 100);

        public static CrtAudioSettings Tone(float value, float outputDb = 0, bool peakProtection = true)
        {
            value = ClampTone(value);
            var weak = new CrtAudioSettings { strength = .5f, lowCutHz = 150, highCutHz = 6500, distortion = .1f, noise = .025f };
            var normal = new CrtAudioSettings();
            var strong = new CrtAudioSettings { lowCutHz = 450, highCutHz = 2200, distortion = .65f, noise = .25f };
            var from = value < 0 ? weak : normal;
            var to = value < 0 ? normal : strong;
            float t = value < 0 ? (value + 100) / 100 : value / 100;
            float Linear(float a, float b) => a + (b - a) * t;
            float Frequency(float a, float b) => t == 0 ? a : t == 1 ? b : (float)Math.Exp(Linear((float)Math.Log(a), (float)Math.Log(b)));
            return new CrtAudioSettings
            {
                strength = Linear(from.strength, to.strength),
                lowCutHz = Frequency(from.lowCutHz, to.lowCutHz), highCutHz = Frequency(from.highCutHz, to.highCutHz),
                distortion = Linear(from.distortion, to.distortion), noise = Linear(from.noise, to.noise),
                outputDb = outputDb, peakProtection = peakProtection
            }.Validated();
        }

        // Asset settings are independent of the selected clip's sample rate and preview bypass.
        public CrtAudioSettings Effective()
        {
            settings ??= new();
            return detailed ? settings.Validated() : Tone(tone, settings.outputDb, settings.peakProtection);
        }

        public CrtAudioSettings Preview(bool bypass, int sampleRate)
        {
            var result = Effective();
            if (bypass) result.strength = 0;
            return result.Validated(sampleRate);
        }

        public void SetDetailed(bool value)
        {
            if (value == detailed) return;
            if (value) settings = Effective();
            detailed = value;
        }

        public void ApplyTone(float value)
        {
            settings ??= new();
            settings = Tone(value, settings.outputDb, settings.peakProtection);
        }
    }
}
