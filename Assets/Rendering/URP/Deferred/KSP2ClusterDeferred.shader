// The Deferred+ light pass of the KSP2Redux renderer, assigned in place of URP's ClusterDeferred in the URP global
// settings (UniversalRendererResources). Pass layout and render state are URP's (the renderer finds the passes by
// index and sets the stencil properties). Pass 0 lights KSP2 G-buffer surfaces with KSP2ClusterDeferred.hlsl.
// Passes 1 (SimpleLit) and 2 (fog) are URP's own, since no KSP2 shader writes a SimpleLit G-buffer.
Shader "Hidden/Redux/KSP2ClusterDeferred"
{
    Properties {
        _LitStencilRef ("LitStencilRef", Int) = 0
        _LitStencilReadMask ("LitStencilReadMask", Int) = 0
        _LitStencilWriteMask ("LitStencilWriteMask", Int) = 0

        _SimpleLitStencilRef ("SimpleLitStencilRef", Int) = 0
        _SimpleLitStencilReadMask ("SimpleLitStencilReadMask", Int) = 0
        _SimpleLitStencilWriteMask ("SimpleLitStencilWriteMask", Int) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        // 0 - Clustered Lights - Lit (KSP2)
        Pass
        {
            Name "Deferred Clustered Lights (Lit)"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One

            Stencil {
                Ref [_LitStencilRef]
                ReadMask [_LitStencilReadMask]
                WriteMask [_LitStencilWriteMask]
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore

            #pragma vertex VertexFullScreen
            #pragma fragment KSP2_DeferredShadingClustered

            #include "Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/KSP2Pipeline.hlsl"
            #define _LIT

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            // URP's render graph hands the G-buffer over as framebuffer inputs (with a depth slice) under this keyword.
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

            #include "KSP2ClusterDeferred.hlsl"
            ENDHLSL
        }

        // 1 - Clustered Lights - SimpleLit (URP's)
        Pass
        {
            Name "Deferred Clustered Lights (SimpleLit)"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend One SrcAlpha, Zero One

            Stencil {
                Ref [_SimpleLitStencilRef]
                ReadMask [_SimpleLitStencilReadMask]
                WriteMask [_SimpleLitStencilWriteMask]
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore

            #pragma vertex VertexFullScreen
            #pragma fragment DeferredShadingClustered

            #define _CLUSTER_LIGHT_LOOP
            #define _SIMPLELIT

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _DEFERRED_MIXED_LIGHTING
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ClusterDeferred.hlsl"
            ENDHLSL
        }

        // 2 - Legacy fog (URP's)
        Pass
        {
            Name "Fog"

            ZTest NotEqual
            ZWrite Off
            Cull Off
            Blend OneMinusSrcAlpha SrcAlpha, Zero One
            BlendOp Add, Add

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore

            #pragma vertex Vertex
            #pragma fragment Frag

            #define _FOG

            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/FogDeferred.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}