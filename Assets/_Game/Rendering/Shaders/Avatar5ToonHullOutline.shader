// Optional inverted-hull outline for hero characters (evaluated as a complement to the screen-space
// InkOutline feature, see docs/P07a_RENDERING_IMPLEMENTATION.md). Add as an extra material slot on a
// renderer. Width is specified in pixels at 1080p and kept constant on screen.
// Needs smooth vertex normals: on hard-edged meshes (e.g. the Unity cube) the hull splits at corners.
Shader "Avatar5/Toon Hull Outline"
{
    Properties
    {
        _OutlineColor("Outline Colour", Color) = (0, 0, 0, 1)
        _OutlineWidthPx("Outline Width (px at 1080p)", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "HullOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HullVertex
            #pragma fragment HullFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidthPx;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HullVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = normalize(mul((float3x3)UNITY_MATRIX_VP, normalWS).xy + 1e-5);
                // Clip-space offset for N pixels: 2 * N / screenHeight * w (scaled for aspect on x).
                float pixels = _OutlineWidthPx * (_ScreenParams.y / 1080.0);
                float2 offset = normalCS * (2.0 * pixels / _ScreenParams.y) * positionCS.w;
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                positionCS.xy += offset;
                output.positionCS = positionCS;
                return output;
            }

            half4 HullFragment(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
