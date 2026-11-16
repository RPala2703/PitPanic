using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CasinoResultsArtImporter
{
    private const string ResultsRoot = "Assets/Resources/Results";
    private const string BackgroundRoot = ResultsRoot + "/Backgrounds";
    private const string UiRoot = ResultsRoot + "/UI";
    private const string SheetPath = ResultsRoot + "/shift-failed-card-final.png";

    [MenuItem("Pit Panic/Import Results Screen Artwork")]
    public static void Import()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(ResultsRoot);
        EnsureFolder(BackgroundRoot);
        EnsureFolder(UiRoot);

        foreach (int level in new[] { 1, 2, 3 })
        foreach (string outcome in new[] { "Pass", "Fail" })
        {
            string path = $"{BackgroundRoot}/Level{level}_{outcome}_Background.png";
            if (!File.Exists(path))
                throw new FileNotFoundException($"Supplied results background is missing: {path}");
            ConfigureSprite(path);
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (background == null || background.width != 1920 || background.height != 1080)
                throw new InvalidDataException($"Results background must be 1920x1080: {path}");
            ValidateSpriteImport(path);
        }

        if (!File.Exists(SheetPath))
            throw new FileNotFoundException($"Supplied results sprite sheet is missing: {SheetPath}");

        byte[] sourceBytes = File.ReadAllBytes(SheetPath);
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(source, sourceBytes, false) ||
                source.width != 1536 || source.height != 1024)
                throw new InvalidDataException("The supplied results UI sheet must be 1536x1024 RGBA.");
            source.filterMode = FilterMode.Point;

            CropSprite(source, "Results_Passed", new RectInt(34, 17, 718, 336));
            CropSprite(source, "Results_Failed", new RectInt(785, 25, 714, 328));
            CropSprite(source, "Results_Info_Panel", new RectInt(42, 345, 1452, 430),
                new[]
                {
                    new Vector2(48, 421), new Vector2(53, 407), new Vector2(73, 399),
                    new Vector2(598, 399), new Vector2(634, 386), new Vector2(657, 364),
                    new Vector2(682, 350), new Vector2(786, 350), new Vector2(812, 364),
                    new Vector2(837, 386), new Vector2(875, 399), new Vector2(1461, 399),
                    new Vector2(1486, 420), new Vector2(1482, 741), new Vector2(1460, 765),
                    new Vector2(72, 765), new Vector2(49, 741)
                });
            CropSprite(source, "Results_Star_Filled", new RectInt(530, 765, 225, 220));
            CropSprite(source, "Results_Star_Hollow", new RectInt(770, 765, 225, 220));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }

        ConfigureSprite(SheetPath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        ValidateImportedSprites();
        Debug.Log("Imported six pixel-art results backgrounds and five transparent UI sprites.");
    }

    private static void CropSprite(Texture2D source, string name, RectInt topLeftRect,
        Vector2[] topLeftMask = null)
    {
        if (topLeftRect.xMin < 0 || topLeftRect.yMin < 0 ||
            topLeftRect.xMax > source.width || topLeftRect.yMax > source.height)
            throw new ArgumentOutOfRangeException(nameof(topLeftRect), $"Crop is outside the sprite sheet: {name}");

        Texture2D crop = new Texture2D(topLeftRect.width, topLeftRect.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        try
        {
            int bottomY = source.height - topLeftRect.yMax;
            Color[] pixels = source.GetPixels(topLeftRect.x, bottomY, topLeftRect.width, topLeftRect.height);
            if (topLeftMask != null)
            {
                for (int y = 0; y < topLeftRect.height; y++)
                for (int x = 0; x < topLeftRect.width; x++)
                {
                    int sourceY = source.height - (bottomY + y) - 1;
                    if (!IsInsidePolygon(topLeftMask, topLeftRect.x + x, sourceY))
                    {
                        int index = y * topLeftRect.width + x;
                        Color pixel = pixels[index];
                        pixel.a = 0f;
                        pixels[index] = pixel;
                    }
                }
            }
            crop.SetPixels(pixels);
            crop.Apply(false, false);
            string path = $"{UiRoot}/{name}.png";
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(crop));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSprite(path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crop);
        }
    }

    private static bool IsInsidePolygon(Vector2[] polygon, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            if ((a.y > y) != (b.y > y) &&
                x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    private static void ConfigureSprite(string path)
    {
        AssetImporter rawImporter = AssetImporter.GetAtPath(path);
        if (!(rawImporter is TextureImporter importer))
            throw new InvalidOperationException($"Could not configure pixel sprite importer: {path}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static void ValidateImportedSprites()
    {
        foreach (string name in new[]
        {
            "Results_Passed", "Results_Failed", "Results_Info_Panel",
            "Results_Star_Filled", "Results_Star_Hollow"
        })
        {
            ValidateSpriteImport($"{UiRoot}/{name}.png");
        }
    }

    private static void ValidateSpriteImport(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        bool alphaExpected = path == SheetPath || path.StartsWith(UiRoot + "/", StringComparison.Ordinal);
        bool correctFormat = sprite != null &&
            (alphaExpected ? sprite.texture.format == TextureFormat.RGBA32 :
                sprite.texture.format == TextureFormat.RGBA32 || sprite.texture.format == TextureFormat.RGB24);
        if (sprite == null || importer == null || importer.textureType != TextureImporterType.Sprite ||
            importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.spritePixelsPerUnit != 100 || !correctFormat ||
            sprite.rect.width < 1f || sprite.rect.height < 1f)
            throw new InvalidOperationException($"Results sprite import settings are incorrect: {path}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            throw new DirectoryNotFoundException($"Unity asset folder parent is missing: {path}");
        AssetDatabase.CreateFolder(parent, folder);
    }
}
