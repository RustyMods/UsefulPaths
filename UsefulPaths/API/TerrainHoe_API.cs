using System;
using System.Reflection;
using UnityEngine;

#nullable disable

namespace UsefulPaths;

public static class TerrainHoe_API
{
    private const string Namespace = "TerrainHoe";
    private const string ClassName = "API";
    private const string Assembly = "TerrainHoe";
    private const string TypeNameAssembly = $"{Namespace}.{ClassName}, {Assembly}";

    private static readonly bool isLoaded = false;

    public static bool IsLoaded() => isLoaded;

    private static readonly MethodInfo API_GetBiome;
    private static readonly MethodInfo API_GetHeightmapBiome;

    static TerrainHoe_API()
    {
        Type type = Type.GetType(TypeNameAssembly);
        if (type == null) return;
        isLoaded = true;
        API_GetBiome = type.GetMethod("GetBiome", BindingFlags.Public | BindingFlags.Static);
        API_GetHeightmapBiome = type.GetMethod("GetHeightmapBiome", BindingFlags.Public | BindingFlags.Static);
    }

    public static Heightmap.Biome GetBiome(Vector3 position) =>
        (Heightmap.Biome)(API_GetBiome.Invoke(null, new object[] { position }) ?? Heightmap.Biome.None);
    
    public static Heightmap.Biome GetBiome(Heightmap heightmap, Vector3 position) =>        
        (Heightmap.Biome)(API_GetHeightmapBiome.Invoke(null, new object[] { heightmap, position }) ?? heightmap.GetBiome(position));
}