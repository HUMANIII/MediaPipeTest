using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MediaPipeTest.CRT.AudioPreview
{
    public sealed class CrtDecodedAudio
    {
        public float[] samples;
        public int channels, rate;
        public int Frames => samples.Length / channels;
        public float Duration => (float)Frames / rate;
    }
    public static class CrtAudioClipLoader
    {
        public static async Task<CrtDecodedAudio> Read(AudioClip source, CancellationToken cancel)
        {
            if (!source) throw new InvalidOperationException("AudioClip을 선택해 주세요.");
            AudioClip decoded = null;
            try
            {
                AudioClip input = source;
                if (source.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    string path = AssetDatabase.GetAssetPath(source);
                    AudioType type = Path.GetExtension(path).ToLowerInvariant() switch
                    { ".wav" => AudioType.WAV, ".ogg" => AudioType.OGGVORBIS, ".mp3" => AudioType.MPEG, ".aif" or ".aiff" => AudioType.AIFF, _ => AudioType.UNKNOWN };
                    if (type == AudioType.UNKNOWN || !File.Exists(path))
                        throw new InvalidOperationException("이 클립은 PCM 읽기를 지원하지 않아요. WAV/OGG/MP3/AIFF 클립을 선택해 주세요.");
                    using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri, type);
                    ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = false;
                    _ = request.SendWebRequest();
                    while (!request.isDone) { cancel.ThrowIfCancellationRequested(); await Task.Yield(); }
                    cancel.ThrowIfCancellationRequested();
                    if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
                    input = decoded = DownloadHandlerAudioClip.GetContent(request);
                }
                else
                {
                    input.LoadAudioData(); double timeout = EditorApplication.timeSinceStartup + 15;
                    while (input.loadState == AudioDataLoadState.Loading)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (EditorApplication.timeSinceStartup > timeout) throw new TimeoutException("오디오 데이터를 읽는 데 시간이 너무 오래 걸려요.");
                        await Task.Yield();
                    }
                }
                cancel.ThrowIfCancellationRequested();
                long count = (long)input.samples * input.channels;
                if (count <= 0 || count > 20000000 || input.frequency < 8000 || input.frequency > 192000 || input.channels < 1 || input.channels > 8)
                    throw new InvalidOperationException("8~192kHz, 1~8채널, 2천만 샘플 이하 클립을 사용해 주세요.");
                var values = new float[(int)count];
                if (!input.GetData(values, 0)) throw new InvalidOperationException("클립의 PCM 데이터를 읽지 못했어요.");
                foreach (float value in values) if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("잘못된 오디오 샘플이 있어요.");
                return new CrtDecodedAudio { samples = values, channels = input.channels, rate = input.frequency };
            }
            finally { if (decoded) UnityEngine.Object.DestroyImmediate(decoded); }
        }
    }

    // Unity's preview output works in Edit mode, without adding an AudioListener or a scene object.
    public sealed class CrtAudioPreviewEngine : IDisposable
    {
        static readonly Type Util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        static MethodInfo Method(string name) => Util?.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo PlayMethod = Method("PlayPreviewClip"), StopMethod = Method("StopAllPreviewClips"),
            PauseMethod = Method("PausePreviewClip"), ResumeMethod = Method("ResumePreviewClip"), LoopMethod = Method("LoopPreviewClip"),
            PositionMethod = Method("GetPreviewClipSamplePosition"), SeekMethod = Method("SetPreviewClipSamplePosition"),
            PlayingMethod = Method("IsPreviewClipPlaying"), UpdateMethod = Method("UpdateAudio");
        public static bool Supported => PlayMethod != null && StopMethod != null && PauseMethod != null && ResumeMethod != null &&
            LoopMethod != null && PositionMethod != null && SeekMethod != null && PlayingMethod != null && UpdateMethod != null;
        readonly CrtDecodedAudio audio;
        readonly object sync = new();
        readonly CrtAudioDsp processor;
        AudioClip preview;
        volatile CrtAudioSettings target;
        int cursor, stoppedFrame, callbackCount, renderedSamples;
        float peak, outputRms;
        bool disposed, owned;
        public bool Playing { get; private set; }
        public bool Paused { get; private set; }
        public int CallbackCount => Volatile.Read(ref callbackCount);
        public int RenderedSamples => Volatile.Read(ref renderedSamples);
        public float Peak => Volatile.Read(ref peak);
        public float OutputRms => Volatile.Read(ref outputRms);
        public int Position => !owned ? stoppedFrame : Math.Max(0, (int)PositionMethod.Invoke(null, null));

        public CrtAudioPreviewEngine(CrtDecodedAudio audio, CrtAudioSettings settings)
        {
            if (!Supported) throw new NotSupportedException("이 Unity 버전의 Editor 오디오 미리듣기 API를 지원하지 않아요.");
            this.audio = audio; target = settings.Validated(audio.rate); processor = new CrtAudioDsp(audio.channels, audio.rate, target);
            preview = AudioClip.Create("CRT Audio Tool Preview (temporary)", audio.Frames, audio.channels, audio.rate, true, Read, SetPosition);
            preview.hideFlags = HideFlags.HideAndDontSave;
            EditorApplication.update += Tick;
        }
        public void SetSettings(CrtAudioSettings settings) => target = settings.Validated(audio.rate);
        public void Play(bool loop)
        {
            if (disposed) return;
            StopMethod.Invoke(null, null); PlayMethod.Invoke(null, new object[] { preview, Math.Min(stoppedFrame, audio.Frames - 1), loop });
            owned = Playing = true; Paused = false;
        }
        public void Pause()
        {
            if (!Playing || Paused) return;
            PauseMethod.Invoke(null, null); Paused = true;
        }
        public void Resume() { if (!Paused) return; ResumeMethod.Invoke(null, null); Paused = false; }
        public void SetLoop(bool loop) { if (owned) LoopMethod.Invoke(null, new object[] { loop }); }
        public void Seek(int frame)
        {
            frame = Math.Max(0, Math.Min(frame, audio.Frames - 1));
            stoppedFrame = frame;
            if (owned) SeekMethod.Invoke(null, new object[] { preview, frame }); else SetPosition(frame);
        }
        public void Stop()
        {
            if (owned) StopMethod.Invoke(null, null);
            owned = Playing = Paused = false; stoppedFrame = 0; SetPosition(0); Volatile.Write(ref peak, 0); Volatile.Write(ref outputRms, 0);
        }
        void Tick()
        {
            if (!owned || disposed) return;
            UpdateMethod.Invoke(null, null);
            if (!Paused && !(bool)PlayingMethod.Invoke(null, null)) Stop();
        }
        void SetPosition(int frame) { lock (sync) { cursor = Math.Max(0, Math.Min(frame, audio.Frames)); processor.Reset(cursor); } }
        void Read(float[] data)
        {
            lock (sync)
            {
                if (disposed) { Array.Clear(data, 0, data.Length); return; }
                processor.Configure(target);
                double squared = 0; float maximum = 0;
                for (int i = 0; i < data.Length; i += audio.channels)
                {
                    for (int c = 0; c < audio.channels && i + c < data.Length; c++)
                    {
                        float value = cursor < audio.Frames ? processor.Process(audio.samples, cursor, c) : 0;
                        data[i + c] = value; squared += value * value; maximum = Math.Max(maximum, Math.Abs(value));
                    }
                    cursor++;
                }
                Volatile.Write(ref peak, maximum); Volatile.Write(ref outputRms, (float)Math.Sqrt(squared / Math.Max(1, data.Length)));
                Interlocked.Increment(ref callbackCount); Interlocked.Add(ref renderedSamples, data.Length);
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            Stop(); lock (sync) disposed = true;
            EditorApplication.update -= Tick;
            if (preview) UnityEngine.Object.DestroyImmediate(preview);
        }
    }
}
