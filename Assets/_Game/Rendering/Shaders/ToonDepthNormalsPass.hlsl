#ifndef AVATAR5_TOON_DEPTH_NORMALS_PASS_INCLUDED
#define AVATAR5_TOON_DEPTH_NORMALS_PASS_INCLUDED

// Same as URP's DepthNormals pass, but the otherwise unused alpha channel of _CameraNormalsTexture
// carries the Avatar5 ink-line category for the outline renderer feature:
//   0        = default (full silhouette and inner lines; also what every non-Avatar5 shader writes)
//   (0, 1]   = inner-line suppression (1 - Inner Line Weight)
//   -1       = excluded from all ink lines
// See InkOutline.shader for the decoder.

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    half3 normalWS    : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonDepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(input.normalOS));
    return output;
}

void ToonDepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half category = _OutlineExclude > 0.5h ? -1.0h : saturate(1.0h - _InnerLineWeight);

#if defined(_GBUFFER_NORMALS_OCT)
    float3 normalWS = normalize(input.normalWS);
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    outNormalWS = half4(PackFloat2To888(remappedOctNormalWS), category);
#else
    outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), category);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
