using UnityEngine;

namespace MediaPipeTest.SakuraHair
{
    // Skinned meshes can use bone-space bounds unrelated to their Transform.
    // Supply the actual world bounds, keeping gravity independent of head tilt.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    [DefaultExecutionOrder(-100)]
    public sealed class SakuraHairVolumeBounds : MonoBehaviour
    {
        static readonly int Center = Shader.PropertyToID("_VolumeCenterWS");
        static readonly int Extents = Shader.PropertyToID("_VolumeExtentsWS");
        Renderer targetRenderer;
        MaterialPropertyBlock properties;

        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
            if (!targetRenderer) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(properties);
            Bounds bounds = targetRenderer.bounds;
            properties.SetVector(Center, bounds.center);
            properties.SetVector(Extents, bounds.extents);
            targetRenderer.SetPropertyBlock(properties);
        }
    }
}
