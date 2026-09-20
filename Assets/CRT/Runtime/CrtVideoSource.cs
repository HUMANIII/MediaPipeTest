using System;
using UnityEngine;
using UnityEngine.Video;

namespace MediaPipeTest.CRT
{
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class CrtVideoSource : MonoBehaviour
    {
        public VideoClip clip;
        public Texture2D fallback;
        public bool playOnReady = true;
        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        public int LoopCount { get; private set; }
        public Texture Texture => IsReady && target ? target : fallback;
        public VideoPlayer Player => player;
        public event Action Prepared;
        VideoPlayer player;
        RenderTexture target;

        void OnEnable()
        {
            player = GetComponent<VideoPlayer>();
            IsReady = false; Error = null; LoopCount = 0;
            player.playOnAwake = false;
            player.isLooping = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnError;
            player.loopPointReached += OnLoop;
            if (!clip) { Error = "No video clip assigned"; return; }
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            target = new RenderTexture(Mathf.Max(1, (int)clip.width), Mathf.Max(1, (int)clip.height), 0, RenderTextureFormat.ARGB32)
            { name = "CRT video frames", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            target.Create();
            var old = RenderTexture.active;
            RenderTexture.active = target; GL.Clear(true, true, Color.black); RenderTexture.active = old;
            player.targetTexture = target;
            player.Prepare();
        }
        void OnPrepared(VideoPlayer p) { IsReady = true; if (playOnReady) p.Play(); Prepared?.Invoke(); }
        void OnError(VideoPlayer p, string message) { Error = message; IsReady = false; Debug.LogError("CRT video: " + message, this); }
        void OnLoop(VideoPlayer p) => LoopCount++;
        public void TogglePlayback() { if (!IsReady) return; if (player.isPlaying) player.Pause(); else player.Play(); }
        void OnDisable()
        {
            if (player)
            {
                player.prepareCompleted -= OnPrepared; player.errorReceived -= OnError; player.loopPointReached -= OnLoop;
                player.Stop(); player.targetTexture = null;
            }
            IsReady = false;
            if (target) { target.Release(); Destroy(target); target = null; }
        }
    }
}
