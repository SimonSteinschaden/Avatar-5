// Stylised comic water for Avatar5: depth-banded colour, animated wave stripes, shore foam and a thin
// ink line where the water meets geometry. Transparent queue, so it never produces screen-space ink
// lines itself (it is not in the depth/normals prepass) and does not hide opaque outlines below it.
Shader "Avatar5/Toon Water"
{
    Properties
    {
        _ShallowColor("Shallow Colour", Color) = (0.35, 0.85, 0.85, 1)
        _DeepColor("Deep Colour", Color) = (0.08, 0.35, 0.65, 1)
        _DepthRange("Depth For Deep Colour (m)", Range(0.1, 5)) = 1.2
        _Opacity("Opacity", Range(0, 1)) = 0.88
        [IntRange] _DepthBands("Depth Bands", Range(2, 4)) = 3

        _StripeColor("Wave Stripe Colour", Color) = (0.75, 0.97, 1, 1)
        _StripeScale("Wave Stripe Scale", Range(0.05, 2)) = 0.45
        _StripeSpeed("Wave Stripe Speed", Range(0, 2)) = 0.35
        _StripeAmount("Wave Stripe Amount", Range(0, 1)) = 0.32

        _FoamColor("Shore Foam Colour", Color) = (0.95, 1, 1, 1)
        _FoamDistance("Shore Foam Width (m)", Range(0.01, 1)) = 0.28
        _InkColor("Shore Ink Colour", Color) = (0.02, 0.03, 0.06, 1)
        _InkDistance("Shore Ink Width (m)", Range(0, 0.2)) = 0.05

        _WaveHeight("Vertex Wave Height (m)", Range(0, 0.3)) = 0.04
        _WaveFrequency("Vertex Wave Frequency", Range(0.1, 4)) = 1.1
        _WaveSpeed("Vertex Wave Speed", Range(0, 4)) = 1.2
        _SpecularSize("Glint Size", Range(0, 0.3)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ A5_TOON_SIMPLE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthRange;
                half _Opacity;
                half _DepthBands;
                half4 _StripeColor;
                half _StripeScale;
                half _StripeSpeed;
                half _StripeAmount;
                half4 _FoamColor;
                half _FoamDistance;
                half4 _InkColor;
                half _InkDistance;
                half _WaveHeight;
                half _WaveFrequency;
                half _WaveSpeed;
                half _SpecularSize;
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
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                half fogFactor    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float A5WaterHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float A5WaterNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(A5WaterHash(i), A5WaterHash(i + float2(1, 0)), u.x),
                            lerp(A5WaterHash(i + float2(0, 1)), A5WaterHash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings WaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                positionWS.y += (sin(positionWS.x * _WaveFrequency + t) + sin(positionWS.z * _WaveFrequency * 0.8 + t * 1.3)) * 0.5 * _WaveHeight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float A5WaterEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                #if UNITY_REVERSED_Z
                    rawDepth = 1.0 - rawDepth;
                #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEye = A5WaterEyeDepth(SampleSceneDepth(screenUV));
                float surfaceEye = A5WaterEyeDepth(input.positionCS.z);
                // View-space thickness of the water column behind this pixel.
                float thickness = max(sceneEye - surfaceEye, 0.0);

                half n = clamp(round(_DepthBands), 2.0h, 4.0h);
                half depth01 = saturate(thickness / _DepthRange);
                half banded = floor(depth01 * n) / (n - 1.0h);
                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(banded));

            #if !defined(A5_TOON_SIMPLE)
                float t = _Time.y * _StripeSpeed;
                float2 p = input.positionWS.xz * _StripeScale;
                float stripes = A5WaterNoise(p + float2(t, t * 0.6)) * 0.6 + A5WaterNoise(p * 2.3 - float2(t * 0.8, 0)) * 0.4;
                half stripe = smoothstep(0.62h, 0.66h, (half)stripes) * _StripeAmount;
                color = lerp(color, _StripeColor.rgb, stripe);
            #endif

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.72h, 1.0h, step(0.5h, mainLight.shadowAttenuation));
                color *= shadow * lerp(half3(1, 1, 1), mainLight.color, 0.5h);

            #if !defined(A5_TOON_SIMPLE)
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDir = SafeNormalize(mainLight.direction + viewDir);
                half glint = step(1.0h - _SpecularSize * 0.1h, dot(half3(0, 1, 0), halfDir)) * step(0.55h, (half)stripes);
                color += glint * mainLight.color * 0.6h;
            #endif

                half foam = 1.0h - smoothstep(_FoamDistance * 0.85h, _FoamDistance, (half)thickness);
                color = lerp(color, _FoamColor.rgb, foam);
                half ink = 1.0h - smoothstep(_InkDistance * 0.6h, _InkDistance, (half)thickness);
                color = lerp(color, _InkColor.rgb, ink * step(0.001h, _InkDistance));

                color = MixFog(color, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                half alpha = saturate(max(_Opacity, foam));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
