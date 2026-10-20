// Unity Stable shaders (shaders-shop, Unity Unicorn), Claude, 2026-10-19. Own work for Astra; free to use in her worlds.
// Handmade PBR: for props modelled and baked by hand (Tools/Blender/make_phone_booth.py bakes albedo, metallic / clear
// coat / edges / smoothness, normal, occlusion and emission). Astra, 2026-10-19: "the colors all look flat, there is no
// texture. I think we are missing shaders." Unity's Standard lighting does the base; this adds what Standard lacks:
//   - a clear coat: a second, sharp reflection over the colour, like enamel or lacquer (where the G channel says so),
//     so the paint mirrors the sunset sky and keeps its colour underneath;
//   - fine surface detail from tiling scans, projected in world space (triplanar), so close up the paint has orange peel
//     and scratches and the steel its brushing, sharper than any atlas can hold;
//   - specular occlusion from the baked AO, so reflections don't light up corners;
//   - edge sheen: worn edges (B channel) catch a little extra light, the way handled metal does;
//   - per-material saturation and contrast.
// Surface shader: shadows, lightmaps, light probes, fog and GPU instancing come from Unity.
Shader "Unity Stable/Handmade PBR"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (baked)", 2D) = "white" {}
        _MetallicGlossMap ("Metallic (R), clear coat (G), edges (B), smoothness (A)", 2D) = "black" {}
        _SmoothnessScale ("Smoothness", Range(0, 1.5)) = 1
        [Normal] _BumpMap ("Normal map (baked)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        _OcclusionMap ("Occlusion (baked)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion", Range(0, 1)) = 1
        _SpecularOcclusion ("Specular occlusion", Range(0, 1)) = 0.8
        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission colour", Color) = (0, 0, 0, 1)
        [Header(Fine detail from scans (world space))]
        [Normal] _DetailNormalMap ("Detail normal", 2D) = "bump" {}
        _DetailNormalScale ("Detail normal strength", Range(0, 2)) = 0.4
        _DetailRoughMap ("Detail roughness (R)", 2D) = "gray" {}
        _DetailRoughAmount ("Detail roughness amount", Range(0, 1)) = 0.15
        _DetailTiling ("Detail tiles per metre", Float) = 1.5
        [Header(Clear coat)]
        _CoatStrength ("Clear coat", Range(0, 1)) = 0
        _CoatSmoothness ("Clear coat smoothness", Range(0, 1)) = 0.92
        [Header(Edges and colour)]
        _EdgeSheen ("Edge sheen", Range(0, 1)) = 0.3
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Contrast ("Contrast", Range(0.5, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Handmade fullforwardshadows
        #pragma target 3.5
        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex, _MetallicGlossMap, _BumpMap, _OcclusionMap, _EmissionMap, _DetailNormalMap, _DetailRoughMap;
        half4 _Color, _EmissionColor;
        half _SmoothnessScale, _BumpScale, _OcclusionStrength, _SpecularOcclusion;
        half _DetailNormalScale, _DetailRoughAmount, _DetailTiling;
        half _CoatStrength, _CoatSmoothness, _EdgeSheen, _Saturation, _Contrast;

        struct Input { float2 uv_MainTex; float3 worldPos; float3 worldNormal; INTERNAL_DATA };

        struct SurfaceOutputHandmade
        {
            fixed3 Albedo; float3 Normal; half3 Emission; half Metallic; half Smoothness; half Occlusion; fixed Alpha;
            half Coat; half Edge;
        };

        inline SurfaceOutputStandard ToStandard(SurfaceOutputHandmade s)
        {
            SurfaceOutputStandard o;
            o.Albedo = s.Albedo; o.Normal = s.Normal; o.Emission = s.Emission; o.Metallic = s.Metallic;
            o.Smoothness = s.Smoothness; o.Occlusion = s.Occlusion; o.Alpha = s.Alpha;
            return o;
        }

        inline half4 LightingHandmade(SurfaceOutputHandmade s, float3 viewDir, UnityGI gi)
        {
            half4 c = LightingStandard(ToStandard(s), viewDir, gi);
            float3 N = s.Normal;
            float nv = saturate(dot(N, viewDir));
            float nl = saturate(dot(N, gi.light.dir));

            // clear coat: a dielectric layer (F0 0.04) with its own sharp highlight and reflection
            half coat = s.Coat * _CoatStrength;
            if (coat > 0.001)
            {
                float3 H = normalize(gi.light.dir + viewDir);
                float nh = saturate(dot(N, H)), lh = saturate(dot(gi.light.dir, H));
                float perceptual = max(1 - _CoatSmoothness, 0.02); float rough = perceptual * perceptual;
                half F = 0.04 + 0.96 * Pow5(1 - nv);
                half Fh = 0.04 + 0.96 * Pow5(1 - lh);
                half3 coatLight = GGXTerm(nh, rough) * SmithJointGGXVisibilityTerm(nl, nv, rough) * UNITY_PI * Fh * nl * gi.light.color;
                half3 coatEnv = 0;
                #ifdef UNITY_PASS_FORWARDBASE
                    Unity_GlossyEnvironmentData g; g.roughness = perceptual; g.reflUVW = reflect(-viewDir, N);
                    coatEnv = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, g) * lerp(1, s.Occlusion, _SpecularOcclusion) * F;
                #endif
                c.rgb = c.rgb * (1 - F * coat) + (max(coatLight, 0) + coatEnv) * coat;
            }

            // edge sheen: worn edges catch a little more of the light and the sky
            c.rgb += s.Edge * _EdgeSheen * s.Albedo * (gi.light.color * (nl * 0.5 + 0.5) * 0.3 + gi.indirect.diffuse * 0.4);
            return c;
        }

        inline void LightingHandmade_GI(SurfaceOutputHandmade s, UnityGIInput data, inout UnityGI gi)
        {
            LightingStandard_GI(ToStandard(s), data, gi);
        }

        // world-space triplanar detail normal blended onto a world normal (whiteout blend)
        float3 DetailNormal(float3 p, float3 nW)
        {
            half3 w = pow(abs(nW), 4); w /= (w.x + w.y + w.z);
            half3 tx = UnpackScaleNormal(tex2D(_DetailNormalMap, p.zy), _DetailNormalScale);
            half3 ty = UnpackScaleNormal(tex2D(_DetailNormalMap, p.xz), _DetailNormalScale);
            half3 tz = UnpackScaleNormal(tex2D(_DetailNormalMap, p.xy), _DetailNormalScale);
            tx = half3(tx.xy + nW.zy, abs(tx.z) * nW.x);
            ty = half3(ty.xy + nW.xz, abs(ty.z) * nW.y);
            tz = half3(tz.xy + nW.xy, abs(tz.z) * nW.z);
            return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
        }

        half DetailRough(float3 p, float3 nW)
        {
            half3 w = pow(abs(nW), 4); w /= (w.x + w.y + w.z);
            return tex2D(_DetailRoughMap, p.zy).r * w.x + tex2D(_DetailRoughMap, p.xz).r * w.y + tex2D(_DetailRoughMap, p.xy).r * w.z;
        }

        void surf(Input IN, inout SurfaceOutputHandmade o)
        {
            float2 uv = IN.uv_MainTex;
            half4 ms = tex2D(_MetallicGlossMap, uv);

            // baked normal -> world, add the scan detail in world space, back to tangent space
            half3 nT = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            float3 tW = WorldNormalVector(IN, float3(1, 0, 0)), bW = WorldNormalVector(IN, float3(0, 1, 0)), gW = WorldNormalVector(IN, float3(0, 0, 1));
            float3 nW = normalize(tW * nT.x + bW * nT.y + gW * nT.z);
            float3 p = IN.worldPos * _DetailTiling;
            float3 dW = DetailNormal(p, nW);
            o.Normal = normalize(float3(dot(dW, tW), dot(dW, bW), dot(dW, gW)));

            half3 c = tex2D(_MainTex, uv).rgb * _Color.rgb;
            half l = dot(c, half3(0.2126, 0.7152, 0.0722));
            c = lerp(l.xxx, c, _Saturation);
            c = 0.18 * pow(max(c, 1e-4) / 0.18, _Contrast);   // contrast around mid grey (linear)
            o.Albedo = saturate(c);

            o.Metallic = ms.r;
            o.Smoothness = saturate(ms.a * _SmoothnessScale - (DetailRough(p, nW) - 0.5) * _DetailRoughAmount);
            o.Coat = ms.g; o.Edge = ms.b;
            o.Occlusion = lerp(1, tex2D(_OcclusionMap, uv).g, _OcclusionStrength);
            o.Emission = tex2D(_EmissionMap, uv).rgb * _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
