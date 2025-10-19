using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace UsefulPaths.Managers;

public static class SpriteManager
{
    public static readonly Sprite? WingedBoots = RegisterSprite("icon.png");
    public static Sprite? paved;
    public static Sprite? dirt;
    public static Sprite? cultivated;
    public static Sprite? wood;
    public static Sprite? stone;
    public static Sprite? metal;
    public static Sprite? mud;

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNetScene __instance)
        {
            mud = __instance.GetPrefab("mud_road").GetComponent<Piece>().m_icon;
            paved = __instance.GetPrefab("paved_road").GetComponent<Piece>().m_icon;
            dirt = __instance.GetPrefab("path").GetComponent<Piece>().m_icon;
            metal = __instance.GetPrefab("iron_floor_2x2").GetComponent<Piece>().m_icon;
            cultivated = __instance.GetPrefab("Cultivator").GetComponent<ItemDrop>().m_itemData.GetIcon();
            wood = __instance.GetPrefab("wood_floor").GetComponent<Piece>().m_icon;
            stone = __instance.GetPrefab("stone_floor_2x2").GetComponent<Piece>().m_icon;
        }
    }
    private static Sprite? RegisterSprite(string fileName, string folderName = "icons")
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string path = $"{UsefulPathsPlugin.ModName}.{folderName}.{fileName}";
        using var stream = assembly.GetManifestResourceStream(path);
        if (stream == null) return null;
        byte[] buffer = new byte[stream.Length];
        _ = stream.Read(buffer, 0, buffer.Length);
        Texture2D texture = new Texture2D(2, 2);
        
        Sprite? sprite = texture.LoadImage(buffer) ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero) : null;
        if (sprite != null) sprite.name = fileName;
        return sprite;
    }
}