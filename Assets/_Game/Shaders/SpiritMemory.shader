// The spirit camera's memory overlay (TECHNICAL_DESIGN 12): see-through amber copies of items where the ghost remembers
// them, brighter at the edges. Basic version for the M3 prototype; the art pass comes in M5.
Shader "SpiritMovers/SpiritMemory"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.7, 0.28, 0.35)
        _RimPower ("Rim power", Range(0.5, 8)) = 2
        _RimBoost ("Rim boost", Range(0, 2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "SpiritMemory"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _RimBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0h - facing, _RimPower);
                half4 colour = _BaseColor;
                colour.rgb += rim * _RimBoost;
                colour.a = saturate(colour.a + rim * 0.5h);
                return colour;
            }
            ENDHLSL
        }
    }
}
