// Avatar5 cel/toon surface shader for characters, enemies and environment (URP 17.3, Forward/Forward+).
// Opaque only. Ink lines come from the InkOutline renderer feature, which reads the DepthNormals pass below.
Shader "Avatar5/Toon"
{
    Properties
    {
        [Header(Colour)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1, 1, 1, 1)
        _ShadeColor("Shade Colour (multiplied with base)", Color) = (0.55, 0.45, 0.65, 1)

        [Header(Cel Bands)]
        [IntRange] _BandCount("Light Bands", Range(2, 4)) = 3
        _BandSoftness("Band Transition Softness", Range(0.001, 0.2)) = 0.02
        _ShadeOffset("Light/Shadow Boundary Offset", Range(-0.5, 0.5)) = 0
        _AmbientStrength("Ambient Influence On Shade", Range(0, 1)) = 0.5

        [Header(Highlights)]
        _SpecularColor("Specular Colour", Color) = (0, 0, 0, 1)
        _SpecularSize("Specular Size", Range(0, 0.5)) = 0.05
        _SpecularSoftness("Specular Softness", Range(0.001, 0.1)) = 0.01
        _RimColor("Rim Colour", Color) = (1, 1, 1, 1)
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.72
        _RimStrength("Rim Strength", Range(0, 1)) = 0

        [Header(Hand Painted Variation)]
        _PaintNoiseStrength("Band Wobble", Range(0, 0.3)) = 0.06
        _PaintNoiseScale("Wobble Scale (per metre)", Range(0.1, 8)) = 1.6
        _ColorVariation("Colour Variation", Range(0, 0.2)) = 0.04

        [Header(Emission)]
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)

        [Header(Ink Lines)]
        _InnerLineWeight("Inner Line Weight", Range(0, 1)) = 1
        [Toggle] _OutlineExclude("Exclude From Ink Lines", Float) = 0

        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // Global quality switch set by GraphicsQualityController (Low profile).
            #pragma multi_compile_fragment _ A5_TOON_SIMPLE

            #include "ToonInput.hlsl"
            #include "ToonForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonDepthNormalsVertex
            #pragma fragment ToonDepthNormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "ToonInput.hlsl"
            #include "ToonDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
