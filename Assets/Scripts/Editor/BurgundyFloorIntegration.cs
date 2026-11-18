using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BurgundyFloorIntegration
{
    private const string Root = "Assets/Resources/Art/BurgundyFloor/";

    [MenuItem("Pit Panic/Art/Apply Supplied Burgundy Flooring")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Apply flooring outside Play Mode with the current scene saved.");
        ImportArtwork();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PitPanicPrototype.unity");
        SpriteRenderer carpet = GameObject.Find("Pixel Carpet")?.GetComponent<SpriteRenderer>();
        if (carpet == null) throw new InvalidOperationException("The playable casino carpet is missing.");
        EnsureFloorVisuals(carpet);
        CasinoCampaignPresentation.ApplyCarpet(carpet, 1);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the playable burgundy flooring.");
        Debug.Log("BURGUNDY FLOOR SAVED: supplied atlas crops only; no gameplay, wall or furniture changes.");
    }

    private static void ImportArtwork()
    {
        AssetDatabase.Refresh();
        string path = Root + "Burgundy_Casino_Floor_Tile_Atlas.png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture.width != 1448 || texture.height != 1086)
            throw new InvalidOperationException("The supplied flooring atlas was resized.");

        // Measured atlas coordinates use its top-left origin, not a regular tile grid.
        SaveCrop(texture, "Carpet", new RectInt(1004, 510, 160, 160));
        SaveCrop(texture, "Runner", new RectInt(280, 951, 103, 64));
        SaveCrop(texture, "Corner", new RectInt(26, 512, 14, 14));
        SaveCrop(texture, "BorderHorizontal", new RectInt(164, 512, 64, 14));
        SaveCrop(texture, "BorderVertical", new RectInt(26, 560, 14, 64));
        AssetDatabase.SaveAssets();
    }

    private static void SaveCrop(Texture2D texture, string name, RectInt topLeft)
    {
        Sprite sprite = Sprite.Create(texture,
            new Rect(topLeft.x, texture.height - topLeft.yMax, topLeft.width, topLeft.height),
            new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        string path = Root + name + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing == null) AssetDatabase.CreateAsset(sprite, path);
        else
        {
            EditorUtility.CopySerialized(sprite, existing);
            UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    public static void EnsureFloorVisuals(SpriteRenderer carpet)
    {
        foreach (string name in CasinoCampaignPresentation.FloorVisualNames)
        {
            Transform existing = carpet.transform.parent.Find(name);
            if (existing != null) continue;
            GameObject visual = new GameObject(name, typeof(SpriteRenderer), typeof(PixelSpriteTextureBinding));
            visual.transform.SetParent(carpet.transform.parent, false);
            visual.GetComponent<SpriteRenderer>().sharedMaterial = carpet.sharedMaterial;
        }
    }
}
