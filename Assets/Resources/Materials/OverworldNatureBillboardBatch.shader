Shader "Custom/OverworldNatureBillboardBatch"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _MainTex("Albedo Map", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _UpVector("Up Vector (XYZ)", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        LOD 200

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Lambert alphatest:_Cutoff vertex:vert addshadow

        half4 _Color;
        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _EmissionMap;
        half4 _EmissionColor;
        float3 _UpVector;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_EmissionMap;
        };

        void vert(inout appdata_full v)
        {
            float3 viewDirection = UNITY_MATRIX_V._m02_m12_m22;
            float3 rightVector = normalize(cross(viewDirection, _UpVector));
            v.normal = mul((float3x3)UNITY_MATRIX_V, v.normal);
            v.vertex.xyz += rightVector * (v.tangent.z - 0.5) * v.tangent.x;
            v.vertex.xyz += _UpVector * (v.tangent.w - 0.5) * v.tangent.y;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 emission = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb * _EmissionColor.rgb;
            o.Albedo = albedo.rgb - emission;
            o.Alpha = albedo.a;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Emission = emission;
        }
        ENDCG
    }
    FallBack "Transparent/VertexLit"
}
