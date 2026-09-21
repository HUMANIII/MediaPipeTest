using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MediaPipeTest.CRT.Surveillance
{
    [DefaultExecutionOrder(-200)]
    public sealed class SurveillanceEnvironment : MonoBehaviour
    {
        public NavMeshData navigation;
        public UniversalRenderPipelineAsset pipeline;
        NavMeshDataInstance navigationInstance;
        RenderPipelineAsset previousPipeline;
        void OnEnable()
        {
            previousPipeline = QualitySettings.renderPipeline;
            if (pipeline) QualitySettings.renderPipeline = pipeline;
            if (navigation) navigationInstance = NavMesh.AddNavMeshData(navigation);
        }
        void OnDisable()
        {
            if (navigationInstance.valid) navigationInstance.Remove();
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
        }
    }
}
