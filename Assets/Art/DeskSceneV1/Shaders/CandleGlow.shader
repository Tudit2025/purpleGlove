Shader "PurpleGlove/Candle Glow"
{
    Properties { _Color ("Color", Color) = (1,0.3,0.05,0.35) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 center = mul(UNITY_MATRIX_MV, float4(0,0,0,1)).xyz;
                float2 scale = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                o.pos = mul(UNITY_MATRIX_P, float4(center + float3(v.vertex.xy * scale, 0), 1));
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float r = length(i.uv * 2 - 1);
                float glow = pow(saturate(1 - r), 3);
                return fixed4(_Color.rgb, _Color.a * glow);
            }
            ENDCG
        }
    }
}
