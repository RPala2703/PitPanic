using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PitPanicArtPrefabLibrary
{
    private const string CharacterFolder = "Assets/Prefabs/Characters";
    private const string CasinoFolder = "Assets/Prefabs/Casino";
    private const string ArtMaterialFolder = "Assets/Materials/PitPanic/Art";
    private const string TextureFolder = "Assets/Art/Textures";
    private static readonly CasinoAnimalSpecies[] Species =
    {
        CasinoAnimalSpecies.Penguin,
        CasinoAnimalSpecies.Tiger,
        CasinoAnimalSpecies.Pig,
        CasinoAnimalSpecies.Fox,
        CasinoAnimalSpecies.Rabbit,
        CasinoAnimalSpecies.Bear,
        CasinoAnimalSpecies.Dog,
        CasinoAnimalSpecies.Cat,
        CasinoAnimalSpecies.Raccoon,
        CasinoAnimalSpecies.Elephant
    };

    private static readonly Color[] FurColors =
    {
        new Color(0.055f, 0.09f, 0.14f),
        new Color(0.91f, 0.43f, 0.14f),
        new Color(0.96f, 0.63f, 0.69f),
        new Color(0.91f, 0.42f, 0.12f),
        new Color(0.91f, 0.88f, 0.79f),
        new Color(0.43f, 0.27f, 0.18f),
        new Color(0.72f, 0.47f, 0.27f),
        new Color(0.92f, 0.81f, 0.62f),
        new Color(0.52f, 0.49f, 0.46f),
        new Color(0.56f, 0.66f, 0.68f)
    };

    private static readonly Color[] DealerOutfits =
    {
        new Color(0.43f, 0.09f, 0.14f),
        new Color(0.05f, 0.34f, 0.23f),
        new Color(0.08f, 0.17f, 0.34f),
        new Color(0.26f, 0.12f, 0.33f)
    };

    public static void EnsureStarterAssets()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(TextureFolder);
        EnsureFolder("Assets/Art/Meshes");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(CharacterFolder);
        EnsureFolder(CasinoFolder);
        EnsureFolder("Assets/Materials/PitPanic");
        EnsureFolder(ArtMaterialFolder);

        Material walnut = GetOrCreateMaterial("ArtWalnut", new Color(0.29f, 0.13f, 0.075f), 0.12f);
        Material darkWalnut = GetOrCreateMaterial("ArtDarkWalnut", new Color(0.105f, 0.055f, 0.04f), 0.08f);
        Material emerald = GetOrCreateMaterial("ArtEmeraldFelt", new Color(0.035f, 0.33f, 0.22f), 0.02f);
        Material brass = GetOrCreateMaterial("ArtBrass", new Color(0.79f, 0.52f, 0.16f), 0.62f);
        Material burgundy = GetOrCreateMaterial("ArtBurgundy", new Color(0.30f, 0.075f, 0.12f), 0.03f);
        Material cream = GetOrCreateMaterial("ArtIvory", new Color(0.94f, 0.88f, 0.71f), 0f);
        Material charcoal = GetOrCreateMaterial("ArtCharcoal", new Color(0.035f, 0.045f, 0.06f), 0.05f);
        Material black = GetOrCreateMaterial("ArtBlack", new Color(0.025f, 0.032f, 0.045f), 0.01f);
        Material white = GetOrCreateMaterial("ArtWhite", new Color(0.97f, 0.96f, 0.91f), 0f);
        Material gold = GetOrCreateMaterial("ArtBadgeGold", new Color(0.95f, 0.71f, 0.25f), 0.35f);
        Material pink = GetOrCreateMaterial("ArtPink", new Color(0.96f, 0.40f, 0.52f), 0.02f);
        Material brown = GetOrCreateMaterial("ArtBrown", new Color(0.25f, 0.12f, 0.07f), 0.01f);
        Material dark = GetOrCreateMaterial("ArtDarkAccent", new Color(0.06f, 0.055f, 0.055f), 0.01f);
        Material tigerStripe = GetOrCreateMaterial("ArtTigerStripe", new Color(0.16f, 0.085f, 0.035f), 0.01f);
        Material red = GetOrCreateMaterial("ArtRouletteRed", new Color(0.64f, 0.075f, 0.085f), 0.02f);
        Material rouletteBlack = GetOrCreateMaterial("ArtRouletteBlack", new Color(0.035f, 0.043f, 0.05f), 0.02f);
        Material zeroGreen = GetOrCreateMaterial("ArtRouletteZero", new Color(0.06f, 0.40f, 0.23f), 0.02f);
        Material blue = GetOrCreateMaterial("ArtCustomerBlue", new Color(0.10f, 0.32f, 0.69f), 0.04f);
        Material coral = GetOrCreateMaterial("ArtCustomerCoral", new Color(0.78f, 0.22f, 0.22f), 0.04f);
        Material violet = GetOrCreateMaterial("ArtCustomerViolet", new Color(0.42f, 0.19f, 0.58f), 0.04f);
        Material teal = GetOrCreateMaterial("ArtCustomerTeal", new Color(0.035f, 0.40f, 0.39f), 0.04f);
        Material carpet = GetCarpetMaterial();

        Material[] suits = { black, burgundy, emerald, blue, violet, teal, darkWalnut, coral, charcoal, blue };
        for (int i = 0; i < Species.Length; i++)
        {
            int index = i;
            EnsurePrefab($"{CharacterFolder}/Character_{Species[i]}.prefab",
                root => BuildCharacter(root, Species[index], MaterialForSpecies(index), suits[index % suits.Length],
                    cream, gold, white, pink, dark, tigerStripe, brown));
        }

        EnsurePrefab($"{CasinoFolder}/BlackjackTableHeroArt.prefab", root =>
            BuildBlackjackTable(root, walnut, darkWalnut, emerald, brass, burgundy, cream, gold));
        EnsurePrefab($"{CasinoFolder}/RouletteTableHeroArt.prefab", root =>
            BuildRouletteTable(root, walnut, darkWalnut, emerald, brass, cream, red, rouletteBlack, zeroGreen, gold));
        EnsurePrefab($"{CasinoFolder}/CashDeskArt.prefab", root =>
            BuildCashDesk(root, walnut, darkWalnut, emerald, brass, cream, gold));
        EnsurePrefab($"{CasinoFolder}/PitDeskArt.prefab", root =>
            BuildPitDesk(root, walnut, darkWalnut, burgundy, brass, cream, gold));
        EnsurePrefab($"{CasinoFolder}/BreakRoomArt.prefab", root =>
            BuildBreakRoom(root, walnut, darkWalnut, burgundy, emerald, brass, cream));
        EnsurePrefab($"{CasinoFolder}/CarriedFloatTrayArt.prefab", root =>
            BuildFloatTray(root, walnut, brass, emerald));
        EnsurePrefab($"{CasinoFolder}/CasinoColumnArt.prefab", root =>
            BuildColumn(root, walnut, brass, cream));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static GameObject GetCharacterPrefab(CasinoAnimalSpecies species)
    {
        string path = $"{CharacterFolder}/Character_{species}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new FileNotFoundException($"The starter character prefab was not created at {path}.");
        }

        return prefab;
    }

    public static GameObject[] GetCustomerCharacterPrefabs()
    {
        GameObject[] prefabs = new GameObject[Species.Length];
        for (int i = 0; i < Species.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{CharacterFolder}/Character_{Species[i]}.prefab");
        }

        return prefabs;
    }

    public static GameObject GetCasinoPrefab(string prefabName)
    {
        string path = $"{CasinoFolder}/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new FileNotFoundException($"The casino art prefab was not created at {path}.");
        }

        return prefab;
    }

    public static Material GetFloorCarpetMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            $"{ArtMaterialFolder}/ArtCasinoCarpet.mat");
        if (material == null)
        {
            throw new FileNotFoundException("The casino carpet material has not been generated.");
        }

        return material;
    }

    private static void BuildCharacter(
        GameObject root,
        CasinoAnimalSpecies species,
        Material fur,
        Material outfit,
        Material cream,
        Material gold,
        Material white,
        Material pink,
        Material dark,
        Material stripe,
        Material brown)
    {
        root.name = $"Character_{species}";
        Transform bodyRig = CreateEmpty("Animated Body", root.transform);
        Transform headRig = CreateEmpty("Animated Head", root.transform);
        Transform tailRig = CreateEmpty("Animated Accessory", root.transform);
        GameObject torso = Part(PrimitiveType.Sphere, "Uniform Jacket", bodyRig, new Vector3(0f, 0.87f, 0f),
            new Vector3(0.68f, 0.82f, 0.53f), outfit);
        Part(PrimitiveType.Sphere, "White Shirt Front", bodyRig, new Vector3(0f, 0.94f, 0.444f),
            new Vector3(0.25f, 0.53f, 0.09f), white);
        Part(PrimitiveType.Sphere, "Waistcoat Panel", bodyRig, new Vector3(0f, 0.72f, 0.51f),
            new Vector3(0.29f, 0.34f, 0.055f), outfit);
        Part(PrimitiveType.Sphere, "Bow Tie", bodyRig, new Vector3(0f, 1.35f, 0.50f),
            new Vector3(0.19f, 0.09f, 0.08f), gold);
        Part(PrimitiveType.Sphere, "Gold Name Badge", bodyRig, new Vector3(0.23f, 1.00f, 0.52f),
            new Vector3(0.10f, 0.13f, 0.055f), gold);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, side < 0 ? "Left Sleeve" : "Right Sleeve", bodyRig,
                new Vector3(side * 0.43f, 0.91f, 0.015f), new Vector3(0.23f, 0.53f, 0.25f), outfit);
            Part(PrimitiveType.Sphere, side < 0 ? "Left Paw" : "Right Paw", bodyRig,
                new Vector3(side * 0.45f, 0.43f, 0.10f), new Vector3(0.22f, 0.22f, 0.20f), fur);
            Part(PrimitiveType.Sphere, side < 0 ? "Left Trouser" : "Right Trouser", bodyRig,
                new Vector3(side * 0.18f, 0.26f, 0f), new Vector3(0.22f, 0.37f, 0.24f), dark);
            Part(PrimitiveType.Sphere, side < 0 ? "Left Shoe" : "Right Shoe", bodyRig,
                new Vector3(side * 0.18f, 0.08f, 0.10f), new Vector3(0.28f, 0.13f, 0.36f), dark);
        }

        GameObject head = Part(PrimitiveType.Sphere, "Head Shape", headRig, new Vector3(0f, 1.58f, 0f),
            new Vector3(0.72f, 0.64f, 0.60f), fur);
        Transform animatedHead = headRig;
        Vector3 eyeScale = new Vector3(0.09f, 0.12f, 0.045f);
        Material faceFur = species == CasinoAnimalSpecies.Penguin ? white : cream;
        if (species == CasinoAnimalSpecies.Penguin)
        {
            Part(PrimitiveType.Sphere, "White Penguin Belly", bodyRig, new Vector3(0f, 0.84f, 0.47f),
                new Vector3(0.40f, 0.63f, 0.12f), white);
            Part(PrimitiveType.Sphere, "Penguin Beak", headRig, new Vector3(0f, 1.47f, 0.63f),
                new Vector3(0.19f, 0.10f, 0.15f), gold);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Capsule, side < 0 ? "Left Flipper" : "Right Flipper", bodyRig,
                    new Vector3(side * 0.62f, 0.85f, 0f), new Vector3(0.20f, 0.60f, 0.18f), fur,
                    Quaternion.Euler(0f, 0f, side * 12f));
            }
        }
        else
        {
            BuildSpeciesFeatures(species, headRig, tailRig, fur, cream, white, pink, dark, stripe, brown);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, side < 0 ? "Left Eye" : "Right Eye", headRig,
                new Vector3(side * 0.17f, 1.68f, 0.52f), eyeScale, dark);
            Part(PrimitiveType.Sphere, side < 0 ? "Eye Glint Left" : "Eye Glint Right", headRig,
                new Vector3(side * 0.17f + 0.025f, 1.72f, 0.558f), new Vector3(0.025f, 0.035f, 0.018f), white);
        }

        if (species != CasinoAnimalSpecies.Penguin)
        {
            Part(PrimitiveType.Sphere, "Soft Muzzle", headRig, new Vector3(0f, 1.48f, 0.52f),
                new Vector3(0.25f, 0.15f, 0.17f), faceFur);
        }

        Part(PrimitiveType.Sphere, "Uniform Button 1", bodyRig, new Vector3(0f, 1.11f, 0.56f),
            new Vector3(0.035f, 0.035f, 0.025f), gold);
        Part(PrimitiveType.Sphere, "Uniform Button 2", bodyRig, new Vector3(0f, 0.98f, 0.56f),
            new Vector3(0.035f, 0.035f, 0.025f), gold);

        CasinoCharacterVisual visual = root.AddComponent<CasinoCharacterVisual>();
        visual.Configure(species, bodyRig, animatedHead, tailRig, torso.GetComponent<Renderer>());
    }

    private static void BuildSpeciesFeatures(
        CasinoAnimalSpecies species,
        Transform head,
        Transform tail,
        Material fur,
        Material cream,
        Material white,
        Material pink,
        Material dark,
        Material stripe,
        Material brown)
    {
        switch (species)
        {
            case CasinoAnimalSpecies.Tiger:
                Ears(head, fur, cream, 0.25f, 0.28f);
                for (int i = -1; i <= 1; i++)
                {
                    Part(PrimitiveType.Cube, $"Tiger Forehead Stripe {i + 2}", head,
                        new Vector3(i * 0.16f, 1.94f, 0.48f), new Vector3(0.07f, 0.24f, 0.04f),
                        stripe, Quaternion.Euler(0f, 0f, -i * 14f));
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    Part(PrimitiveType.Cube, side < 0 ? "Tiger Cheek Stripe L" : "Tiger Cheek Stripe R", head,
                        new Vector3(side * 0.31f, 1.50f, 0.48f), new Vector3(0.08f, 0.24f, 0.035f),
                        stripe, Quaternion.Euler(0f, 0f, side * 20f));
                }
                break;
            case CasinoAnimalSpecies.Pig:
                Ears(head, pink, pink, 0.27f, 0.22f);
                Part(PrimitiveType.Sphere, "Pig Snout", head, new Vector3(0f, 1.48f, 0.62f),
                    new Vector3(0.29f, 0.21f, 0.10f), pink);
                Part(PrimitiveType.Sphere, "Pig Nostril Left", head, new Vector3(-0.09f, 1.49f, 0.714f),
                    new Vector3(0.04f, 0.075f, 0.025f), brown);
                Part(PrimitiveType.Sphere, "Pig Nostril Right", head, new Vector3(0.09f, 1.49f, 0.714f),
                    new Vector3(0.04f, 0.075f, 0.025f), brown);
                break;
            case CasinoAnimalSpecies.Fox:
                PointedEars(head, fur, cream);
                Part(PrimitiveType.Sphere, "Fox White Muzzle", head, new Vector3(0f, 1.47f, 0.57f),
                    new Vector3(0.29f, 0.17f, 0.16f), cream);
                Part(PrimitiveType.Sphere, "Fox Nose", head, new Vector3(0f, 1.47f, 0.72f),
                    new Vector3(0.08f, 0.06f, 0.05f), dark);
                Tail(tail, fur, cream, new Vector3(0f, 0.62f, -0.52f), new Vector3(0.30f, 0.48f, 0.46f));
                Part(PrimitiveType.Sphere, "Fox Tail Tip", tail, new Vector3(0f, 0.85f, -0.81f),
                    new Vector3(0.22f, 0.22f, 0.18f), cream);
                break;
            case CasinoAnimalSpecies.Rabbit:
                Ears(head, fur, pink, 0.20f, 0.58f);
                Part(PrimitiveType.Sphere, "Rabbit Muzzle", head, new Vector3(0f, 1.48f, 0.57f),
                    new Vector3(0.23f, 0.17f, 0.13f), white);
                Part(PrimitiveType.Sphere, "Rabbit Nose", head, new Vector3(0f, 1.55f, 0.69f),
                    new Vector3(0.07f, 0.055f, 0.035f), pink);
                Part(PrimitiveType.Cube, "Rabbit Teeth", head, new Vector3(0f, 1.38f, 0.67f),
                    new Vector3(0.16f, 0.105f, 0.04f), white);
                break;
            case CasinoAnimalSpecies.Bear:
                Ears(head, fur, brown, 0.22f, 0.19f);
                Part(PrimitiveType.Sphere, "Bear Muzzle", head, new Vector3(0f, 1.47f, 0.58f),
                    new Vector3(0.31f, 0.20f, 0.16f), brown);
                Part(PrimitiveType.Sphere, "Bear Nose", head, new Vector3(0f, 1.52f, 0.72f),
                    new Vector3(0.12f, 0.085f, 0.05f), dark);
                break;
            case CasinoAnimalSpecies.Dog:
                Ears(head, brown, brown, 0.27f, 0.40f);
                Part(PrimitiveType.Sphere, "Dog Muzzle", head, new Vector3(0f, 1.48f, 0.58f),
                    new Vector3(0.29f, 0.20f, 0.18f), cream);
                Part(PrimitiveType.Sphere, "Dog Nose", head, new Vector3(0f, 1.50f, 0.74f),
                    new Vector3(0.11f, 0.075f, 0.05f), dark);
                break;
            case CasinoAnimalSpecies.Cat:
                PointedEars(head, fur, pink);
                Part(PrimitiveType.Sphere, "Cat Muzzle Left", head, new Vector3(-0.10f, 1.48f, 0.59f),
                    new Vector3(0.15f, 0.12f, 0.12f), cream);
                Part(PrimitiveType.Sphere, "Cat Muzzle Right", head, new Vector3(0.10f, 1.48f, 0.59f),
                    new Vector3(0.15f, 0.12f, 0.12f), cream);
                Part(PrimitiveType.Sphere, "Cat Nose", head, new Vector3(0f, 1.53f, 0.69f),
                    new Vector3(0.06f, 0.05f, 0.035f), pink);
                Whiskers(head, cream);
                break;
            case CasinoAnimalSpecies.Raccoon:
                Ears(head, brown, brown, 0.24f, 0.20f);
                Part(PrimitiveType.Sphere, "Raccoon Eye Mask", head, new Vector3(0f, 1.68f, 0.49f),
                    new Vector3(0.46f, 0.17f, 0.09f), dark);
                Part(PrimitiveType.Sphere, "Raccoon Muzzle", head, new Vector3(0f, 1.46f, 0.58f),
                    new Vector3(0.27f, 0.16f, 0.15f), cream);
                Tail(tail, brown, dark, new Vector3(0f, 0.58f, -0.55f), new Vector3(0.25f, 0.26f, 0.65f));
                for (int i = 0; i < 3; i++)
                {
                    Part(PrimitiveType.Sphere, $"Raccoon Tail Ring {i}", tail,
                        new Vector3(0f, 0.58f, -0.35f - i * 0.16f),
                        new Vector3(0.27f, 0.28f, 0.09f), i % 2 == 0 ? dark : cream);
                }
                break;
            case CasinoAnimalSpecies.Elephant:
                Ears(head, fur, fur, 0.42f, 0.40f);
                Part(PrimitiveType.Capsule, "Elephant Trunk", head, new Vector3(0f, 1.36f, 0.62f),
                    new Vector3(0.13f, 0.36f, 0.14f), fur, Quaternion.Euler(0f, 0f, 0f));
                Part(PrimitiveType.Sphere, "Elephant Trunk Tip", head, new Vector3(0f, 1.09f, 0.68f),
                    new Vector3(0.16f, 0.12f, 0.15f), fur);
                Part(PrimitiveType.Sphere, "Elephant Tusk Left", head, new Vector3(-0.19f, 1.39f, 0.60f),
                    new Vector3(0.065f, 0.15f, 0.07f), cream, Quaternion.Euler(0f, 0f, -18f));
                Part(PrimitiveType.Sphere, "Elephant Tusk Right", head, new Vector3(0.19f, 1.39f, 0.60f),
                    new Vector3(0.065f, 0.15f, 0.07f), cream, Quaternion.Euler(0f, 0f, 18f));
                break;
        }
    }

    private static void BuildBlackjackTable(
        GameObject root,
        Material walnut,
        Material darkWalnut,
        Material felt,
        Material brass,
        Material burgundy,
        Material cream,
        Material gold)
    {
        root.name = "BlackjackTableArt";
        CreateRoundTableBody(root.transform, walnut, darkWalnut, brass, felt, new Vector3(0f, 1.02f, 0f),
            new Vector3(5.4f, 0.48f, 3.05f));
        Part(PrimitiveType.Sphere, "Raised Dealer Rail", root.transform, new Vector3(0f, 1.49f, 1.05f),
            new Vector3(5.0f, 0.22f, 0.52f), burgundy);
        Part(PrimitiveType.Sphere, "Dealer Rail Brass Inlay", root.transform, new Vector3(0f, 1.65f, 1.04f),
            new Vector3(4.2f, 0.035f, 0.27f), brass);
        for (int side = -1; side <= 1; side++)
        {
            CreateBettingCircle(root.transform, side + 2,
                new Vector3(side * 1.68f, 1.35f, -0.68f), brass, felt);
        }

        BuildChipTray(root.transform, new Vector3(1.70f, 1.57f, 0.88f), walnut, brass, new[] { gold, burgundy, felt, darkWalnut });
        Part(PrimitiveType.Cube, "Card Shoe Body", root.transform, new Vector3(-1.52f, 1.57f, 1.0f),
            new Vector3(0.42f, 0.32f, 0.55f), darkWalnut);
        Part(PrimitiveType.Cube, "Card Shoe Brass Lip", root.transform, new Vector3(-1.52f, 1.73f, 0.77f),
            new Vector3(0.44f, 0.045f, 0.12f), brass);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int front = -1; front <= 1; front += 2)
            {
                Part(PrimitiveType.Cylinder, "Walnut Pedestal Leg", root.transform,
                    new Vector3(side * 1.75f, 0.52f, front * 0.84f),
                    new Vector3(0.30f, 0.52f, 0.30f), darkWalnut);
                Part(PrimitiveType.Sphere, "Brass Table Foot", root.transform,
                    new Vector3(side * 1.75f, 0.08f, front * 0.84f),
                    new Vector3(0.40f, 0.10f, 0.40f), brass);
            }
        }

        for (int seat = -1; seat <= 1; seat++)
        {
            Part(PrimitiveType.Cylinder, $"Customer Stool {seat + 2}", root.transform,
                new Vector3(seat * 1.7f, 0.43f, -1.88f), new Vector3(0.60f, 0.40f, 0.60f), darkWalnut);
            Part(PrimitiveType.Sphere, $"Burgundy Padded Stool {seat + 2}", root.transform,
                new Vector3(seat * 1.7f, 0.84f, -1.88f), new Vector3(0.64f, 0.14f, 0.64f), burgundy);
            Part(PrimitiveType.Sphere, $"Brass Stool Stud {seat + 2}", root.transform,
                new Vector3(seat * 1.7f, 0.97f, -1.88f), new Vector3(0.10f, 0.035f, 0.10f), brass);
        }

        Part(PrimitiveType.Cube, "Table Name Plaque", root.transform, new Vector3(0f, 1.60f, -1.30f),
            new Vector3(2.3f, 0.12f, 0.35f), walnut);
        Part(PrimitiveType.Cube, "Plaque Gold Inlay", root.transform, new Vector3(0f, 1.68f, -1.30f),
            new Vector3(2.12f, 0.025f, 0.20f), gold);
        Part(PrimitiveType.Cube, "Plaque Ivory Face", root.transform, new Vector3(0f, 1.70f, -1.30f),
            new Vector3(1.94f, 0.025f, 0.15f), cream);
    }

    private static void BuildRouletteTable(
        GameObject root,
        Material walnut,
        Material darkWalnut,
        Material felt,
        Material brass,
        Material cream,
        Material red,
        Material black,
        Material green,
        Material gold)
    {
        root.name = "RouletteTableArt";
        CreateRoundTableBody(root.transform, walnut, darkWalnut, brass, felt, new Vector3(0f, 1.00f, 0f),
            new Vector3(5.0f, 0.48f, 3.25f));
        Part(PrimitiveType.Cylinder, "Brass Wheel Foundation", root.transform, new Vector3(0f, 1.42f, 0.35f),
            new Vector3(1.86f, 0.17f, 1.86f), brass);
        Transform wheel = new GameObject("Roulette Wheel Visual").transform;
        wheel.SetParent(root.transform, false);
        wheel.localPosition = new Vector3(0f, 1.55f, 0.35f);
        Part(PrimitiveType.Cylinder, "Roulette Wheel Core", wheel,
            Vector3.zero, new Vector3(1.60f, 0.17f, 1.60f), darkWalnut);
        Part(PrimitiveType.Cylinder, "Roulette Outer Brass Ring", wheel,
            new Vector3(0f, 0.10f, 0f), new Vector3(1.50f, 0.055f, 1.50f), gold);
        Part(PrimitiveType.Cylinder, "Roulette Pocket Base", wheel,
            new Vector3(0f, 0.13f, 0f), new Vector3(1.36f, 0.045f, 1.36f), black);
        CreateRoulettePocketRing(wheel, red, black, green);

        Part(PrimitiveType.Cylinder, "Roulette Brass Spindle", wheel, new Vector3(0f, 0.24f, 0f),
            new Vector3(0.40f, 0.10f, 0.40f), brass);
        Part(PrimitiveType.Sphere, "Roulette Spindle Crown", wheel, new Vector3(0f, 0.36f, 0f),
            new Vector3(0.27f, 0.15f, 0.27f), gold);
        Part(PrimitiveType.Sphere, "Roulette Ball", wheel, new Vector3(0f, 0.25f, 1.19f),
            new Vector3(0.13f, 0.13f, 0.13f), cream);
        for (int i = 0; i < 12; i++)
        {
            Part(PrimitiveType.Cube, $"Roulette Number Tile {i}", root.transform,
                new Vector3(-1.55f + (i % 6) * 0.58f, 1.335f, -0.66f - (i / 6) * 0.39f),
                new Vector3(0.53f, 0.025f, 0.34f), i % 2 == 0 ? red : black,
                Quaternion.Euler(0f, 0f, 0f));
        }

        BuildChipTray(root.transform, new Vector3(-1.65f, 1.54f, 0.70f), walnut, brass, new[] { red, black, green, gold });
        for (int side = -1; side <= 1; side += 2)
        {
            for (int front = -1; front <= 1; front += 2)
            {
                Part(PrimitiveType.Cylinder, "Roulette Pedestal Leg", root.transform,
                    new Vector3(side * 1.65f, 0.50f, front * 0.78f),
                    new Vector3(0.28f, 0.50f, 0.28f), darkWalnut);
            }
        }
    }

    private static void BuildCashDesk(
        GameObject root,
        Material walnut,
        Material darkWalnut,
        Material felt,
        Material brass,
        Material cream,
        Material gold)
    {
        root.name = "CashDeskArt";
        Part(PrimitiveType.Cube, "Cashier Walnut Base", root.transform, new Vector3(0f, 0.61f, 0f),
            new Vector3(4.55f, 1.22f, 1.45f), walnut);
        Part(PrimitiveType.Cube, "Cashier Kick Plate", root.transform, new Vector3(0f, 0.20f, -0.74f),
            new Vector3(3.90f, 0.24f, 0.06f), darkWalnut);
        Part(PrimitiveType.Cube, "Cashier Brass Counter Edge", root.transform, new Vector3(0f, 1.27f, 0.73f),
            new Vector3(4.72f, 0.12f, 0.12f), brass);
        Part(PrimitiveType.Cube, "Emerald Cashier Countertop", root.transform, new Vector3(0f, 1.26f, 0f),
            new Vector3(4.68f, 0.17f, 1.55f), felt);
        Part(PrimitiveType.Cube, "Cashier Sign Back", root.transform, new Vector3(0f, 2.05f, 0.54f),
            new Vector3(2.65f, 0.56f, 0.16f), darkWalnut);
        Part(PrimitiveType.Cube, "Cashier Sign Brass Face", root.transform, new Vector3(0f, 2.06f, 0.64f),
            new Vector3(2.48f, 0.42f, 0.04f), brass);
        Part(PrimitiveType.Cube, "Cashier Sign Ivory Panel", root.transform, new Vector3(0f, 2.06f, 0.67f),
            new Vector3(2.27f, 0.29f, 0.03f), cream);
        BuildChipTray(root.transform, new Vector3(-1.35f, 1.43f, -0.08f), walnut, brass,
            new[] { gold, brass, felt, darkWalnut });
        BuildChipTray(root.transform, new Vector3(1.36f, 1.43f, -0.08f), walnut, brass,
            new[] { gold, brass, felt, darkWalnut });
        for (int i = 0; i < 4; i++)
        {
            Part(PrimitiveType.Cylinder, $"Cashier Chip Roll {i}", root.transform,
                new Vector3(-0.90f + i * 0.60f, 1.47f, 0.33f),
                new Vector3(0.22f, 0.14f, 0.22f), i % 2 == 0 ? gold : brass);
        }
    }

    private static void BuildPitDesk(
        GameObject root,
        Material walnut,
        Material darkWalnut,
        Material burgundy,
        Material brass,
        Material cream,
        Material gold)
    {
        root.name = "PitDeskArt";
        Part(PrimitiveType.Cube, "Pit Boss Walnut Base", root.transform, new Vector3(0f, 0.59f, 0f),
            new Vector3(3.75f, 1.18f, 1.62f), darkWalnut);
        Part(PrimitiveType.Cube, "Pit Desk Brass Counter Edge", root.transform, new Vector3(0f, 1.22f, 0.79f),
            new Vector3(3.92f, 0.12f, 0.12f), brass);
        Part(PrimitiveType.Cube, "Pit Desk Burgundy Countertop", root.transform, new Vector3(0f, 1.20f, 0f),
            new Vector3(3.88f, 0.18f, 1.72f), burgundy);
        Part(PrimitiveType.Cube, "Pit Desk Back Panel", root.transform, new Vector3(0f, 1.95f, 0.62f),
            new Vector3(2.82f, 0.53f, 0.16f), walnut);
        Part(PrimitiveType.Cube, "Pit Desk Brass Panel", root.transform, new Vector3(0f, 1.96f, 0.73f),
            new Vector3(2.56f, 0.36f, 0.04f), brass);
        Part(PrimitiveType.Cube, "Pit Desk Ivory Sign", root.transform, new Vector3(0f, 1.96f, 0.76f),
            new Vector3(2.28f, 0.24f, 0.03f), cream);
        Part(PrimitiveType.Cube, "Schedule Board Walnut Frame", root.transform, new Vector3(0f, 2.58f, 0.40f),
            new Vector3(2.72f, 0.73f, 0.12f), walnut);
        Part(PrimitiveType.Cube, "Schedule Board Green Felt", root.transform, new Vector3(0f, 2.59f, 0.48f),
            new Vector3(2.50f, 0.57f, 0.04f), darkWalnut);
        for (int i = 0; i < 3; i++)
        {
            Part(PrimitiveType.Cube, $"Dealer Schedule Marker {i}", root.transform,
                new Vector3(-0.72f + i * 0.72f, 2.65f, 0.52f),
                new Vector3(0.50f, 0.11f, 0.03f), i == 0 ? gold : brass);
        }
    }

    private static void BuildBreakRoom(
        GameObject root,
        Material walnut,
        Material darkWalnut,
        Material burgundy,
        Material felt,
        Material brass,
        Material cream)
    {
        root.name = "BreakRoomArt";
        Part(PrimitiveType.Cube, "Burgundy Break Room Rug", root.transform, new Vector3(0f, 0.025f, 0f),
            new Vector3(7.6f, 0.05f, 4.8f), burgundy);
        Part(PrimitiveType.Cube, "Rug Brass Border", root.transform, new Vector3(0f, 0.06f, 0f),
            new Vector3(7.28f, 0.025f, 4.48f), brass);
        Part(PrimitiveType.Cube, "Rug Felt Inset", root.transform, new Vector3(0f, 0.08f, 0f),
            new Vector3(7.00f, 0.025f, 4.20f), felt);
        Part(PrimitiveType.Cube, "Break Room Sofa Base", root.transform, new Vector3(0f, 0.36f, 1.42f),
            new Vector3(4.90f, 0.48f, 0.80f), darkWalnut);
        Part(PrimitiveType.Cube, "Break Room Sofa Cushion", root.transform, new Vector3(0f, 0.69f, 1.13f),
            new Vector3(4.58f, 0.40f, 0.69f), burgundy);
        Part(PrimitiveType.Cube, "Break Room Sofa Back", root.transform, new Vector3(0f, 0.92f, 1.78f),
            new Vector3(4.90f, 0.94f, 0.42f), walnut);
        for (int seat = -1; seat <= 1; seat++)
        {
            Part(PrimitiveType.Sphere, $"Sofa Cushion {seat + 2}", root.transform,
                new Vector3(seat * 1.35f, 0.92f, 1.48f), new Vector3(1.10f, 0.18f, 0.60f), burgundy);
        }

        Part(PrimitiveType.Cylinder, "Break Room Tea Table", root.transform, new Vector3(0f, 0.46f, -0.65f),
            new Vector3(1.00f, 0.10f, 1.00f), walnut);
        Part(PrimitiveType.Cylinder, "Tea Table Brass Top", root.transform, new Vector3(0f, 0.57f, -0.65f),
            new Vector3(0.94f, 0.045f, 0.94f), brass);
        Part(PrimitiveType.Cylinder, "Tea Cup", root.transform, new Vector3(0.10f, 0.68f, -0.65f),
            new Vector3(0.16f, 0.10f, 0.16f), cream);
    }

    private static void BuildFloatTray(
        GameObject root,
        Material walnut,
        Material brass,
        Material felt)
    {
        root.name = "CarriedFloatTrayArt";
        Part(PrimitiveType.Cube, "Tray Walnut Base", root.transform, Vector3.zero,
            new Vector3(1.54f, 0.12f, 1.86f), walnut);
        Part(PrimitiveType.Cube, "Tray Felt Lining", root.transform, new Vector3(0f, 0.075f, 0f),
            new Vector3(1.34f, 0.035f, 1.63f), felt);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Cube, side < 0 ? "Tray Left Brass Rail" : "Tray Right Brass Rail",
                root.transform, new Vector3(side * 0.74f, 0.13f, 0f),
                new Vector3(0.06f, 0.17f, 1.88f), brass);
            Part(PrimitiveType.Cube, side < 0 ? "Tray Front Brass Rail" : "Tray Back Brass Rail",
                root.transform, new Vector3(0f, 0.13f, side * 0.89f),
                new Vector3(1.50f, 0.17f, 0.06f), brass);
        }

    }

    private static void BuildColumn(GameObject root, Material walnut, Material brass, Material cream)
    {
        root.name = "CasinoColumnArt";
        Part(PrimitiveType.Cylinder, "Walnut Column", root.transform, new Vector3(0f, 2f, 0f),
            new Vector3(0.72f, 2f, 0.72f), walnut);
        Part(PrimitiveType.Cylinder, "Column Brass Foot", root.transform, new Vector3(0f, 0.25f, 0f),
            new Vector3(1.05f, 0.25f, 1.05f), brass);
        Part(PrimitiveType.Cylinder, "Column Brass Capital", root.transform, new Vector3(0f, 3.72f, 0f),
            new Vector3(1.0f, 0.22f, 1.0f), brass);
        Part(PrimitiveType.Cylinder, "Column Ivory Crown", root.transform, new Vector3(0f, 3.96f, 0f),
            new Vector3(0.84f, 0.08f, 0.84f), cream);
    }

    private static void CreateRoundTableBody(
        Transform root,
        Material walnut,
        Material darkWalnut,
        Material brass,
        Material felt,
        Vector3 center,
        Vector3 size)
    {
        Part(PrimitiveType.Cylinder, "Sculpted Walnut Pedestal", root,
            center + Vector3.down * 0.44f,
            new Vector3(size.x * 0.43f, 0.52f, size.z * 0.43f), darkWalnut);
        CreateCasinoTableShell(root, center, size, darkWalnut, walnut, brass, felt);
    }

    private static void CreateCasinoTableShell(
        Transform root,
        Vector3 center,
        Vector3 size,
        Material darkWalnut,
        Material walnut,
        Material brass,
        Material felt)
    {
        const int segments = 64;
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        float[] scales = { 0.95f, 1f, 1f, 0.98f, 0.86f };
        float[] heights =
        {
            center.y - size.y * 0.5f,
            center.y - size.y * 0.32f,
            center.y + size.y * 0.58f,
            center.y + size.y * 0.66f,
            center.y + size.y * 0.66f
        };
        Vector3[] vertices = new Vector3[segments * scales.Length + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int ring = 0; ring < scales.Length; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float x = Superellipse(cos, 0.72f) * halfX * scales[ring];
                float z = Superellipse(sin, 0.72f) * halfZ * scales[ring];
                int index = ring * segments + i;
                vertices[index] = new Vector3(center.x + x, heights[ring], center.z + z);
                uvs[index] = new Vector2((x / size.x) + 0.5f, (z / size.z) + 0.5f);
            }
        }

        int centerIndex = vertices.Length - 1;
        vertices[centerIndex] = new Vector3(center.x, heights[4], center.z);
        uvs[centerIndex] = new Vector2(0.5f, 0.5f);
        Mesh mesh = new Mesh { name = $"{root.name} Smooth Table Shell" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 5;
        for (int ring = 0; ring < scales.Length - 1; ring++)
        {
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int outer = ring * segments + i;
                int outerNext = ring * segments + next;
                int inner = (ring + 1) * segments + i;
                int innerNext = (ring + 1) * segments + next;
                int triangle = i * 6;
                triangles[triangle] = outer;
                triangles[triangle + 1] = inner;
                triangles[triangle + 2] = innerNext;
                triangles[triangle + 3] = outer;
                triangles[triangle + 4] = innerNext;
                triangles[triangle + 5] = outerNext;
            }

            mesh.SetTriangles(triangles, ring);
        }

        int[] feltTriangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            feltTriangles[i * 3] = centerIndex;
            feltTriangles[i * 3 + 1] = (scales.Length - 1) * segments + next;
            feltTriangles[i * 3 + 2] = (scales.Length - 1) * segments + i;
        }

        mesh.SetTriangles(feltTriangles, 4);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh = PersistGeneratedMesh(mesh);
        GameObject shell = new GameObject("Rounded Casino Table Shell", typeof(MeshFilter), typeof(MeshRenderer));
        shell.transform.SetParent(root, false);
        shell.GetComponent<MeshFilter>().sharedMesh = mesh;
        shell.GetComponent<MeshRenderer>().sharedMaterials = new[] { darkWalnut, walnut, brass, walnut, felt };
        shell.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        shell.GetComponent<MeshRenderer>().receiveShadows = true;
    }

    private static float Superellipse(float value, float exponent) =>
        Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);

    private static void CreateBettingCircle(
        Transform root,
        int index,
        Vector3 center,
        Material brass,
        Material felt)
    {
        GameObject ringObject = new GameObject($"Blackjack Bet Spot {index}", typeof(MeshFilter), typeof(MeshRenderer));
        ringObject.transform.SetParent(root, false);
        ringObject.transform.localPosition = center;
        Mesh mesh = CreateFlatRingMesh($"Betting Ring {index}", 32, 0.25f, 0.30f);
        ringObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        ringObject.GetComponent<MeshRenderer>().sharedMaterial = brass;
        GameObject inset = new GameObject($"Betting Felt {index}", typeof(MeshFilter), typeof(MeshRenderer));
        inset.transform.SetParent(root, false);
        inset.transform.localPosition = center + Vector3.up * 0.006f;
        Mesh disk = CreateFlatRingMesh($"Betting Felt Disc {index}", 32, 0f, 0.255f);
        inset.GetComponent<MeshFilter>().sharedMesh = disk;
        inset.GetComponent<MeshRenderer>().sharedMaterial = felt;
    }

    private static Mesh CreateFlatRingMesh(string meshName, int segments, float innerRadius, float outerRadius)
    {
        Vector3[] vertices = new Vector3[segments * 2 + (innerRadius <= 0f ? 1 : 0)];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i] = direction * (innerRadius <= 0f ? 0f : innerRadius);
            vertices[segments + i] = direction * outerRadius;
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        List<int> triangles = new List<int>(segments * 6);
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            if (innerRadius <= 0f)
            {
                triangles.Add(segments * 2);
                triangles.Add(segments + next);
                triangles.Add(segments + i);
            }
            else
            {
                triangles.Add(i);
                triangles.Add(next);
                triangles.Add(segments + next);
                triangles.Add(i);
                triangles.Add(segments + next);
                triangles.Add(segments + i);
            }
        }

        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return PersistGeneratedMesh(mesh);
    }

    private static void CreateRoulettePocketRing(Transform wheel, Material red, Material black, Material green)
    {
        const int pockets = 37;
        const float innerRadius = 0.68f;
        const float outerRadius = 0.96f;
        const float height = 0.17f;
        Vector3[] vertices = new Vector3[pockets * 4];
        List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
        for (int pocket = 0; pocket < pockets; pocket++)
        {
            float start = (pocket + 0.06f) * Mathf.PI * 2f / pockets;
            float end = (pocket + 0.94f) * Mathf.PI * 2f / pockets;
            int first = pocket * 4;
            vertices[first] = new Vector3(Mathf.Cos(start) * innerRadius, height, Mathf.Sin(start) * innerRadius);
            vertices[first + 1] = new Vector3(Mathf.Cos(start) * outerRadius, height, Mathf.Sin(start) * outerRadius);
            vertices[first + 2] = new Vector3(Mathf.Cos(end) * outerRadius, height, Mathf.Sin(end) * outerRadius);
            vertices[first + 3] = new Vector3(Mathf.Cos(end) * innerRadius, height, Mathf.Sin(end) * innerRadius);
            List<int> materialTriangles = triangles[pocket == 0 ? 2 : pocket % 2 == 0 ? 0 : 1];
            materialTriangles.Add(first);
            materialTriangles.Add(first + 3);
            materialTriangles.Add(first + 2);
            materialTriangles.Add(first);
            materialTriangles.Add(first + 2);
            materialTriangles.Add(first + 1);
        }

        Mesh mesh = new Mesh { name = "37 Pocket European Roulette Ring" };
        mesh.vertices = vertices;
        mesh.subMeshCount = 3;
        for (int i = 0; i < triangles.Length; i++)
        {
            mesh.SetTriangles(triangles[i], i);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh = PersistGeneratedMesh(mesh);
        GameObject ring = new GameObject("37 Individual Roulette Pockets", typeof(MeshFilter), typeof(MeshRenderer));
        ring.transform.SetParent(wheel, false);
        ring.GetComponent<MeshFilter>().sharedMesh = mesh;
        ring.GetComponent<MeshRenderer>().sharedMaterials = new[] { red, black, green };
        ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        ring.GetComponent<MeshRenderer>().receiveShadows = true;
    }

    private static void BuildChipTray(
        Transform root,
        Vector3 position,
        Material walnut,
        Material brass,
        IReadOnlyList<Material> chipMaterials)
    {
        Part(PrimitiveType.Cube, "Chip Tray Walnut Base", root, position,
            new Vector3(1.20f, 0.12f, 0.53f), walnut);
        Part(PrimitiveType.Cube, "Chip Tray Brass Lip", root, position + new Vector3(0f, 0.08f, 0.24f),
            new Vector3(1.24f, 0.045f, 0.05f), brass);
        for (int lane = 0; lane < chipMaterials.Count; lane++)
        {
            for (int chip = 0; chip < 3; chip++)
            {
                Part(PrimitiveType.Cylinder, $"Visible Chip Stack {lane}-{chip}", root,
                    position + new Vector3(-0.42f + lane * 0.28f, 0.12f + chip * 0.043f, -0.06f),
                    new Vector3(0.18f, 0.035f, 0.18f), chipMaterials[lane]);
            }
        }
    }

    private static Material MaterialForSpecies(int index)
    {
        return GetOrCreateMaterial($"Fur{Species[index]}", FurColors[index], 0.02f);
    }

    private static Material GetCarpetMaterial()
    {
        string texturePath = $"{TextureFolder}/CasinoCarpet.png";
        TextureImporter currentImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (!File.Exists(texturePath) || currentImporter == null || currentImporter.userData != "PitPanicCasinoCarpetV2")
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Casino Carpet Pattern",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            Color baseColor = new Color(0.105f, 0.028f, 0.055f);
            Color motifColor = new Color(0.132f, 0.042f, 0.068f);
            Color goldColor = new Color(0.19f, 0.095f, 0.045f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int tileX = x % 32;
                    int tileY = y % 32;
                    int distance = Mathf.Abs(tileX - 16) + Mathf.Abs(tileY - 16);
                    Color color = distance <= 9 ? motifColor : baseColor;
                    if (distance >= 8 && distance <= 10)
                    {
                        color = goldColor;
                    }

                    if (tileX == 0 || tileY == 0)
                    {
                        color *= 0.85f;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter updatedImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (updatedImporter != null)
            {
                updatedImporter.userData = "PitPanicCasinoCarpetV2";
                updatedImporter.textureType = TextureImporterType.Default;
                updatedImporter.wrapMode = TextureWrapMode.Repeat;
                updatedImporter.filterMode = FilterMode.Bilinear;
                updatedImporter.mipmapEnabled = true;
                updatedImporter.SaveAndReimport();
            }
        }

        Material carpet = GetOrCreateMaterial("ArtCasinoCarpet", Color.white, 0.01f);
        carpet.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        carpet.mainTextureScale = new Vector2(5f, 4f);
        EditorUtility.SetDirty(carpet);
        return carpet;
    }

    private static Material GetOrCreateMaterial(string name, Color color, float metallic)
    {
        string path = $"{ArtMaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            throw new InvalidOperationException("No supported render shader is available for casino art materials.");
        }

        material = new Material(shader)
        {
            name = name,
            color = color,
            enableInstancing = true
        };
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", metallic > 0.3f ? 0.72f : 0.22f);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Mesh PersistGeneratedMesh(Mesh generatedMesh)
    {
        string path = $"Assets/Art/Meshes/{generatedMesh.name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(generatedMesh);
            return existing;
        }

        AssetDatabase.CreateAsset(generatedMesh, path);
        return generatedMesh;
    }

    private static void EnsurePrefab(string path, Action<GameObject> build)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return;
        }

        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));
        build(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static GameObject Part(
        PrimitiveType primitiveType,
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Material material,
        Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (rotation.HasValue)
        {
            part.transform.localRotation = rotation.Value;
        }

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        return part;
    }

    private static Transform CreateEmpty(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void Ears(Transform head, Material outer, Material inner, float width, float height)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, side < 0 ? "Left Ear" : "Right Ear", head,
                new Vector3(side * 0.29f, 2.08f, -0.01f), new Vector3(width, height, 0.20f), outer,
                Quaternion.Euler(0f, 0f, side * 12f));
            Part(PrimitiveType.Sphere, side < 0 ? "Left Ear Inner" : "Right Ear Inner", head,
                new Vector3(side * 0.29f, 2.08f, 0.12f), new Vector3(width * 0.48f, height * 0.70f, 0.07f), inner,
                Quaternion.Euler(0f, 0f, side * 12f));
        }
    }

    private static void PointedEars(Transform head, Material outer, Material inner)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, side < 0 ? "Left Pointed Ear" : "Right Pointed Ear", head,
                new Vector3(side * 0.27f, 2.00f, 0f), new Vector3(0.28f, 0.46f, 0.23f), outer,
                Quaternion.Euler(0f, 0f, side * 10f));
            Part(PrimitiveType.Sphere, side < 0 ? "Left Ear Inner" : "Right Ear Inner", head,
                new Vector3(side * 0.27f, 2.00f, 0.14f), new Vector3(0.13f, 0.28f, 0.09f), inner,
                Quaternion.Euler(0f, 0f, side * 10f));
        }
    }

    private static void Whiskers(Transform head, Material material)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int line = -1; line <= 1; line++)
            {
                Part(PrimitiveType.Cylinder, $"Whisker {side}-{line}", head,
                    new Vector3(side * 0.30f, 1.45f + line * 0.055f, 0.60f),
                    new Vector3(0.012f, 0.24f, 0.012f), material,
                    Quaternion.Euler(0f, 0f, side * (76f + line * 8f)));
            }
        }
    }

    private static void Tail(Transform tail, Material main, Material accent, Vector3 center, Vector3 scale)
    {
        Part(PrimitiveType.Sphere, "Tail Base", tail, center, scale, main);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string name = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
        {
            throw new DirectoryNotFoundException($"Cannot create art asset folder '{folderPath}'.");
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
