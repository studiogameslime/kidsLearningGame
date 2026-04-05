// Sand drawing surface shader.
// Renders a two-layer sand effect: light surface sand and dark wet groove underneath.
// A runtime mask (R channel) controls which layer is visible.
// Includes grain variation, groove depth darkening, and raised ridge edges.
// UI-compatible with full stencil block for Canvas masking.
Shader "UI/SandSurface"
{
    Properties
    {
        _BottomTex ("Bottom (Wet Sand)", 2D) = "black" {}
        _TopTex ("Top (Surface Sand)", 2D) = "white" {}
        _MaskTex ("Mask (R=sand)", 2D) = "white" {}
        _GrainTex ("Grain Noise", 2D) = "gray" {}

        _EdgeWidth ("Edge Width", Range(0.001, 0.15)) = 0.06
        _EdgeBrightness ("Edge Brightness", Range(0, 2)) = 1.2
        _GrainStrength ("Grain Strength", Range(0, 0.5)) = 0.12
        _GrooveDepth ("Groove Depth Darken", Range(0, 0.6)) = 0.3
        _TopTiling ("Top Tiling", Float) = 3.0
        _BottomTiling ("Bottom Tiling", Float) = 2.5

        // Required for UI masking
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _BottomTex;
            sampler2D _TopTex;
            sampler2D _MaskTex;
            sampler2D _GrainTex;

            float _EdgeWidth;
            float _EdgeBrightness;
            float _GrainStrength;
            float _GrooveDepth;
            float _TopTiling;
            float _BottomTiling;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample mask: 1 = sand surface, 0 = groove (drawn area)
                float mask = tex2D(_MaskTex, i.uv).r;

                // Tiled texture sampling
                float2 topUV = i.uv * _TopTiling;
                float2 bottomUV = i.uv * _BottomTiling;

                fixed4 topCol = tex2D(_TopTex, topUV);
                fixed4 bottomCol = tex2D(_BottomTex, bottomUV);

                // Grain variation on top sand — subtle noise to break uniformity
                float grain = tex2D(_GrainTex, i.uv * 5.0).r;
                float grainOffset = (grain - 0.5) * _GrainStrength;
                topCol.rgb += grainOffset;

                // Darken bottom for groove depth illusion
                bottomCol.rgb *= (1.0 - _GrooveDepth);

                // Edge detection via screen-space derivatives of the mask
                // This finds the boundary between sand and groove
                float dMdx = ddx(mask);
                float dMdy = ddy(mask);
                float edgeGrad = sqrt(dMdx * dMdx + dMdy * dMdy);

                // Normalize edge intensity based on edge width
                float edgeIntensity = saturate(edgeGrad / _EdgeWidth);

                // Warm sandy ridge color for the pushed-aside sand at edges
                fixed3 ridgeColor = topCol.rgb * _EdgeBrightness;
                // Add warm tint to ridges (slight orange/golden push)
                ridgeColor += fixed3(0.06, 0.03, -0.01) * edgeIntensity;

                // Lerp between bottom (groove) and top (sand) based on mask
                // Use smoothstep for slightly softer transition
                float blendMask = smoothstep(0.05, 0.45, mask);
                fixed3 baseColor = lerp(bottomCol.rgb, topCol.rgb, blendMask);

                // Apply edge ridges on top — only where there IS a gradient
                baseColor = lerp(baseColor, ridgeColor, edgeIntensity * 0.7);

                // Subtle vignette-like depth: slightly darken groove centers
                // (areas far from edges within the groove)
                float grooveFactor = 1.0 - blendMask;
                float centerDarken = grooveFactor * (1.0 - edgeIntensity) * 0.08;
                baseColor -= centerDarken;

                return fixed4(baseColor * i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}
