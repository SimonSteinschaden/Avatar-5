// Screen-space ink lines for Avatar5. Drawn by InkOutlineRendererFeature as a full-screen triangle
// blended over the camera colour after opaques (before transparents).
//
// Silhouettes ("outer" lines): linear eye-depth discontinuities.
// Inner lines: world-normal discontinuities where the depth is continuous (creases, contact lines).
// Material category is decoded from the alpha of _CameraNormalsTexture (see ToonDepthNormalsPass.hlsl).
Shader "Hidden/Avatar5/InkOutline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "InkOutline"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment InkFragment
            #pragma multi_compile_local_fragment _ _A5_INNER_LINES
            #pragma multi_compile_local_fragment _ _A5_HQ_SAMPLING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _A5_OutlineColor;
            float4 _A5_TargetSize;      // xy = pixel size of the target, zw = 1 / size
            float4 _A5_LineParams;      // x = silhouette radius px, y = inner radius px, z = silhouette intensity, w = inner intensity
            float4 _A5_DepthParams;     // x = depth threshold (m), y = relative threshold (per m of eye depth), z = grazing-angle correction, w = depth softness
            float4 _A5_NormalParams;    // x = normal threshold (1 - cos), y = softness, z = inner fade start (m), w = inner fade end (m)
            float4 _A5_CameraParams;    // x = is orthographic, y = near, z = far
            float4 _A5_CameraForward;   // xyz = camera forward in world space
            float _A5_DebugView;        // 0 = off, 1 = lines only, 2 = colour-coded line types

            float A5EyeDepth(float rawDepth)
            {
                if (_A5_CameraParams.x > 0.5)
                {
                #if UNITY_REVERSED_Z
                    rawDepth = 1.0 - rawDepth;
                #endif
                    return lerp(_A5_CameraParams.y, _A5_CameraParams.z, rawDepth);
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            // Neighbour taps are bilinear: sub-pixel radii then give partially covered, anti-aliased lines
            // instead of popping between 1 and 2 pixels while the camera moves.
            float A5SampleEye(float2 uv, SamplerState samp)
            {
                return A5EyeDepth(SAMPLE_TEXTURE2D_X(_CameraDepthTexture, samp, uv).r);
            }

            float4 A5SampleNormalCategory(float2 uv, SamplerState samp)
            {
                return SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, samp, uv);
            }

        #if defined(_A5_HQ_SAMPLING)
            #define A5_TAPS 8
            static const float2 kTaps[8] =
            {
                float2( 1, 0), float2(-1, 0), float2(0,  1), float2(0, -1),
                float2( 0.7071,  0.7071), float2(-0.7071,  0.7071),
                float2( 0.7071, -0.7071), float2(-0.7071, -0.7071)
            };
        #else
            #define A5_TAPS 4
            static const float2 kTaps[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
        #endif

            half4 InkFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _A5_TargetSize.zw;

                float centerEye = A5SampleEye(uv, sampler_PointClamp);
                float4 centerNC = A5SampleNormalCategory(uv, sampler_PointClamp);
                float3 centerN = centerNC.xyz;
                float farPlane = _A5_CameraParams.z * 0.999;

                // Depth threshold grows with distance and at grazing angles, which suppresses
                // the classic false lines on slopes seen from a shallow angle.
                float nDotV = abs(dot(centerN, -_A5_CameraForward.xyz));
                float grazing = 1.0 + _A5_DepthParams.z * (1.0 / max(nDotV, 0.2) - 1.0);
                float depthThreshold = (_A5_DepthParams.x + _A5_DepthParams.y * centerEye) * grazing;

                // ---- Silhouettes ------------------------------------------------------------
                float maxDepthDelta = 0.0;
                float nearestEye = centerEye;
                float nearestCategory = centerNC.w;
                [unroll]
                for (int i = 0; i < A5_TAPS; i++)
                {
                    float2 suv = uv + kTaps[i] * _A5_LineParams.x * texel;
                    float sEye = A5SampleEye(suv, sampler_LinearClamp);
                    maxDepthDelta = max(maxDepthDelta, abs(sEye - centerEye));
                    if (sEye < nearestEye)
                    {
                        nearestEye = sEye;
                        nearestCategory = A5SampleNormalCategory(suv, sampler_PointClamp).w;
                    }
                }
                float silhouette = smoothstep(depthThreshold, depthThreshold * (1.0 + _A5_DepthParams.w), maxDepthDelta);
                // The line belongs to the object in front; excluded categories (-1) draw none.
                silhouette *= step(-0.5, nearestCategory);
                // Nothing in front of the sky: no line on empty background pixels.
                silhouette *= step(nearestEye, farPlane);

                // ---- Inner lines ------------------------------------------------------------
                float inner = 0.0;
            #if defined(_A5_INNER_LINES)
                if (centerEye < farPlane && centerNC.w > -0.5)
                {
                    float maxNormalDelta = 0.0;
                    [unroll]
                    for (int j = 0; j < A5_TAPS; j++)
                    {
                        float2 suv = uv + kTaps[j] * _A5_LineParams.y * texel;
                        float4 sNC = A5SampleNormalCategory(suv, sampler_LinearClamp);
                        float sEye = A5SampleEye(suv, sampler_LinearClamp);
                        // Only compare normals across continuous depth; depth jumps are silhouettes.
                        float continuous = step(abs(sEye - centerEye), depthThreshold * 2.0) * step(-0.5, sNC.w);
                        maxNormalDelta = max(maxNormalDelta, (1.0 - dot(centerN, SafeNormalize(sNC.xyz))) * continuous);
                    }
                    inner = smoothstep(_A5_NormalParams.x, _A5_NormalParams.x + _A5_NormalParams.y, maxNormalDelta);
                    inner *= 1.0 - saturate(centerNC.w);                 // per-material inner-line weight
                    inner *= 1.0 - smoothstep(_A5_NormalParams.z, _A5_NormalParams.w, centerEye);
                }
            #endif

                float silhouetteInk = silhouette * _A5_LineParams.z;
                float innerInk = inner * _A5_LineParams.w;
                float ink = saturate(max(silhouetteInk, innerInk));

                if (_A5_DebugView > 1.5)
                {
                    // Colour-coded: red = silhouette/outer line, cyan = inner line (linear values, saturated after sRGB encode).
                    half3 debugColor = silhouetteInk >= innerInk ? half3(1, 0.008, 0.008) : half3(0.008, 0.75, 1);
                    return half4(debugColor, ink);
                }
                if (_A5_DebugView > 0.5)
                {
                    // Lines only on paper white.
                    return half4(lerp(half3(1, 1, 1), _A5_OutlineColor.rgb, ink), 1);
                }
                return half4(_A5_OutlineColor.rgb, ink * _A5_OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
