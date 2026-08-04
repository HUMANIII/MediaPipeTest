using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RenderFeatureController : MonoBehaviour
{
    [SerializeField]
    private ScriptableRendererFeature[] RenderFeatures;
    
    public void SetActiveRenderFeature(bool active)
    {
        foreach (var feature in RenderFeatures)
        {
            feature.SetActive(active);
        }
    }
    
    public void ToggleRenderFeature()
    {
        foreach (var feature in RenderFeatures)
        {
            feature.SetActive(!feature.isActive);
        }
    }
}
