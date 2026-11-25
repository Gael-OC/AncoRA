// Flat colour with a fixed half-Lambert light for the block characters and props of the park (PaseoPark). The scene has no
// lights, so the light direction lives here. Unlike AncoRA/Paseo/Caja it tests the depth buffer, but it queues after the
// translucent fill of the building box (ZTest Always, Transparent queue), or the fill would tint the park.
// _Reveal < 1 cuts the mesh above a height in object space (the building models are normalized to their box, y in
// -0.5..0.5 plus the roof; _RevealRange is set per model from its mesh bounds): the era change builds a model up from the ground or takes it down from the top, with a
// glowing, slightly ragged edge. At 1 (the default, and the park) nothing changes.
Shader "AncoRA/Paseo/Bloques"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Texture", 2D) = "white" {}
        _Reveal ("Reveal", Range(0, 1)) = 1
        _RevealRange ("Reveal bottom and top (object y)", Vector) = (-0.6, 0.85, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Bloques"
            ZWrite On
            ZTest LEqual
            // Both sides, but back faces are drawn only while a model is cut open by the era change: it then shows a dark
            // inside instead of floating facades.
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PaseoOcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _Reveal;
                float4 _RevealRange;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                float objectY : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float2 uv : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                output.objectY = input.positionOS.y;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                PaseoClipForeground(input.screenPosition, input.eyeDepth);
                // Whole models (and the park) look exactly as with back-face culling.
                if (_Reveal >= 0.999 && !IS_FRONT_VFACE(face, true, false))
                    discard;
                half edge = 0.0h;
                if (_Reveal < 0.999)
                {
                    // Ragged edge: each 0.5 m cell of the building is a little ahead or behind the cut.
                    float3 cell = floor(input.positionWS * 2.0);
                    float jitter = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453) - 0.5;
                    float cut = lerp(_RevealRange.x, _RevealRange.y, _Reveal) + jitter * 0.05;
                    float below = cut - input.objectY;
                    clip(below);
                    edge = (half)(1.0 - saturate(below / 0.04));
                }
                const half3 lightDirection = half3(0.3578, 0.8944, 0.2683); // normalize(0.4, 1, 0.3)
                half halfLambert = dot(normalize(input.normalWS), lightDirection) * 0.5h + 0.5h;
                half light = lerp(0.45h, 1.0h, halfLambert);
                half3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 color = albedo * light;
                if (!IS_FRONT_VFACE(face, true, false))
                    color = albedo * 0.3h;
                return half4(lerp(color, half3(1.0h, 0.82h, 0.45h), edge), 1.0h);
            }
            ENDHLSL
        }
    }
}
