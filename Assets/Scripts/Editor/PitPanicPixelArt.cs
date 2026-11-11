using System.IO;
using UnityEditor;
using UnityEngine;

// Original, generated pixel art. Gameplay colliders and navigation remain independent.
public static class PitPanicPixelArt
{
    private const string Folder = "Assets/Art/Pixel";
    private static readonly Color32 Ink = new Color32(29, 24, 43, 255);
    private static readonly Color32 Cream = new Color32(255, 227, 178, 255);
    private static readonly Color32 Gold = new Color32(247, 185, 71, 255);

    public static Sprite[] CharacterFrames(CasinoAnimalSpecies species)
    {
        EnsureFolder();
        Sprite[] frames = new Sprite[8];
        for (int direction = 0; direction < 4; direction++)
        {
            for (int step = 0; step < 2; step++)
            {
                string path = $"{Folder}/Character_{species}_{direction}_{step}.png";
                if (!File.Exists(path))
                {
                    Texture2D texture = NewTexture(32, 40);
                    DrawCharacter(texture, species, direction, step);
                    Save(texture, path, new Vector2(0.5f, 0f), 20);
                }

                frames[direction * 2 + step] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[direction * 2 + step] == null)
                    throw new System.InvalidOperationException($"Pixel character sprite could not be loaded: {path}");
            }
        }

