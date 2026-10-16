#ifndef AVATAR5_TOON_FORWARD_PASS_INCLUDED
#define AVATAR5_TOON_FORWARD_PASS_INCLUDED

#include "ToonLighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv          : TEXCOORD0;
    float3 positionWS  : TEXCOORD1;
    half3 normalWS     : TEXCOORD2;
    half fogFactor     : TEXCOORD3;
    float4 positionCS  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = positionInputs.positionWS;
    output.positionCS = positionInputs.positionCS;
    output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
    return output;
}

half4 ToonFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

    A5ToonSurface s;
    s.albedo = baseSample.rgb * _BaseColor.rgb;
    s.shadeColor = _ShadeColor.rgb;
    s.emission = _EmissionColor.rgb;
    s.bandSoftness = _BandSoftness;
    s.shadeOffset = _ShadeOffset;
    s.ambientStrength = _AmbientStrength;
    s.specularColor = _SpecularColor.rgb;
    s.specularSize = _SpecularSize;
    s.specularSoftness = _SpecularSoftness;
    s.rimColor = _RimColor.rgb;
    s.rimThreshold = _RimThreshold;
    s.rimStrength = _RimStrength;

#if defined(A5_TOON_SIMPLE)
    s.bandCount = 2.0h;
    s.paintNoise = 0.0h;
#else
    s.bandCount = _BandCount;
    float noiseA = A5ValueNoise(input.positionWS * _PaintNoiseScale);
    float noiseB = A5ValueNoise(input.positionWS * (_PaintNoiseScale * 2.7) + 17.0);
    s.paintNoise = (half)((noiseA * 0.65 + noiseB * 0.35) - 0.5) * _PaintNoiseStrength;
    s.albedo *= 1.0h + (half)((noiseB - 0.5) * 2.0) * _ColorVariation;
#endif

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    half4 shadowMask = half4(1, 1, 1, 1);

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    half3 ambient = A5ToonAmbient(normalWS, s.ambientStrength);
    half3 color = A5ToonMainLight(s, mainLight, normalWS, viewDirWS, ambient);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        color += A5ToonAdditionalLight(s, light, normalWS);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        color += A5ToonAdditionalLight(s, light, normalWS);
    LIGHT_LOOP_END
#endif

    color += s.emission;
    color = MixFog(color, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
    return half4(color, 1.0h);
}

#endif
