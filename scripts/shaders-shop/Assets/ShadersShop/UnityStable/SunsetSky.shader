// Unity Stable shaders (shaders-shop, Unity Unicorn), Claude, 2026-10-18. Own work for Astra; free to use in her worlds.
// Sunset Sky: a dusk gradient (lavender zenith, pink, apricot, a butter-yellow horizon), a low sun with a soft halo and
// thin drifting wisps lit from the sun's side. Works as a skybox material or on a sky dome mesh (a sphere around the
// stop): the dome is drawn behind everything, at the far plane, so its size never clips.
Shader "Unity Stable/Sunset Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.40, 0.31, 0.62, 1)
        _Upper ("Upper sky", Color) = (0.93, 0.44, 0.70, 1)
        _Lower ("Low sky", Color) = (1.00, 0.60, 0.56, 1)
        _Horizon ("Horizon", Color) = (1.00, 0.83, 0.56, 1)
        _Below ("Below the horizon", Color) = (0.96, 0.62, 0.66, 1)
        _SunDir ("Direction to the sun (world)", Vector) = (-0.55, 0.24, -0.80, 0)
        [HDR] _SunColor ("Sun", Color) = (3.2, 2.5, 1.6, 1)
        _SunSize ("Sun size", Range(0.0005, 0.03)) = 0.006
        _HaloStrength ("Halo", Range(0, 2)) = 0.7
        [HDR] _WispColor ("Wisps, sunlit", Color) = (1.35, 0.95, 0.95, 1)
        _WispShade ("Wisps, shaded", Color) = (0.74, 0.46, 0.68, 1)
        _WispCover ("Wisp cover", Range(0, 1)) = 0.42
        _WispScale ("Wisp scale", Range(0.2, 4)) = 1.1
        _WispSpeed ("Wisp drift", Range(0, 0.05)) = 0.006
        _Exposure ("Exposure", Range(0.2, 3)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Zenith, _Upper, _Lower, _Horizon, _Below, _SunDir, _SunColor, _WispColor, _WispShade;
            float _SunSize, _HaloStrength, _WispCover, _WispScale, _WispSpeed, _Exposure;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z = o.pos.w * 1e-6;          // at the far plane: behind everything, never clipped
                #else
                    o.pos.z = o.pos.w * (1 - 1e-6);
                #endif
                o.dir = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos;   // works for a dome and for the skybox
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }
            float fbm(float2 p) { float s = 0, a = 0.5; for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; } return s; }

            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir); float h = d.y;
                float3 s = normalize(_SunDir.xyz); float toSun = dot(d, s);
                float3 c;
                if (h >= 0)
                {
                    c = lerp(_Horizon.rgb, _Lower.rgb, smoothstep(0.0, 0.10, h));
                    c = lerp(c, _Upper.rgb, smoothstep(0.07, 0.36, h));
                    c = lerp(c, _Zenith.rgb, smoothstep(0.32, 1.0, h));
                }
                else c = lerp(_Horizon.rgb, _Below.rgb, smoothstep(0.0, -0.22, h));
                // the horizon burns warmer toward the sun
                c = lerp(c, _Horizon.rgb * 1.08, pow(saturate(toSun * 0.5 + 0.5), 5) * (1 - smoothstep(0.0, 0.45, abs(h))) * 0.6);
                // wisps: thin stretched streaks on a sky layer, sunlit on the sun's side, shaded rose away from it
                if (h > 0.015)
                {
                    float2 uv = d.xz / (h + 0.12) * _WispScale * 0.6;
                    uv = float2(uv.x * 0.55, uv.y * 1.6) + _Time.y * _WispSpeed * float2(1, 0.3);
                    float n = fbm(uv * 2.2) * 0.75 + fbm(uv * 7.5) * 0.25;
                    float w = smoothstep(1 - _WispCover, 1 - _WispCover + 0.28, n) * smoothstep(0.015, 0.09, h) * (1 - smoothstep(0.42, 0.8, h));
                    float lit = pow(saturate(toSun * 0.5 + 0.5), 2.2);
                    float3 wc = lerp(_WispShade.rgb, _WispColor.rgb, lit) + _SunColor.rgb * pow(saturate(toSun), 12) * 0.25;   // silver lining
                    c = lerp(c, wc, w * 0.8);
                }
                // sun: halo, glow and a soft disc
                float sa = saturate(toSun);
                c += _SunColor.rgb * (pow(sa, 24) * 0.12 + pow(sa, 300) * 0.35) * _HaloStrength;
                c += _SunColor.rgb * smoothstep(1 - _SunSize, 1 - _SunSize * 0.5, toSun);
                return float4(c * _Exposure, 1);
            }
            ENDCG
        }
    }
}
