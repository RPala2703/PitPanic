using UnityEditor;
using UnityEngine;

public sealed class CasinoPortraitImporter : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    private void OnPreprocessTexture()
    {
        bool sprite = assetPath.StartsWith("Assets/Resources/TitleScreen/", System.StringComparison.Ordinal) ||
                      assetPath.StartsWith("Assets/Resources/Portraits/Staff/", System.StringComparison.Ordinal);
        if (!sprite && assetPath != "Assets/Resources/Portraits/StaffPortraits.png") return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
    }
}
