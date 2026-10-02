// ProceduralCityStreamingTypes.cs
// Schema starter for the V19 Unity streaming export.
// Adapt namespace and serializer attributes to the RPG's existing conventions.
// The project should route all coordinates through its existing world-coordinate adapter.

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ProceduralSettlementManifest
{
    public string format;
    public int schema_version;
    public string settlement_id;
    public int seed;
    public int population;
    public int units_per_map_pixel;
    public int generator_unit_m;
    public int[] world_map_pixel_origin_xy;
    public int[] world_unit_origin_xy;
    public ProceduralChunkIndexEntry[] chunks;
    public ProceduralRoadConnector[] road_connectors;
}

[Serializable]
public sealed class ProceduralChunkIndexEntry
{
    public int[] map_pixel_xy;
    public string file;
    public int owned_building_count;
    public int referenced_building_count;
    public string[] district_ids;
}

[Serializable]
public sealed class ProceduralSettlementChunk
{
    public string format;
    public int schema_version;
    public string settlement_id;
    public int[] map_pixel_xy;
    public int map_pixel_size_units;

    public string[] district_ids;
    public string[] owned_building_ids;
    public string[] referenced_building_ids;
    public string[] door_ids_present;

    public ProceduralStreetSegment[] street_segments;
    public ProceduralFieldSlice[] field_slices;

    public string[] special_site_ids;
    public string[] fringe_plot_ids;
    public string[] farmstead_ids;
    public string[] road_connector_ids;
}

[Serializable]
public sealed class ProceduralStreetSegment
{
    public string segment_id;
    public string street_id;
    public string kind;
    public int width_units;
    public float[][] points_map_pixel_local_xy;
}

[Serializable]
public sealed class ProceduralFieldSlice
{
    public string field_id;
    public string kind;
    public string zone;
    public ProceduralPolygon[] polygons_map_pixel_local;
}

[Serializable]
public sealed class ProceduralPolygon
{
    public float[][] outer;
    public float[][][] holes;
}

[Serializable]
public sealed class ProceduralRoadConnector
{
    public string road_connector_id;
    public string entity_id;
    public string direction;
    public int bit_index;
    public int[] city_entry_point;
    public int[] major_road_outer_point;
    public int[] world_connector_point;
}

[Serializable]
public sealed class ProceduralBuildingRecord
{
    public int building_id;
    public string local_entity_id;
    public string entity_id;

    public string kind;
    public string identity;
    public string identity_family;
    public string subtype;
    public string[] secondary_uses;

    public string district_id;

    public float[][] outer;
    public float[][][] holes;

    public int stories;
    public float ground_floor_height_m;
    public float upper_floor_height_m;
    public float eave_height_m;

    public string[] door_ids;

    public int[] home_map_pixel_xy;
    public int[][] overlapping_map_pixels_xy;
}

[Serializable]
public sealed class ProceduralDoorRecord
{
    public string door_id;
    public string entity_id;
    public int building_id;
    public string building_entity_id;

    public string role;
    public int[][] segment;
    public string orientation;
    public int width_steps;
    public float width_m;

    public string outside_space;
    public string inside_space;

    public string portal_group_id;
    public string paired_door_id;
    public int? target_unit_index;
}

[Serializable]
public sealed class ProceduralDistrictRecord
{
    public string district_id;
    public string entity_id;
    public long district_seed;
    public string name;

    public string primary_identity;
    public string wealth;
    public string density;
    public string age;
    public float prestige;
    public float commercial_intensity;
    public float industrial_intensity;
    public string urban_form;
    public string anchor_type;
    public string[] tags;
}

// Deliberately an interface: the procedural-city importer must not choose
// the RPG's global X/Z orientation or floating-origin policy itself.
public interface IProceduralCityWorldCoordinateAdapter
{
    Vector3 MapPixelLocalToUnity(int mapPixelX, int mapPixelY, float localX, float localY, float elevation = 0f);
}
