// KSP2ClusterDeferred.hlsl
//
// Deferred+ light pass for the KSP2 G-buffer (Include/KSP2GBuffer.hlsl). URP's cluster deferred structure with
// KSP2's direct lighting in place of URP's: the Standard BRDF1 the ported shaders use (KSP2_StandardDirectLight),
// the Built-in falloff and spot cone for additional lights (KSP2LegacyLights.hlsl), and stock's deferred extras
// on every light, the cloud shadow (KSP2CloudShadow.hlsl) and the main light shadow looked up at the water
// surface under water (KSP2WaterShadow.hlsl). Stock KSP2 rendered deferred and got these from its deferred
// lighting. The forward ports reproduce them per shader, and G-buffer surfaces get them here.
//
// The G-buffer holds the KSP2 surface in URP's layout (metallic or specular setup, see KSP2GBuffer.hlsl).
// Indirect light and emission were written to the lighting target by the G-buffer pass, so this pass only adds
// direct light.

#ifndef KSP2_CLUSTER_DEFERRED_INCLUDED
#define KSP2_CLUSTER_DEFERRED_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"
#include "Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/KSP2Lighting.hlsl"
#include "Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/KSP2GBufferFlags.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 screenUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings VertexFullScreen(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Full-screen triangle on the far plane, so ZTest NotEqual skips the sky.
    output.positionCS = float4(input.positionOS.xy, UNITY_RAW_FAR_CLIP_VALUE, 1.0);
    output.screenUV = output.positionCS.xyw;
#if UNITY_UV_STARTS_AT_TOP
    output.screenUV.xy = output.screenUV.xy * float2(0.5, -0.5) + 0.5 * output.screenUV.z;
#else
    output.screenUV.xy = output.screenUV.xy * 0.5 + 0.5 * output.screenUV.z;
#endif
    output.screenUV.xy = DynamicScalingApplyScaleBias(output.screenUV.xy, float4(_RTHandleScale.xy, 0.0f, 0.0f));
    return output;
}

float4x4 _ScreenToWorld[2];

// URP's metallic decode is the Standard shader's (dielectric F0 0.04, diffuse scaled by 0.96 - 0.96 metallic),
// the same as KSP2_MakeMetallicSurface. So a metallic KSP2 surface is URP's BRDF diffuse and specular colours.
// Specular KSP2 surfaces store their diffuse colour as is (kKSP2MaterialFlagDiffuseAsIs).
KSP2Surface KSP2_SurfaceFromGBuffer(GBufferData gBufferData)
{
    BRDFData brdfData = GBufferDataToBRDFData(gBufferData);
    KSP2Surface s = (KSP2Surface)0;
    s.albedo = gBufferData.baseColor;
    s.diffuseAlbedo = (gBufferData.materialFlags & kKSP2MaterialFlagDiffuseAsIs) != 0 ? gBufferData.baseColor : brdfData.diffuse;
    // Specular highlights off (Standard): KSP2_StandardDirectLight drops the specular lobe for a zero F0.
    s.F0 = (gBufferData.materialFlags & kMaterialFlagSpecularHighlightsOff) != 0 ? 0.0 : brdfData.specular;
    s.smoothness = gBufferData.smoothness;
    s.occlusion = gBufferData.occlusion;
    s.alpha = 1.0;
    return s;
}

half4 KSP2_DeferredShadingClustered(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 screenUV = input.screenUV.xy / input.screenUV.z;
    GBufferData gBufferData = UnpackGBuffers(input.positionCS.xy);

    float4 positionWS = mul(_ScreenToWorld[SLICE_ARRAY_INDEX], float4(input.positionCS.xy, gBufferData.depth, 1.0));
    positionWS.xyz *= rcp(positionWS.w);

    float3 N = gBufferData.normalWS;
    float3 V = GetWorldSpaceNormalizeViewDir(positionWS.xyz);
    KSP2Surface s = KSP2_SurfaceFromGBuffer(gBufferData);
    float4 pixelCS = float4(input.positionCS.xy, gBufferData.depth, 1.0);
    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS.xyz;
    inputData.positionCS = pixelCS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
    inputData.shadowMask = gBufferData.shadowMask;

    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenUV);
    half alpha = 1.0;
#if defined(_SCREEN_SPACE_OCCLUSION)
    // The G-buffer pass already applied the baked occlusion to the indirect light, so the darker of the two wins.
    half occlusion = aoFactor.indirectAmbientOcclusion < gBufferData.occlusion
        ? aoFactor.indirectAmbientOcclusion * rcp(gBufferData.occlusion) : 1.0;
    alpha = occlusion;
