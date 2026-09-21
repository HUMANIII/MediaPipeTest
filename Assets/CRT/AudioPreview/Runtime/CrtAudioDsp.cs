using System;
using System.IO;
using System.Text;
using System.Threading;

namespace MediaPipeTest.CRT.AudioPreview
{
    // No Unity calls or allocations in Process. The same processor renders preview and WAV exports.
    public sealed class CrtAudioDsp
    {
        struct Filter
        {
            double b0, b1, b2, a1, a2, tb0, tb1, tb2, ta1, ta2, z1, z2;
            bool initialized;
            public void Set(double hz, int rate, bool high)
            {
                double w = 2 * Math.PI * hz / rate, c = Math.Cos(w), alpha = Math.Sin(w) / Math.Sqrt(2), d = 1 + alpha;
                tb0 = (high ? 1 + c : 1 - c) / 2 / d; tb1 = (high ? -(1 + c) : 1 - c) / d; tb2 = tb0;
                ta1 = -2 * c / d; ta2 = (1 - alpha) / d;
                if (!initialized) { b0 = tb0; b1 = tb1; b2 = tb2; a1 = ta1; a2 = ta2; initialized = true; }
            }
            public double Tick(double x, double slew)
            {
                b0 += (tb0 - b0) * slew; b1 += (tb1 - b1) * slew; b2 += (tb2 - b2) * slew;
                a1 += (ta1 - a1) * slew; a2 += (ta2 - a2) * slew;
                double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y;
            }
            public void Reset() { z1 = z2 = 0; }
        }
        readonly Filter[] high, low, finish, noiseHigh, noiseLow;
        readonly int channels, rate;
        readonly double slew;
        CrtAudioSettings settings;
        double strength, distortion, noise, gain, targetGain, humPhase;
        uint random = 91921;
        public CrtAudioDsp(int channels, int rate, CrtAudioSettings settings)
        {
            this.channels = channels; this.rate = rate; slew = 1 - Math.Exp(-1.0 / (.03 * rate));
            high = new Filter[channels]; low = new Filter[channels]; finish = new Filter[channels];
            noiseHigh = new Filter[channels]; noiseLow = new Filter[channels];
            Configure(settings);
            strength = settings.strength; distortion = settings.distortion; noise = settings.noise; gain = targetGain;
        }
        public void Configure(CrtAudioSettings next)
        {
            if (ReferenceEquals(settings, next)) return;
            settings = next; targetGain = Math.Pow(10, settings.outputDb / 20);
            for (int c = 0; c < channels; c++)
            {
                high[c].Set(settings.lowCutHz, rate, true); low[c].Set(settings.highCutHz, rate, false);
                finish[c].Set(Math.Min(settings.highCutHz * 1.2, rate * .45), rate, false);
                noiseHigh[c].Set(Math.Min(500, rate * .1), rate, true); noiseLow[c].Set(Math.Min(4200, rate * .45), rate, false);
            }
        }
        public float Process(float[] input, int frame, int channel)
        {
            if (channel == 0)
            {
                strength += (settings.strength - strength) * slew; distortion += (settings.distortion - distortion) * slew;
                noise += (settings.noise - noise) * slew; gain += (targetGain - gain) * slew;
                humPhase += 2 * Math.PI * 100 / rate; if (humPhase > 2 * Math.PI) humPhase -= 2 * Math.PI;
            }
            double dry = input[frame * channels + channel];
            double wet = low[channel].Tick(high[channel].Tick(dry, slew), slew);
            wet += distortion * (.24 * Math.Tanh(wet / .24) - wet);
            wet = finish[channel].Tick(wet, slew);
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            double hiss = random / (double)uint.MaxValue * 2 - 1;
            hiss = noiseLow[channel].Tick(noiseHigh[channel].Tick(hiss, slew), slew);
            wet += noise * (.075 * hiss + .0024 * Math.Sin(humPhase) + .0012 * Math.Sin(humPhase * 2));
            double output = (dry + strength * (wet - dry)) * gain;
            if (settings.peakProtection && Math.Abs(output) > .98)
                output = Math.Sign(output) * (.98 + .019 * Math.Tanh((Math.Abs(output) - .98) / .019));
            return (float)output;
        }
        public void Reset(int frame)
        {
            for (int c = 0; c < channels; c++) { high[c].Reset(); low[c].Reset(); finish[c].Reset(); noiseHigh[c].Reset(); noiseLow[c].Reset(); }
            random = 91921u ^ (uint)frame; if (random == 0) random = 91921;
            humPhase = frame * 2 * Math.PI * 100 / rate % (2 * Math.PI);
        }
        public static float[] Render(float[] source, int channels, int rate, CrtAudioSettings settings, CancellationToken cancel = default)
        {
            var processor = new CrtAudioDsp(channels, rate, settings.Validated(rate)); var output = new float[source.Length];
            for (int frame = 0; frame < source.Length / channels; frame++)
            {
                if (frame % 4096 == 0) cancel.ThrowIfCancellationRequested();
                for (int c = 0; c < channels; c++) output[frame * channels + c] = processor.Process(source, frame, c);
            }
            return output;
        }
        public static void WriteWav(string path, float[] samples, int channels, int rate)
        {
            double peak = 0;
            foreach (var x in samples) { if (float.IsNaN(x) || float.IsInfinity(x)) throw new InvalidDataException("Non-finite audio sample."); peak = Math.Max(peak, Math.Abs(x)); }
            if (peak > 1) throw new InvalidDataException("출력이 0 dBFS를 초과해요. 출력 볼륨을 낮추거나 피크 보호를 켜 주세요.");
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels);
            writer.Write(rate); writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float sample in samples) writer.Write((short)Math.Round(sample * 32767));
        }
    }
}
