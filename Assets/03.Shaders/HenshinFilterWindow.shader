Shader "Henshin/Filter Window"
{
    Properties { _FilterView ("Casual camera view", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "FilterWindow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_FilterView); SAMPLER(sampler_FilterView);
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPosition : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPosition.xy / input.screenPosition.w;
                return half4(SAMPLE_TEXTURE2D(_FilterView, sampler_FilterView, uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
