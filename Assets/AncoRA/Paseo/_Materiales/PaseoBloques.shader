// Flat colour with a fixed half-Lambert light for the block characters and props of the park (PaseoPark). The scene has no
// lights, so the light direction lives here. Unlike AncoRA/Paseo/Caja it tests the depth buffer, but it queues after the
// translucent fill of the building box (ZTest Always, Transparent queue), or the fill would tint the park.
Shader "AncoRA/Paseo/Bloques"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Bloques"
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const half3 lightDirection = half3(0.3578, 0.8944, 0.2683); // normalize(0.4, 1, 0.3)
                half halfLambert = dot(normalize(input.normalWS), lightDirection) * 0.5h + 0.5h;
                half light = lerp(0.45h, 1.0h, halfLambert);
                return half4(_BaseColor.rgb * light, 1.0h);
            }
            ENDHLSL
        }
    }
}
