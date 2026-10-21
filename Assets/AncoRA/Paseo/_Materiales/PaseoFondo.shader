// Paints the real building over (PaseoReplacement). Drawn on the inner faces of the grown box, it covers the camera
// image with sky above the horizon and ground below it, chosen by the view direction, so the building leaves the
// screen. Opaque and writing depth: the inner faces lie behind the model inside the box, which draws in front.
Shader "AncoRA/Paseo/Fondo"
{
    Properties
    {
        _SkyTop ("Cielo arriba", Color) = (0.40, 0.62, 0.90, 1)
        _SkyHorizon ("Cielo en el horizonte", Color) = (0.80, 0.87, 0.95, 1)
        _GroundHorizon ("Suelo en el horizonte", Color) = (0.62, 0.60, 0.56, 1)
        _GroundNear ("Suelo cerca", Color) = (0.46, 0.44, 0.41, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Fondo"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTop;
                half4 _SkyHorizon;
                half4 _GroundHorizon;
                half4 _GroundNear;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // AR world y is up (gravity), so this is the sine of the elevation of the view ray.
                float up = normalize(input.positionWS - GetCameraPositionWS()).y;
                half4 sky = lerp(_SkyHorizon, _SkyTop, saturate(up * 2.0));
                half4 ground = lerp(_GroundHorizon, _GroundNear, saturate(-up * 3.0));
                return lerp(ground, sky, smoothstep(-0.01, 0.01, up));
            }
            ENDHLSL
        }
    }
}
