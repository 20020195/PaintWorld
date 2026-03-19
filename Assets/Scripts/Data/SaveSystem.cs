using UnityEngine;
using System.IO;

/// <summary>
/// Static utility for saving and loading paint textures to disk.
/// </summary>
public static class SaveSystem
{
    public static string GetSavePath(string pictureName)
    {
        return Path.Combine(Application.persistentDataPath, pictureName + "_save.png");
    }

    public static string GetPreviewPath(string pictureName)
    {
        return Path.Combine(Application.persistentDataPath, pictureName + "_preview.png");
    }

    public static void SavePaintProgress(string pictureName, Texture2D tex, Texture2D previewTex = null)
    {
        if (tex == null || string.IsNullOrEmpty(pictureName)) return;
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(GetSavePath(pictureName), bytes);

        if (previewTex != null)
        {
            byte[] previewBytes = previewTex.EncodeToPNG();
            File.WriteAllBytes(GetPreviewPath(pictureName), previewBytes);
        }
    }

    public static bool LoadPaintProgress(string pictureName, Texture2D targetTex)
    {
        return LoadFromFile(GetSavePath(pictureName), targetTex);
    }

    public static bool LoadPaintPreview(string pictureName, Texture2D targetTex)
    {
        // Try to load preview. If not found, fallback to the raw save (for old saves)
        bool success = LoadFromFile(GetPreviewPath(pictureName), targetTex);
        if (!success)
            success = LoadFromFile(GetSavePath(pictureName), targetTex);
        return success;
    }

    private static bool LoadFromFile(string path, Texture2D targetTex)
    {
        if (File.Exists(path))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return ImageConversion.LoadImage(targetTex, bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to load paint texture: " + e.Message);
            }
        }
        return false;
    }

    public static void DeleteSave(string pictureName)
    {
        if (string.IsNullOrEmpty(pictureName)) return;
        string path = GetSavePath(pictureName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        string previewPath = GetPreviewPath(pictureName);
        if (File.Exists(previewPath))
        {
            File.Delete(previewPath);
        }
    }

    public static bool HasSave(string pictureName)
    {
        if (string.IsNullOrEmpty(pictureName)) return false;
        return File.Exists(GetSavePath(pictureName));
    }
}
