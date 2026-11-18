using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BurgundyWallIntegration
{
    public const string Root = "Assets/Resources/Art/BurgundyWalls/";
    public const string WallName = "Burgundy Wall Modules";

    [MenuItem("Pit Panic/Art/Apply Supplied Burgundy Walls")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Apply walls outside Play Mode with the scene saved.");
        AssetDatabase.Refresh();
        foreach (string path in Directory.GetFiles(Root, "*.png"))
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 80;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.maxTextureSize = 2048;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        ApplyToCurrentScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the supplied wall modules.");
        Debug.Log("BURGUNDY WALLS SAVED: shallow supplied panels only; windows/doorway hidden; gameplay/floor/furniture unchanged.");
    }

    public static void ApplyToCurrentScene()
    {
        Transform floor = GameObject.Find("Casino Floor")?.transform;
        if (floor == null || Camera.main == null)
            throw new InvalidOperationException("Wall installation requires the playable casino and camera.");
        Transform root = floor.Find(WallName);
        if (root == null)
        {
            root = new GameObject(WallName).transform;
            root.SetParent(floor, false);
        }
        Material material = floor.Find("Casino Back Wall").GetComponent<SpriteRenderer>().sharedMaterial;
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (renderer.name == "Casino Back Wall" || renderer.name == "Pixel Window" ||
                renderer.name == "Pixel Curtain" || renderer.name == "Pixel Column" ||
                (renderer.name == "Pixel Lamp" && renderer.transform.parent.name == "Casino Atmosphere" &&
                    renderer.transform.localPosition.z >= 8f))
                renderer.enabled = false;
        GameObject label = GameObject.Find("Break Room Label");
        if (label != null) label.GetComponent<Canvas>().enabled = false;

        foreach (SpriteRenderer existing in root.GetComponentsInChildren<SpriteRenderer>(true))
            existing.enabled = false;

        const float pitch = 7.6f;
        const float baseline = 2.65f;
        for (int bay = 0; bay < 5; bay++)
        {
            float x = (bay - 2) * pitch;
            const string wood = "Wainscot_WoodGold_A";
            Sprite woodSprite = Artwork(wood);
            // Supplied modules have two transparent pixels on each side.
            float woodHeight = pitch * woodSprite.rect.height / (woodSprite.rect.width - 4f);
            Place(root, material, $"Wood Base {bay + 1}", wood,
                new Vector2(x, baseline + woodHeight * .5f), woodHeight, -95);
        }
    }

    private static Sprite Artwork(string name) =>
        Resources.Load<Sprite>("Art/BurgundyWalls/" + name) ??
        throw new InvalidOperationException($"Missing supplied wall artwork: {name}.");

    private static void Place(Transform root, Material material, string name, string art,
        Vector2 position, float height, int order)
    {
        Transform child = root.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(SpriteRenderer), typeof(PixelSpriteTextureBinding),
                typeof(PixelBillboard)).transform;
            child.SetParent(root, false);
        }
        Sprite sprite = Artwork(art);
        // All modules share a camera-facing wall plane; only uniform scaling is allowed.
        child.position = new Vector3(position.x, 0, 5.3f) + Camera.main.transform.up * position.y;
        child.rotation = Camera.main.transform.rotation;
        child.localScale = Vector3.one * (height / sprite.bounds.size.y);
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.color = Color.white;
        renderer.enabled = true;
        child.GetComponent<PixelSpriteTextureBinding>().RefreshSprite();
    }
}
