Shader "PurpleGlove/UI/Pixel Text"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelSize ("Pixel Size at 1080p", Range(1,4)) = 2
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 glyphBounds : TEXCOORD1;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                float4 glyphBounds : TEXCOORD2;
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect;
            float _PixelSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.localPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.glyphBounds = v.glyphBounds;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample every display-pixel block at its center. Derivatives map the
                // screen grid into each glyph's atlas UVs, including rotated glyphs.
                float size = max(1.0, floor(_PixelSize * _ScreenParams.y / 1080.0 + 0.5));
                float2 offset = (floor(i.vertex.xy / size) + 0.5) * size - i.vertex.xy;
                float2 uv = i.uv + ddx(i.uv) * offset.x + ddy(i.uv) * offset.y;
                float2 inside = step(i.glyphBounds.xy, uv) * step(uv, i.glyphBounds.zw);
                uv = clamp(uv, i.glyphBounds.xy + _MainTex_TexelSize.xy * 0.5,
                    i.glyphBounds.zw - _MainTex_TexelSize.xy * 0.5);
                fixed4 ink = tex2D(_MainTex, uv) + _TextureSampleAdd;
                ink.a *= inside.x * inside.y;
                // A few coverage levels keep thin Chinese strokes while making edges crisp.
                ink.a = floor(saturate(ink.a) * 4.0 + 0.5) / 4.0;
                fixed4 color = ink * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
