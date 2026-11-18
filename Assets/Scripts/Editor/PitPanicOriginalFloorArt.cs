using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PitPanicOriginalFloorArt
{
    private const int Width = 608;
    private const int Height = 352;
    private const int SideMargin = (Width - 512) / 2;
    private const int PixelsPerUnit = 16;
    private const string OutputFolder = "Assets/Resources/Art/Original/World";

    [MenuItem("Pit Panic/Art/Generate Original Casino Floors")]
    public static void Generate()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Art");
        EnsureFolder("Assets/Resources/Art/Original");
        EnsureFolder("Assets/Resources/Art/Original/World");
        GenerateOpeningNight();
        GenerateHighRollers();
        GenerateShadyBusiness();
        AssetDatabase.Refresh();
        foreach (string name in new[] { "OpeningNightFloor", "HighRollersFloor", "ShadyBusinessFloor" })
            ConfigureSprite($"{OutputFolder}/{name}.png");
        AssetDatabase.SaveAssets();
        ValidateGeneratedAssets();
    }

    [MenuItem("Pit Panic/Art/Validate Original Casino Floors")]
    public static void ValidateGeneratedAssets()
    {
        string[] names = { "OpeningNightFloor", "HighRollersFloor", "ShadyBusinessFloor" };
        int[] signatures = new int[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = $"{OutputFolder}/{names[i]}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || importer == null || sprite.texture.width != Width ||
                sprite.texture.height != Height || sprite.texture.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled || importer.spritePixelsPerUnit != PixelsPerUnit ||
                !sprite.texture.isReadable)
                throw new InvalidOperationException($"Original floor art has incorrect sprite settings: {path}");

            Color32[] pixels = sprite.texture.GetPixels32();
            int signature = 17;
            for (int p = 0; p < pixels.Length; p += 97)
                signature = unchecked(signature * 31 + pixels[p].GetHashCode());
            signatures[i] = signature;
        }
        if (signatures[0] == signatures[1] || signatures[0] == signatures[2] ||
            signatures[1] == signatures[2])
            throw new InvalidOperationException("Each casino level must use a distinct original floor design.");
        Debug.Log("Original casino floor art validated: three unique point-filtered, uncompressed 608x352 sprites.");
    }

    private static void GenerateOpeningNight()
    {
        Texture2D texture = New("Original Opening Night Floor");
        Color32[] pixels = Pixels(new Color32(54, 25, 55, 255));
        TileCarpet(pixels, new Color32(73, 31, 65, 255), new Color32(100, 40, 74, 255));
        FrontPlanks(pixels, new Color32(45, 28, 48, 255), new Color32(105, 61, 71, 255));
        InsetRunway(pixels, new Color32(106, 34, 69, 255), new Color32(239, 177, 82, 255),
            184 + SideMargin, 12, 144, 158);
        WallBand(pixels, new Color32(49, 25, 52, 255), new Color32(164, 87, 83, 255), new Color32(236, 176, 86, 255));
        for (int x = 28; x < 484; x += 38)
        {
            Diamond(pixels, x, 232, 5, new Color32(200, 125, 68, 255));
            Pixel(pixels, x, 233, new Color32(255, 223, 153, 255));
        }
        Save(texture, pixels, "OpeningNightFloor");
    }

    private static void GenerateHighRollers()
    {
        Texture2D texture = New("Original High Rollers Floor");
        Color32[] pixels = Pixels(new Color32(20, 42, 48, 255));
        TileCarpet(pixels, new Color32(26, 57, 57, 255), new Color32(57, 88, 72, 255));
        FrontPlanks(pixels, new Color32(19, 34, 45, 255), new Color32(102, 80, 51, 255));
        InsetRunway(pixels, new Color32(35, 75, 63, 255), new Color32(222, 181, 91, 255),
            166 + SideMargin, 24, 180, 138);
        WallBand(pixels, new Color32(26, 39, 49, 255), new Color32(130, 111, 73, 255), new Color32(245, 204, 112, 255));
        for (int x = 20; x < Width - 20; x += 32)
        {
            Diamond(pixels, x, 204, 7, new Color32(130, 106, 60, 255));
            Diamond(pixels, x, 204, 3, new Color32(38, 89, 72, 255));
            Pixel(pixels, x, 204, new Color32(247, 215, 136, 255));
        }
        Save(texture, pixels, "HighRollersFloor");
    }

    private static void GenerateShadyBusiness()
    {
        Texture2D texture = New("Original Shady Business Floor");
        Color32[] pixels = Pixels(new Color32(26, 30, 51, 255));
        TileCarpet(pixels, new Color32(34, 39, 65, 255), new Color32(63, 46, 75, 255));
        FrontPlanks(pixels, new Color32(22, 27, 43, 255), new Color32(68, 60, 87, 255));
        InsetRunway(pixels, new Color32(50, 37, 68, 255), new Color32(180, 123, 92, 255),
            192 + SideMargin, 8, 128, 164);
        WallBand(pixels, new Color32(23, 29, 47, 255), new Color32(91, 74, 97, 255), new Color32(169, 126, 91, 255));
        for (int x = 18; x < Width - 18; x += 48)
        {
            int y = 206 + (x / 48 % 2) * 10;
            Rect(pixels, x - 10, y - 2, 20, 4, new Color32(69, 82, 110, 255));
            Rect(pixels, x - 1, y - 8, 2, 16, new Color32(122, 102, 107, 255));
            Pixel(pixels, x, y, new Color32(224, 159, 104, 255));
        }
        Save(texture, pixels, "ShadyBusinessFloor");
    }

    private static void TileCarpet(Color32[] pixels, Color32 baseColor, Color32 motif)
    {
        for (int y = 0; y < 224; y += 32)
        for (int x = 0; x < Width; x += 32)
        {
            Color32 tile = ((x / 32 + y / 32) & 1) == 0 ? baseColor : Shade(baseColor, 1.035f);
            Rect(pixels, x, y, 32, 32, tile);
            int cx = x + 16 + ((y / 32) & 1) * 8;
            int cy = y + 16;
            Rosette(pixels, cx, cy, motif);
            Color32 seam = Shade(baseColor, .73f);
            Rect(pixels, x + 1, y + 1, 30, 1, Shade(baseColor, 1.14f));
            Rect(pixels, x + 1, y + 30, 30, 1, seam);
            Rect(pixels, x + 1, y + 2, 1, 28, seam);
            Rect(pixels, x + 30, y + 2, 1, 28, Shade(baseColor, .82f));
            for (int detail = 0; detail < 4; detail++)
            {
                int px = x + 3 + (detail * 7 + y / 32 * 3) % 25;
                int py = y + 4 + (detail * 11 + x / 32 * 5) % 24;
                Pixel(pixels, px, py, Shade(motif, detail % 2 == 0 ? 1.18f : .68f));
                Pixel(pixels, px + 1, py, Shade(motif, 1.12f));
            }
        }

    }

    private static void FrontPlanks(Color32[] pixels, Color32 fill, Color32 seam)
    {
        Rect(pixels, 0, 0, Width, 76, fill);
        for (int y = 5; y < 76; y += 14)
        {
            Rect(pixels, 0, y, Width, 1, seam);
            int offset = (y / 14 % 2) * 23;
            for (int x = offset; x < Width; x += 48)
                Rect(pixels, x, y, 1, 14, Shade(seam, .76f));
        }
        Rect(pixels, 0, 77, Width, 2, new Color32(232, 175, 85, 255));
        Rect(pixels, 0, 81, Width, 2, Shade(seam, .8f));
    }

    private static void InsetRunway(Color32[] pixels, Color32 rug, Color32 trim, int left, int bottom, int width, int height)
    {
        int right = left + width;
        int top = bottom + height;
        Rect(pixels, left, bottom, width, height, new Color32(40, 24, 45, 255));
        Rect(pixels, left + 4, bottom + 4, width - 8, height - 8, trim);
        Rect(pixels, left + 7, bottom + 7, width - 14, height - 14, rug);
        Rect(pixels, left + 13, bottom + 13, width - 26, height - 26, Shade(rug, 1.08f));
        for (int y = bottom + 18; y < top - 12; y += 18)
        {
            int cx = left + width / 2;
            Diamond(pixels, cx, y, 4, Shade(trim, .78f));
            Diamond(pixels, cx, y, 2, Shade(trim, 1.05f));
            Pixel(pixels, cx, y, new Color32(255, 226, 151, 255));
            for (int side = 0; side < 2; side++)
            {
                int x = side == 0 ? left + 12 : right - 13;
                Diamond(pixels, x, y, 2, Shade(trim, .82f));
            }
        }
        for (int y = bottom + 20; y < top - 14; y += 36)
        {
            for (int side = 0; side < 2; side++)
            {
                int x = side == 0 ? left + 22 : right - 23;
                Diamond(pixels, x, y, 4, Shade(trim, .66f));
                Diamond(pixels, x, y, 2, Shade(trim, 1.18f));
                Pixel(pixels, x, y, new Color32(255, 239, 191, 255));
            }
        }
        Rect(pixels, left + 14, bottom + 13, 1, height - 26, Shade(trim, .78f));
        Rect(pixels, right - 15, bottom + 13, 1, height - 26, Shade(trim, .78f));
        Rect(pixels, left + 1, bottom + 1, width - 2, 1, new Color32(255, 224, 146, 255));
        Rect(pixels, left + 1, top - 2, width - 2, 1, new Color32(255, 224, 146, 255));
        Rect(pixels, left + 2, bottom + 2, 1, height - 4, Shade(trim, .68f));
        Rect(pixels, right - 3, bottom + 2, 1, height - 4, Shade(trim, .68f));
        for (int corner = 0; corner < 4; corner++)
        {
            int x = corner % 2 == 0 ? left + 8 : right - 9;
            int y = corner < 2 ? bottom + 8 : top - 9;
            Diamond(pixels, x, y, 5, Shade(trim, .65f));
            Diamond(pixels, x, y, 3, Shade(trim, 1.16f));
            Pixel(pixels, x, y, new Color32(255, 235, 169, 255));
        }
    }

    private static void Rosette(Color32[] pixels, int cx, int cy, Color32 color)
    {
        Color32 shadow = Shade(color, .6f);
        Diamond(pixels, cx, cy, 11, shadow);
        Diamond(pixels, cx, cy, 9, Shade(color, .72f));
        Diamond(pixels, cx, cy, 7, color);
        Diamond(pixels, cx - 5, cy, 4, shadow);
        Diamond(pixels, cx + 5, cy, 4, shadow);
        Diamond(pixels, cx, cy - 5, 4, shadow);
        Diamond(pixels, cx, cy + 5, 4, shadow);
        Diamond(pixels, cx - 5, cy, 2, Shade(color, 1.12f));
        Diamond(pixels, cx + 5, cy, 2, Shade(color, 1.12f));
        Diamond(pixels, cx, cy - 5, 2, Shade(color, 1.12f));
        Diamond(pixels, cx, cy + 5, 2, Shade(color, 1.12f));
        Diamond(pixels, cx, cy, 3, shadow);
        Diamond(pixels, cx, cy, 1, Shade(color, 1.32f));
        Pixel(pixels, cx, cy, new Color32(255, 226, 151, 255));
        for (int side = -1; side <= 1; side += 2)
        {
            Pixel(pixels, cx + side * 8, cy + side * 2, Shade(color, 1.25f));
            Pixel(pixels, cx + side * 8, cy - side * 2, Shade(color, 1.25f));
            Pixel(pixels, cx + side * 2, cy + side * 8, Shade(color, 1.25f));
            Pixel(pixels, cx - side * 2, cy + side * 8, Shade(color, 1.25f));
        }
    }

    private static void WallBand(Color32[] pixels, Color32 wall, Color32 panel, Color32 trim)
    {
        Rect(pixels, 0, 284, Width, Height - 284, wall);
        Rect(pixels, 0, 281, Width, 2, trim);
        Rect(pixels, 0, 276, Width, 2, panel);
        for (int x = 10; x < Width - 10; x += 64)
        {
            Rect(pixels, x, 285, 3, 67, trim);
            Rect(pixels, x + 4, 288, 48, 58, Shade(wall, 1.2f));
            Rect(pixels, x + 8, 292, 40, 50, panel);
            Rect(pixels, x + 11, 295, 34, 44, Shade(wall, 1.1f));
            Rect(pixels, x + 13, 297, 30, 40, wall);
            Rect(pixels, x + 14, 319, 28, 2, trim);
        }
        Rect(pixels, 0, Height - 3, Width, 3, trim);
    }

    private static void Diamond(Color32[] pixels, int cx, int cy, int radius, Color32 color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Math.Abs(dy);
            Rect(pixels, cx - span, cy + dy, span * 2 + 1, 1, color);
        }
    }

    private static Color32 Shade(Color32 color, float factor) => new Color32(
        (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
        (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
        (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255), color.a);

    private static Color32[] Pixels(Color32 fill)
    {
        Color32[] pixels = new Color32[Width * Height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
        return pixels;
    }

    private static Texture2D New(string name) =>
        new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = name };

    private static void Save(Texture2D texture, Color32[] pixels, string name)
    {
        texture.SetPixels32(pixels);
        texture.Apply();
        string path = $"{OutputFolder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"Could not import generated artwork: {path}");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            throw new InvalidOperationException($"Generated floor is not available as a sprite: {path}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void Rect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        int minX = Mathf.Max(0, x);
        int maxX = Mathf.Min(Width, x + width);
        int minY = Mathf.Max(0, y);
        int maxY = Mathf.Min(Height, y + height);
        for (int py = minY; py < maxY; py++)
            for (int px = minX; px < maxX; px++)
                pixels[py * Width + px] = color;
    }

    private static void Pixel(Color32[] pixels, int x, int y, Color32 color)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height) pixels[y * Width + x] = color;
    }
}
