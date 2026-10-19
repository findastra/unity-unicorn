// Unity Stable shaders (shaders-shop, Unity Unicorn), Claude, 2026-10-18. Own work for Astra; free to use in her worlds.
// Candy Paint: lacquered toy paint. Soft two-tone light and shade, a warm rim light, a tight gloss highlight, the sky's
// reflection on the clear coat, and glitter flecks that catch the light as you move. No textures needed, any mesh
// (the glitter is laid out in the object's own space). Built-in pipeline, forward, GPU instancing, shadows, fog.
Shader "Unity Stable/Candy Paint"
{
    Properties
    {
        _Color ("Paint", Color) = (1, 0.2, 0.56, 1)
        _MainTex ("Paint texture (optional)", 2D) = "white" {}
        _ShadeColor ("Shade", Color) = (0.62, 0.22, 0.55, 1)
        _ShadeStep ("Shade line (0..1)", Range(0, 1)) = 0.42
        _ShadeSoftness ("Shade softness", Range(0.001, 0.5)) = 0.22
        [HDR] _RimColor ("Rim light", Color) = (1.4, 0.95, 0.7, 1)
        _RimPower ("Rim width (higher = thinner)", Range(0.5, 8)) = 3
        [HDR] _GlossColor ("Gloss highlight", Color) = (1.6, 1.5, 1.45, 1)
        _GlossPower ("Gloss sharpness", Range(8, 512)) = 140
        _CoatStrength ("Clear-coat reflection", Range(0, 1)) = 0.35
        [HDR] _GlitterColor ("Glitter", Color) = (2.6, 2.1, 1.1, 1)
        _GlitterDensity ("Glitter flecks per metre", Range(10, 400)) = 120
        _GlitterAmount ("Glitter amount", Range(0, 1)) = 0.35
        _GlitterSharpness ("Glitter sharpness", Range(4, 400)) = 90
        _GlitterTwinkle ("Glitter twinkle speed", Range(0, 4)) = 1.2
        [HDR] _EmissionColor ("Glow", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            float4 _ShadeColor, _RimColor, _GlossColor, _GlitterColor, _EmissionColor;
            float _ShadeStep, _ShadeSoftness, _RimPower, _GlossPower, _CoatStrength, _GlitterDensity, _GlitterAmount, _GlitterSharpness, _GlitterTwinkle;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wn : TEXCOORD2; float3 opos : TEXCOORD3; float3 on : TEXCOORD4;
                SHADOW_COORDS(5) UNITY_FOG_COORDS(6) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; o.wn = UnityObjectToWorldNormal(v.normal);
                o.opos = v.vertex.xyz; o.on = v.normal;
                TRANSFER_SHADOW(o); UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float3 hash3(float3 p) { p = frac(p * float3(443.897, 441.423, 437.195)); p += dot(p, p.yxz + 19.19); return frac((p.xxy + p.yxx) * p.zyx); }

            // Glitter: one fleck per cell of a 3D grid in object space; each fleck has its own tilt and blinks when its
            // tilt mirrors the light into your eye. Cells are only lit near the surface, so it reads as flakes in paint.
            float glitter(float3 opos, float3 n, float3 V, float3 L)
            {
                float3 p = opos * _GlitterDensity; float3 cell = floor(p); float3 f = frac(p);
                float3 r = hash3(cell);
                float3 centre = 0.2 + 0.6 * r;
                float near = saturate(1.0 - length(f - centre) * 3.2);
                float3 tilt = normalize(n + (hash3(cell + 7.1) - 0.5) * 1.6);
                float3 H = normalize(L + V);
                float catchLight = pow(saturate(dot(tilt, H)), _GlitterSharpness);
                float blink = 0.55 + 0.45 * sin(_Time.y * _GlitterTwinkle * (2.0 + 4.0 * r.x) + r.y * 6.2831);
                float keep = step(1.0 - _GlitterAmount, r.z);
                return near * catchLight * blink * keep;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 paint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * tex2D(_MainTex, i.uv);
                float3 N = normalize(i.wn); float3 V = normalize(UnityWorldSpaceViewDir(i.wpos));
                float3 L = normalize(UnityWorldSpaceLightDir(i.wpos));
                UNITY_LIGHT_ATTENUATION(atten, i, i.wpos);
                float ndl = dot(N, L) * 0.5 + 0.5;                                                    // half-lambert: soft wrap
                float lit = smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, ndl * atten);
                float3 base = lerp(paint.rgb * _ShadeColor.rgb, paint.rgb, lit);
                float3 ambient = ShadeSH9(float4(N, 1));
                float3 col = base * (_LightColor0.rgb * (0.35 + 0.65 * lit) + ambient);
                float nv = saturate(dot(N, V));
                col += _RimColor.rgb * pow(1.0 - nv, _RimPower) * (0.4 + 0.6 * lit);                   // warm sunset rim
                float3 H = normalize(L + V);
                col += _GlossColor.rgb * _LightColor0.rgb * pow(saturate(dot(N, H)), _GlossPower) * atten;   // lacquer highlight
                float fres = 0.04 + 0.96 * pow(1.0 - nv, 5.0);
                float3 R = reflect(-V, N);
                float3 sky = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, 1.5), unity_SpecCube0_HDR);
                col = lerp(col, col + sky, _CoatStrength * fres);                                    // clear coat
                float3 on = normalize(i.on); float3 ov = normalize(mul((float3x3)unity_WorldToObject, V)); float3 ol = normalize(mul((float3x3)unity_WorldToObject, L));
                col += _GlitterColor.rgb * glitter(i.opos, on, ov, ol) * (0.35 + 0.65 * atten);
                col += _EmissionColor.rgb;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, 1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
