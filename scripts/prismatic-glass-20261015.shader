Shader "Astra/Prismatic Glass"
{
    Properties
    {
        _Tint ("Glass tint", Color) = (0.7,0.9,1,1)
        _Opacity ("Body opacity", Range(0.02,0.95)) = 0.22
        _Rainbow ("Prismatic flashes", Range(0,2)) = 0.9
        _Reflection ("Reflection strength", Range(0,2)) = 1
        _Contrast ("Facet contrast", Range(0.2,6)) = 2.4
        _SpectrumScale ("Spectrum frequency", Range(0.5,8)) = 3
        _Rim ("Edge highlight", Range(0,2)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Visible faces", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull [_Cull]
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _Tint;
            float _Opacity, _Rainbow, _Reflection, _Contrast, _SpectrumScale, _Rim;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal);
                float3 v=normalize(_WorldSpaceCameraPos-i.world);
                float3 r=reflect(-v,n);
                float edge=pow(1-saturate(abs(dot(n,v))),3);
                float3 environment=DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,r,0),unity_SpecCube0_HDR);
                float phase=dot(r,float3(0.53,0.71,0.37))*_SpectrumScale;
                float3 spectrum=0.5+0.5*cos(6.2831853*(phase+float3(0,0.333333,0.666667)));
                float flash=pow(saturate(dot(r,normalize(float3(0.5,0.7,0.4)))*0.5+0.5),_Contrast*3);
                float3 color=_Tint.rgb*0.055+environment*_Reflection;
                color+=spectrum*flash*_Rainbow*1.4+_Tint.rgb*edge*_Rim;
                float alpha=saturate(_Opacity+edge*0.5+flash*_Rainbow*0.12);
                return float4(color,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
