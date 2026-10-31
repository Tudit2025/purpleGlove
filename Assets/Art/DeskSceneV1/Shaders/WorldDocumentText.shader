Shader "PurpleGlove/World Document Text"
{
    Properties { _MainTex ("Font Atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return fixed4(i.color.rgb, i.color.a * tex2D(_MainTex, i.uv).a); }
            ENDCG
        }
    }
}
