Shader "Custom/OverworldTransitionAtlas"
{
    Properties
    {
        _TileIdMap("Tile Id Map", 2D) = "black" {}
        _TileAtlas("Tile Atlas", 2D) = "white" {}
        _WaterMask("Water Mask", 2D) = "black" {}
        _AtlasGrid("Atlas Grid", Vector) = (8,8,0,0)
        _Color("Main Color", Color) = (1,1,1,1)
        _ColorPaletteIn("Color Palette", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Name "OverworldTransitionAtlasPass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            sampler2D _TileIdMap;
            sampler2D _TileAtlas;
            sampler2D _WaterMask;
            sampler2D _ColorPaletteIn;
            float4 _TileIdMap_TexelSize;
            float4 _AtlasGrid;
            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                LIGHTING_COORDS(0,1)
                float2 uv : TEXCOORD2;
            };

            v2f vert(appdata_tan v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            float2 ComputeAtlasUV(float2 uv)
            {
                float2 mapSize = float2(1.0 / _TileIdMap_TexelSize.x, 1.0 / _TileIdMap_TexelSize.y);
                float2 tileUV = floor(saturate(uv) * mapSize) / mapSize;
                float idNorm = tex2D(_TileIdMap, tileUV + (0.5 / mapSize)).a;
                float tileId = floor(idNorm * 255.0 + 0.5);

                float atlasCols = max(1.0, _AtlasGrid.x);
                float atlasRows = max(1.0, _AtlasGrid.y);
                float atlasX = fmod(tileId, atlasCols);
                float atlasY = floor(tileId / atlasCols);

                float2 inTile = frac(saturate(uv) * mapSize);
                return (float2(atlasX, atlasY) + inTile) / float2(atlasCols, atlasRows);
            }

            float4 frag(v2f i) : COLOR
            {
                float2 atlasUV = ComputeAtlasUV(i.uv);
                float4 atlasSample = tex2D(_TileAtlas, atlasUV);
                float greyscale = atlasSample.r;
                float4 result;
                result.rgb = tex2D(_ColorPaletteIn, float2(greyscale, 0.1)).rgb;
                result.a = 1.0;
                result *= _Color;
                return result * LIGHT_ATTENUATION(i);
            }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/VertexLit"
}
