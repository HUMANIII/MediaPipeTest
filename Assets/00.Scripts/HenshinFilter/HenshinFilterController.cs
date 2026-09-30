using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MediaPipeTest.HenshinFilter
{
    /// <summary>Two views of one skeleton, composited only on a world-space window.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class HenshinFilterController : MonoBehaviour
    {
        public Camera viewCamera;
        public Camera filterCamera;
        public Renderer panelRenderer;
        public Renderer[] baseRenderers = Array.Empty<Renderer>();
        public Renderer[] casualRenderers = Array.Empty<Renderer>();
        public Material effectMaterial;
        [Range(.25f, 1f)] public float resolutionScale = 1f;
        public bool effectEnabled = true;
        public int baseLayer = 10;
        public int filteredLayer = 11;
        public int panelLayer = 12;

        public bool IsReady { get; private set; }
        public RenderTexture Output => output;
        public IReadOnlyList<Renderer> FilteredRenderers => proxies;

        readonly List<Renderer> proxies = new List<Renderer>();
        readonly List<Material[]> normalMaterials = new List<Material[]>();
        readonly List<Material[]> effectMaterials = new List<Material[]>();
        readonly Dictionary<Material, Material> materialCopies = new Dictionary<Material, Material>();
        readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
        GameObject proxyRoot;
        RenderTexture output;
        Material panelMaterial;
        Material originalPanelMaterial;
        int originalCameraMask;
        bool lastEffectEnabled;
        bool capturedState;

        public string ConfigurationError()
        {
            if (!viewCamera || !filterCamera || viewCamera == filterCamera) return "Two distinct cameras are required.";
            if (!panelRenderer || !panelRenderer.sharedMaterial) return "The filter panel material is missing.";
            if (!effectMaterial || !effectMaterial.shader || !effectMaterial.shader.isSupported) return "The Henshin filter shader is missing or unsupported.";
            if (baseRenderers == null || baseRenderers.Length == 0 || casualRenderers == null || casualRenderers.Length == 0)
                return "Both outfit renderer groups are required.";
            foreach (Renderer r in baseRenderers) if (!r) return "A base renderer reference is missing.";
            foreach (Renderer r in casualRenderers)
            {
                if (!r) return "A casual renderer reference is missing.";
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) return "Unsupported character renderer: " + r.name;
                foreach (Material m in r.sharedMaterials) if (!m) return "A character material is missing: " + r.name;
            }
            if (baseLayer < 8 || filteredLayer < 8 || panelLayer < 8 || baseLayer > 31 || filteredLayer > 31 || panelLayer > 31
                || baseLayer == filteredLayer || baseLayer == panelLayer || filteredLayer == panelLayer)
                return "Three different user layers are required.";
            return null;
        }

        void OnEnable()
        {
            string error = ConfigurationError();
            if (error != null)
            {
                if (panelRenderer) panelRenderer.enabled = false;
                if (filterCamera) filterCamera.enabled = false;
                Debug.LogError("Henshin filter: " + error, this);
                return;
            }
            originalCameraMask = viewCamera.cullingMask;
            originalPanelMaterial = panelRenderer.sharedMaterial;
            capturedState = true;
            panelMaterial = new Material(originalPanelMaterial) { name = "Filter Window (runtime)" };
            panelRenderer.sharedMaterial = panelMaterial;
            AssignLayer(panelRenderer.gameObject, panelLayer);
            foreach (Renderer r in baseRenderers) AssignLayer(r.gameObject, baseLayer);
            viewCamera.cullingMask = (originalCameraMask | (1 << baseLayer) | (1 << panelLayer)) & ~(1 << filteredLayer);
            proxyRoot = new GameObject("Casual view (shared skeleton)");
            proxyRoot.transform.SetParent(transform, false);
            foreach (Renderer source in casualRenderers) CreateProxy(source);
            filterCamera.enabled = false;
            filterCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            filterCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
            lastEffectEnabled = !effectEnabled;
            IsReady = true;
            Synchronize();
            RenderPipelineManager.beginContextRendering += BeforeRendering;
            panelRenderer.enabled = true;
            filterCamera.enabled = true;
        }

        void AssignLayer(GameObject target, int layer)
        {
            if (!originalLayers.ContainsKey(target)) originalLayers.Add(target, target.layer);
            target.layer = layer;
        }

        void CreateProxy(Renderer source)
        {
            var go = new GameObject(source.name + " (filtered)") { layer = filteredLayer };
            go.transform.SetParent(proxyRoot.transform, false);
            Renderer proxy;
            if (source is SkinnedMeshRenderer skin)
            {
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skin.sharedMesh;
                copy.bones = skin.bones;
                copy.rootBone = skin.rootBone;
                copy.localBounds = skin.localBounds;
                copy.quality = skin.quality;
                copy.updateWhenOffscreen = true;
                proxy = copy;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                proxy = go.AddComponent<MeshRenderer>();
            }
            proxy.shadowCastingMode = ShadowCastingMode.Off;
            proxy.receiveShadows = source.receiveShadows;
            proxy.lightProbeUsage = source.lightProbeUsage;
            proxy.reflectionProbeUsage = source.reflectionProbeUsage;
            Material[] originals = source.sharedMaterials;
            var changed = new Material[originals.Length];
            for (int i = 0; i < changed.Length; i++) changed[i] = EffectFor(originals[i]);
            normalMaterials.Add(originals);
            effectMaterials.Add(changed);
            proxies.Add(proxy);
        }

        Material EffectFor(Material source)
        {
            if (materialCopies.TryGetValue(source, out Material copy)) return copy;
            copy = new Material(effectMaterial) { name = source.name + " (Henshin filter)" };
            // Preserve the source mesh's alpha-shaped eyelashes/hair rather than making cards solid.
            string textureProperty = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") ? "_BaseMap"
                : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (textureProperty != null)
            {
                copy.SetTexture("_MainTexture", source.GetTexture(textureProperty));
                copy.SetTextureScale("_MainTexture", source.GetTextureScale(textureProperty));
                copy.SetTextureOffset("_MainTexture", source.GetTextureOffset(textureProperty));
            }
            copy.EnableKeyword("_USEALPHACLIP_ON");
            materialCopies.Add(source, copy);
            return copy;
        }

        void LateUpdate() { if (IsReady) Synchronize(); }

        public void RefreshView() { if (IsReady) Synchronize(); }

        void BeforeRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (IsReady && cameras.Contains(viewCamera)) Synchronize();
        }

        void Synchronize()
        {
            if (!viewCamera || !filterCamera || !panelRenderer) return;
            for (int i = 0; i < proxies.Count; i++)
            {
                Renderer source = casualRenderers[i];
                Renderer proxy = proxies[i];
                if (!source || !proxy) continue;
                proxy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                proxy.transform.localScale = source.transform.lossyScale;
                if (source is SkinnedMeshRenderer skin && proxy is SkinnedMeshRenderer target && skin.sharedMesh)
                    for (int shape = 0; shape < skin.sharedMesh.blendShapeCount; shape++)
                        target.SetBlendShapeWeight(shape, skin.GetBlendShapeWeight(shape));
                if (lastEffectEnabled != effectEnabled)
                    proxy.sharedMaterials = effectEnabled ? effectMaterials[i] : normalMaterials[i];
            }
            lastEffectEnabled = effectEnabled;
            int width = Mathf.Max(1, Mathf.RoundToInt(viewCamera.pixelWidth * resolutionScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(viewCamera.pixelHeight * resolutionScale));
            if (!output || output.width != width || output.height != height)
            {
                ReleaseOutput();
                output = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
                { name = "Henshin filter view", antiAliasing = 1, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                output.Create();
                panelMaterial.SetTexture("_FilterView", output);
            }
            filterCamera.CopyFrom(viewCamera);
            filterCamera.transform.SetPositionAndRotation(viewCamera.transform.position, viewCamera.transform.rotation);
            filterCamera.projectionMatrix = viewCamera.projectionMatrix;
            filterCamera.targetTexture = output;
            filterCamera.rect = new Rect(0, 0, 1, 1);
            filterCamera.depth = viewCamera.depth - 1;
            filterCamera.cullingMask = (originalCameraMask | (1 << filteredLayer)) & ~((1 << baseLayer) | (1 << panelLayer) | (1 << 5));
            filterCamera.enabled = true;
            filterCamera.allowMSAA = false;
            // CopyFrom copies Camera fields, not URP's additional camera component.
            filterCamera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
        }

        void ReleaseOutput()
        {
            if (filterCamera) filterCamera.targetTexture = null;
            if (!output) return;
            output.Release();
            Destroy(output);
            output = null;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= BeforeRendering;
            IsReady = false;
            if (filterCamera) filterCamera.enabled = false;
            ReleaseOutput();
            if (capturedState && viewCamera) viewCamera.cullingMask = originalCameraMask;
            if (panelRenderer)
            {
                panelRenderer.enabled = false;
                if (capturedState) panelRenderer.sharedMaterial = originalPanelMaterial;
            }
            foreach (var pair in originalLayers) if (pair.Key) pair.Key.layer = pair.Value;
            originalLayers.Clear();
            if (proxyRoot) { proxyRoot.SetActive(false); Destroy(proxyRoot); }
            foreach (Material material in materialCopies.Values) Destroy(material);
            materialCopies.Clear();
            if (panelMaterial) Destroy(panelMaterial);
            proxies.Clear(); normalMaterials.Clear(); effectMaterials.Clear();
            capturedState = false;
        }
    }
}
