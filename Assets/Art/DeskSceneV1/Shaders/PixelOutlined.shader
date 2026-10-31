Shader "PurpleGlove/Pixel Outlined"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _PixelSize ("Texture pixel size multiplier", Range(1,16)) = 4
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _OutlineColor ("Ink outline", Color) = (0.025,0.017,0.028,1)
        _OutlineWidth ("Outline pixels at 540p", Range(0,4)) = 1.2
        [HideInInspector] _InspectionFill ("Inspection fill", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        fixed4 _Color;
        fixed4 _InspectionFill;
        half _Metallic, _Glossiness;
        float _PixelSize;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Group source texels into cells without scaling the wood/stone motifs.
            // Architecture uses 2x2; other surfaces retain their authored pixel size.
            float2 grid = max(float2(1,1), _MainTex_TexelSize.zw / max(1, _PixelSize));
            float2 uv = (floor(IN.uv_MainTex * grid) + 0.5) / grid;
            fixed4 c = tex2D(_MainTex, uv) * _Color;
            o.Albedo = c.rgb;
            o.Emission = c.rgb * _InspectionFill.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG

        Pass
        {
            Name "INK_OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _OutlineWidth;
            fixed4 _OutlineColor;
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 outlineNormal : TEXCOORD2;
            };
            struct v2f { float4 position : SV_POSITION; UNITY_FOG_COORDS(0) };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                // UV3 holds smoothed extrusion normals; lighting keeps the original faceted normals.
                float3 normal = dot(v.outlineNormal, v.outlineNormal) > 0.1 ? v.outlineNormal : v.normal;
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(normal));
                float2 direction = mul((float3x3)UNITY_MATRIX_P, viewNormal).xy;
                direction *= _ScreenParams.xy;
                direction /= max(length(direction), 0.00001);
                float pixels = _OutlineWidth * _ScreenParams.y / 540.0;
                o.position.xy += direction * (2.0 * pixels / _ScreenParams.xy) * o.position.w;
                UNITY_TRANSFER_FOG(o, o.position);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                clip(_OutlineWidth - 0.001);
                fixed4 color = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
    FallBack "Standard"
}
