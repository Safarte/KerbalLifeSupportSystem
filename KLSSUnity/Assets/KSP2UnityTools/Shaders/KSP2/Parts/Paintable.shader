Shader "KSP2/Parts/Paintable"
{
    Properties
    {
        [Header(Color)] _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo Map", 2D) = "white" { }
        [Space()] [Header(Metallic Smoothness)] _MetallicGlossMap ("Metallic", 2D) = "white" { }
        _Metallic ("Metallic/Smoothness Map", Range(0, 1)) = 0
        _GlossMapScale ("Smoothness Scale", Range(0, 1)) = 1
        _MipBias ("Mip Bias", Range(0, 1)) = 0.8
        [Space()] [Header(Normals)] _BumpMap ("Normal Map", 2D) = "bump" { }
        _DetailBumpMap ("Detail Normal Map", 2D) = "bump" { }
        _DetailMask ("Detail Mask", 2D) = "white" { }
        _DetailBumpScale ("Detail Normal Scale", Range(0, 1)) = 1
        _DetailBumpTiling ("Detail Normal Tiling", Range(0.01, 10)) = 1
        [Space()] [Header(Occlusion)] _OcclusionMap ("Occlusion Map", 2D) = "white" { }
        _OcclusionStrength ("Strength", Range(0, 1)) = 1
        [Space()] [Header(Emission)] _EmissionMap ("Emission Map", 2D) = "white" { }
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        [Space()] [Toggle(USE_TIME_OF_DAY)] _UseTimeOfDay ("Use Time of Day", Float) = 0
        _TimeOfDayDotMin ("Min", Range(-1, 1)) = -0.005
        _TimeOfDayDotMax ("Max", Range(-1, 1)) = 0.005
        [Space()] [Header(Paint)] _PaintA ("Paint Color A", Color) = (1,1,1,0)
        _PaintB ("Paint Color B", Color) = (1,1,1,0)
        _PaintMaskGlossMap ("Paint Mask (RG Masks B Dirt A Smooth)", 2D) = "white" { }
        _PaintGlossMapScale ("Paint Smoothness Scale", Range(0, 1)) = 1
        [Toggle] _SmoothnessOverride ("Use PaintMask for Paint Smoothness (And not the Metallic Map)?", Float) = 0
        _RimFalloff ("_RimFalloff", Range(0.01, 5)) = 0.1
        _RimColor ("_RimColor", Color) = (0,0,0,0)
        [Header(Rendering)] [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Float) = 2
        _Offset ("Depth Offset", Range(-1, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0


        struct Input
        {
            float2 uv_MainTex;
            float2 uv_MetallicGlossMap;
            float2 uv_BumpMap;
            float2 uv_OcclusionMap;
            float2 uv_EmissionMap;
            float2 uv_PaintMaskGlossMap;
            float3 viewDir;
        };

        fixed4 _Color;
        sampler2D _MainTex;

        sampler2D _MetallicGlossMap;
        half _Metallic;
        half _GlossMapScale;
        half _MipBias;

        sampler2D _BumpMap;

        sampler2D _OcclusionMap;
        half _OcclusionStrength;

        sampler2D _EmissionMap;
        fixed4 _EmissionColor;

        sampler2D _PaintMaskGlossMap;
        fixed4 _PaintA;
        fixed4 _PaintB;
        float _PaintGlossMapScale;
        bool _SmoothnessOverride;

        fixed4 _RimColor;
        float _RimFalloff;


        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            fixed4 PaintMaskColor = tex2D(_PaintMaskGlossMap, IN.uv_PaintMaskGlossMap);
            fixed4 MetallicValue = tex2D(_MetallicGlossMap, IN.uv_MetallicGlossMap);

            fixed4 paintAColor = lerp(c.rgba, _PaintA * PaintMaskColor.g, _PaintA.a);
            c.rgba = lerp(c.rgba, paintAColor, PaintMaskColor.g);
            fixed4 paintBColor = lerp(c.rgba, _PaintB * PaintMaskColor.r, _PaintB.a);
            c.rgba = lerp(c.rgba, paintBColor, PaintMaskColor.r);

            fixed4 paintColor = lerp(paintAColor, paintBColor, PaintMaskColor.r);
            
            _Metallic = MetallicValue.a;

            if(_SmoothnessOverride){
                _Metallic = lerp(_Metallic, PaintMaskColor.a, _PaintA.a * PaintMaskColor.g);
                _Metallic = lerp(_Metallic, PaintMaskColor.a, _PaintB.a * PaintMaskColor.r);
                }
            
            o.Albedo = c;
            o.Metallic = MetallicValue.rgb;
            o.Smoothness = _Metallic * _GlossMapScale;
            o.Normal = UnpackNormal (tex2D(_BumpMap, IN.uv_BumpMap));
            o.Occlusion = tex2D(_OcclusionMap, IN.uv_OcclusionMap);
            o.Occlusion = o.Occlusion * _OcclusionStrength;
            
          half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
          o.Emission = tex2D(_EmissionMap, IN.uv_EmissionMap) * _EmissionColor + (_RimColor.rgb * pow (rim, _RimFalloff));

        }
        ENDCG
    }
    FallBack "Diffuse"
}
