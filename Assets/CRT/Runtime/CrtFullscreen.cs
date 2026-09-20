using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MediaPipeTest.CRT
{
    [RequireComponent(typeof(Camera))]
    public sealed class CrtFullscreen : MonoBehaviour
    {
        public FullScreenPassRendererFeature feature;
        public Material template;
        public Canvas[] displayCanvases;
        [Tooltip("Optional shared settings asset. When assigned, local Settings are ignored.")]
        public CrtProfile profile;
        public CrtSettings settings = new CrtSettings();
        public CrtSettings ActiveSettings => profile && profile.settings != null ? profile.settings : settings;
        public bool effectEnabled;
        public bool includeUI;
        Material instance, previousMaterial;
        bool previousActive;
        CanvasState[] original;
        Camera targetCamera;
        struct CanvasState { public RenderMode mode; public Camera camera; public float distance; }
        public Material Instance => instance;

        void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            if (!feature || !template) return;
            previousMaterial = feature.passMaterial; previousActive = feature.isActive;
            instance = new Material(template) { name = "CRT fullscreen instance" };
            feature.passMaterial = instance;
            original = new CanvasState[displayCanvases?.Length ?? 0];
            for (int i = 0; i < original.Length; i++) if (displayCanvases[i])
                original[i] = new CanvasState { mode = displayCanvases[i].renderMode, camera = displayCanvases[i].worldCamera, distance = displayCanvases[i].planeDistance };
            Refresh();
        }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (!instance || !feature) return;
            ActiveSettings.Apply(instance, effectEnabled);
            feature.SetActive(effectEnabled);
            for (int i = 0; i < (displayCanvases?.Length ?? 0); i++)
            {
                Canvas canvas = displayCanvases[i]; if (!canvas) continue;
                bool cameraUI = effectEnabled && includeUI;
                var mode = cameraUI ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                if (canvas.renderMode != mode) canvas.renderMode = mode;
                if (cameraUI) { canvas.worldCamera = targetCamera; canvas.planeDistance = targetCamera.nearClipPlane + .1f; }
            }
        }
        void OnDisable()
        {
            if (feature && feature.passMaterial == instance) { feature.passMaterial = previousMaterial; feature.SetActive(previousActive); }
            for (int i = 0; i < (original?.Length ?? 0); i++) if (displayCanvases[i])
            { displayCanvases[i].renderMode = original[i].mode; displayCanvases[i].worldCamera = original[i].camera; displayCanvases[i].planeDistance = original[i].distance; }
            if (instance) Destroy(instance);
            instance = null;
        }
    }
}
