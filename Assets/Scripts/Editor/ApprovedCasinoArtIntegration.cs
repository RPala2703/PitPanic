using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class ApprovedCasinoArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/", StringComparison.Ordinal)) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        bool directionalArt = assetPath.StartsWith(
            ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Player/", StringComparison.Ordinal) ||
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Staff/tiger-walk-",
                StringComparison.Ordinal) ||
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Staff/pig-walk-",
                StringComparison.Ordinal) ||
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Staff/bear-walk-",
                StringComparison.Ordinal) ||
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Staff/fox-walk-",
                StringComparison.Ordinal) ||
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Customers/rabbit-walk-",
                StringComparison.Ordinal) ||
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/Customers/dog-walk-",
                StringComparison.Ordinal);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = directionalArt ? 256 : 32;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(.5f, 0);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.maxTextureSize = 2048;
        if (!directionalArt &&
            assetPath.StartsWith(ApprovedCasinoArtIntegration.ArtRoot + "/Characters/", StringComparison.Ordinal))
            ApprovedCasinoArtIntegration.ConfigureStandingImport(importer);
    }
}

public static class ApprovedCasinoArtIntegration
{
    public const string ArtRoot = "Assets/Art/ApprovedCasino";

    public static void ConfigureStandingImport(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.npotScale = TextureImporterNPOTScale.None;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(.5f, 0);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        foreach (TextureImporterPlatformSettings platform in new[]
            { importer.GetDefaultPlatformTextureSettings(), importer.GetPlatformTextureSettings("Standalone") })
        {
            platform.overridden = false;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);
        }
    }

    [MenuItem("Pit Panic/Art/Reimport Full Body Standing Characters")]
    public static void ReimportStandingCharacters()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Reimport standing characters outside Play Mode.");
        string[] paths = Directory.GetFiles(ArtRoot + "/Characters", "*.png", SearchOption.AllDirectories);
        if (paths.Length != 17) throw new InvalidOperationException("Expected all 17 approved standing PNGs.");
        foreach (string path in paths)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ConfigureStandingImport(importer);
            importer.SaveAndReimport();
            ValidateStandingBounds(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        Debug.Log("FULL BODY IMPORT VERIFIED: all 17 PNGs reimported, 48x64 full-image rects, rectangular meshes, " +
            "complete 0..1 UVs, bottom-center pivots, 32 PPU, no platform compression or scaling; source artwork unchanged.");
    }

    public static void ValidateStandingBounds(Sprite sprite)
    {
        if (sprite == null) throw new InvalidOperationException("Missing imported standing character.");
        Texture2D texture = sprite.texture;
        Debug.Log($"STANDING IMPORT {sprite.name}: texture={texture.width}x{texture.height}, rect={sprite.rect}, " +
            $"textureRect={sprite.textureRect}, pivot={sprite.pivot}, PPU={sprite.pixelsPerUnit}, packed={sprite.packed}, " +
            $"vertices={sprite.vertices.Length}, triangles={sprite.triangles.Length}, min={sprite.bounds.min}, max={sprite.bounds.max}");
        if (texture.width != 48 || texture.height != 64 || sprite.rect != new Rect(0, 0, 48, 64) ||
            sprite.textureRect != sprite.rect || sprite.pivot != new Vector2(24, 0) ||
            sprite.pixelsPerUnit != 32 || sprite.packed || sprite.vertices.Length != 4 ||
            sprite.triangles.Length != 6 || (Vector2)sprite.bounds.min != new Vector2(-.75f, 0) ||
            (Vector2)sprite.bounds.max != new Vector2(.75f, 2))
            throw new InvalidOperationException("Standing artwork is trimmed, packed or has inconsistent bounds: " + sprite.name);
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            if (Array.IndexOf(sprite.uv, corner) < 0)
                throw new InvalidOperationException("Standing artwork UVs crop the source: " + sprite.name);
        string path = AssetDatabase.GetAssetPath(sprite);
        Texture2D source = new Texture2D(2, 2);
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(path)))
                throw new IOException("Cannot decode standing PNG: " + path);
            Color32[] original = source.GetPixels32();
            Color32[] imported = texture.GetPixels32();
            int bottomOpaquePixels = 0;
            if (original.Length != imported.Length)
                throw new InvalidOperationException("Standing texture was resized: " + path);
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i].a != imported[i].a ||
                    original[i].a == 255 && !original[i].Equals(imported[i]))
                    throw new InvalidOperationException($"Standing image pixel {i} was lost during import: {path}");
                if (i / 48 < 4 && original[i].a > 0) bottomOpaquePixels++;
            }
            if (bottomOpaquePixels < 20)
                throw new InvalidOperationException("Standing image is missing its shoes/feet at the bottom of the source canvas: " + path);
        }
        finally { Object.DestroyImmediate(source); }
        Debug.Log($"FULL IMAGE BOUNDS {sprite.name}: rect={sprite.rect}, vertices=4, UV=0..1, " +
            "pivot=(24,0), source alpha and opaque pixels preserved.");
    }

    private static Sprite Load(string relativePath)
    {
        string path = $"{ArtRoot}/{relativePath}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new FileNotFoundException("Missing approved artwork", path);
        return sprite;
    }

    public static void ConfigureCharacter(PixelCharacterVisual pixel)
    {
        CasinoAnimalSpecies species = pixel.GetComponent<CasinoCharacterVisual>().Species;
        bool staff = species == CasinoAnimalSpecies.Penguin || species == CasinoAnimalSpecies.Tiger ||
            species == CasinoAnimalSpecies.Bear || species == CasinoAnimalSpecies.Fox || species == CasinoAnimalSpecies.Pig;
        pixel.ConfigureApprovedArt(
            Load($"Characters/{(staff ? "Staff" : "Customers")}/{species}_Standing_48x64"),
            staff ? null : Load($"Characters/VIPs/VIP_{species}_Standing_48x64"));
        EditorUtility.SetDirty(pixel);
    }

    [MenuItem("Pit Panic/Art/Integrate Approved Art Into Real Game")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Integrate saved assets outside Play Mode.");
        AssetDatabase.Refresh();
        foreach (CasinoAnimalSpecies species in Enum.GetValues(typeof(CasinoAnimalSpecies)))
        {
            string path = $"Assets/Prefabs/Characters/Character_{species}Pixel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureCharacter(root.GetComponent<PixelCharacterVisual>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        ApplyToCurrentScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save real gameplay scene.");
        AssetDatabase.SaveAssets();
        Debug.Log("APPROVED ART INTEGRATED: real scene, 11 live character prefabs, 17 character sprites, 30 prop sprites. No showcase scene.");
    }

    public static void ApplyToCurrentScene()
    {
        PrepareBackdropSprites();
        SpriteRenderer carpet = GameObject.Find("Pixel Carpet")?.GetComponent<SpriteRenderer>();
        if (carpet == null) throw new InvalidOperationException("The real casino floor is missing.");
        Transform wall = carpet.transform.parent.Find("Casino Back Wall");
        if (wall == null)
        {
            GameObject backdrop = new GameObject("Casino Back Wall", typeof(SpriteRenderer),
                typeof(PixelSpriteTextureBinding), typeof(PixelBillboard));
            wall = backdrop.transform;
            wall.SetParent(carpet.transform.parent, false);
        }
        wall.localPosition = new Vector3(0, 0, 9.8f);
        wall.rotation = Camera.main.transform.rotation;
        wall.localScale = new Vector3(1, 3.2f / 4.75f, 1);
        wall.GetComponent<SpriteRenderer>().sortingOrder = -95;
        BurgundyFloorIntegration.EnsureFloorVisuals(carpet);
        CasinoCampaignPresentation.ApplyCarpet(carpet, 1);

        CasinoCustomerSpawner spawner = Object.FindFirstObjectByType<CasinoCustomerSpawner>();
        if (spawner == null) throw new InvalidOperationException("The real game requires its customer spawner.");
        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty pool = serializedSpawner.FindProperty("customerCharacterPrefabs");
        GameObject[] customerPrefabs = PitPanicArtPrefabLibrary.GetCustomerCharacterPrefabs();
        pool.arraySize = customerPrefabs.Length;
        for (int i = 0; i < customerPrefabs.Length; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = customerPrefabs[i];
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        foreach (PixelCharacterVisual pixel in Object.FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None))
            ConfigureCharacter(pixel);

        ConfigureRouletteSeating();
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            switch (renderer.name)
            {
                case "Pixel BlackjackV2":
                    Surface(renderer, "tables/BlackjackTable_Concept", 6.2f);
                    break;
                case "Pixel Roulette":
                    Surface(renderer, "tables/roulette-table-idle", 6f);
                    break;
                case "Pixel CashDesk":
                    Surface(renderer, "stations/CashDesk_Concept", 4f);
                    break;
                case "Pixel PitDesk":
                    Surface(renderer, "stations/PitDesk_Concept", 4.4f);
                    break;
                case "Pixel Roulette Wheel":
                    // The approved table already contains the wheel. Keep its gameplay pivot, not a second wheel.
                    renderer.enabled = false;
                    break;
                case "Pixel Chair":
                    ConfigureChair(renderer);
                    break;
                case "Pixel BreakRoom":
                case "Pixel Entrance":
                case "Table Rug":
                    renderer.enabled = false;
                    break;
                case "Pixel Column":
                    Billboard(renderer, "decor/WallSconce_Concept", 1.5f);
                    renderer.enabled = renderer.transform.parent.position.z > 0;
                    renderer.transform.localPosition = new Vector3(
                        renderer.transform.parent.position.x < 0 ? 5.9f : -5.9f, 1.1f, .8f);
                    break;
                case "Pixel Plant": Billboard(renderer, "decor/Plant_Concept", 2f); break;
                case "Pixel Window":
                    Billboard(renderer, "decor/WindowCurtains_Concept", 2.5f);
                    Vector3 windowPosition = renderer.transform.localPosition;
                    windowPosition.y = .2f;
                    windowPosition.z = 10.3f;
                    renderer.transform.localPosition = windowPosition;
                    break;
                case "Pixel Lamp": Billboard(renderer, "decor/SideTableAndLamp_Concept", 1.8f); break;
                case "Pixel Curtain":
                    renderer.enabled = false;
                    break;
                case "Pixel Sofa":
                    Billboard(renderer, "decor/Sofa_Concept", 1.7f);
                    renderer.transform.position = new Vector3(-11.4f, .1f, 6.1f);
                    break;
            }
        }
        Transform roomLamp = GameObject.Find("Casino Atmosphere")?.transform;
        if (roomLamp != null)
            foreach (Transform child in roomLamp)
                if (child.name == "Pixel Lamp" && child.localPosition.z < 8)
                    child.localPosition = new Vector3(-9.35f, .1f, 6.1f);

        MoveLabel("Blackjack Table 1 Label", new Vector3(-4.3f, .15f, -.3f));
        MoveLabel("Blackjack Table 2 Label", new Vector3(4.3f, .15f, -.3f));
        MoveLabel("Roulette Table Label", new Vector3(-5.3f, .15f, -.5f));
        MoveLabel("Break Room Label", new Vector3(0, .15f, 3f));
        BurgundyWallIntegration.ApplyToCurrentScene();
    }

    [MenuItem("Pit Panic/Art/Update Blackjack Table Artwork")]
    public static void UpdateBlackjackArtwork()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Update Blackjack artwork outside Play Mode with the scene saved.");
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        int count = 0;
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (renderer.name != "Pixel BlackjackV2") continue;
            Surface(renderer, "tables/BlackjackTable_Concept", 6.2f);
            EditorUtility.SetDirty(renderer.transform);
            count++;
        }
        if (count != 2) throw new InvalidOperationException($"Expected two real Blackjack tables, found {count}.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not save Blackjack artwork.");
        Debug.Log("BLACKJACK ARTWORK SAVED: both real tables replaced; gameplay anchors and colliders unchanged.");
    }

    [MenuItem("Pit Panic/Art/Arrange Roulette Chairs Around Betting Layout")]
    public static void ArrangeRouletteChairs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Arrange Roulette seating outside Play Mode with the scene saved.");
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        ConfigureRouletteSeating();
        foreach (CasinoSeat seat in Object.FindObjectsByType<CasinoSeat>(FindObjectsSortMode.None))
            if (seat.GameType == CasinoCustomerGame.Roulette)
                ConfigureChair(seat.GetComponentInChildren<SpriteRenderer>());
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not save Roulette seating.");
        Debug.Log("ROULETTE SEATING SAVED: three rear-facing chairs by the layout, one left-facing at the right end; four real seats retained.");
    }

    private static void ConfigureRouletteSeating()
    {
        RouletteRoundController roulette = Object.FindFirstObjectByType<RouletteRoundController>();
        if (roulette == null) throw new InvalidOperationException("The real Roulette table is missing.");
        for (int i = 0; i < 4; i++)
        {
            Transform seat = roulette.transform.Find($"Roulette Customer Position {i + 1}");
            Transform facing = seat != null ? seat.Find("Customer Facing Position") : null;
            CasinoSeat customerSeat = seat != null ? seat.GetComponent<CasinoSeat>() : null;
            if (facing == null || customerSeat == null || customerSeat.BettingPosition == null)
                throw new InvalidOperationException($"Roulette seat {i + 1} is missing its sitting or betting anchor.");
            bool rightEnd = i == 3;
            // Approach anchors stay outside the collider; the seated visual moves inward onto its chair.
            seat.localPosition = rightEnd ? new Vector3(4.35f, 0, -.35f) :
                new Vector3(i * 1.25f, 0, -3.05f);
            facing.localPosition = rightEnd ? Vector3.left : Vector3.forward;
            facing.localRotation = rightEnd ? Quaternion.Euler(0, -90, 0) : Quaternion.identity;
            customerSeat.BettingPosition.localPosition = rightEnd ? new Vector3(2.1f, 1.3f, -.35f) :
                new Vector3(Mathf.Min(i * 1.25f, 2.1f), 1.3f, -.8f);
        }
    }

    private static void ConfigureChair(SpriteRenderer renderer)
    {
        if (renderer == null) throw new InvalidOperationException("A real seat is missing its independent chair.");
        CasinoSeat seat = renderer.GetComponentInParent<CasinoSeat>();
        if (seat == null) throw new InvalidOperationException("A table chair requires its real seat.");
        bool roulette = seat.GameType == CasinoCustomerGame.Roulette;
        Vector3 facing = seat.Facing * Vector3.forward;
        int view = !roulette ? 5 : facing.x < -.2f ? 3 : facing.x > .2f ? 7 :
            facing.z > 0 ? 5 : 1;
        Billboard(renderer, roulette ? $"stools/CasinoStool_View{view:00}" :
            $"chairs/VelvetChair_View{view:00}", 1.3f);
        renderer.transform.position = seat.SittingPosition;
    }

    private static void MoveLabel(string name, Vector3 localPosition)
    {
        GameObject label = GameObject.Find(name);
        if (label == null) throw new InvalidOperationException("Missing real station label: " + name);
        ((RectTransform)label.transform).anchoredPosition3D = localPosition;
        EditorUtility.SetDirty(label.transform);
    }

    private static void PrepareBackdropSprites()
    {
        foreach (string name in new[] { "OpeningNightFloor", "HighRollersFloor", "ShadyBusinessFloor" })
        {
            string root = "Assets/Resources/Art/Original/World/" + name;
            Sprite source = AssetDatabase.LoadAssetAtPath<Sprite>(root + ".png");
            if (source == null) throw new FileNotFoundException("Missing current casino floor PNG.", root);
            SaveRegion(root + "Carpet.asset", source, new Rect(270, 100, 16, 16), new Vector2(.5f, .5f));
            SaveRegion(root + "Wall.asset", source, new Rect(0, 276, 608, 76), new Vector2(.5f, 0));
        }
    }

    private static void SaveRegion(string path, Sprite source, Rect region, Vector2 pivot)
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) return;
        Sprite sprite = Sprite.Create(source.texture, region, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = source.name;
        AssetDatabase.CreateAsset(sprite, path);
    }

    private static void Surface(SpriteRenderer renderer, string path, float width)
    {
        Sprite sprite = Load("CasinoProps/" + path);
        renderer.sprite = sprite;
        float scale = width / sprite.bounds.size.x;
        renderer.transform.localScale = Vector3.one * scale;
        Vector3 position = renderer.transform.localPosition;
        position.z = -sprite.bounds.size.y * scale * .5f;
        renderer.transform.localPosition = position;
        EditorUtility.SetDirty(renderer);
    }

    private static void Billboard(SpriteRenderer renderer, string path, float height)
    {
        renderer.sprite = Load("CasinoProps/" + path);
        float scale = height / renderer.sprite.bounds.size.y;
        renderer.transform.localScale = new Vector3(scale, scale, scale);
        EditorUtility.SetDirty(renderer);
    }
}
