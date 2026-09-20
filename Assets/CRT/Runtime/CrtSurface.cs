using UnityEngine;
using UnityEngine.UI;

namespace MediaPipeTest.CRT
{
    [DisallowMultipleComponent]
    public sealed class CrtSurface : MonoBehaviour
    {
        public Material template;
        public Texture source;
        [Tooltip("Optional shared settings asset. When assigned, local Settings are ignored.")]
        public CrtProfile profile;
        public CrtSettings settings = new CrtSettings();
        public CrtSettings ActiveSettings => profile && profile.settings != null ? profile.settings : settings;
        public bool effectEnabled = true;
        [Tooltip("Use 0 for automatic RawImage/quad/sprite aspect.")]
        public float displayAspect;
        public Material Instance => instance;
        Material instance, previous;
        Renderer targetRenderer;
        RawImage targetImage;
        Texture previousTexture;

        void OnEnable()
        {
            targetRenderer = GetComponent<Renderer>();
            targetImage = GetComponent<RawImage>();
            if (!template || (!targetRenderer && !targetImage)) return;
            instance = new Material(template) { name = template.name + " (CRT instance)" };
            if (targetImage)
            {
                previous = targetImage.material; previousTexture = targetImage.texture;
                targetImage.material = instance;
            }
            else { previous = targetRenderer.sharedMaterial; targetRenderer.sharedMaterial = instance; }
            Refresh();
        }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (!instance) return;
            ActiveSettings.Apply(instance, effectEnabled);
            Texture effective = source;
            if (targetRenderer is SpriteRenderer sr && sr.sprite)
            {
                var sprite = sr.sprite;
                Rect r = sprite.textureRect;
                instance.SetVector("_SpriteUVRect", new Vector4(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height));
                instance.SetFloat("_UseSourceTexture", source ? 1 : 0);
                instance.SetTexture("_SourceTex", source ? source : sprite.texture);
                if (!effective) effective = sprite.texture;
            }
            else if (targetImage)
            {
                targetImage.texture = source ? source : previousTexture;
                effective = targetImage.texture;
            }
            else if (effective) instance.SetTexture("_MainTex", effective);
            float aspect = displayAspect;
            if (aspect <= 0 && targetImage)
            {
                Rect r = targetImage.rectTransform.rect;
                aspect = r.width / Mathf.Max(.001f, r.height);
            }
            else if (aspect <= 0 && targetRenderer is SpriteRenderer spriteRenderer && spriteRenderer.sprite)
            {
                Vector2 size = spriteRenderer.sprite.bounds.size;
                aspect = size.x * Mathf.Abs(transform.lossyScale.x) / Mathf.Max(.001f, size.y * Mathf.Abs(transform.lossyScale.y));
            }
            else if (aspect <= 0) aspect = Mathf.Abs(transform.lossyScale.x) / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
            // An ordinary sprite already has its own UV mapping; fit only explicit image/video sources.
            Vector2 fit = effective && !(targetRenderer is SpriteRenderer && !source)
                ? CrtSettings.FitScale((float)effective.width / effective.height, aspect) : Vector2.one;
            instance.SetVector("_FitScale", fit);
            if (targetImage)
            {
                // uGUI stencil masks maintain a derived material; update only CRT properties
                // so runtime sliders also work beneath a Mask without altering stencil state.
                var masked = targetImage.materialForRendering;
                if (masked && masked != instance) { ActiveSettings.Apply(masked, effectEnabled); masked.SetVector("_FitScale", fit); }
            }
        }
        void OnDisable()
        {
            if (targetImage && targetImage.material == instance)
            { targetImage.material = previous; targetImage.texture = previousTexture; }
            if (targetRenderer && targetRenderer.sharedMaterial == instance) targetRenderer.sharedMaterial = previous;
            if (instance) Destroy(instance);
            instance = null;
        }
    }
}