#endif

    bool receiveShadowsOff = (gBufferData.materialFlags & kMaterialFlagReceiveShadowsOff) != 0;
    float cloudShadow = KSP2_CloudShadow(normalizedScreenSpaceUV);
    float3 color = 0;

    Light mainLight = GetMainLight();
    mainLight.distanceAttenuation = 1.0;
    UNITY_BRANCH if (!receiveShadowsOff)
    {
        float3 shadowPositionWS = KSP2_MainLightShadowPosition(positionWS.xyz, pixelCS, normalizedScreenSpaceUV);
        mainLight.shadowAttenuation = MainLightShadow(TransformWorldToShadowCoord(shadowPositionWS), shadowPositionWS,
            gBufferData.shadowMask, _MainLightOcclusionProbes);
    }
#if defined(_LIGHT_COOKIES)
    mainLight.color *= SampleMainLightCookie(positionWS.xyz);
#endif
#if defined(_SCREEN_SPACE_OCCLUSION)
    mainLight.shadowAttenuation *= aoFactor.directAmbientOcclusion;
#endif
    // The local sun carries the night and the atmosphere's colour at the observer. A pixel past the terminator or in a
    // moon's shadow goes darker, and takes its own colour of the light.
    KSP2_ApplyLocalSun(mainLight, positionWS.xyz, N);
#if defined(_LIGHT_LAYERS)
    UNITY_BRANCH if (IsMatchingLightLayer(mainLight.layerMask, gBufferData.meshRenderingLayers))
#endif
    {
        color += KSP2_ShadeSun(mainLight, N, V, s, cloudShadow);
    }

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = KSP2_GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);
        UNITY_BRANCH if (receiveShadowsOff)
            light.shadowAttenuation = 1.0;
#if defined(_LIGHT_LAYERS)
        UNITY_BRANCH if (IsMatchingLightLayer(light.layerMask, gBufferData.meshRenderingLayers))
#endif
        {
            color += KSP2_ShadeLight(light, N, V, s, cloudShadow);
        }
    LIGHT_LOOP_END

    // Additional directional lights last, as URP does (FXC otherwise rejects the loop structure).
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = KSP2_GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);
        UNITY_BRANCH if (receiveShadowsOff)
            light.shadowAttenuation = 1.0;
#if defined(_LIGHT_LAYERS)
        UNITY_BRANCH if (IsMatchingLightLayer(light.layerMask, gBufferData.meshRenderingLayers))
#endif
        {
            color += KSP2_ShadeLight(light, N, V, s, cloudShadow);
        }
    }

    // Debug views of the KSP2 G-buffer pixels, chosen with the global _KSP2DeferredDebug (Shader.SetGlobalFloat):
    // 1 coverage (magenta), 2 normal, 3 diffuse albedo, 4 unshadowed sun, 5 main light layer match (r), sun shadow
    // (g) and cloud shadow (b). Views 1 to 5 replace the lighting buffer, so post-processing still applies on top.
    // 6 marks pixels whose direct light is not finite in magenta and keeps the rest. 7 is normal rendering here, and
    // the G-buffer pass marks indirect light that is not finite in bright green (KSP2GBuffer.hlsl). 8 shows the terrain
    // shadow (KSP2TerrainShadow.hlsl, KSP2_TerrainShadowDebug). 0 is normal rendering.
    [branch] if (_KSP2DeferredDebug > 7.5 && _KSP2DeferredDebug < 8.5)
        return half4(KSP2_TerrainShadowDebug(positionWS.xyz, N), 0);
    if (_KSP2DeferredDebug > 0.5 && _KSP2DeferredDebug < 6.5)
    {
        if (_KSP2DeferredDebug < 1.5) return half4(1, 0, 1, 0);
        if (_KSP2DeferredDebug < 2.5) return half4(N * 0.5 + 0.5, 0);
        if (_KSP2DeferredDebug < 3.5) return half4(s.diffuseAlbedo, 0);
        if (_KSP2DeferredDebug < 4.5) return half4(mainLight.color * saturate(dot(N, mainLight.direction)), 0);
        if (_KSP2DeferredDebug < 5.5)
            return half4(IsMatchingLightLayer(mainLight.layerMask, gBufferData.meshRenderingLayers) ? 1 : 0, mainLight.shadowAttenuation, cloudShadow, 0);
        if (any(!isfinite(color)))
            return half4(50, 0, 50, 0);
    }

    return half4(color, alpha);
}

#endif // KSP2_CLUSTER_DEFERRED_INCLUDED