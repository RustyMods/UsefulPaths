using System.Reflection;
using UnityEngine;

namespace UsefulPaths.Managers;

public static class SpriteManager
{
    public static readonly Sprite? WingedBoots = RegisterSprite("icon.png");
    private static Sprite? RegisterSprite(string fileName, string folderName = "icons")
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string path = $"{UsefulPathsPlugin.ModName}.{folderName}.{fileName}";
        using var stream = assembly.GetManifestResourceStream(path);
        if (stream == null) return null;
        byte[] buffer = new byte[stream.Length];
        int read = stream.Read(buffer, 0, buffer.Length);
        Texture2D texture = new Texture2D(2, 2);
        
        Sprite? sprite = texture.LoadImage(buffer) ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero) : null;
        if (sprite != null) sprite.name = fileName;
        return sprite;
    }

    public static Sprite? GetIconFromPrefab(string prefabName)
    {
        if (!ObjectDB.instance) return null;
        GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
        if (!prefab) return null;
        if (!prefab.TryGetComponent(out ItemDrop component)) return null;
        return component.m_itemData.GetIcon();
    }
}