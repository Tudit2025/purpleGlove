Shader "PurpleGlove/Night Sky"
{
    Properties
    {
        _ZenithColor ("Upper sky", Color) = (0.018, 0.029, 0.065, 1)
        _HorizonColor ("Horizon haze", Color) = (0.075, 0.105, 0.16, 1)
        _CloudColor ("Moonlit clouds", Color) = (0.105, 0.13, 0.19, 1)
        _CloudCoverage ("Cloud coverage", Range(0, 1)) = 0.58
        _CloudSpeed ("Cloud drift", Range(0, 0.02)) = 0.002
        _StarBrightness ("Star brightness", Range(0, 1)) = 0.28
        _MoonColor ("Moonlight", Color) = (0.52, 0.60, 0.72, 1)
        _MoonDirection ("Moon direction", Vector) = (-0.696, 0.174, 0.696, 0)
        _MoonRadius ("Moon angular radius (degrees)", Range(0.2, 4)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "IgnoreProjector"="True" "PreviewType"="Skybox" }
        Cull Front
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ZenithColor, _HorizonColor, _CloudColor, _MoonColor, _MoonDirection;
            float _CloudCoverage, _CloudSpeed, _StarBrightness, _MoonRadius;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 worldPosition : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash(cell), hash(cell + float3(1,0,0)), f.x),
                                 lerp(hash(cell + float3(0,1,0)), hash(cell + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(cell + float3(0,0,1)), hash(cell + float3(1,0,1)), f.x),
                                 lerp(hash(cell + float3(0,1,1)), hash(cell + float3(1,1,1)), f.x), f.y), f.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // World directions keep the sky distant; 3D noise has no longitude seam.
                float3 d = normalize(i.worldPosition - _WorldSpaceCameraPos);
                d = normalize(floor(d * 650 + 0.5) / 650);
                float height = smoothstep(-0.1, 0.65, d.y);
                float3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, height);

                float3 moonDirection = normalize(_MoonDirection.xyz);
                float3 moonRight = normalize(cross(float3(0,1,0), moonDirection));
                float3 moonUp = cross(moonDirection, moonRight);
                float2 moonUV = float2(dot(d, moonRight), dot(d, moonUp)) / radians(_MoonRadius);
                float moonFacing = step(0, dot(d, moonDirection));
                float disc = (1 - smoothstep(0.95, 1.05, length(moonUV))) * moonFacing;
                float crescent = disc * smoothstep(0.96, 1.04, length(moonUV - float2(0.46, 0.12)));
                float halo = exp(-dot(moonUV, moonUV) * 0.045) * moonFacing;
                color += _MoonColor.rgb * (crescent * 0.85 + disc * 0.025 + halo * 0.035);

                float3 cloudPosition = d * float3(4.5, 13, 4.5) + float3(_Time.y * _CloudSpeed, 0, 0);
                float cloudNoise = noise(cloudPosition) * 0.57 + noise(cloudPosition * 2.03 + 7.1) * 0.28
                    + noise(cloudPosition * 4.11 + 19.3) * 0.15;
                float cloud = smoothstep(0.75 - _CloudCoverage * 0.5, 0.94 - _CloudCoverage * 0.5, cloudNoise);
                cloud = floor(cloud * 12 + 0.5) / 12;
                cloud *= smoothstep(-0.15, 0.08, d.y);

                float3 starGrid = d * 180;
                float starSeed = hash(floor(starGrid));
                float star = step(0.982, starSeed) * (1 - smoothstep(0.10, 0.32, length(frac(starGrid) - 0.5)));
                color += star * _StarBrightness * float3(0.74, 0.83, 1) * smoothstep(-0.02, 0.12, d.y);
                color = lerp(color, _CloudColor.rgb * (0.62 + halo * 0.38), cloud * 0.85);
                // The lower hemisphere stays dark instead of exposing a blue void below the room.
                color *= lerp(0.2, 1, smoothstep(-0.25, 0.02, d.y));
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