        return frames;
    }

    public static Sprite StationSprite(string kind)
    {
        EnsureFolder();
        string path = $"{Folder}/Station_{kind}.png";
        if (!File.Exists(path))
        {
            Texture2D texture = NewTexture(96, 48);
            DrawStation(texture, kind);
            Save(texture, path, new Vector2(0.5f, 0.5f), 16);
        }

        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (result == null)
            throw new System.InvalidOperationException($"Pixel station sprite could not be loaded: {path}");
        return result;
    }

    public static Sprite FloorSprite()
    {
        EnsureFolder();
        string path = $"{Folder}/CasinoFloor.png";
        if (!File.Exists(path))
        {
            Texture2D texture = NewTexture(256, 176);
            Color32 carpet = new Color32(54, 35, 67, 255);
            Color32 tile = new Color32(67, 42, 77, 255);
            Color32 pattern = new Color32(100, 57, 78, 255);
            Rect(texture, 0, 0, 256, 176, carpet);
            for (int x = 0; x < 256; x += 16)
            {
                for (int y = 0; y < 176; y += 16)
                {
                    if ((x / 16 + y / 16) % 2 == 0)
                        Rect(texture, x, y, 16, 16, tile);
                    Rect(texture, x + 7, y + 5, 2, 6, pattern);
                    Rect(texture, x + 5, y + 7, 6, 2, pattern);
                }
            }
            Rect(texture, 0, 0, 256, 2, Gold); Rect(texture, 0, 174, 256, 2, Gold);
            Rect(texture, 0, 0, 2, 176, Gold); Rect(texture, 254, 0, 2, 176, Gold);
            Save(texture, path, new Vector2(0.5f, 0.5f), 8);
        }
        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (result == null)
            throw new System.InvalidOperationException($"Pixel floor sprite could not be loaded: {path}");
        return result;
    }

    public static Sprite WheelSprite()
    {
        EnsureFolder();
        string path = $"{Folder}/RouletteWheel.png";
        if (!File.Exists(path))
        {
            Texture2D texture = NewTexture(32, 32);
            Color32 red = new Color32(178, 53, 65, 255);
            Color32 green = new Color32(48, 132, 87, 255);
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    float dx = x - 15.5f;
                    float dy = y - 15.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    if (radius > 15f) continue;
                    Color32 color = radius > 13f ? Ink : radius > 11f ? Gold : Ink;
                    if (radius <= 11f && radius > 5f)
                    {
                        int pocket = Mathf.FloorToInt((Mathf.Atan2(dy, dx) + Mathf.PI) * 37f / (2f * Mathf.PI));
                        color = pocket == 0 ? green : pocket % 2 == 0 ? Ink : red;
                    }
                    if (radius <= 5f) color = Gold;
                    if (radius <= 2f) color = Cream;
                    texture.SetPixel(x, y, color);
                }
            }
            Save(texture, path, new Vector2(0.5f, 0.5f), 20);
        }
        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (result == null)
            throw new System.InvalidOperationException($"Pixel roulette wheel could not be loaded: {path}");
        return result;
    }

    public static Sprite StructureSprite(string kind)
    {
        EnsureFolder();
        string path = $"{Folder}/Structure_{kind}.png";
        if (!File.Exists(path))
        {
            int width = kind == "Arch" ? 64 : 32;
            Texture2D texture = NewTexture(width, 64);
            Color32 wood = new Color32(101, 48, 65, 255);
            Color32 highlight = new Color32(165, 88, 91, 255);
            if (kind == "Arch")
            {
                Rect(texture, 1, 0, 10, 52, Ink); Rect(texture, 53, 0, 10, 52, Ink);
                Rect(texture, 3, 0, 6, 50, wood); Rect(texture, 55, 0, 6, 50, wood);
                Rect(texture, 0, 48, 64, 13, Ink); Rect(texture, 2, 50, 60, 9, Gold);
                Rect(texture, 5, 52, 54, 5, wood);
                for (int x = 10; x < 58; x += 8) Rect(texture, x, 54, 2, 2, Cream);
            }
            else
            {
                Rect(texture, 3, 0, 26, 6, Ink); Rect(texture, 5, 2, 22, 3, Gold);
                Rect(texture, 7, 5, 18, 50, Ink); Rect(texture, 9, 6, 14, 48, wood);
                Rect(texture, 11, 8, 3, 44, highlight); Rect(texture, 19, 8, 2, 44, Ink);
                Rect(texture, 3, 53, 26, 7, Ink); Rect(texture, 5, 55, 22, 3, Gold);
                Rect(texture, 5, 60, 22, 3, wood);
            }
            Save(texture, path, new Vector2(0.5f, 0f), 20);
        }
        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (result == null)
            throw new System.InvalidOperationException($"Pixel structure sprite could not be loaded: {path}");
        return result;
    }

    private static void DrawCharacter(Texture2D t, CasinoAnimalSpecies species, int direction, int step)
    {
        Color32[] fur =
        {
            new Color32(57, 78, 99, 255), new Color32(230, 141, 54, 255),
            new Color32(237, 154, 160, 255), new Color32(226, 115, 58, 255),
            new Color32(229, 218, 198, 255), new Color32(158, 105, 75, 255),
            new Color32(196, 146, 97, 255), new Color32(219, 177, 115, 255),
            new Color32(137, 139, 151, 255), new Color32(151, 173, 183, 255)
        };
        Color32 skin = fur[Mathf.Clamp((int)species, 0, fur.Length - 1)];
        Color32 coat = new Color32(95, 47, 85, 255);
        Color32 light = new Color32(255, 242, 215, 255);

        // Feet, jacket, paws and head are deliberately outlined for readability.
        Rect(t, 6, 1 + step, 9, 5, Ink); Rect(t, 18, 1 + (1 - step), 9, 5, Ink);
        Rect(t, 8, 3 + step, 6, 2, new Color32(94, 70, 72, 255));
        Rect(t, 19, 3 + (1 - step), 6, 2, new Color32(94, 70, 72, 255));
        Rect(t, 5, 6, 22, 16, Ink); Rect(t, 7, 7, 18, 14, coat);
        Rect(t, 3, 8, 4, 10, Ink); Rect(t, 25, 8, 4, 10, Ink);
        Rect(t, 4, 8, 3, 3, skin); Rect(t, 25, 8, 3, 3, skin);
        if (direction != 1)
        {
            Rect(t, 13, 9, 6, 10, light);
            Rect(t, 11, 18, 4, 3, Gold); Rect(t, 18, 18, 4, 3, Gold);
        }
        else
            Rect(t, 8, 17, 16, 2, new Color32(148, 79, 114, 255));

        Rect(t, 5, 21, 22, 15, Ink); Rect(t, 7, 22, 18, 13, skin);
        Rect(t, 5, 33, 6, 5, Ink); Rect(t, 21, 33, 6, 5, Ink);
        Rect(t, 7, 34, 3, 3, skin); Rect(t, 22, 34, 3, 3, skin);
        if (species == CasinoAnimalSpecies.Rabbit)
        {
            Rect(t, 7, 35, 4, 5, skin); Rect(t, 21, 35, 4, 5, skin);
        }

        if (direction == 0)
        {
            Rect(t, 9, 28, 3, 3, Ink); Rect(t, 20, 28, 3, 3, Ink);
            Rect(t, 10, 30, 1, 1, light); Rect(t, 21, 30, 1, 1, light);
            Rect(t, 12, 23, 8, 4, Cream); Rect(t, 15, 25, 2, 2, Ink);
        }
        else if (direction >= 2)
        {
            int eye = direction == 2 ? 9 : 21;
            Rect(t, eye, 28, 2, 3, Ink);
            Rect(t, direction == 2 ? 5 : 23, 23, 4, 4, Cream);
        }
        if (species == CasinoAnimalSpecies.Pig && direction == 0)
        {
            Rect(t, 11, 23, 10, 5, new Color32(231, 118, 134, 255));
            Rect(t, 13, 25, 2, 1, Ink); Rect(t, 18, 25, 2, 1, Ink);
        }
        if (species == CasinoAnimalSpecies.Tiger && direction != 1)
        {
            Rect(t, 7, 31, 3, 2, Ink); Rect(t, 22, 31, 3, 2, Ink);
            Rect(t, 14, 33, 4, 2, Ink);
        }
        if (species == CasinoAnimalSpecies.Fox && direction == 0)
        {
            Rect(t, 7, 22, 4, 3, Cream); Rect(t, 21, 22, 4, 3, Cream);
        }
        if (species == CasinoAnimalSpecies.Raccoon && direction == 0)
        {
            Rect(t, 8, 27, 5, 5, new Color32(73, 69, 84, 255));
            Rect(t, 19, 27, 5, 5, new Color32(73, 69, 84, 255));
            Rect(t, 10, 29, 2, 2, Cream); Rect(t, 20, 29, 2, 2, Cream);
        }
        if (species == CasinoAnimalSpecies.Elephant && direction == 0)
        {
            Rect(t, 14, 22, 4, 7, skin);
            Rect(t, 15, 22, 3, 2, Ink);
        }
        if (species == CasinoAnimalSpecies.Dog)
        {
            Rect(t, 4, 26, 4, 8, new Color32(124, 77, 60, 255));
            Rect(t, 24, 26, 4, 8, new Color32(124, 77, 60, 255));
        }
        if (species == CasinoAnimalSpecies.Penguin)
        {
            Rect(t, 9, 34, 14, 3, Ink);
            Rect(t, 12, 37, 8, 2, Gold);
        }
    }

    private static void DrawStation(Texture2D t, string kind)
    {
        Color32 wood = new Color32(112, 55, 53, 255);
        Color32 felt = new Color32(30, 117, 97, 255);
        Color32 shade = new Color32(18, 68, 66, 255);
        Rect(t, 2, 3, 92, 42, Ink);
        Rect(t, 4, 5, 88, 38, wood);
        Rect(t, 6, 7, 84, 34, Gold);
        Rect(t, 8, 9, 80, 30, felt);
        Rect(t, 10, 11, 76, 2, shade);
        Rect(t, 10, 35, 76, 2, new Color32(81, 162, 119, 255));
        if (kind.StartsWith("Blackjack"))
        {
            for (int x = 20; x <= 68; x += 24)
            {
                Rect(t, x, 14, 12, 2, Gold); Rect(t, x, 16, 2, 7, Gold); Rect(t, x + 10, 16, 2, 7, Gold);
                Rect(t, x + 4, 17, 4, 4, new Color32(76, 158, 118, 255));
            }
            Rect(t, 41, 26, 14, 8, Ink); Rect(t, 42, 27, 12, 6, Cream);
            Rect(t, 45, 28, 2, 3, new Color32(174, 53, 64, 255));
            for (int x = 12; x < 86; x += 8)
                Rect(t, x, 38, 3, 1, new Color32(255, 227, 178, 255));
        }
        else if (kind == "Roulette")
        {
            for (int x = 48; x < 82; x += 6)
                for (int y = 16; y < 32; y += 6)
                    Rect(t, x, y, 5, 5, (x + y) % 12 == 0 ? Ink : new Color32(174, 53, 64, 255));
        }
        else if (kind == "CashDesk")
        {
            Rect(t, 11, 13, 74, 19, wood);
            for (int x = 17; x < 78; x += 11)
            {
                Rect(t, x, 17, 7, 9, Ink);
                Rect(t, x + 1, 18, 5, 7, x % 2 == 0 ? Gold : new Color32(174, 53, 64, 255));
            }
            Rect(t, 32, 29, 32, 5, Cream);
        }
        else if (kind == "PitDesk")
        {
            Rect(t, 10, 12, 76, 23, wood);
            Rect(t, 26, 16, 31, 16, Ink); Rect(t, 28, 18, 27, 12, Cream);
            for (int y = 20; y < 28; y += 3) Rect(t, 31, y, 17, 1, shade);
            Rect(t, 66, 19, 9, 9, Ink); Rect(t, 68, 21, 5, 5, Gold);
        }
        else
        {
            Rect(t, 10, 12, 76, 23, wood);
            Rect(t, 14, 16, 20, 14, Ink); Rect(t, 16, 18, 16, 10, new Color32(151, 51, 74, 255));
            Rect(t, 62, 16, 20, 14, Ink); Rect(t, 64, 18, 16, 10, new Color32(151, 51, 74, 255));
            Rect(t, 40, 18, 16, 12, Ink); Rect(t, 42, 20, 12, 8, Cream);
            Rect(t, 45, 22, 6, 4, wood);
        }
    }

    private static Texture2D NewTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(new Color32[width * height]);
        return texture;
    }

    private static void Rect(Texture2D t, int x, int y, int width, int height, Color32 color)
    {
        for (int px = x; px < x + width; px++)
            for (int py = y; py < y + height; py++)
                t.SetPixel(px, py, color);
    }

    private static void Save(Texture2D t, string path, Vector2 pivot, int pixelsPerUnit)
    {
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Art", "Pixel");
    }
}
