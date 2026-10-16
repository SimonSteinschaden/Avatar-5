#ifndef AVATAR5_TOON_LIGHTING_INCLUDED
#define AVATAR5_TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---------------------------------------------------------------------------------------------
// Procedural "paint" noise: cheap 3D value noise in world space. Breaks up perfectly flat bands
// so surfaces read as hand-painted without needing textures.
// ---------------------------------------------------------------------------------------------
float A5Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float A5ValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = A5Hash13(i);
    float n100 = A5Hash13(i + float3(1, 0, 0));
    float n010 = A5Hash13(i + float3(0, 1, 0));
    float n110 = A5Hash13(i + float3(1, 1, 0));
    float n001 = A5Hash13(i + float3(0, 0, 1));
    float n101 = A5Hash13(i + float3(1, 0, 1));
    float n011 = A5Hash13(i + float3(0, 1, 1));
    float n111 = A5Hash13(i + float3(1, 1, 1));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
}

// ---------------------------------------------------------------------------------------------
// Banding: maps a continuous light value x in [0,1] onto bandCount (2..4) discrete levels
// 0, 1/(n-1), ..., 1. softness widens each step for anti-aliased band borders.
// ---------------------------------------------------------------------------------------------
half A5ToonBands(half x, half bandCount, half softness)
{
    half n = clamp(round(bandCount), 2.0h, 4.0h);
    half s = max(softness, 0.0005h);
    half level = 0.0h;
    [unroll]
    for (int i = 1; i < 4; i++)
    {
        half enabled = step((half)i, n - 1.0h);
        half threshold = (half)i / n;
        level += enabled * smoothstep(threshold - s, threshold + s, x);
    }
    return level / (n - 1.0h);
}

struct A5ToonSurface
{
    half3 albedo;
    half3 shadeColor;
    half3 emission;
    half bandCount;
    half bandSoftness;
    half shadeOffset;
    half ambientStrength;
    half3 specularColor;
    half specularSize;
    half specularSoftness;
    half3 rimColor;
    half rimThreshold;
    half rimStrength;
    half paintNoise;   // already evaluated noise in [-0.5, 0.5] * strength
};

half3 A5ToonMainLight(A5ToonSurface s, Light light, half3 normalWS, half3 viewDirWS, half3 ambient)
{
    half NdotL = dot(normalWS, light.direction);
    // Half-lambert keeps the shadow side from collapsing into a single flat area.
    half x = saturate(NdotL * 0.5h + 0.5h + s.shadeOffset + s.paintNoise);
    // Received shadows clamp the light value, so cast shadows snap to the shade band as well.
    x = min(x, light.shadowAttenuation * light.distanceAttenuation);

    half level = A5ToonBands(x, s.bandCount, s.bandSoftness);

    half3 shade = s.albedo * s.shadeColor * ambient;
    half3 lit = s.albedo * light.color;
    half3 color = lerp(shade, lit, level);

#if !defined(A5_TOON_SIMPLE)
    half3 halfDir = SafeNormalize(light.direction + viewDirWS);
    half NdotH = saturate(dot(normalWS, halfDir));
    half specEdge = 1.0h - s.specularSize;
    half spec = smoothstep(specEdge - s.specularSoftness, specEdge + s.specularSoftness, NdotH);
    color += spec * level * s.specularColor * light.color;

    half fresnel = 1.0h - saturate(dot(normalWS, viewDirWS));
    half rim = smoothstep(s.rimThreshold - 0.03h, s.rimThreshold + 0.03h, fresnel);
    color += rim * s.rimStrength * s.rimColor * (0.35h + 0.65h * level);
#endif

    return color;
}

half3 A5ToonAdditionalLight(A5ToonSurface s, Light light, half3 normalWS)
{
    half NdotL = saturate(dot(normalWS, light.direction));
    half atten = NdotL * light.distanceAttenuation * light.shadowAttenuation;
    // Two hard steps keep point lights (torches, spells) in the comic look.
    half level = smoothstep(0.02h, 0.06h, atten) * 0.5h + smoothstep(0.25h, 0.31h, atten) * 0.5h;
    return s.albedo * light.color * level;
}

half3 A5ToonAmbient(half3 normalWS, half ambientStrength)
{
    // Mostly the "up" probe so the ambient term stays flat like a painted shadow colour.
    half3 sh = lerp(SampleSH(half3(0, 1, 0)), SampleSH(normalWS), 0.35h);
    return lerp(half3(1, 1, 1), max(sh * 2.0h, 0.05h), ambientStrength);
}

#endif
