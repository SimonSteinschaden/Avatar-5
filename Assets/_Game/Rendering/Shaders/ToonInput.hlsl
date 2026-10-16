#ifndef AVATAR5_TOON_INPUT_INCLUDED
#define AVATAR5_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// One CBUFFER for every pass keeps the shader SRP-Batcher compatible.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadeColor;
    half _BandCount;
    half _BandSoftness;
    half _ShadeOffset;
    half _AmbientStrength;
    half4 _SpecularColor;
    half _SpecularSize;
    half _SpecularSoftness;
    half4 _RimColor;
    half _RimThreshold;
    half _RimStrength;
    half _PaintNoiseStrength;
    half _PaintNoiseScale;
    half _ColorVariation;
    half4 _EmissionColor;
    half _InnerLineWeight;
    half _OutlineExclude;
    half _Cutoff;
CBUFFER_END

#endif
