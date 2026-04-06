// Sand surface shader with rainbow drawing.
// Top layer = sand texture. Drawing reveals rainbow colors that shift
// continuously as the finger moves, creating an ever-changing hue trail.
// Edge detection creates raised sand ridges at groove borders.
Shader "UI/SandSurface"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _TopTex ("Top Sand Surface", 2D) = "white" {}
        _MaskTex ("Draw Mask (R=hue, G=drawn)", 2D) = "black" {}
        _GrainTex ("Sand Grain Noise", 2D) = "gray" {}
        _EdgeWidth ("Edge Width", Range(0.01, 0.2)) = 0.04
        _EdgeBrightness ("Edge Brightness", Range(0, 3)) = 1.8
        _GrainStrength ("Grain Strength", Range(0, 0.3)) = 0.08
        _TopTiling ("Top Tex Tiling", Float) = 4
        _Saturation ("Rainbow Saturation", Range(0, 1)) = 0.7
        _Brightness ("Rainbow Brightness", Range(0, 1)) = 0.95

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

            sampler2D _TopTex;
            sampler2D _MaskTex;
            sampler2D _GrainTex;
            float _EdgeWidth;
            float _EdgeBrightness;
            float _GrainStrength;
            float _TopTiling;
            float _Saturation;
            float _Brightness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // HSV to RGB conversion
            fixed3 hsv2rgb(float h, float s, float v)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(float3(h, h, h) + K.xyz) * 6.0 - K.www);
                return v * lerp(K.xxx, saturate(p - K.xxx), s);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample mask: R = hue value (0-1), G = drawn amount (0-1)
                fixed4 maskSample = tex2D(_MaskTex, i.uv);
                float hue = maskSample.r;
                float drawn = maskSample.g; // 0 = untouched sand, 1 = fully drawn

                // Sand surface texture with grain
                float2 topUV = i.uv * _TopTiling;
                fixed4 topCol = tex2D(_TopTex, topUV);
                float grain = tex2D(_GrainTex, i.uv * 5.0).r;
                topCol.rgb += (grain - 0.5) * _GrainStrength;

                // Rainbow color from hue
                fixed3 rainbowCol = hsv2rgb(hue, _Saturation, _Brightness);

                // Edge detection on the drawn mask
                float dMdx = ddx(drawn);
                float dMdy = ddy(drawn);
                float edgeGrad = sqrt(dMdx * dMdx + dMdy * dMdy);
                float edgeIntensity = saturate(edgeGrad / _EdgeWidth);

                // Sand ridge color at edges (bright sand pushed aside)
                fixed3 ridgeColor = topCol.rgb * _EdgeBrightness;
                ridgeColor += fixed3(0.06, 0.03, -0.01) * edgeIntensity;

                // Blend: sand where not drawn, rainbow where drawn
                float blendMask = smoothstep(0.05, 0.4, drawn);
                fixed3 baseColor = lerp(topCol.rgb, rainbowCol, blendMask);

                // Apply edge ridges
                baseColor = lerp(baseColor, ridgeColor, edgeIntensity * 0.6);

                return fixed4(baseColor * i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}
