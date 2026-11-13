using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PitPanicRichPixelArt
{
    private const int AnimationFrames = PixelCharacterVisual.AnimationFramesPerDirection;
    private static readonly Color32 Ink = new Color32(35, 20, 43, 255);
    private static readonly Color32 Cream = new Color32(255, 235, 184, 255);
    private static readonly Color32 Gold = new Color32(239, 178, 64, 255);
    private static readonly Color32 Wood = new Color32(117, 52, 69, 255);
    private static readonly Color32 Felt = new Color32(28, 115, 87, 255);
    private static readonly Color32 Plum = new Color32(63, 32, 72, 255);

    public static void GenerateStations()
    {
        foreach (string kind in new[] { "Blackjack", "BlackjackV2", "Roulette", "CashDesk", "PitDesk", "BreakRoom" })
            GenerateStation(kind);
        AssetDatabase.SaveAssets();
    }

    private static void GenerateStation(string kind)
    {
        Texture2D t = New(192, 96);
        Station(t, kind);
        Save(t, "Station_" + kind, new Vector2(.5f, .5f), 32);
    }

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
            Texture2D atlas = New(48 * AnimationFrames, 64 * 4 * PixelCharacterVisual.PoseCount);
            Texture2D customerAtlas = New(48 * AnimationFrames, 64 * 4 * PixelCharacterVisual.PoseCount);
            Texture2D vipAtlas = New(48 * AnimationFrames, 64 * 4 * PixelCharacterVisual.PoseCount);
            for (int pose = 0; pose < PixelCharacterVisual.PoseCount; pose++)
            for (int direction = 0; direction < 4; direction++)
            for (int frame = 0; frame < AnimationFrames; frame++)
            {
                Texture2D drawing = New(48, 64);
                Character(drawing, species, direction, 0);
                AnimatePose(drawing, species, (CasinoAnimationPose)pose, direction, frame);
                atlas.SetPixels(frame * 48, (pose * 4 + direction) * 64, 48, 64, drawing.GetPixels());
                drawing.SetPixels32(new Color32[48 * 64]);
                Character(drawing, species, direction, 0);
                DressCustomer(drawing, species, direction);
                AnimatePose(drawing, species, (CasinoAnimationPose)pose, direction, frame, true);
                customerAtlas.SetPixels(frame * 48, (pose * 4 + direction) * 64, 48, 64, drawing.GetPixels());
                drawing.SetPixels32(new Color32[48 * 64]);
                Character(drawing, species, direction, 0);
                DressVip(drawing, direction);
                AnimatePose(drawing, species, (CasinoAnimationPose)pose, direction, frame, true, true);
                vipAtlas.SetPixels(frame * 48, (pose * 4 + direction) * 64, 48, 64, drawing.GetPixels());
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
            string customerPath = $"Assets/Art/Pixel/Animation_Customer_{species}.png";
            customerAtlas.Apply();
            File.WriteAllBytes(customerPath, customerAtlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(customerAtlas);
            AssetDatabase.ImportAsset(customerPath);
            TextureImporter customerImporter = (TextureImporter)AssetImporter.GetAtPath(customerPath);
            customerImporter.textureType = TextureImporterType.Default;
            customerImporter.npotScale = TextureImporterNPOTScale.None;
            customerImporter.isReadable = true;
            customerImporter.filterMode = FilterMode.Point;
            customerImporter.mipmapEnabled = false;
            customerImporter.textureCompression = TextureImporterCompression.Uncompressed;
            customerImporter.maxTextureSize = 4096;
            customerImporter.SaveAndReimport();
            Texture2D customerAnimation = AssetDatabase.LoadAssetAtPath<Texture2D>(customerPath);
            string vipPath = $"Assets/Art/Pixel/Animation_VIP_{species}.png";
            vipAtlas.Apply();
            File.WriteAllBytes(vipPath, vipAtlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(vipAtlas);
            AssetDatabase.ImportAsset(vipPath);
            TextureImporter vipImporter = (TextureImporter)AssetImporter.GetAtPath(vipPath);
            vipImporter.textureType = TextureImporterType.Default;
            vipImporter.npotScale = TextureImporterNPOTScale.None;
            vipImporter.isReadable = true;
            vipImporter.filterMode = FilterMode.Point;
            vipImporter.mipmapEnabled = false;
            vipImporter.textureCompression = TextureImporterCompression.Uncompressed;
            vipImporter.maxTextureSize = 4096;
            vipImporter.SaveAndReimport();
            Texture2D vipAnimation = AssetDatabase.LoadAssetAtPath<Texture2D>(vipPath);
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
                        pixel.ConfigureCustomerAnimation(customerAnimation);
                        pixel.ConfigureVipAnimation(vipAnimation);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        foreach (string kind in new[] { "Blackjack", "BlackjackV2", "Roulette", "CashDesk", "PitDesk", "BreakRoom" })
            GenerateStation(kind);
        Texture2D floor = New(512, 352);
        Floor(floor);
        Save(floor, "CasinoFloor", new Vector2(.5f, .5f), 16);
        Texture2D rug = New(192, 96);
        E(rug, 96, 48, 94, 44, new Color32(141, 74, 101, 255));
        E(rug, 96, 48, 91, 41, new Color32(84, 39, 73, 255));
        E(rug, 96, 48, 84, 35, new Color32(119, 50, 87, 255));
        for (int x = 36; x <= 156; x += 24) R(rug, x, 20, 3, 3, Gold);
        Save(rug, "TableRug", new Vector2(.5f, .5f), 24);
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

    private static Color32 CustomerShirt(CasinoAnimalSpecies species) => ((int)species % 4) switch
    {
        0 => new Color32(70, 171, 177, 255),
        1 => new Color32(227, 151, 68, 255),
        2 => new Color32(150, 103, 195, 255),
        _ => new Color32(86, 172, 102, 255)
    };

    private static void DressCustomer(Texture2D t, CasinoAnimalSpecies species, int direction)
    {
        Color32 shirt = CustomerShirt(species);
        R(t, 11, 19, 28, 15, shirt);
        R(t, 12, 20, 3, 12, Shade(shirt, 1.2f));
        R(t, 35, 19, 4, 14, Shade(shirt, .7f));
        R(t, 7, 24, 4, 8, shirt); R(t, 40, 24, 3, 8, shirt);
        if (direction != 1)
        {
            R(t, 20, 30, 10, 4, Cream);
            R(t, 22, 28, 6, 5, shirt);
            R(t, 14, 23, 20, 2, Shade(shirt, 1.35f));
        }
        if ((int)species % 2 == 0) R(t, 14, 26, 20, 2, Shade(shirt, .75f));
    }

    private static void DressVip(Texture2D t, int direction)
    {
        Color32 velvet = new Color32(47, 42, 69, 255);
        R(t, 11, 19, 28, 16, velvet);
        R(t, 12, 19, 2, 15, Gold); R(t, 36, 19, 2, 15, Gold);
        R(t, 14, 19, 22, 2, Gold);
        if (direction != 1)
        {
            R(t, 21, 21, 8, 14, Cream);
            for (int i = 0; i < 7; i++)
            {
                R(t, 14 + i, 33 - i, 3, 2, Gold);
                R(t, 32 - i, 33 - i, 3, 2, Gold);
            }
            R(t, 21, 30, 3, 3, Wood); R(t, 26, 30, 3, 3, Wood);
            R(t, 31, 25, 4, 2, Cream);
        }
        R(t, 8, 54, 33, 3, Ink);
        R(t, 14, 55, 22, 9, Ink);
        R(t, 16, 57, 18, 7, velvet);
        R(t, 15, 57, 20, 2, Gold);
        R(t, 17, 60, 2, 4, Cream);
    }

    private static void AnimatePose(Texture2D t, CasinoAnimalSpecies species, CasinoAnimationPose pose, int direction, int frame, bool customer = false, bool vip = false)
    {
        float time = frame / (float)(AnimationFrames - 1);
        float cycle = Mathf.Sin(time * Mathf.PI * 2);
        float breath = (1 - Mathf.Cos(time * Mathf.PI * 2)) * .5f;
        bool locomotion = pose == CasinoAnimationPose.Walk || pose == CasinoAnimationPose.Carry;
        bool seated = pose == CasinoAnimationPose.Sit || pose == CasinoAnimationPose.Rest ||
            pose == CasinoAnimationPose.Examine || pose == CasinoAnimationPose.SeatedIdle ||
            customer && (pose == CasinoAnimationPose.Anticipate || pose == CasinoAnimationPose.Present);
        bool presenting = pose == CasinoAnimationPose.Present;
        bool working = pose == CasinoAnimationPose.Deal || pose == CasinoAnimationPose.Pay ||
            pose == CasinoAnimationPose.Collect || pose == CasinoAnimationPose.Shuffle;

        // Authored beats: anticipate, reach, contact, follow-through, recover.
        float anticipate = Beat(time, 0, .13f, .22f, .34f);
        float reach = Beat(time, .18f, .48f, .62f, 1f);
        float release = Beat(time, .46f, .63f, .72f, .94f);
        float sit = seated ? pose == CasinoAnimationPose.Sit ? Ease(time, .08f, .85f) : 1 : 0;
        float stride = locomotion ? cycle : 0;
        int bodyY = Mathf.RoundToInt((locomotion ? Mathf.Abs(cycle) * 1.4f : breath) - sit * 3);
        int headY = bodyY;
        int lean = working ? Mathf.RoundToInt(-anticipate + reach * 2) : 0;
        if (pose == CasinoAnimationPose.Idle || pose == CasinoAnimationPose.SeatedIdle) lean = Mathf.RoundToInt(cycle);
        if (direction == 2) lean = -lean;

        Vector2 leftHand = new Vector2(8, 23 + stride * 3 + bodyY);
        Vector2 rightHand = new Vector2(40, 23 - stride * 3 + bodyY);
        if (working)
        {
            float contact = pose == CasinoAnimationPose.Collect ? 1 - reach : reach;
            Vector2 rest = new Vector2(40, 23 + bodyY);
            Vector2 extended = direction == 1 ? new Vector2(40, 44 + bodyY) :
                new Vector2(43, 32 + bodyY);
            rightHand = Vector2.Lerp(rest + Vector2.down * anticipate * 2, extended, contact);
            if (pose == CasinoAnimationPose.Collect)
                rightHand = Vector2.Lerp(rest, extended, Beat(time, .03f, .27f, .4f, .94f));
            if (pose == CasinoAnimationPose.Pay)
                leftHand = Vector2.Lerp(leftHand, new Vector2(18, 29 + bodyY), reach);
            if (pose == CasinoAnimationPose.Shuffle)
            {
                float riffle = Mathf.Sin(Mathf.Clamp01((time - .36f) / .32f) * Mathf.PI * 6) * release;
                leftHand = Vector2.Lerp(leftHand, new Vector2(20, 32 + riffle + bodyY), reach);
                rightHand = Vector2.Lerp(rightHand, new Vector2(29, 32 - riffle + bodyY), reach);
            }
        }
        if (pose == CasinoAnimationPose.Carry)
        {
            leftHand = new Vector2(10, (direction == 1 ? 39 : 25) + Mathf.Abs(cycle));
            rightHand = new Vector2(38, leftHand.y);
        }
        if (pose == CasinoAnimationPose.Celebrate)
        {
            float cheer = Beat(time, .08f, .4f, .65f, 1);
            bodyY -= Mathf.RoundToInt(anticipate * 2);
            headY = bodyY;
            leftHand = Vector2.Lerp(leftHand, new Vector2(7, 49), cheer);
            rightHand = Vector2.Lerp(rightHand, new Vector2(41, 49), cheer);
        }
        if (pose == CasinoAnimationPose.Disappointed || pose == CasinoAnimationPose.Tired)
        {
            float sigh = Beat(time, .08f, .4f, .65f, 1);
            headY -= Mathf.RoundToInt(sigh * 3);
            rightHand = Vector2.Lerp(rightHand, new Vector2(33, 39 + bodyY), sigh);
        }
        if (pose == CasinoAnimationPose.Examine)
        {
            float peek = Beat(time, .12f, .38f, .62f, .92f);
            leftHand = new Vector2(16, 25 + peek + bodyY);
            rightHand = new Vector2(33, leftHand.y);
            headY -= Mathf.RoundToInt(peek);
        }
        if (pose == CasinoAnimationPose.Rest)
        {
            leftHand = new Vector2(19, 22 + bodyY + breath * 2);
            rightHand = new Vector2(30, 22 + bodyY + breath * 2);
            headY -= 2 + Mathf.RoundToInt(breath * 2);
        }
        if (pose == CasinoAnimationPose.Anticipate)
        {
            // Lean in: settle back a touch, then commit forward and hold with hands on the rail.
            float into = Beat(time, .1f, .32f, .72f, 1f);
            bodyY -= Mathf.RoundToInt(anticipate);
            if (customer)
            {
                headY = bodyY - Mathf.RoundToInt(into * 2);
                leftHand = Vector2.Lerp(leftHand, new Vector2(15, 29 + bodyY), into);
                rightHand = Vector2.Lerp(rightHand, new Vector2(33, 29 + bodyY), into);
            }
            else
            {
                // Staff acknowledge with a single nod and a small raised palm.
                headY = bodyY - Mathf.RoundToInt(Beat(time, .18f, .34f, .42f, .62f) * 3);
                rightHand = Vector2.Lerp(rightHand, new Vector2(39, 31 + bodyY), into);
            }
        }
        if (presenting)
        {
            // Offer: wind up, push the hand across, hold the contact, then a grateful nod.
            float offer = Beat(time, .14f, .42f, .64f, .95f);
            bodyY -= Mathf.RoundToInt(anticipate);
            headY = bodyY - Mathf.RoundToInt(Beat(time, .5f, .6f, .66f, .82f) * 2);
            Vector2 extended = direction == 1 ? new Vector2(39, 44 + bodyY) : new Vector2(44, 33 + bodyY);
            rightHand = Vector2.Lerp(new Vector2(38, 23 + bodyY), extended, offer);
            leftHand = Vector2.Lerp(leftHand, new Vector2(19, 28 + bodyY), offer * .7f);
        }
        if (direction == 2 && (working || presenting))
        {
            Vector2 swap = leftHand;
            leftHand = new Vector2(48 - rightHand.x, rightHand.y);
            rightHand = new Vector2(48 - swap.x, swap.y);
        }

        Color32[] reference = t.GetPixels32();
        Color32 coat = customer && !vip ? CustomerShirt(species) : reference[24 * 48 + 18];
        Color32 shoe = species == CasinoAnimalSpecies.Penguin ? Gold : new Color32(96, 82, 101, 255);
        t.SetPixels32(new Color32[48 * 64]);
        // Each frame is a complete drawing. Feet stay grounded; limbs join at elbows/knees.
        bool side = direction >= 2 && locomotion;
        Vector2 leftHip = new Vector2((side ? 22 : 18) + lean, 22 + bodyY);
        Vector2 rightHip = new Vector2((side ? 27 : 31) + lean, 22 + bodyY);
        float swingLeft = Mathf.Max(0, stride);
        float swingRight = Mathf.Max(0, -stride);
        Vector2 leftFoot = new Vector2(side ? 22 + stride * 6 : 18, 4 + swingLeft * 4);
        Vector2 rightFoot = new Vector2(side ? 27 - stride * 6 : 31, 4 + swingRight * 4);
        Color32 trousers = new Color32(67, 53, 81, 255);
        Vector2 leftKnee = Vector2.Lerp(leftHip, leftFoot, .5f) + new Vector2(-sit * 2, swingLeft * 2);
        Vector2 rightKnee = Vector2.Lerp(rightHip, rightFoot, .5f) + new Vector2(sit * 2, swingRight * 2);
        Limb(t, leftHip, leftKnee, leftFoot, Ink, trousers, 4);
        Limb(t, rightHip, rightKnee, rightFoot, Ink, trousers, 4);
        R(t, Mathf.RoundToInt(leftFoot.x) - 4, Mathf.RoundToInt(leftFoot.y) - 2, 9, 4, Ink);
        R(t, Mathf.RoundToInt(rightFoot.x) - 4, Mathf.RoundToInt(rightFoot.y) - 2, 9, 4, Ink);
        R(t, Mathf.RoundToInt(leftFoot.x) - 3, Mathf.RoundToInt(leftFoot.y), 7, 2, shoe);
        R(t, Mathf.RoundToInt(rightFoot.x) - 3, Mathf.RoundToInt(rightFoot.y), 7, 2, shoe);
        R(t, 13 + lean, 18 + bodyY, 24, 5, trousers);
        for (int y = 19; y < 35; y++)
        for (int x = 11; x <= 38; x++)
        {
            Color32 pixel = reference[y * 48 + x];
            if (pixel.a != 0) R(t, x + lean, y + bodyY, 1, 1, pixel);
        }
        Vector2 leftShoulder = new Vector2(12 + lean, 31 + bodyY);
        Vector2 rightShoulder = new Vector2(36 + lean, 31 + bodyY);
        DrawArm(t, leftShoulder, leftHand, coat);
        DrawArm(t, rightShoulder, rightHand, coat);
        for (int y = 35; y < 64; y++)
        for (int x = 0; x < 48; x++)
        {
            Color32 pixel = reference[y * 48 + x];
            if (pixel.a != 0) R(t, x + lean, y + headY, 1, 1, pixel);
        }
        if (direction == 0)
        {
            bool blink = time >= .72f && time <= .79f || pose == CasinoAnimationPose.Rest;
            if (blink)
            {
                R(t, 14 + lean, 44 + headY, 4, 6, Cream);
                R(t, 31 + lean, 44 + headY, 4, 6, Cream);
                R(t, 14 + lean, 46 + headY, 4, 1, Ink);
                R(t, 31 + lean, 46 + headY, 4, 1, Ink);
            }
            if (pose == CasinoAnimationPose.Anticipate && customer)
            {
                float wide = Beat(time, .1f, .3f, .72f, .95f);
                if (wide > .4f)
                {
                    R(t, 13 + lean, 43 + headY, 6, 8, Cream);
                    R(t, 30 + lean, 43 + headY, 6, 8, Cream);
                    R(t, 15 + lean, 45 + headY, 2, 3, Ink);
                    R(t, 32 + lean, 45 + headY, 2, 3, Ink);
                }
            }
            if (presenting && time > .3f && time < .9f)
            { E(t, 24 + lean, 39 + headY, 3, 1, Ink); R(t, 22 + lean, 40 + headY, 5, 1, Ink); }
            if (pose == CasinoAnimationPose.Celebrate && reach > .3f)
            { E(t, 24 + lean, 39 + headY, 3, 2, Ink); R(t, 23 + lean, 38 + headY, 3, 1, new Color32(238, 136, 157, 255)); }
        }

        // Props follow the contact hand, not an unrelated floating transform.
        Vector2 propHand = direction == 2 ? leftHand : rightHand;
        int px = Mathf.RoundToInt(propHand.x);
        int py = Mathf.RoundToInt(propHand.y);
        if (!customer && pose == CasinoAnimationPose.Deal && time > .12f && time < .68f)
            SmallCard(t, px - 3, py + 1, 1 - release);
        if (pose == CasinoAnimationPose.Pay || pose == CasinoAnimationPose.Collect)
        {
            float held = pose == CasinoAnimationPose.Pay ? 1 - release : reach;
            if (held > .15f && time < .9f)
            { E(t, px, py + 2, 4, 2, Ink); E(t, px, py + 3, 3, 1, Gold); }
        }
        if (presenting)
        {
            // Customers push a real chip; staff show an open palm (acknowledge, point, or thank).
            if (customer && time > .1f && time < .6f)
            { E(t, px, py + 2, 4, 2, Ink); E(t, px, py + 3, 3, 1, Gold); }
            else if (!customer)
            { E(t, px, py + 1, 4, 3, Ink); E(t, px, py + 2, 3, 2, Cream); }
        }
        if (!customer && pose == CasinoAnimationPose.Shuffle && reach > .1f)
        {
            SmallCard(t, Mathf.RoundToInt(leftHand.x) - 3, Mathf.RoundToInt(leftHand.y) + 1, 1);
            SmallCard(t, Mathf.RoundToInt(rightHand.x) - 3, Mathf.RoundToInt(rightHand.y) + 2, 1);
            if (time > .45f && time < .72f)
                for (int i = 0; i < 3; i++)
                    R(t, 21 + i * 2, 34 + bodyY + i, 7, 1, i % 2 == 0 ? Cream : Wood);
        }
    }

    private static float Ease(float time, float start, float end)
    {
        float t = Mathf.InverseLerp(start, end, time);
        return t * t * (3 - 2 * t);
    }

    private static float Beat(float time, float start, float contact, float hold, float end) =>
        Ease(time, start, contact) * (1 - Ease(time, hold, end));

    private static void DrawArm(Texture2D t, Vector2 shoulder, Vector2 hand, Color32 sleeve)
    {
        Vector2 elbow = Vector2.Lerp(shoulder, hand, .55f) + Vector2.down * 2;
        Limb(t, shoulder, elbow, hand, Ink, sleeve, 4);
        E(t, Mathf.RoundToInt(hand.x), Mathf.RoundToInt(hand.y), 3, 3, Ink);
        E(t, Mathf.RoundToInt(hand.x), Mathf.RoundToInt(hand.y) + 1, 2, 2, Cream);
    }

    private static void Limb(Texture2D t, Vector2 from, Vector2 joint, Vector2 to, Color32 outline, Color32 fill, int radius)
    {
        Stroke(t, from, joint, radius, outline);
        Stroke(t, joint, to, radius, outline);
        Stroke(t, from, joint, radius - 1, fill);
        Stroke(t, joint, to, radius - 1, fill);
    }

    private static void Stroke(Texture2D t, Vector2 from, Vector2 to, int radius, Color32 color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, steps == 0 ? 0 : i / (float)steps);
            E(t, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), radius, radius, color);
        }
    }

    private static void SmallCard(Texture2D t, int x, int y, float visibility)
    {
        int height = Mathf.Max(2, Mathf.RoundToInt(9 * visibility));
        R(t, x, y, 7, height + 2, Ink);
        R(t, x + 1, y + 1, 5, height, Cream);
        if (height > 4) R(t, x + 3, y + 3, 2, 2, Wood);
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
                // Betting boxes only: cards are dealt live on top of the table, never baked into its art.
                R(t, x - 9, 42, 18, 1, Gold); R(t, x - 9, 43, 1, 9, Gold); R(t, x + 8, 43, 1, 9, Gold);
                R(t, x - 9, 52, 18, 1, Gold);
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
            R(t, 5, 0, 6, 20, Ink); R(t, 53, 0, 6, 20, Ink);
            R(t, 6, 1, 3, 17, Gold); R(t, 54, 1, 3, 17, Gold);
            R(t, 10, 22, 44, 30, Ink); R(t, 13, 24, 38, 26, Gold);
            R(t, 15, 26, 34, 22, new Color32(119, 32, 75, 255));
            R(t, 17, 44, 30, 2, new Color32(204, 86, 132, 255));
            R(t, 4, 15, 56, 9, Ink); R(t, 7, 17, 50, 5, new Color32(180, 59, 108, 255));
            R(t, 3, 22, 8, 4, Gold); R(t, 53, 22, 8, 4, Gold);
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
        var pixels = t.GetRawTextureData<Color32>();
        for (int px = Mathf.Max(0, x); px < Mathf.Min(t.width, x + w); px++)
            for (int py = Mathf.Max(0, y); py < Mathf.Min(t.height, y + h); py++) pixels[py * t.width + px] = c;
    }
    private static void E(Texture2D t, int x, int y, int rx, int ry, Color32 c)
    {
        var pixels = t.GetRawTextureData<Color32>();
        for (int px = -rx; px <= rx; px++)
            for (int py = -ry; py <= ry; py++)
                if (px * px / (float)(rx * rx) + py * py / (float)(ry * ry) <= 1 &&
                    x + px >= 0 && x + px < t.width && y + py >= 0 && y + py < t.height)
                    pixels[(y + py) * t.width + x + px] = c;
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
