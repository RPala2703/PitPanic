using UnityEditor;
using UnityEngine;

public sealed class CasinoPortraitImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/Portraits/StaffPortraits.png") return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
    }
}
