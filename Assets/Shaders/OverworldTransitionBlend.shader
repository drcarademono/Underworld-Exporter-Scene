Shader "Custom/OverworldTransitionBlend"
{
    Properties
    {
        _MainTex ("Base Terrain", 2D) = "white" {}
        _BlendTex ("Blend Terrain", 2D) = "white" {}
        _ControlTex ("Control Mask", 2D) = "black" {}
        _ControlScale ("Control Scale", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };
            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _BlendTex;
            sampler2D _ControlTex; float4 _ControlScale;
            v2f vert (appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv,_MainTex); return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 a = tex2D(_MainTex, i.uv);
                fixed4 b = tex2D(_BlendTex, i.uv);
                float2 cuv = i.uv * _ControlScale.xy;
                fixed m = tex2D(_ControlTex, cuv).r;
                return lerp(a,b,m);
            }
            ENDCG
        }
    }
}
