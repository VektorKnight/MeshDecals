Shader "Custom/MeshDecal_Standard"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
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
            float2 projectedUV;
        };

        half _Glossiness;
        half _Metallic;
        float4 _Color;

        void vert (inout appdata_full v, out Input o) {
            // Offset from the original surface by a small amount to avoid Z-fighting.
            v.vertex.xyz += v.normal * 0.0001f;

            // The UVs are just the local-space positions in the desired plane.
            // XY is chosen since the default is along Z.
            o.projectedUV = v.vertex.xy;
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            float4 c = tex2D (_MainTex, IN.projectedUV + 0.5f) * _Color;
            o.Albedo = c.rgb;
            
            o.Metallic = _Metallic * c.a;
            o.Smoothness = _Glossiness * c.a;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
