using Unity.Cinemachine;
using UnityEngine;

namespace MediaPipeTest.CRT.Surveillance
{
    [DefaultExecutionOrder(-100)]
    public sealed class SurveillanceFeed : MonoBehaviour
    {
        public Camera feedCamera;
        public CinemachineBrain brain;
        public CinemachineCamera[] channels;
        public string[] channelNames = { "WEST CORRIDOR", "EAST CORRIDOR", "WARD A", "WARD B" };
        public CrtSurface screen;
        public AudioSource staticAudio;
        public Vector2Int resolution = new(1024, 768);
        public const float CutTime = .05f, TransitionDuration = .20f;
        public int CurrentChannel { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool CrtEnabled => screen && screen.effectEnabled;
        public RenderTexture Texture { get; private set; }
        public float TransitionElapsed { get; private set; }
        public int CompletedTransitions { get; private set; }
        public int AudioStarts { get; private set; }
        public string ChannelLabel => $"CAM {CurrentChannel + 1:00}  /  " +
            (CurrentChannel < channelNames.Length ? channelNames[CurrentChannel] : "CCTV");
        int requestedChannel;
        bool cut;

        void OnEnable()
        {
            Texture = new RenderTexture(Mathf.Max(16, resolution.x), Mathf.Max(16, resolution.y), 24,
                RenderTextureFormat.ARGB32) { name = "Surveillance Feed (runtime)", antiAliasing = 1,
                useMipMap = false, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Texture.Create();
            feedCamera.targetTexture = Texture;
            feedCamera.aspect = (float)Texture.width / Texture.height;
            screen.source = Texture;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            Select(CurrentChannel);
        }
        public void NextChannel() => Request(1);
        public void PreviousChannel() => Request(-1);
        public void SetCrtEnabled(bool value) { screen.effectEnabled = value; screen.Refresh(); }
        void Request(int delta)
        {
            if (!isActiveAndEnabled || IsTransitioning || channels == null || channels.Length < 2) return;
            requestedChannel = (CurrentChannel + delta + channels.Length) % channels.Length;
            TransitionElapsed = 0; cut = false; IsTransitioning = true;
            if (staticAudio && staticAudio.clip) { staticAudio.Play(); AudioStarts++; }
        }
        void Update()
        {
            screen.transitionTime = Time.unscaledTime;
            if (!IsTransitioning) return;
            TransitionElapsed += Time.unscaledDeltaTime;
            if (!cut && TransitionElapsed >= CutTime) { Select(requestedChannel); cut = true; }
            screen.transitionStrength = TransitionElapsed < CutTime
                ? TransitionElapsed / CutTime
                : Mathf.Clamp01((TransitionDuration - TransitionElapsed) / (TransitionDuration - CutTime));
            if (TransitionElapsed >= TransitionDuration)
            {
                screen.transitionStrength = 0; IsTransitioning = false; CompletedTransitions++;
            }
        }
        void Select(int index)
        {
            if (channels == null || channels.Length == 0) return;
            CurrentChannel = Mathf.Clamp(index, 0, channels.Length - 1);
            for (int i = 0; i < channels.Length; i++) if (channels[i]) channels[i].Priority = i == CurrentChannel ? 20 : 0;
        }
        void OnDisable()
        {
            IsTransitioning = false;
            if (staticAudio) staticAudio.Stop();
            if (screen) { screen.transitionStrength = 0; if (screen.source == Texture) screen.source = null; }
            if (feedCamera && feedCamera.targetTexture == Texture) feedCamera.targetTexture = null;
            if (Texture) { Texture.Release(); Destroy(Texture); Texture = null; }
        }
    }
}
