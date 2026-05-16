Shader "Custom/OverworldTransitionAtlas"
{
    Properties
    {
        _TileIdMap("Tile Id Map", 2D) = "black" {}
        _TileAtlas("Tile Atlas", 2D) = "white" {}
        _AtlasGrid("Atlas Grid", Vector) = (8,8,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TileIdMap;
            sampler2D _TileAtlas;
            float4 _TileIdMap_TexelSize;
            float4 _AtlasGrid;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 mapSize = float2(1.0 / _TileIdMap_TexelSize.x, 1.0 / _TileIdMap_TexelSize.y);
                float2 tileUV = floor(i.uv * mapSize) / mapSize;
                float idNorm = tex2D(_TileIdMap, tileUV + (0.5 / mapSize)).r;
                float tileId = floor(idNorm * 255.0 + 0.5);

                float atlasCols = max(1.0, _AtlasGrid.x);
                float atlasRows = max(1.0, _AtlasGrid.y);
                float atlasX = fmod(tileId, atlasCols);
                float atlasY = floor(tileId / atlasCols);

                float2 inTile = frac(i.uv * mapSize);
                float2 atlasUV = (float2(atlasX, atlasY) + inTile) / float2(atlasCols, atlasRows);
                return tex2D(_TileAtlas, atlasUV);
            }
            ENDCG
        }
    }
}
