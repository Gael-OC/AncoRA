// Paints the real building over (PaseoReplacement). Drawn on the grown box of the building, it covers the camera image with
// sky above the horizon and ground below it, chosen by the view direction, so the building leaves the screen.
//
// One shader, two materials, two draws of the same box mesh (the render state comes from the material, see PaseoSetup):
//  - «marca» (PaseoReemplazoMarca): queue 1998, writes no colour and no depth, only stencil. Its vertices are slid towards the
//    camera along their own view ray by _Holgura metres of depth, so the silhouette does not move. It passes the depth test
//    against what the depth buffer holds at that moment: the ARCore environment depth (the AR background writes it before the
//    opaques, see ARCoreBackground.shader) or the far plane when the phone has none. So the pixels where a real object stands
//    more than _Holgura metres in front of the building are NOT marked.
//  - «fondo» (PaseoReemplazoFondo): queue 1999, paints the inner faces only where the mark is, and writes their depth with
//    ZTest Always. That overrides the environment depth of the real building, so the model inside the box is not hidden by the
//    building it replaces; where the mark is missing the environment depth stays and hides the model, and the camera image
//    shows the real object.
// Without environment depth the depth buffer is the far plane everywhere, the mark covers the whole silhouette and the result is
// the box painted over, as before. The defaults below are the old single-draw state (inner faces, ZTest LEqual, opaque).
Shader "AncoRA/Paseo/Fondo"
{
    Properties
    {
        _SkyTop ("Cielo arriba", Color) = (0.40, 0.62, 0.90, 1)
        _SkyHorizon ("Cielo en el horizonte", Color) = (0.80, 0.87, 0.95, 1)
        _GroundHorizon ("Suelo en el horizonte", Color) = (0.62, 0.60, 0.56, 1)
        _GroundNear ("Suelo cerca", Color) = (0.46, 0.44, 0.41, 1)

        // Metres of depth the vertices slide towards the camera; the material of the painting draw leaves it at 0, and
        // PaseoReplacement sets it per building (edificio.json «reemplazo.holgura») on the mark's renderer.
        _Holgura ("Holgura (m)", Float) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(None, 0, All, 15)] _ColorMask ("ColorMask", Float) = 15
        // URP keeps bits 0 to 3 of the stencil for the user; the tour uses bit 0.
        _StencilRef ("Stencil ref", Float) = 1
        _StencilMask ("Stencil mask", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil pass", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Fondo"
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            ColorMask [_ColorMask]
            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilMask]
                WriteMask [_StencilMask]
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

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
                float _Holgura;
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

            // Slides the point along its own view ray until its depth (distance along the camera's forward axis, which is
            // what the environment depth measures) is `metres` smaller. The ray does not change, so neither does the pixel
            // the point lands on. A point within `metres` of the camera plane is kept in front of the lens, not flipped.
            float3 PullTowardsCamera(float3 positionWS, float metres)
            {
                float depth = -TransformWorldToView(positionWS).z;
                float scale = depth > 0.0 ? max(1.0 - metres / depth, 0.02) : 1.0;
                return lerp(GetCameraPositionWS(), positionWS, scale);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = _Holgura > 0.0 ? PullTowardsCamera(positionWS, _Holgura) : positionWS;
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
