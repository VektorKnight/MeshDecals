Shader "Custom/MeshDecal_Standard"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Offset ("Surface Offset", Range(0, 1)) = 0.001
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows vertex:vert alpha
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input {
            float2 uv_MainTex;
        };

        half4 _Color;
        half _Glossiness;
        half _Metallic;
        half _Offset;

        void vert (inout appdata_full v) {
            // Offset from the original surface by a small amount to avoid Z-fighting.
            v.vertex.xyz += v.normal * _Offset;
        }

        void surf (Input i, inout SurfaceOutputStandard o) {
            float4 c = tex2D(_MainTex, i.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            
            o.Metallic = _Metallic * c.a;
            o.Smoothness = _Glossiness * c.a;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
