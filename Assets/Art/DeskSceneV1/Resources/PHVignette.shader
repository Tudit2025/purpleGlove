Shader "Hidden/PurpleGlove/Vignette"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 0.45
        _Smoothness ("Smoothness", Range(0.01, 1)) = 0.75
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;
            float _Smoothness;

            half4 frag(v2f_img i) : SV_Target
            {
                half4 color = tex2D(_MainTex, i.uv);
                // Viewport-relative ellipse: all four corners match at any aspect ratio.
                float2 offset = i.uv * 2.0 - 1.0;
                float radiusSquared = dot(offset, offset) * 0.5;
                float mask = smoothstep(1.0 - _Smoothness, 1.0, radiusSquared);
                color.rgb *= 1.0 - mask * _Intensity;
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
