using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class OutlineRenderFeature : ScriptableRendererFeature
{
    [SerializeField]
    private OutlineRenderFeatureSettings settings;
    private DetectorRenderPass detectorRenderPass;
    private ComposerRenderPass composerRenderPass;

    /// <inheritdoc/>
    // 상속된 문서 참조
    public override void Create()
    {
        detectorRenderPass = new DetectorRenderPass(settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
        composerRenderPass = new ComposerRenderPass(settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    // Here you can inject one or multiple render passes in the renderer.
    // 여기에서 렌더러에 하나 이상의 렌더 패스를 주입할 수 있습니다.
    // This method is called when setting up the renderer once per-camera.
    // 이 메서드는 카메라당 한 번 렌더러를 설정할 때 호출됩니다.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(detectorRenderPass);
        //renderer.EnqueuePass(composerRenderPass);
    }
    
    public class PassData
    {
        public RendererListHandle rendererListHandle;
    }

    // Use this class to pass around settings from the feature to the pass
    // 이 클래스를 사용하여 피처에서 패스로 설정을 전달합니다
    [Serializable]
    public class OutlineRenderFeatureSettings
    {
        [Header("디버그 모드")]
        [Tooltip("이거 켜면 외각선을 그리는게 아니라 외각선에 그려질 것들을 흰색으로 마스킹해서 다시 그리는 거 할거임 " +
                 "\n \n 흰색이라고 쓰긴했는데 아래 매터리얼에서에서 흰색으로 할 예정이라 흰색이라고 말한거임 조금 더 화려하게 디버그 하고 싶으면 아래 매터리얼 수정 ㄱㄱ")]
        public bool debugMode = false;
        [Header("디버그용 매터리얼")]
        [Tooltip("디버그 켰을때 이 매터리얼에 등록된데로 렌더링 함")]
        public Material debugMaterial;
        
        [Header("외각선을 그릴 오브젝트의 레이어")]
        [Tooltip("이 레이어의 오브젝트를 검출해서 외각선을 그림")]
        public LayerMask outlineTargetLayers;
        [Header("외각선의 매터리얼")]
        [Tooltip("필요시 쉐이더 여기에 적용해서 넣기")]
        public Material outlineMaterial;
    }


    class DetectorRenderPass : ScriptableRenderPass
    {
        private OutlineRenderFeatureSettings settings; 
        
        public DetectorRenderPass(OutlineRenderFeatureSettings settings)
        {
            this.settings = settings;
        }
        
        

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string passName = "DetectorRenderPass";

            // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
            // 이것은 그래프에 래스터 렌더 패스를 추가하며, ExecutePass 함수에 전달될 이름과 데이터 타입을 지정합니다.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                //https://docs.unity3d.com/kr/6000.0/Manual/urp/render-graph-draw-objects-in-a-pass.html <- 여기있는거 긁어와서 수정중
                
                //이전까지의 렌더링 된 오브젝트들 정보를 불러오는 과정으로 보임 
                // 렌더링 결과 -> 컬링적용, 렌더링된 애들 등등에 대한 정보 있음
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                // 카메라 관련 정보 있음 -> 정렬 등등
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                // 빛 관련 정보 있음 -> 지금 만드는 외각선에는 필요 없는데 밑에 DrawSettings 때문에 필요함 
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                //그 위에서 말한 정렬관련 정보임
                SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
                RenderQueueRange renderQueueRange = RenderQueueRange.opaque;
                FilteringSettings filterSettings = new FilteringSettings(renderQueueRange, settings.outlineTargetLayers);

                // 일반적으로는 사용하는 Lit,Unlit,Shader Graph에서는이 밑에꺼 세개로 커버 된다고 해서 세개로 바꿔서 넣음
                List<ShaderTagId> targetShaderTags = new List<ShaderTagId>
                {
                    new("UniversalForward"),
                    new("UniversalForwardOnly"),
                    new("SRPDefaultUnlit")
                };

                // 무슨 오브젝트를 그릴지 저장하기 위한 준비의 준비 준비
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(targetShaderTags, renderingData, cameraData, lightData, sortFlags);
                //디버그용 그리기 일때 다시 그리기 위한 세팅
                if (settings.debugMode)
                {
                    drawSettings.overrideMaterial = settings.debugMaterial;
                    drawSettings.overrideMaterialPassIndex = 0;
                }

                // 무슨 오브젝트를 그릴지 저장하기 위한 준비의 준비
                var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

                // 무슨 오브젝트를 그릴지 저장
                passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
                
                // Set the render target as the color and depth textures of the active camera texture
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                
                builder.UseRendererList(passData.rendererListHandle);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            // Clear the render target to black
            //context.cmd.ClearRenderTarget(false, true, Color.black);

            // Draw the objects in the list
            context.cmd.DrawRendererList(data.rendererListHandle);
        }
    }
    
    class ComposerRenderPass : ScriptableRenderPass
    {
        private OutlineRenderFeatureSettings settings; 
        
        public ComposerRenderPass(OutlineRenderFeatureSettings settings)
        {
            this.settings = settings;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string passName = "ComposerRenderPass";
            using( var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData) )
            {
                var drawMaterial = settings.debugMode ? settings.debugMaterial : settings.outlineMaterial;
            }
        }
    }
}
