Shader "PurpleGlove/Visitor Eyes"
{
    Properties
    {
        _MainTex ("Extended visitor portrait (RGBA)", 2D) = "black" {}
        _Color ("Diffuse tint", Color) = (0.8,0.8,0.8,1)
        _Cutoff ("Silhouette alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On
        ZTest LEqual
        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            // Merge each 2x2 cells of the original 192x288 portrait grid into one.
            // Forehead/cheek borders stay behind the aperture's actual door geometry.
            float2 grid = float2(96, 144);
            float2 uv = (floor(saturate(IN.uv_MainTex) * grid) + 0.5) / grid;
            fixed4 c = tex2D(_MainTex, uv);
            o.Albedo = c.rgb * _Color.rgb;
            o.Alpha = c.a;
            // No emission or specular: use the same ambient and candle lighting as
            // the corridor. Alpha clipping exposes the corridor beside the face.
        }
        ENDCG
    }
}
