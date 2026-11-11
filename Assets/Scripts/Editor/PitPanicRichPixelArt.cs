using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PitPanicRichPixelArt
{
    private static readonly Color32 Ink = new Color32(35, 20, 43, 255);
    private static readonly Color32 Cream = new Color32(255, 235, 184, 255);
    private static readonly Color32 Gold = new Color32(239, 178, 64, 255);
    private static readonly Color32 Wood = new Color32(117, 52, 69, 255);
    private static readonly Color32 Felt = new Color32(28, 115, 87, 255);
    private static readonly Color32 Plum = new Color32(63, 32, 72, 255);

    [MenuItem("Pit Panic/Upgrade Pixel Art")]
    public static void Generate()
    {
        foreach (CasinoAnimalSpecies species in Enum.GetValues(typeof(CasinoAnimalSpecies)))
        {
            for (int direction = 0; direction < 4; direction++)
                for (int frame = 0; frame < 2; frame++)
                {
                    Texture2D t = New(48, 64);
                    Character(t, species, direction, frame);
                    Save(t, $"Character_{species}_{direction}_{frame}", new Vector2(.5f, 0), 30);
                }
            Sprite[] expressions = new Sprite[4];
            Texture2D atlas = New(48 * 8, 64 * 4 * 13);
            for (int pose = 0; pose < 13; pose++)
            for (int direction = 0; direction < 4; direction++)
            for (int frame = 0; frame < 8; frame++)
            {
                Texture2D drawing = New(48, 64);
                Character(drawing, species, direction, 0);
                AnimatePose(drawing, species, (CasinoAnimationPose)pose, direction, frame);
                atlas.SetPixels(frame * 48, (pose * 4 + direction) * 64, 48, 64, drawing.GetPixels());
                UnityEngine.Object.DestroyImmediate(drawing);
            }
            string atlasPath = $"Assets/Art/Pixel/Animation_{species}.png";
            atlas.Apply();
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(atlasPath);
            TextureImporter atlasImporter = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            atlasImporter.textureType = TextureImporterType.Default;
            atlasImporter.npotScale = TextureImporterNPOTScale.None;
            atlasImporter.isReadable = true;
            atlasImporter.filterMode = FilterMode.Point;
            atlasImporter.mipmapEnabled = false;
            atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
            atlasImporter.maxTextureSize = 4096;
            atlasImporter.SaveAndReimport();
            Texture2D animation = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            for (int i = 0; i < expressions.Length; i++)
            {
                Texture2D t = New(48, 64);
                Character(t, species, 0, 0);
                if (i == 0)
                {
                    R(t, 14, 44, 4, 6, Cream);
                    R(t, 31, 44, 4, 6, Cream);
                    R(t, 14, 46, 4, 2, Ink); R(t, 31, 46, 4, 2, Ink);
                    R(t, 22, 37, 6, 3, Ink); R(t, 23, 37, 4, 1, new Color32(229, 119, 139, 255));
                }
                if (i == 1) { R(t, 13, 50, 5, 1, Ink); R(t, 31, 50, 5, 1, Ink); R(t, 22, 38, 6, 1, Ink); }
                if (i == 2)
                {
                    R(t, 14, 44, 4, 6, Cream); R(t, 31, 44, 4, 6, Cream);
                    R(t, 14, 45, 4, 1, Ink); R(t, 31, 45, 4, 1, Ink);
                    R(t, 38, 47, 2, 4, new Color32(88, 179, 216, 255));
                }
                if (i == 3)
                {
                    R(t, 37, 20, 8, 5, Cream); R(t, 40, 24, 5, 4, Gold);
                    R(t, 20, 38, 9, 1, Ink);
                }
                string name = $"Character_{species}_Expression_{i}";
                Save(t, name, new Vector2(.5f, 0), 30);
                expressions[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Pixel/{name}.png");
            }
            string[] guids = AssetDatabase.FindAssets($"Character_{species}Pixel t:Prefab", new[] { "Assets/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PixelCharacterVisual pixel = root.GetComponent<PixelCharacterVisual>();
                    if (pixel != null)
                    {
                        pixel.ConfigureExpressions(expressions);
                        pixel.ConfigureAnimation(animation);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        foreach (string kind in new[] { "Blackjack", "BlackjackV2", "Roulette", "CashDesk", "PitDesk", "BreakRoom" })
        {
            Texture2D t = New(192, 96);
            Station(t, kind);
            Save(t, "Station_" + kind, new Vector2(.5f, .5f), 32);
        }
        Texture2D floor = New(512, 352);
        Floor(floor);
        Save(floor, "CasinoFloor", new Vector2(.5f, .5f), 16);
        foreach (string kind in new[] { "Plant", "Lamp", "Sofa", "Chair", "Window", "Curtain", "Column", "Arch" })
        {
            Texture2D t = New(kind == "Arch" ? 128 : 64, 96);
            Prop(t, kind);
            Save(t, "Structure_" + kind, new Vector2(.5f, 0), kind == "Arch" ? 40 : 30);
        }
        Texture2D wheel = New(64, 64);
        Wheel(wheel, 32, 32, 30);
        Save(wheel, "RouletteWheel", new Vector2(.5f, .5f), 40);
        Debug.Log("Rich pixel artwork rebuilt with original GUIDs and point filtering.");
    }

    private static void AnimatePose(Texture2D t, CasinoAnimalSpecies species, CasinoAnimationPose pose, int direction, int frame)
    {
        int[] ease = { 0, -1, -2, 1, 4, 6, 3, 0 };
        int reach = ease[frame];
        if (pose == CasinoAnimationPose.Collect)
            reach = new[] { 0, 2, 5, 6, 4, 2, -1, 0 }[frame];
        Color32 sleeve = new Color32(137, 42, 81, 255);
        bool action = pose == CasinoAnimationPose.Deal || pose == CasinoAnimationPose.Pay ||
            pose == CasinoAnimationPose.Collect || pose == CasinoAnimationPose.Shuffle;
        if (pose == CasinoAnimationPose.Walk || pose == CasinoAnimationPose.Carry)
        {
            int[] stride = { 0, 1, 3, 2, 0, -1, -3, -2 };
            R(t, 5, 0, 39, 10, new Color32(0, 0, 0, 0));
            R(t, 10 + stride[frame], 2, 11, 8, Ink);
            R(t, 29 - stride[frame], 3, 11, 7, Ink);
            R(t, 11 + stride[frame], 3, 8, 2, species == CasinoAnimalSpecies.Penguin ? Gold : Wood);
            R(t, 30 - stride[frame], 4, 8, 2, species == CasinoAnimalSpecies.Penguin ? Gold : Wood);
        }
        if (action)
        {
            R(t, 37, 17, 10, 18, new Color32(0, 0, 0, 0));
            int y = 23 + reach;
            R(t, 35, y, 11, 8, Ink); R(t, 36, y + 2, 8, 4, sleeve);
            R(t, 40, y + 2, 6, 4, Cream);
            if ((pose == CasinoAnimationPose.Deal && frame < 5) || pose == CasinoAnimationPose.Shuffle)
            {
                R(t, 39, y + 5, 7, 10, Ink); R(t, 40, y + 6, 5, 8, Cream);
                R(t, 41, y + 11, 2, 2, Wood);
                if (pose == CasinoAnimationPose.Shuffle)
                { R(t, 6, y + 4, 9, 8, Ink); R(t, 7, y + 5, 7, 6, Cream); }
            }
            else if (pose == CasinoAnimationPose.Pay || pose == CasinoAnimationPose.Collect)
            { E(t, 43, y + 6, 4, 2, Gold); R(t, 40, y + 5, 6, 1, Wood); }
        }
        if (pose == CasinoAnimationPose.Carry)
        {
            R(t, 8, 21, 33, 8, Ink); R(t, 10, 23, 29, 5, Gold);
            R(t, 12, 24, 25, 3, Felt);
            for (int x = 15; x < 37; x += 7) { R(t, x, 26, 4, 4, Wood); R(t, x, 29, 4, 1, Cream); }
            R(t, 7, 23, 6, 4, Cream); R(t, 37, 23, 6, 4, Cream);
        }
        if (pose == CasinoAnimationPose.Celebrate)
        {
            int lift = new[] { 0, 2, 5, 9, 11, 8, 4, 1 }[frame];
            R(t, 2, 21 + lift, 8, 13, Ink); R(t, 4, 30 + lift, 5, 5, Cream);
            R(t, 39, 21 + lift, 8, 13, Ink); R(t, 40, 30 + lift, 5, 5, Cream);
            if (direction == 0 && frame >= 2 && frame <= 6)
            { R(t, 21, 37, 8, 4, Ink); R(t, 23, 37, 4, 2, new Color32(238, 136, 157, 255)); }
        }
        if (pose == CasinoAnimationPose.Tired || pose == CasinoAnimationPose.Rest || pose == CasinoAnimationPose.Disappointed)
        {
            int hand = new[] { 0, 1, 3, 5, 6, 4, 2, 0 }[frame];
            if (pose == CasinoAnimationPose.Disappointed)
            {
                R(t, 30, 27 + hand, 10, 5, Cream);
                R(t, 31, 25 + hand, 9, 3, sleeve);
            }
            if (pose == CasinoAnimationPose.Rest)
            {
                R(t, 10, 18 + hand / 2, 12, 4, Cream);
                R(t, 28, 18 + hand / 2, 12, 4, Cream);
            }
            if (direction == 0)
            {
                R(t, 14, 44, 4, 6, Cream); R(t, 31, 44, 4, 6, Cream);
                R(t, 14, 46, 4, 1, Ink); R(t, 31, 46, 4, 1, Ink);
                R(t, 21, 38, 8, 1, Ink);
                if (pose == CasinoAnimationPose.Tired && frame >= 2 && frame <= 5)
                { E(t, 24, 39, 3, 3, Ink); R(t, 30, 38 + reach, 9, 5, Cream); }
            }
            if (pose == CasinoAnimationPose.Rest)
            { R(t, 10, 10, 29, 8, Ink); R(t, 10, 12, 12, 3, Wood); }
        }
        if (pose == CasinoAnimationPose.Examine)
        {
            R(t, 15, 28 + Mathf.Max(0, reach), 18, 12, Ink);
            R(t, 17, 30 + Mathf.Max(0, reach), 6, 8, Cream);
            R(t, 25, 30 + Mathf.Max(0, reach), 6, 8, Cream);
        }
        if (pose == CasinoAnimationPose.Idle && direction == 0 && frame >= 3 && frame <= 4)
        { R(t, 14, 44, 4, 6, Cream); R(t, 31, 44, 4, 6, Cream); R(t, 14, 46, 4, 1, Ink); R(t, 31, 46, 4, 1, Ink); }
        if (pose == CasinoAnimationPose.Idle && direction == 0 && frame >= 5)
        {
            R(t, 14, 44, 4, 6, Cream); R(t, 31, 44, 4, 6, Cream);
            int glance = frame == 5 ? -1 : 1;
            R(t, 15 + glance, 45, 2, 4, Ink); R(t, 32 + glance, 45, 2, 4, Ink);
            R(t, 30, 29, 10, 4, Cream);
        }
        if (pose == CasinoAnimationPose.Walk && direction != 1)
        {
            int swing = new[] { 0, 1, 2, 1, 0, -1, -2, -1 }[frame];
            R(t, 6, 19 + swing, 6, 5, Cream); R(t, 39, 19 - swing, 6, 5, Cream);
        }
        if (pose == CasinoAnimationPose.Examine && frame >= 3 && frame <= 5 && direction == 0)
        {
            R(t, 14, 44, 4, 6, Cream); R(t, 31, 44, 4, 6, Cream);
            R(t, 15, 44, 2, 4, Ink); R(t, 32, 44, 2, 4, Ink);
        }
        if (action && direction == 2)
        {
            Color[] pixels = t.GetPixels();
            for (int y = 17; y < 35; y++)
            for (int x = 0; x < 48; x++) t.SetPixel(x, y, pixels[y * 48 + 47 - x]);
        }
        if (action || pose == CasinoAnimationPose.Celebrate || pose == CasinoAnimationPose.Disappointed)
        {
            int lean = new[] { 0, -1, -2, 0, 2, 3, 1, 0 }[frame];
            if (direction == 2 || pose == CasinoAnimationPose.Collect) lean = -lean;
            int crouch = new[] { 0, 0, -1, -1, 0, 0, 1, 0 }[frame];
            Color[] upper = t.GetPixels();
            R(t, 0, 13, 48, 51, new Color32(0, 0, 0, 0));
            for (int y = 13; y < 64; y++)
            for (int x = 0; x < 48; x++)
                if (x + lean >= 0 && x + lean < 48 && y + crouch >= 13 && y + crouch < 64)
                    t.SetPixel(x + lean, y + crouch, upper[y * 48 + x]);
        }
        if (pose == CasinoAnimationPose.Sit || pose == CasinoAnimationPose.Rest || pose == CasinoAnimationPose.Examine)
        {
            Color[] pixels = t.GetPixels();
            int drop = pose == CasinoAnimationPose.Sit ? new[] { 0, 0, 1, 3, 5, 7, 8, 8 }[frame] : 8;
            t.SetPixels(new Color[48 * 64]);
            for (int y = drop; y < 64; y++)
            for (int x = 0; x < 48; x++) t.SetPixel(x, y - drop, pixels[y * 48 + x]);
            R(t, 10, 3, 29, 6, Ink);
        }
    }

    private static void Character(Texture2D t, CasinoAnimalSpecies species, int dir, int step)
    {
        Color32[] furs =
        {
            new Color32(100, 119, 159, 255), new Color32(237, 159, 66, 255),
            new Color32(241, 163, 179, 255), new Color32(232, 132, 57, 255),
            new Color32(218, 221, 229, 255), new Color32(165, 118, 87, 255),
            new Color32(184, 151, 118, 255), new Color32(221, 173, 106, 255),
            new Color32(147, 139, 163, 255), new Color32(160, 179, 190, 255)
        };
        Color32 fur = furs[(int)species % furs.Length];
        Color32 shadow = Shade(fur, .7f);
        Color32 coat = species switch
        {
            CasinoAnimalSpecies.Rabbit => new Color32(55, 119, 119, 255),
            CasinoAnimalSpecies.Bear => new Color32(67, 89, 144, 255),
            CasinoAnimalSpecies.Cat => new Color32(140, 78, 157, 255),
            CasinoAnimalSpecies.Dog => new Color32(159, 95, 44, 255),
            CasinoAnimalSpecies.Raccoon => new Color32(49, 114, 82, 255),
            CasinoAnimalSpecies.Elephant => new Color32(62, 109, 147, 255),
            _ => new Color32(137, 42, 81, 255)
        };
        R(t, 9, 2 + step * 2, 13, 8, Ink); R(t, 28, 4 - step * 2, 12, 8, Ink);
        R(t, 11, 5 + step * 2, 8, 2, new Color32(96, 82, 101, 255));
        R(t, 30, 7 - step * 2, 7, 2, new Color32(96, 82, 101, 255));
        R(t, 12, 10, 26, 9, Ink); R(t, 14, 12, 22, 5, new Color32(67, 53, 81, 255));
        R(t, 9, 17, 32, 19, Ink); R(t, 11, 19, 28, 15, coat);
        R(t, 12, 20, 3, 12, Shade(coat, 1.4f)); R(t, 35, 19, 4, 14, Shade(coat, .65f));
        R(t, 5, 18 + step, 7, 15, Ink); R(t, 39, 18 - step, 6, 15, Ink);
        R(t, 7, 20 + step, 4, 5, fur); R(t, 40, 20 - step, 3, 5, fur);
        R(t, 7, 26, 4, 6, Cream); R(t, 40, 25, 3, 7, Cream);
        if (dir != 1)
        {
            R(t, 20, 20, 8, 14, Cream);
            R(t, 16, 30, 5, 3, Gold); R(t, 27, 30, 5, 3, Gold);
            R(t, 23, 28, 3, 4, coat); R(t, 23, 22, 2, 2, Gold);
        }
        else R(t, 15, 29, 21, 2, Shade(coat, 1.4f));
        // Stepped cheeks and tapered ears, rather than square heads.
        R(t, 9, 35, 31, 19, Ink); R(t, 6, 39, 37, 11, Ink);
        R(t, 11, 36, 27, 17, fur); R(t, 8, 40, 33, 9, fur);
        R(t, 11, 36, 27, 3, shadow); R(t, 36, 40, 4, 10, shadow);
        R(t, 13, 51, 18, 2, Shade(fur, 1.16f));
        int earHeight = species == CasinoAnimalSpecies.Rabbit ? 11 : 8;
        R(t, 9, 52, 9, earHeight, Ink); R(t, 31, 52, 9, earHeight, Ink);
        R(t, 11, 54, 5, earHeight - 3, fur); R(t, 33, 54, 5, earHeight - 3, fur);
        R(t, 12, 54, 3, 3, new Color32(194, 104, 132, 255));
        R(t, 34, 54, 3, 3, new Color32(194, 104, 132, 255));
        if (dir == 0)
        {
            R(t, 11, 37, 8, 6, Cream); R(t, 30, 37, 8, 6, Cream);
            R(t, 17, 36, 14, 7, Cream);
            R(t, 14, 44, 4, 6, Ink); R(t, 31, 44, 4, 6, Ink);
            R(t, 15, 48, 1, 2, Cream); R(t, 32, 48, 1, 2, Cream);
            R(t, 23, 41, 4, 3, Ink); R(t, 22, 38, 6, 1, Ink);
            R(t, 10, 42, 4, 2, new Color32(227, 118, 132, 255));
            R(t, 35, 42, 4, 2, new Color32(227, 118, 132, 255));
        }
        else if (dir > 1)
        {
            int x = dir == 2 ? 12 : 33;
            R(t, x, 45, 3, 5, Ink); R(t, x, 49, 1, 1, Cream);
            R(t, dir == 2 ? 5 : 36, 38, 9, 6, Cream);
            R(t, dir == 2 ? 5 : 41, 41, 2, 2, Ink);
        }
        else
        {
            R(t, 14, 38, 4, 6, shadow); R(t, 30, 38, 4, 6, shadow);
            R(t, 21, 35, 8, 3, shadow);
        }
        if (species == CasinoAnimalSpecies.Tiger)
        {
            R(t, 21, 52, 7, 2, Ink); R(t, 10, 47, 3, 2, Ink); R(t, 37, 47, 3, 2, Ink);
        }
        if (species == CasinoAnimalSpecies.Pig && dir == 0)
        {
            R(t, 18, 38, 14, 6, new Color32(211, 110, 147, 255));
            R(t, 21, 40, 2, 2, Ink); R(t, 27, 40, 2, 2, Ink);
        }
        if (species == CasinoAnimalSpecies.Raccoon && dir == 0)
        {
            R(t, 11, 43, 9, 7, Shade(fur, .5f)); R(t, 29, 43, 9, 7, Shade(fur, .5f));
            R(t, 15, 46, 2, 3, Cream); R(t, 32, 46, 2, 3, Cream);
        }
        if (species == CasinoAnimalSpecies.Elephant && dir == 0)
        {
            R(t, 21, 34, 8, 10, shadow); R(t, 22, 36, 5, 9, fur); R(t, 24, 35, 4, 2, Cream);
        }
        if (species == CasinoAnimalSpecies.Penguin)
        {
            R(t, 0, 35, 48, 29, new Color32(0, 0, 0, 0));
            E(t, 24, 46, 17, 15, Ink);
            E(t, 24, 47, 14, 12, new Color32(56, 70, 98, 255));
            R(t, 13, 55, 19, 2, new Color32(94, 111, 141, 255));
            if (dir == 0)
            {
                E(t, 18, 45, 7, 9, Cream); E(t, 30, 45, 7, 9, Cream);
                R(t, 14, 44, 4, 6, Ink); R(t, 31, 44, 4, 6, Ink);
                R(t, 15, 48, 1, 2, Cream); R(t, 32, 48, 1, 2, Cream);
                R(t, 20, 39, 10, 5, Gold); R(t, 22, 38, 6, 2, new Color32(216, 115, 46, 255));
            }
            else if (dir > 1)
            {
                int x = dir == 2 ? 14 : 29;
                E(t, x + 2, 45, 6, 9, Cream);
                R(t, x, 45, 3, 5, Ink);
                R(t, dir == 2 ? 5 : 35, 39, 8, 4, Gold);
            }
            R(t, 10, 2 + step * 2, 11, 3, Gold);
            R(t, 29, 4 - step * 2, 11, 3, Gold);
        }
        if (species == CasinoAnimalSpecies.Dog)
        {
            R(t, 6, 41, 7, 15, Ink); R(t, 8, 43, 4, 11, shadow);
            R(t, 37, 41, 7, 15, Ink); R(t, 38, 43, 4, 11, shadow);
        }
        if (species == CasinoAnimalSpecies.Rabbit && dir != 1)
        {
            R(t, 12, 56, 3, 6, new Color32(229, 151, 172, 255));
            R(t, 34, 56, 3, 6, new Color32(229, 151, 172, 255));
        }
        if (species == CasinoAnimalSpecies.Bear)
        {
            E(t, 13, 56, 5, 5, Ink); E(t, 13, 56, 3, 3, shadow);
            E(t, 35, 56, 5, 5, Ink); E(t, 35, 56, 3, 3, shadow);
        }
    }

    private static void Station(Texture2D t, string kind)
    {
        if (kind == "CashDesk" || kind == "PitDesk" || kind == "BreakRoom")
        {
            R(t, 8, 5, 177, 82, Ink); R(t, 11, 8, 171, 75, Wood);
            R(t, 14, 10, 165, 23, Shade(Felt, .75f));
            R(t, 14, 34, 165, 3, Gold);
            for (int x = 16; x < 178; x += 33) { R(t, x, 12, 2, 21, Gold); R(t, x + 4, 13, 25, 1, Shade(Felt, 1.3f)); }
            R(t, 6, 38, 181, 7, Ink); R(t, 9, 40, 176, 5, Gold);
            R(t, 14, 46, 163, 33, Shade(Wood, .8f));
            if (kind == "CashDesk")
            {
                for (int x = 18; x < 177; x += 18) { R(t, x, 50, 3, 29, Gold); R(t, x + 3, 52, 2, 27, Shade(Gold, .6f)); }
                R(t, 55, 52, 31, 24, Ink); R(t, 58, 56, 25, 17, new Color32(110, 167, 155, 255));
                R(t, 60, 59, 20, 2, Cream);
                ChipStack(t, 37, 52, new Color32(203, 67, 87, 255), 4);
                ChipStack(t, 133, 51, Gold, 5);
                R(t, 9, 82, 176, 4, Gold);
            }
            else if (kind == "PitDesk")
            {
                R(t, 65, 50, 38, 27, Ink); R(t, 69, 56, 30, 18, new Color32(88, 160, 141, 255));
                R(t, 73, 58, 23, 2, Cream); R(t, 73, 63, 17, 1, Cream);
                R(t, 111, 49, 22, 26, Cream);
                for (int y = 53; y < 72; y += 4) R(t, 115, y, 14, 1, Shade(Wood, .7f));
                R(t, 149, 49, 14, 16, Gold); R(t, 151, 62, 10, 13, Cream);
                R(t, 17, 53, 16, 3, Cream); R(t, 23, 55, 4, 22, Gold);
                E(t, 25, 77, 17, 7, Gold);
            }
            else
            {
                Sofa(t, 14, 48, 52); Sofa(t, 125, 48, 52);
                R(t, 78, 49, 33, 24, Ink); R(t, 80, 52, 29, 19, Gold);
                R(t, 88, 57, 8, 7, Cream); R(t, 96, 59, 3, 3, Cream);
            }
            return;
        }
        // Rounded felt, a padded rail, wood aprons and visible feet.
        E(t, 96, 36, 89, 31, Ink); E(t, 96, 41, 88, 30, Wood);
        for (int x = 26; x < 179; x += 43) { R(t, x, 7, 8, 18, Ink); R(t, x + 2, 9, 3, 14, Wood); }
        E(t, 96, 51, 91, 36, Ink); E(t, 96, 53, 87, 32, Gold);
        E(t, 96, 55, 83, 28, Shade(Gold, .7f)); E(t, 96, 57, 80, 26, Felt);
        E(t, 96, 64, 72, 15, Shade(Felt, 1.14f));
        R(t, 40, 31, 112, 2, Shade(Felt, .7f));
        if (kind.StartsWith("Blackjack"))
        {
            for (int x = 46; x <= 134; x += 44)
            {
                R(t, x - 9, 42, 18, 1, Gold); R(t, x - 9, 43, 1, 9, Gold); R(t, x + 8, 43, 1, 9, Gold);
                Card(t, x - 2, 48, true); Card(t, x + 9, 51, false);
            }
            ChipStack(t, 25, 65, new Color32(200, 58, 79, 255), 3);
            ChipStack(t, 39, 67, new Color32(55, 141, 199, 255), 3);
            ChipStack(t, 151, 67, Gold, 4);
            R(t, 163, 60, 13, 17, Ink); R(t, 165, 64, 9, 10, Wood); R(t, 165, 73, 9, 2, Cream);
            R(t, 84, 69, 30, 8, Ink);
            for (int x = 87; x < 113; x += 6) R(t, x, 71, 4, 4, Gold);
        }
        else
        {
            Wheel(t, 51, 58, 24);
            for (int x = 96; x <= 159; x += 8)
                for (int y = 47; y <= 71; y += 8)
                {
                    R(t, x, y, 8, 8, Gold);
                    R(t, x + 1, y + 1, 6, 6, ((x + y) / 8) % 2 == 0 ? Ink : new Color32(181, 52, 78, 255));
                    R(t, x + 3, y + 3, 2, 2, Cream);
                }
            ChipStack(t, 83, 44, new Color32(213, 59, 73, 255), 4);
            ChipStack(t, 167, 51, new Color32(75, 145, 207, 255), 4);
        }
    }

    private static void Floor(Texture2D t)
    {
        R(t, 0, 0, 512, 352, Plum);
        for (int x = 0; x < 512; x += 24)
            for (int y = 0; y < 352; y += 24)
            {
                if ((x / 24 + y / 24) % 2 == 0) R(t, x, y, 24, 24, new Color32(69, 35, 79, 255));
                R(t, x + 10, y + 7, 4, 10, new Color32(94, 43, 86, 255));
                R(t, x + 7, y + 10, 10, 4, new Color32(94, 43, 86, 255));
                R(t, x + 10, y + 13, 4, 1, new Color32(113, 51, 94, 255));
            }
        R(t, 0, 291, 512, 61, new Color32(43, 25, 57, 255));
        for (int y = 294; y < 352; y += 15)
        {
            R(t, 4, y, 504, 1, new Color32(74, 43, 86, 255));
            for (int x = y % 30; x < 512; x += 48) R(t, x, y, 1, 15, new Color32(74, 43, 86, 255));
        }
        R(t, 0, 288, 512, 3, Gold); R(t, 0, 284, 512, 4, Wood);
        foreach (Vector2Int center in new[] { new Vector2Int(157, 186), new Vector2Int(355, 186), new Vector2Int(256, 278) })
        {
            E(t, center.x, center.y, 58, 27, new Color32(141, 74, 101, 255));
            E(t, center.x, center.y, 56, 25, new Color32(84, 39, 73, 255));
            E(t, center.x, center.y, 51, 21, new Color32(119, 50, 87, 255));
            for (int x = center.x - 38; x <= center.x + 38; x += 19)
                R(t, x, center.y - 15, 3, 3, Gold);
        }
        R(t, 226, 5, 60, 76, new Color32(119, 32, 75, 255));
        R(t, 230, 5, 2, 76, Gold); R(t, 280, 5, 2, 76, Gold);
        R(t, 4, 4, 504, 2, Gold); R(t, 4, 346, 504, 2, Gold);
        R(t, 4, 4, 2, 344, Gold); R(t, 506, 4, 2, 344, Gold);
        for (int x = 18; x < 500; x += 32) R(t, x, 8, 3, 1, Cream);
    }

    private static void Prop(Texture2D t, string kind)
    {
        if (kind == "Chair")
        {
            R(t, 15, 10, 5, 34, Ink); R(t, 43, 10, 5, 34, Ink);
            R(t, 16, 11, 2, 25, Gold); R(t, 44, 11, 2, 25, Gold);
            R(t, 11, 30, 42, 16, Ink); R(t, 14, 33, 36, 11, new Color32(180, 59, 108, 255));
            R(t, 15, 41, 34, 34, Ink); R(t, 18, 44, 28, 28, Gold);
            R(t, 20, 46, 24, 24, new Color32(119, 32, 75, 255));
            R(t, 22, 65, 20, 3, new Color32(204, 86, 132, 255));
            R(t, 25, 55, 14, 2, new Color32(150, 51, 95, 255));
            return;
        }
        if (kind == "Plant")
        {
            R(t, 18, 1, 28, 25, Ink); R(t, 20, 3, 24, 21, Wood); R(t, 20, 21, 24, 4, Gold);
            R(t, 23, 4, 4, 17, Shade(Wood, 1.4f)); R(t, 30, 24, 3, 42, Ink);
            for (int i = 0; i < 7; i++)
            {
                int x = i % 2 == 0 ? 15 + i : 42 - i;
                int y = 31 + i * 7;
                E(t, x, y, 14, 5, Ink); E(t, x, y + 1, 12, 4, new Color32((byte)(46 + i * 4), (byte)(113 + i * 4), 80, 255));
                R(t, Mathf.Min(x, 32), y, Mathf.Abs(x - 32) + 2, 2, new Color32(99, 166, 93, 255));
            }
        }
        else if (kind == "Sofa") Sofa(t, 3, 10, 58);
        else if (kind == "Lamp")
        {
            E(t, 32, 6, 20, 4, Ink); E(t, 32, 8, 18, 4, Gold);
            R(t, 29, 9, 6, 49, Ink); R(t, 30, 11, 3, 45, Gold);
            E(t, 32, 69, 23, 16, new Color32(124, 59, 75, 120));
            E(t, 32, 68, 17, 11, Gold); E(t, 32, 69, 12, 8, Cream);
            R(t, 24, 76, 16, 3, Gold); R(t, 24, 59, 16, 3, Gold);
        }
        else if (kind == "Window")
        {
            R(t, 2, 10, 60, 73, Ink); R(t, 4, 12, 56, 69, Wood);
            R(t, 8, 16, 48, 61, new Color32(27, 40, 89, 255));
            for (int x = 10; x < 55; x += 9)
            {
                int height = 10 + x % 19;
                R(t, x, 16, 7, height, new Color32(52, 44, 92, 255));
                for (int y = 19; y < 16 + height; y += 5) R(t, x + 2, y, 2, 2, Gold);
            }
            R(t, 30, 16, 4, 61, Wood); R(t, 8, 45, 48, 4, Wood);
            R(t, 3, 9, 59, 4, Gold); R(t, 13, 65, 2, 2, Cream); R(t, 43, 72, 1, 1, Cream);
        }
        else if (kind == "Curtain")
        {
            for (int y = 14; y < 94; y++)
            {
                int w = 13 + (y - 14) / 2;
                R(t, 0, y, w, 1, new Color32((byte)(116 + y % 12), 25, 67, 255));
                for (int x = 4; x < w; x += 10) R(t, x, y, 3, 1, new Color32(158, 38, 79, 255));
            }
            R(t, 0, 94, 62, 2, Gold); R(t, 1, 24, 17, 3, Gold);
        }
        else
        {
            int w = t.width;
            R(t, 6, 0, 15, 85, Ink); R(t, 9, 3, 9, 77, Wood); R(t, 10, 5, 3, 73, Shade(Wood, 1.5f));
            R(t, w - 21, 0, 15, 85, Ink); R(t, w - 18, 3, 9, 77, Wood);
            R(t, 3, 4, 23, 4, Gold); R(t, w - 26, 4, 23, 4, Gold);
            R(t, 2, 82, w - 4, 12, Ink); R(t, 4, 84, w - 8, 8, Gold);
            R(t, 9, 86, w - 18, 4, Wood);
        }
    }

    private static void Sofa(Texture2D t, int x, int y, int w)
    {
        R(t, x, y, w, 28, Ink); R(t, x + 2, y + 2, w - 4, 24, new Color32(142, 39, 77, 255));
        R(t, x + 5, y + 5, w - 10, 8, new Color32(193, 65, 99, 255));
        R(t, x + 7, y + 17, w - 14, 7, new Color32(172, 56, 90, 255));
        R(t, x + w / 2, y + 5, 2, 20, Shade(Wood, .7f));
        R(t, x, y + 2, 5, 16, Gold); R(t, x + w - 5, y + 2, 5, 16, Gold);
        R(t, x + 5, y - 4, 4, 4, Ink); R(t, x + w - 9, y - 4, 4, 4, Ink);
    }

    private static void ChipStack(Texture2D t, int x, int y, Color32 color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            E(t, x, y + i * 3, 5, 3, Ink); E(t, x, y + i * 3 + 1, 4, 2, color);
            R(t, x - 1, y + i * 3 + 2, 2, 1, Cream);
        }
    }

    private static void Card(Texture2D t, int x, int y, bool red)
    {
        R(t, x, y, 10, 14, Ink); R(t, x + 1, y + 1, 8, 12, Cream);
        Color32 ink = red ? new Color32(189, 47, 72, 255) : Ink;
        R(t, x + 2, y + 10, 2, 2, ink); R(t, x + 4, y + 5, 3, 3, ink);
    }

    private static void Wheel(Texture2D t, int cx, int cy, int r)
    {
        for (int x = cx - r; x <= cx + r; x++)
            for (int y = cy - r; y <= cy + r; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (d > r) continue;
                int sector = Mathf.FloorToInt((Mathf.Atan2(y - cy, x - cx) + Mathf.PI) * 37 / (2 * Mathf.PI));
                int number = CasinoPixelArt.WheelOrder[Mathf.Clamp(sector, 0, 36)];
                Color32 c = d > r - 2 ? Ink : d > r - 4 ? Gold : d > r - 7 ? Wood :
                    d > r * .42f ? number == 0 ? Felt : EuropeanRouletteRules.IsRed(number) ?
                        new Color32(182, 48, 79, 255) : Ink : Wood;
                if (d < 4) c = Gold;
                R(t, x, y, 1, 1, c);
            }
        R(t, cx - 2, cy - 2, 5, 9, Gold); R(t, cx - 1, cy + 5, 3, 3, Cream);
        R(t, cx + r - 8, cy + 4, 3, 3, Cream);
    }

    private static Color32 Shade(Color32 c, float scale) => new Color32(
        (byte)Mathf.Min(255, c.r * scale), (byte)Mathf.Min(255, c.g * scale), (byte)Mathf.Min(255, c.b * scale), c.a);
    private static Texture2D New(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(new Color32[w * h]); return t;
    }
    private static void R(Texture2D t, int x, int y, int w, int h, Color32 c)
    {
        for (int px = Mathf.Max(0, x); px < Mathf.Min(t.width, x + w); px++)
            for (int py = Mathf.Max(0, y); py < Mathf.Min(t.height, y + h); py++) t.SetPixel(px, py, c);
    }
    private static void E(Texture2D t, int x, int y, int rx, int ry, Color32 c)
    {
        for (int px = -rx; px <= rx; px++)
            for (int py = -ry; py <= ry; py++)
                if (px * px / (float)(rx * rx) + py * py / (float)(ry * ry) <= 1) R(t, x + px, y + py, 1, 1, c);
    }
    private static void Save(Texture2D t, string name, Vector2 pivot, int ppu)
    {
        string path = $"Assets/Art/Pixel/{name}.png";
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
