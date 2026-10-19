// Unity Stable shaders (shaders-shop, Unity Unicorn), Claude, 2026-10-18. Own work for Astra; free to use in her worlds.
// Cloud Sea: a sea of rolling cloud tops on one big flat surface. Each pixel steps down into a billow heightfield
// (parallax relief) to find the cloud surface, lights it from the sun (sunlit tops, pink sides, rose-lavender valleys,
// soft self-shadow), adds the sun's glow through thin edges when you look toward it, and melts into the horizon haze.
// The mesh sits at the height of the highest tops; the billows go down from there. Unlit: needs no scene lights.
Shader "Unity Stable/Cloud Sea"
{
    Properties
    {
        _Noise ("Cloud shapes (R billows, G detail, B large swell)", 2D) = "gray" {}
        _Scale ("Billow tiles per metre", Float) = 0.04
        _Depth ("Billow depth (m)", Range(0.2, 10)) = 2.4
        _Steps ("Relief steps", Range(4, 48)) = 22
        _TopColor ("Sunlit tops", Color) = (1.00, 0.87, 0.76, 1)
        _SideColor ("Sides", Color) = (1.00, 0.63, 0.77, 1)
        _ShadowColor ("Shade", Color) = (0.60, 0.43, 0.69, 1)
        _HazeColor ("Far haze", Color) = (1.00, 0.78, 0.66, 1)
        _HazeStart ("Haze starts (m)", Float) = 35
        _HazeEnd ("Haze ends (m)", Float) = 1100
        _SunDir ("Direction to the sun (world)", Vector) = (-0.55, 0.24, -0.80, 0)
        [HDR] _GlowColor ("Sun glow through thin edges", Color) = (1.7, 1.05, 0.75, 1)
        _ShadowSoftness ("Self-shadow softness", Range(0.05, 1)) = 0.35
        _Drift ("Drift speed", Range(0, 0.2)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _Noise;
            float4 _TopColor, _SideColor, _ShadowColor, _HazeColor, _SunDir, _GlowColor;
            float _Scale, _Depth, _Steps, _HazeStart, _HazeEnd, _ShadowSoftness, _Drift;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // billow height at a world xz: 1 = at the surface (highest tops), 0 = the deepest valley
            float height(float2 xz, float lod)
            {
                float2 uv = xz * _Scale; float t = _Time.y * _Drift;
                float4 a = tex2Dlod(_Noise, float4(uv + float2(t, t * 0.35), 0, lod));
                float4 b = tex2Dlod(_Noise, float4(uv * 2.7 + float2(-t * 0.6, t * 0.2), 0, lod));
                float4 c = tex2Dlod(_Noise, float4(uv * 0.11 + float2(t * 0.1, 0), 0, lod));
                float hgt = a.r * 0.85 + b.g * 0.15;                      // big rounded billows, a little fine detail
                return saturate(hgt * lerp(0.62, 1.0, c.b));
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 cam = _WorldSpaceCameraPos; float3 r = normalize(i.wpos - cam);
                float dist0 = distance(cam, i.wpos);
                float lod = saturate(dist0 / 300.0) * 5.0;
                float down = max(-r.y, 0.035);                                   // keep grazing angles stable
                float2 stepXZ = r.xz / down * _Depth;                              // xz travel per unit of depth fraction
                int n = (int)lerp(_Steps, _Steps * 0.4, saturate(down));
                float layer = 1.0 / n;
                float t = 0, prevT = 0, prevGap = 0;
                float2 xz = i.wpos.xz;
                float hS = height(xz, lod);
                float gap = (1 - hS) - t;                                          // > 0 while the ray is above the cloud
                [loop] for (int k = 0; k < 48 && k < n && gap > 0; k++)
                {
                    prevT = t; prevGap = gap;
                    t += layer; xz = i.wpos.xz + stepXZ * t;
                    hS = height(xz, lod); gap = (1 - hS) - t;
                }
                if (gap <= 0 && prevGap > 0) { float w = prevGap / (prevGap - gap); t = lerp(prevT, t, w); xz = i.wpos.xz + stepXZ * t; hS = height(xz, lod); }
                float3 hitPos = float3(xz.x, i.wpos.y - t * _Depth, xz.y);

                // normal from the heightfield (metres)
                float e = 1.2;                                                     // metres: smooth, puffy normals
                float hx = height(xz + float2(e, 0), lod) - height(xz - float2(e, 0), lod);
                float hz = height(xz + float2(0, e), lod) - height(xz - float2(0, e), lod);
                float3 N = normalize(float3(-hx * _Depth / (2 * e), 1, -hz * _Depth / (2 * e)));

                // soft self-shadow: a few steps toward the sun through the heightfield
                float3 L = normalize(_SunDir.xyz);
                float sh = 1;
                float2 sunXZ = L.xz / max(L.y, 0.08) * _Depth * 0.18;
                [unroll] for (int s = 1; s <= 5; s++)
                {
                    float ts = t - s * 0.18; if (ts < 0) break;                   // ray height (depth fraction) as it rises toward the sun
                    float occ = (1 - height(xz + sunXZ * s, lod)) - ts;           // < 0: cloud above the ray
                    sh = min(sh, saturate(1 + occ / _ShadowSoftness));
                }
                float wrap = (dot(N, L) * 0.5 + 0.5) * lerp(0.45, 1.0, sh);
                float3 col = lerp(_ShadowColor.rgb, _SideColor.rgb, smoothstep(0.18, 0.58, wrap));
                col = lerp(col, _TopColor.rgb, smoothstep(0.55, 0.92, wrap) * smoothstep(0.35, 0.95, hS));
                col *= lerp(0.8, 1.0, hS);                                          // deeper valleys are a little darker
                float towardSun = saturate(dot(r, L));
                float thin = pow(1 - saturate(dot(N, -r)), 2.5);
                col += _GlowColor.rgb * (pow(towardSun, 6) * (1.15 - hS) * 0.3 + thin * pow(towardSun, 2) * 0.1);

                float dist = distance(cam, hitPos);
                float haze = smoothstep(_HazeStart, _HazeEnd, dist);
                float3 hazeCol = lerp(_HazeColor.rgb, _HazeColor.rgb * 1.1 + _GlowColor.rgb * 0.12, pow(towardSun, 3));
                col = lerp(col, hazeCol, haze);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
