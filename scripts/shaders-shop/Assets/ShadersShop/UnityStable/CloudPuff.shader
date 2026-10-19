// Unity Stable shaders (shaders-shop, Unity Unicorn), Claude, 2026-10-18. Own work for Astra; free to use in her worlds.
// Cloud Puff: soft cumulus on any rounded mesh. Fluffy wrap lighting from the sun (sunlit tops, pink sides,
// rose-lavender shade), a silver lining when backlit, a fluffy break-up from the shared cloud noise, and a soft blend
// into the cloud sea at its base and into the haze far away. Same colours as Cloud Sea, so they read as one sky.
// Unlit: needs no scene lights.
Shader "Unity Stable/Cloud Puff"
{
    Properties
    {
        _Noise ("Cloud shapes (shared with Cloud Sea)", 2D) = "gray" {}
        _FluffScale ("Fluff per metre", Float) = 0.35
        _Fluff ("Fluff strength", Range(0, 1.5)) = 0.55
        _TopColor ("Sunlit tops", Color) = (1.00, 0.87, 0.76, 1)
        _SideColor ("Sides", Color) = (1.00, 0.63, 0.77, 1)
        _ShadowColor ("Shade", Color) = (0.60, 0.43, 0.69, 1)
        _HazeColor ("Far haze", Color) = (1.00, 0.78, 0.66, 1)
        _HazeStart ("Haze starts (m)", Float) = 35
        _HazeEnd ("Haze ends (m)", Float) = 1100
        _SunDir ("Direction to the sun (world)", Vector) = (-0.55, 0.24, -0.80, 0)
        [HDR] _RimColor ("Silver lining", Color) = (1.8, 1.2, 0.85, 1)
        _SeaHeight ("Cloud sea height (world y)", Float) = -0.05
        _SeaBlend ("Blend into the sea (m)", Float) = 1.2
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _Noise;
            float4 _TopColor, _SideColor, _ShadowColor, _HazeColor, _SunDir, _RimColor;
            float _FluffScale, _Fluff, _HazeStart, _HazeEnd, _SeaHeight, _SeaBlend;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 wn : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; o.wn = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.wn);
                // fluffy break-up: triplanar noise nudges the normal, like billows on billows
                float3 p = i.wpos * _FluffScale; float3 bw = pow(abs(N), 4); bw /= (bw.x + bw.y + bw.z);
                float nx = tex2D(_Noise, p.yz).r, ny = tex2D(_Noise, p.xz).r, nz = tex2D(_Noise, p.xy).r;
                float3 g = float3(tex2D(_Noise, p.yz + 0.37).g, tex2D(_Noise, p.xz + 0.37).g, tex2D(_Noise, p.xy + 0.37).g) - 0.5;
                float fluffN = nx * bw.x + ny * bw.y + nz * bw.z;
                N = normalize(N + g * _Fluff);
                float3 L = normalize(_SunDir.xyz); float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float wrap = (dot(N, L) * 0.5 + 0.5) * lerp(0.75, 1.0, fluffN);
                float3 col = lerp(_ShadowColor.rgb, _SideColor.rgb, smoothstep(0.2, 0.6, wrap));
                col = lerp(col, _TopColor.rgb, smoothstep(0.56, 0.93, wrap));
                float backlit = pow(saturate(dot(-V, L)), 2);
                col += _RimColor.rgb * pow(1 - saturate(dot(N, V)), 3) * (0.25 + backlit) * 0.6;   // silver lining
                float base = saturate((i.wpos.y - _SeaHeight) / _SeaBlend);
                col = lerp(lerp(_ShadowColor.rgb, _SideColor.rgb, 0.55), col, base);              // melt into the sea
                float haze = smoothstep(_HazeStart, _HazeEnd, distance(_WorldSpaceCameraPos, i.wpos));
                col = lerp(col, _HazeColor.rgb, haze);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
