Shader "Custom/OverworldTransitionAtlas"
{
    Properties
    {
        _TileIdMap("Tile Id Map", 2D) = "black" {}
        _TileAtlas("Tile Atlas", 2D) = "white" {}
        _WaterMask("Water Mask", 2D) = "black" {}
        _AtlasGrid("Atlas Grid", Vector) = (8,8,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #include "UnityCG.cginc"

        sampler2D _TileIdMap;
        sampler2D _TileAtlas;
        sampler2D _WaterMask;
        float4 _TileIdMap_TexelSize;
        float4 _AtlasGrid;

        struct Input
        {
            float2 uv_TileAtlas;
        };

        float2 ComputeAtlasUV(float2 uv)
        {
            float2 mapSize = float2(1.0 / _TileIdMap_TexelSize.x, 1.0 / _TileIdMap_TexelSize.y);
            float2 uvClamped = saturate(uv);
            // Keep tile coordinate in [0, mapSize-1] even when UV is exactly 1.0 at chunk borders.
            float2 tileCoord = min(floor(uvClamped * mapSize), mapSize - 1.0);
            float2 tileUV = tileCoord / mapSize;
            float4 idSample = tex2D(_TileIdMap, tileUV + (0.5 / mapSize));
            float tileId = floor(idSample.r * 255.0 + 0.5) + (floor(idSample.g * 255.0 + 0.5) * 256.0);

            float atlasCols = max(1.0, _AtlasGrid.x);
            float atlasRows = max(1.0, _AtlasGrid.y);
            // Avoid fmod precision artifacts on non-power-of-two column counts (e.g. 7),
            // which can misindex atlas tiles on some shader targets.
            float atlasY = floor(tileId / atlasCols);
            float atlasX = tileId - (atlasY * atlasCols);

            float2 inTile = frac(uvClamped * mapSize);
            return (float2(atlasX, atlasY) + inTile) / float2(atlasCols, atlasRows);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Runtime materials explicitly force _TileAtlas scale/offset to identity.
            // So uv_TileAtlas can be used directly as stable chunk UVs.
            float2 atlasUV = ComputeAtlasUV(IN.uv_TileAtlas);
            fixed4 c = tex2D(_TileAtlas, atlasUV);
            o.Albedo = c.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
